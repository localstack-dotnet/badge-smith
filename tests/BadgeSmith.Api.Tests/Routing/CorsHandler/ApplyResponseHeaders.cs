using BadgeSmith.Api.Core.Routing.Contracts;
using BadgeSmith.Api.Core.Routing.Cors;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing.CorsHandler;

[Trait("Category", TestCategories.Unit)]
public class ApplyResponseHeadersTests : TestBase
{
    private readonly Mock<IRouteResolver> _mockRouteResolver;
    private readonly Mock<ILogger<Core.Routing.Cors.CorsHandler>> _mockLogger;

    public ApplyResponseHeadersTests()
    {
        _mockRouteResolver = new Mock<IRouteResolver>();
        _mockLogger = SetupILoggerWithService<Core.Routing.Cors.CorsHandler>();
    }

    [Fact]
    public void ApplyResponseHeaders_Should_Add_Wildcard_When_Api_Is_Public()
    {
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        handler.ApplyResponseHeaders(responseHeaders, "https://example.com");

        Assert.Equal("*", responseHeaders["Access-Control-Allow-Origin"]);
        Assert.False(responseHeaders.ContainsKey("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public void ApplyResponseHeaders_Should_Echo_Origin_When_UseWildcard_Is_False()
    {
        var options = new CorsOptions
        {
            UseWildcardWhenNoCredentials = false,
        };
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        handler.ApplyResponseHeaders(responseHeaders, "https://example.com");

        Assert.Equal("https://example.com", responseHeaders["Access-Control-Allow-Origin"]);
        Assert.Contains("Origin", responseHeaders["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyResponseHeaders_Should_Handle_Credentials_When_Origin_Is_Trusted()
    {
        var allowedOrigins = new HashSet<string>
            (StringComparer.OrdinalIgnoreCase)
            {
                "https://trusted.com",
            };
        var options = new CorsOptions
        {
            AllowCredentials = true,
            AllowedOrigins = allowedOrigins,
        };
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        handler.ApplyResponseHeaders(responseHeaders, "https://trusted.com");

        Assert.Equal("https://trusted.com", responseHeaders["Access-Control-Allow-Origin"]);
        Assert.Equal("true", responseHeaders["Access-Control-Allow-Credentials"]);
        Assert.Contains("Origin", responseHeaders["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyResponseHeaders_Should_Reject_Untrusted_Origin_When_Credentials_Are_Enabled()
    {
        var allowedOrigins = new HashSet<string>
            (StringComparer.OrdinalIgnoreCase)
            {
                "https://trusted.com",
            };
        var options = new CorsOptions
        {
            AllowCredentials = true,
            AllowedOrigins = allowedOrigins,
        };
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        handler.ApplyResponseHeaders(responseHeaders, "https://malicious.com");

        Assert.False(responseHeaders.ContainsKey("Access-Control-Allow-Origin"));
        Assert.False(responseHeaders.ContainsKey("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public void ApplyResponseHeaders_Should_Add_Expose_Headers()
    {
        var exposeHeaders = new HashSet<string>
            (StringComparer.OrdinalIgnoreCase)
            {
                "x-custom-header",
                "x-rate-limit",
            };
        var options = new CorsOptions
        {
            ExposeHeaders = exposeHeaders,
        };
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        handler.ApplyResponseHeaders(responseHeaders, "https://example.com");

        Assert.Equal("x-custom-header, x-rate-limit", responseHeaders["Access-Control-Expose-Headers"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyResponseHeaders_Should_Handle_Missing_Origin(string? origin)
    {
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        handler.ApplyResponseHeaders(responseHeaders, origin);

        Assert.Equal("*", responseHeaders["Access-Control-Allow-Origin"]); // Default wildcard
    }
}
