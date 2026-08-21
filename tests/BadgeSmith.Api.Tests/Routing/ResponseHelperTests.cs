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
        var cache = new ResponseHelper.CacheSettings(
            SMaxAgeSeconds: 600,
            MaxAgeSeconds: 300,
            SwrSeconds: 1200,
            SieSeconds: 3600);

        var response = CreateCachedResponse(cache: cache, lastModifiedUtc: lastModified);

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
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.SeeOther)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public void Redirect_Should_Compose_Public_CacheControl_When_Cache_Directives_Are_Provided(HttpStatusCode status)
    {
        var response = ResponseHelper.Redirect(
            "https://example.com/results/42",
            status,
            sMaxAge: 600,
            maxAge: 300,
            staleWhileRevalidate: 1200,
            staleIfError: 3600);

        Assert.Equal((int)status, response.StatusCode);
        Assert.Null(response.Body);
        Assert.Equal("https://example.com/results/42", response.Headers["Location"]);
        Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        Assert.Equal("public, s-maxage=600, max-age=300, stale-while-revalidate=1200, stale-if-error=3600", response.Headers["Cache-Control"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.SeeOther)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public void Redirect_Should_Use_NoStore_When_NoStore_Is_Requested(HttpStatusCode status)
    {
        var response = ResponseHelper.Redirect(
            "https://example.com/results/42",
            status,
            sMaxAge: 600,
            noStore: true);

        Assert.Equal((int)status, response.StatusCode);
        Assert.Null(response.Body);
        Assert.Equal("https://example.com/results/42", response.Headers["Location"]);
        Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        Assert.Equal("no-store, no-cache, must-revalidate", response.Headers["Cache-Control"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Redirect_Should_Throw_When_Location_Is_Null_Empty_Or_Whitespace(string? location)
    {
        _ = Assert.Throws<ArgumentException>(() => ResponseHelper.Redirect(location!, noStore: true));
    }

    [Fact]
    public void NoCacheHeaders_Should_Return_NoCache_Headers_When_ContentType_Is_Provided()
    {
        var headers = ResponseHelper.NoCacheHeaders("text/plain; charset=utf-8");

        Assert.Equal(4, headers.Count);
        Assert.Equal("no-store, no-cache, must-revalidate", headers["Cache-Control"]);
        Assert.Equal("no-cache", headers["Pragma"]);
        Assert.Equal("0", headers["Expires"]);
        Assert.Equal("text/plain; charset=utf-8", headers["Content-Type"]);
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateCachedResponse(
        string? ifNoneMatchHeader = null,
        ResponseHelper.CacheSettings? cache = null,
        DateTimeOffset? lastModifiedUtc = null)
    {
        var badge = new ShieldsBadgeResponse(1, "NuGet", "1.2.3", "blue");
        return ResponseHelper.OkCached(
            badge,
            LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
            ifNoneMatchHeader,
            cache,
            lastModifiedUtc);
    }
}
