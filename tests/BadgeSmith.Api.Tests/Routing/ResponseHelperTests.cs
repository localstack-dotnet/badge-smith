using System.Net;
using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Infrastructure;
using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Features;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing;

[Trait("Category", TestCategories.Unit)]
public sealed class ResponseHelperTests
{
    private const string BadgeEntityTagValue = "E24A8A8C2B7F48A8D9A54E94B26732DA86C90E2F1AAAFB0B2AA8A5D3BAE7DF64";
    private const string BadgeEntityTag = "\"" + BadgeEntityTagValue + "\"";

    [Fact]
    public void CreateResponse_Should_Set_Status_Body_And_Default_ContentType_When_Custom_Headers_Are_Provided()
    {
        var response = ResponseHelper.CreateResponse(
            HttpStatusCode.Accepted,
            "response body",
            () => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Test"] = "value",
            });

        Assert.Equal(202, response.StatusCode);
        Assert.Equal("response body", response.Body);
        Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        Assert.Equal("value", response.Headers["X-Test"]);
    }

    [Fact]
    public void OkCached_Should_Return_Cacheable_Response_When_Entity_Tag_Does_Not_Match()
    {
        var lastModified = new DateTimeOffset(2026, 8, 20, 12, 34, 56, TimeSpan.FromHours(3));

        var response = CreateCachedResponse(lastModifiedUtc: lastModified);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{\"schemaVersion\":1,\"label\":\"NuGet\",\"message\":\"1.2.3\",\"color\":\"blue\"}", response.Body);
        Assert.Equal("public, s-maxage=600, max-age=300, stale-while-revalidate=1200, stale-if-error=3600", response.Headers["Cache-Control"]);
        Assert.Equal("Thu, 20 Aug 2026 09:34:56 GMT", response.Headers["Last-Modified"]);

        var expectedEtag = $"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(response.Body)))}\"";
        Assert.Equal(expectedEtag, response.Headers["ETag"]);
    }

    [Fact]
    public void OkCached_Should_Return_NotModified_When_IfNoneMatch_Matches_Entity_Tag()
    {
        var initial = CreateCachedResponse();
        var etag = initial.Headers["ETag"];
        string[] matchingHeaders =
        [
            etag,
            $"W/{etag}",
            $"\"different\", W/{etag}",
            etag[1..^1],
            "*",
        ];

        foreach (var ifNoneMatch in matchingHeaders)
        {
            var response = CreateCachedResponse(ifNoneMatch);

            Assert.Equal(304, response.StatusCode);
            Assert.Null(response.Body);
            Assert.Equal(etag, response.Headers["ETag"]);
            Assert.Equal(initial.Headers["Cache-Control"], response.Headers["Cache-Control"]);
        }
    }

    [Fact]
    public void OkCached_Should_Return_200_When_IfNoneMatch_Does_Not_Match()
    {
        var response = CreateCachedResponse("\"NOT-A-MATCH\"");

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Body);
    }

    [Theory]
    [InlineData(null, 200)]
    [InlineData("", 200)]
    [InlineData(" \t\u2003 ", 200)]
    [InlineData(" \u2003*\t ", 304)]
    [InlineData(",, " + BadgeEntityTag + ",,", 304)]
    [InlineData("\"different\", " + BadgeEntityTag, 304)]
    [InlineData("w/\t" + BadgeEntityTag + " \u2003", 304)]
    [InlineData(" \u2003W/ " + BadgeEntityTagValue + " ,\"other\"", 304)]
    [InlineData("w/ \"e24a8a8c2b7f48a8d9a54e94b26732da86c90e2f1aaafb0b2aa8a5d3bae7df64\"", 304)]
    [InlineData("W/*", 200)]
    [InlineData("\"different\", *", 200)]
    [InlineData("W /" + BadgeEntityTag, 200)]
    [InlineData("W/W/" + BadgeEntityTag, 200)]
    [InlineData("\"W/" + BadgeEntityTagValue + "\"", 200)]
    [InlineData("\"", 200)]
    [InlineData("\"\"", 200)]
    [InlineData(", , W/ ,", 200)]
    [InlineData(BadgeEntityTagValue + "0", 200)]
    public void OkCached_Should_Preserve_Validator_Compatibility_When_Header_Contains_Whitespace_Or_List_Tokens(string? header, int expectedStatus)
    {
        var response = CreateCachedResponse(header);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(BadgeEntityTag, response.Headers["ETag"]);
        Assert.Equal("public, s-maxage=600, max-age=300, stale-while-revalidate=1200, stale-if-error=3600", response.Headers["Cache-Control"]);
        Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        Assert.Equal(expectedStatus == 304 ? null : "{\"schemaVersion\":1,\"label\":\"NuGet\",\"message\":\"1.2.3\",\"color\":\"blue\"}", response.Body);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public void RedirectCached_Should_Compose_Preset_CacheControl_When_Status_Is_Supported(int statusCode)
    {
        var status = StatusFromCode(statusCode);
        var policy = BadgeResponsePolicy.PublicCache;

        var response = ResponseHelper.RedirectCached("https://example.com/results/42", status, policy);

        Assert.Equal(statusCode, response.StatusCode);
        Assert.Null(response.Body);
        Assert.Equal("https://example.com/results/42", response.Headers["Location"]);
        Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        Assert.Equal(policy.CacheControl, response.Headers["Cache-Control"]);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public void RedirectNoStore_Should_Emit_Exact_NoStore_Without_Legacy_Headers(int statusCode)
    {
        var status = StatusFromCode(statusCode);

        var response = ResponseHelper.RedirectNoStore("https://example.com/results/42", status);

        Assert.Equal(statusCode, response.StatusCode);
        Assert.Null(response.Body);
        Assert.Equal("https://example.com/results/42", response.Headers["Location"]);
        Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        Assert.Equal("no-store", response.Headers["Cache-Control"]);
        Assert.False(response.Headers.ContainsKey("Pragma"));
        Assert.False(response.Headers.ContainsKey("Expires"));
    }

    [Fact]
    public void Redirect_Cached_And_NoStore_Should_Default_To_Found_When_Only_Location_Is_Given()
    {
        var cached = ResponseHelper.RedirectCached("https://example.com/a", BadgeResponsePolicy.PublicCache);
        var noStore = ResponseHelper.RedirectNoStore("https://example.com/b");

        Assert.Equal(302, cached.StatusCode);
        Assert.Equal(302, noStore.StatusCode);
    }

    [Fact]
    public void RedirectApis_Should_Reject_Default_RedirectStatus()
    {
        var status = default(RedirectStatus);
        var policy = BadgeResponsePolicy.PublicCache;

        Assert.Throws<ArgumentOutOfRangeException>(() => ResponseHelper.RedirectCached("https://example.com/a", status, policy));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResponseHelper.RedirectNoStore("https://example.com/a", status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RedirectApis_Should_Reject_Null_Empty_Or_Whitespace_Location(string? location)
    {
        var policy = BadgeResponsePolicy.PublicCache;

        var cachedException = Assert.Throws<ArgumentException>(() => ResponseHelper.RedirectCached(location!, policy));
        var explicitException = Assert.Throws<ArgumentException>(() => ResponseHelper.RedirectCached(location!, RedirectStatus.Found, policy));
        var noStoreException = Assert.Throws<ArgumentException>(() => ResponseHelper.RedirectNoStore(location!));

        Assert.Contains("Location cannot be null, empty, or whitespace.", cachedException.Message, StringComparison.Ordinal);
        Assert.Contains("Location cannot be null, empty, or whitespace.", explicitException.Message, StringComparison.Ordinal);
        Assert.Contains("Location cannot be null, empty, or whitespace.", noStoreException.Message, StringComparison.Ordinal);
    }

    private static RedirectStatus StatusFromCode(int code) => code switch
    {
        301 => RedirectStatus.MovedPermanently,
        302 => RedirectStatus.Found,
        303 => RedirectStatus.SeeOther,
        307 => RedirectStatus.TemporaryRedirect,
        308 => RedirectStatus.PermanentRedirect,
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private static APIGatewayHttpApiV2ProxyResponse CreateCachedResponse(
        string? ifNoneMatchHeader = null,
        DateTimeOffset? lastModifiedUtc = null)
    {
        var badge = new ShieldsBadgeResponse(1, "NuGet", "1.2.3", "blue");
        return ResponseHelper.OkCached(
            badge,
            LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
            BadgeResponsePolicy.PublicCache,
            ifNoneMatchHeader,
            lastModifiedUtc);
    }
}
