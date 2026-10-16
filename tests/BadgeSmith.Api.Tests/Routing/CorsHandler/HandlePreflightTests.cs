using BadgeSmith.Api.Core.Routing.Contracts;
using BadgeSmith.Api.Core.Routing.Cors;
using BadgeSmith.Api.Tests.TestHelpers;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static BadgeSmith.Api.Tests.TestBase;

namespace BadgeSmith.Api.Tests.Routing.CorsHandler;

[Trait("Category", TestCategories.Unit)]
public class HandlePreflightTests
{
    private readonly Mock<IRouteResolver> _mockRouteResolver;
    private readonly Mock<ILogger<Core.Routing.Cors.CorsHandler>> _mockLogger;

    public HandlePreflightTests()
    {
        _mockRouteResolver = new Mock<IRouteResolver>();
        _mockLogger = SetupILoggerWithService<Core.Routing.Cors.CorsHandler>();
    }

    [Fact]
    public void HandlePreflight_Should_Log_Structured_Request_Before_Resolving_Route_When_Debug_Is_Enabled()
    {
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, CorsOptions.Default);
        _mockRouteResolver.Setup(resolver => resolver.GetAllowedMethods("/health"))
            .Callback(() => Assert.Single(_mockLogger.Invocations, invocation => invocation.Method.Name == nameof(ILogger.Log)))
            .Returns(["GET", "HEAD", "OPTIONS"]);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = " https://example.com ",
            ["Access-Control-Request-Method"] = " GET ",
            ["Access-Control-Request-Headers"] = " content-type, x-custom-header ",
        };

        var response = handler.HandlePreflight(headers, "/health");

        VerifyLogging(_mockLogger, "CORS preflight request: Origin=https://example.com, Method=GET, Headers=content-type, x-custom-header");
        VerifyStructuredLogging(_mockLogger, new EventId(1000, "CorsPreflightRequest"),
        [
            new("Origin", "https://example.com"),
            new("Method", "GET"),
            new("Headers", "content-type, x-custom-header"),
            new("{OriginalFormat}", "CORS preflight request: Origin={Origin}, Method={Method}, Headers={Headers}"),
        ]);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(response.Body);
        Assert.False(response.IsBase64Encoded);
        Assert.Equal(6, response.Headers.Count);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("GET", response.Headers["Access-Control-Allow-Methods"]);
        Assert.Equal("content-type", response.Headers["Access-Control-Allow-Headers"]);
        Assert.Equal("Access-Control-Request-Method, Access-Control-Request-Headers", response.Headers["Vary"]);
        Assert.Equal("3600", response.Headers["Access-Control-Max-Age"]);
        Assert.Equal("text/plain", response.Headers["Content-Type"]);
    }

    [Theory]
    [InlineData(null, null, "CORS preflight request: Origin=(null), Method=(null), Headers=(null)")]
    [InlineData("", "", "CORS preflight request: Origin=, Method=, Headers=")]
    [InlineData(" \t ", "", "CORS preflight request: Origin=, Method=, Headers=")]
    public void HandlePreflight_Should_Preserve_Structured_Values_When_Headers_Are_Missing_Or_Blank(
        string? headerValue, string? expectedValue, string expectedMessage)
    {
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, CorsOptions.Default);
        _mockRouteResolver.Setup(resolver => resolver.GetAllowedMethods("/health"))
            .Returns(["GET", "HEAD", "OPTIONS"]);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headerValue is not null)
        {
            headers["Origin"] = headerValue;
            headers["Access-Control-Request-Method"] = headerValue;
            headers["Access-Control-Request-Headers"] = headerValue;
        }

        var response = handler.HandlePreflight(headers, "/health");

        VerifyLogging(_mockLogger, expectedMessage);
        VerifyStructuredLogging(_mockLogger, new EventId(1000, "CorsPreflightRequest"),
        [
            new("Origin", expectedValue),
            new("Method", expectedValue),
            new("Headers", expectedValue),
            new("{OriginalFormat}", "CORS preflight request: Origin={Origin}, Method={Method}, Headers={Headers}"),
        ]);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(response.Body);
        Assert.False(response.IsBase64Encoded);
        Assert.Equal(5, response.Headers.Count);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("GET, HEAD, OPTIONS", response.Headers["Access-Control-Allow-Methods"]);
        Assert.Equal("Access-Control-Request-Method", response.Headers["Vary"]);
        Assert.Equal("3600", response.Headers["Access-Control-Max-Age"]);
        Assert.Equal("text/plain", response.Headers["Content-Type"]);
    }

    [Fact]
    public void HandlePreflight_Should_Return_Cors_Response_Without_Logging_When_Debug_Is_Disabled()
    {
        var logger = SetupILoggerWithService<Core.Routing.Cors.CorsHandler>(isEnabled: false);
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, logger.Object, CorsOptions.Default);
        _mockRouteResolver.Setup(resolver => resolver.GetAllowedMethods("/health"))
            .Returns(["GET", "HEAD", "OPTIONS"]);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
            ["Access-Control-Request-Method"] = "GET",
            ["Access-Control-Request-Headers"] = "content-type, x-custom-header",
        };

        var response = handler.HandlePreflight(headers, "/health");

        Assert.DoesNotContain(logger.Invocations, invocation => invocation.Method.Name == nameof(ILogger.Log));
        Assert.Equal(204, response.StatusCode);
        Assert.Null(response.Body);
        Assert.False(response.IsBase64Encoded);
        Assert.Equal(6, response.Headers.Count);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("GET", response.Headers["Access-Control-Allow-Methods"]);
        Assert.Equal("content-type", response.Headers["Access-Control-Allow-Headers"]);
        Assert.Equal("Access-Control-Request-Method, Access-Control-Request-Headers", response.Headers["Vary"]);
        Assert.Equal("3600", response.Headers["Access-Control-Max-Age"]);
        Assert.Equal("text/plain", response.Headers["Content-Type"]);
    }

    [Fact]
    public void HandlePreflight_Should_Return_Basic_Cors_Headers_When_Request_Is_Simple()
    {
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

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

        var response = handler.HandlePreflight(headers, "/health");

        Assert.Equal(204, response.StatusCode);
        Assert.NotNull(response.Headers);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("GET, HEAD, OPTIONS", response.Headers["Access-Control-Allow-Methods"]);
        Assert.Equal("3600", response.Headers["Access-Control-Max-Age"]);
    }

    [Fact]
    public void HandlePreflight_Should_Handle_Specific_Method_Request()
    {
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

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

        var response = handler.HandlePreflight(headers, "/badges/packages/nuget/test");

        Assert.Equal(204, response.StatusCode);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("GET", response.Headers["Access-Control-Allow-Methods"]); // Only requested method
        Assert.Contains("Access-Control-Request-Method", response.Headers["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandlePreflight_Should_Filter_Request_Headers()
    {
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

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

        var response = handler.HandlePreflight(headers, "/tests/results");

        Assert.Equal(204, response.StatusCode);
        Assert.Equal("content-type", response.Headers["Access-Control-Allow-Headers"]);
        Assert.Contains("Access-Control-Request-Headers", response.Headers["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void HandlePreflight_Should_Handle_Missing_Request_Headers(string? requestHeaders)
    {
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

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

        var response = handler.HandlePreflight(headers, "/health");

        Assert.Equal(204, response.StatusCode);
        Assert.False(response.Headers.ContainsKey("Access-Control-Allow-Headers"));
    }

    [Fact]
    public void HandlePreflight_Should_Handle_Credentials_When_Origin_Is_Specific()
    {
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
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

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

        var response = handler.HandlePreflight(headers, "/badges/packages/nuget/test");

        Assert.Equal(204, response.StatusCode);
        Assert.Equal("https://trusted.com", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("true", response.Headers["Access-Control-Allow-Credentials"]);
        Assert.Contains("Origin", response.Headers["Vary"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandlePreflight_Should_Reject_Untrusted_Origin_When_Credentials_Are_Enabled()
    {
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "https://trusted.com",
        };
        var options = new CorsOptions
        {
            AllowCredentials = true,
            AllowedOrigins = allowedOrigins,
        };
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

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

        var response = handler.HandlePreflight(headers, "/badges/packages/nuget/test");

        Assert.Equal(204, response.StatusCode);
        Assert.False(response.Headers.ContainsKey("Access-Control-Allow-Origin")); // Origin rejected
        Assert.False(response.Headers.ContainsKey("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public void HandlePreflight_Should_Use_Origin_Predicate_When_Provided()
    {
        var options = new CorsOptions
        {
            AllowCredentials = true,
            OriginAllowed = origin => origin.EndsWith(".example.com", StringComparison.OrdinalIgnoreCase),
        };
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

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

        var response = handler.HandlePreflight(headers, "/health");

        Assert.Equal(204, response.StatusCode);
        Assert.Equal("https://app.example.com", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("true", response.Headers["Access-Control-Allow-Credentials"]);
    }

    [Fact]
    public void HandlePreflight_Should_Handle_No_Origin_Header()
    {
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

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

        var response = handler.HandlePreflight(headers, "/health");

        Assert.Equal(204, response.StatusCode);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]); // Default for public API
    }

    [Fact]
    public void HandlePreflight_Should_Handle_Null_Headers()
    {
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

        _mockRouteResolver.Setup(r => r.GetAllowedMethods("/health"))
            .Returns(
            [
                "GET",
                "OPTIONS",
            ]);

        var response = handler.HandlePreflight(null, "/health");

        VerifyLogging(_mockLogger, "CORS preflight request: Origin=(null), Method=(null), Headers=(null)");
        VerifyStructuredLogging(_mockLogger, new EventId(1000, "CorsPreflightRequest"),
        [
            new("Origin", null),
            new("Method", null),
            new("Headers", null),
            new("{OriginalFormat}", "CORS preflight request: Origin={Origin}, Method={Method}, Headers={Headers}"),
        ]);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
    }

    [Fact]
    public void HandlePreflight_Should_Integrate_With_RouteResolver()
    {
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("Health", "GET", RouteTestBuilder.CreateExactPattern("/health")),
            RouteTestBuilder.CreateRouteDescriptor("NugetPackage", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("TestIngestion", "POST", RouteTestBuilder.CreateExactPattern("/tests/results")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(resolver, _mockLogger.Object, options);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
            ["Access-Control-Request-Method"] = "POST",
        };

        var response = handler.HandlePreflight(headers, "/tests/results");

        Assert.Equal(204, response.StatusCode);
        Assert.Equal("POST", response.Headers["Access-Control-Allow-Methods"]);
    }

    [Fact]
    public void HandlePreflight_Should_Handle_Nonexistent_Route()
    {
        var routes = new[] { RouteTestBuilder.CreateRouteDescriptor("Health", "GET", RouteTestBuilder.CreateExactPattern("/health")), };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(resolver, _mockLogger.Object, options);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = "https://example.com",
        };

        var response = handler.HandlePreflight(headers, "/nonexistent");

        Assert.Equal(204, response.StatusCode);
        Assert.Equal("OPTIONS", response.Headers["Access-Control-Allow-Methods"]); // Only OPTIONS for unknown routes
    }

    [Fact]
    public void HandlePreflight_Should_Always_Include_Content_Type_Header()
    {
        var options = CorsOptions.Default;
        var handler = new Core.Routing.Cors.CorsHandler(_mockRouteResolver.Object, _mockLogger.Object, options);

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

        var response = handler.HandlePreflight(headers, "/health");

        Assert.Equal(204, response.StatusCode);
        Assert.NotNull(response.Headers);
        Assert.Equal("text/plain", response.Headers["Content-Type"]);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal("GET, OPTIONS", response.Headers["Access-Control-Allow-Methods"]);
    }
}
