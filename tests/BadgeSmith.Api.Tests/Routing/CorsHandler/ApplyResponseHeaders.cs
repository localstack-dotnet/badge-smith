using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Cors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing.CorsHandler;

public class ApplyResponseHeadersTests : TestBase
{
    private readonly Mock<IRouteResolver> _mockRouteResolver;
    private readonly Mock<ILogger<Api.Routing.Cors.CorsHandler>> _mockLogger;

    public ApplyResponseHeadersTests()
    {
        _mockRouteResolver = new Mock<IRouteResolver>();
        _mockLogger = SetupILoggerWithService<Api.Routing.Cors.CorsHandler>();
    }

    [Fact]
    public void ApplyResponseHeaders_Should_AddWildcardForPublicAPI()
    {
        // Arrange
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Act
        handler.ApplyResponseHeaders(responseHeaders, "https://example.com");

        // Assert
        Assert.Equal("*", responseHeaders["Access-Control-Allow-Origin"]);
        Assert.False(responseHeaders.ContainsKey("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public void ApplyResponseHeaders_Should_EchoOriginWhenUseWildcardIsFalse()
    {
        // Arrange
        var options = new CorsOptions
        {
            UseWildcardWhenNoCredentials = false,
        };
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Act
        handler.ApplyResponseHeaders(responseHeaders, "https://example.com");

        // Assert
        Assert.Equal("https://example.com", responseHeaders["Access-Control-Allow-Origin"]);
        Assert.Contains("Origin", responseHeaders["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyResponseHeaders_Should_HandleCredentialsWithTrustedOrigin()
    {
        // Arrange
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
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Act
        handler.ApplyResponseHeaders(responseHeaders, "https://trusted.com");

        // Assert
        Assert.Equal("https://trusted.com", responseHeaders["Access-Control-Allow-Origin"]);
        Assert.Equal("true", responseHeaders["Access-Control-Allow-Credentials"]);
        Assert.Contains("Origin", responseHeaders["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyResponseHeaders_Should_RejectUntrustedOriginWithCredentials()
    {
        // Arrange
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
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Act
        handler.ApplyResponseHeaders(responseHeaders, "https://malicious.com");

        // Assert
        Assert.False(responseHeaders.ContainsKey("Access-Control-Allow-Origin"));
        Assert.False(responseHeaders.ContainsKey("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public void ApplyResponseHeaders_Should_AddExposeHeaders()
    {
        // Arrange
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
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Act
        handler.ApplyResponseHeaders(responseHeaders, "https://example.com");

        // Assert
        Assert.Equal("x-custom-header, x-rate-limit", responseHeaders["Access-Control-Expose-Headers"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyResponseHeaders_Should_HandleMissingOrigin(string? origin)
    {
        // Arrange
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Act
        handler.ApplyResponseHeaders(responseHeaders, origin);

        // Assert
        Assert.Equal("*", responseHeaders["Access-Control-Allow-Origin"]); // Default wildcard
    }
}
