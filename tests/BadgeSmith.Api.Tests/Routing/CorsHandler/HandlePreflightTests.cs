using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Cors;
using BadgeSmith.Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing.CorsHandler;

public class HandlePreflightTests : TestBase
{
    private readonly Mock<IRouteResolver> _mockRouteResolver;
    private readonly Mock<ILogger<Api.Routing.Cors.CorsHandler>> _mockLogger;

    public HandlePreflightTests()
    {
        _mockRouteResolver = new Mock<IRouteResolver>();
        _mockLogger = SetupILoggerWithService<Api.Routing.Cors.CorsHandler>();
    }

    [Fact]
    public void HandlePreflight_Should_ReturnBasicCorsHeaders_ForSimpleRequest()
    {
        // Arrange
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/health"))
            .Returns(
            [
                "GET",
                "HEAD",
                "OPTIONS",
            ]);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/health");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.NotNull(response.Headers);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("GET, HEAD, OPTIONS", response.Headers["Access-Control-Allow-Methods"]);
        Assert.Equal("3600", response.Headers["Access-Control-Max-Age"]);
    }

    [Fact]
    public void HandlePreflight_Should_HandleSpecificMethodRequest()
    {
        // Arrange
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/badges/packages/nuget/test"))
            .Returns(
            [
                "GET",
                "HEAD",
                "OPTIONS",
            ]);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
            ["Access-Control-Request-Method"] = "GET",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/badges/packages/nuget/test");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("GET", response.Headers["Access-Control-Allow-Methods"]); // Only requested method
        Assert.Contains("Access-Control-Request-Method", response.Headers["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandlePreflight_Should_FilterRequestHeaders()
    {
        // Arrange
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/tests/results"))
            .Returns(
            [
                "POST",
                "OPTIONS",
            ]);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
            ["Access-Control-Request-Method"] = "POST",
            ["Access-Control-Request-Headers"] = "content-type, authorization, x-custom-header",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/tests/results");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("content-type", response.Headers["Access-Control-Allow-Headers"]);
        Assert.Contains("Access-Control-Request-Headers", response.Headers["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void HandlePreflight_Should_HandleMissingRequestHeaders(string? requestHeaders)
    {
        // Arrange
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/health"))
            .Returns(
            [
                "GET",
                "OPTIONS",
            ]);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
        };

        if (requestHeaders != null)
        {
            headers["Access-Control-Request-Headers"] = requestHeaders;
        }

        // Act
        var response = handler.HandlePreflight(headers, "/health");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.False(response.Headers.ContainsKey("Access-Control-Allow-Headers"));
    }

    [Fact]
    public void HandlePreflight_Should_HandleCredentialsWithSpecificOrigin()
    {
        // Arrange
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "https://trusted.com",
            "https://app.example.com",
        };
        var options = new CorsOptions
        {
            AllowCredentials = true,
            AllowedOrigins = allowedOrigins,
        };
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/badges/packages/nuget/test"))
            .Returns(
            [
                "GET",
                "OPTIONS",
            ]);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://trusted.com",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/badges/packages/nuget/test");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("https://trusted.com", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("true", response.Headers["Access-Control-Allow-Credentials"]);
        Assert.Contains("Origin", response.Headers["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandlePreflight_Should_RejectUntrustedOriginWithCredentials()
    {
        // Arrange
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "https://trusted.com",
        };
        var options = new CorsOptions
        {
            AllowCredentials = true,
            AllowedOrigins = allowedOrigins,
        };
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/badges/packages/nuget/test"))
            .Returns(
            [
                "GET",
                "OPTIONS",
            ]);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://malicious.com",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/badges/packages/nuget/test");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.False(response.Headers.ContainsKey("Access-Control-Allow-Origin")); // Origin rejected
        Assert.False(response.Headers.ContainsKey("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public void HandlePreflight_Should_UseOriginPredicateWhenProvided()
    {
        // Arrange
        var options = new CorsOptions
        {
            AllowCredentials = true,
            OriginAllowed = origin => origin.EndsWith(".example.com", StringComparison.OrdinalIgnoreCase),
        };
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/health"))
            .Returns(
            [
                "GET",
                "OPTIONS",
            ]);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://app.example.com",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/health");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("https://app.example.com", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("true", response.Headers["Access-Control-Allow-Credentials"]);
    }

    [Fact]
    public void HandlePreflight_Should_HandleNoOriginHeader()
    {
        // Arrange
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/health"))
            .Returns(
            [
                "GET",
                "OPTIONS",
            ]);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Access-Control-Request-Method"] = "GET",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/health");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]); // Default for public API
    }

    [Fact]
    public void HandlePreflight_Should_HandleNullHeaders()
    {
        // Arrange
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/health"))
            .Returns(
            [
                "GET",
                "OPTIONS",
            ]);

        // Act
        var response = handler.HandlePreflight(null, "/health");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
    }

    [Fact]
    public void HandlePreflight_Should_IntegrateWithRouteResolver()
    {
        // Arrange
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("Health", "GET", RouteTestBuilder.CreateExactPattern("/health")),
            RouteTestBuilder.CreateRouteDescriptor("NugetPackage", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("TestIngestion", "POST", RouteTestBuilder.CreateExactPattern("/tests/results")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(resolver, _mockLogger.Object, options);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
            ["Access-Control-Request-Method"] = "POST",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/tests/results");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("POST", response.Headers["Access-Control-Allow-Methods"]);
    }

    [Fact]
    public void HandlePreflight_Should_HandleNonExistentRoute()
    {
        // Arrange
        var routes = new[] { RouteTestBuilder.CreateRouteDescriptor("Health", "GET", RouteTestBuilder.CreateExactPattern("/health")), };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(resolver, _mockLogger.Object, options);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/nonexistent");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("OPTIONS", response.Headers["Access-Control-Allow-Methods"]); // Only OPTIONS for unknown routes
    }

    [Fact]
    public void HandlePreflight_Should_AlwaysIncludeContentTypeHeader()
    {
        // Arrange
        var options = CorsOptions.Default;
        var handler = new Api.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/health"))
            .Returns(
            [
                "GET",
                "OPTIONS",
            ]);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
        };

        // Act
        var response = handler.HandlePreflight(headers, "/health");

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.NotNull(response.Headers);
        Assert.Equal("text/plain", response.Headers["Content-Type"]);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("GET, OPTIONS", response.Headers["Access-Control-Allow-Methods"]);
    }
}
