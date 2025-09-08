using System.Diagnostics.CodeAnalysis;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Tests.TestHelpers;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing;

public sealed class RouteResolverTests
{
    [Fact]
    public void Constructor_Should_StoreRoutesCorrectly()
    {
        var routes = CreateTestRoutes();

        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        Assert.NotNull(resolver);
    }

    [Theory]
    [InlineData("GET", "/health", true, "Health")]
    [InlineData("get", "/health", true, "Health")] // Case insensitive method
    [InlineData("GET", "/Health", true, "Health")] // Case insensitive path (exact pattern)
    [InlineData("POST", "/tests/results", true, "TestIngestion")]
    [InlineData("post", "/tests/results", true, "TestIngestion")] // Case insensitive method
    [InlineData("GET", "/badges/packages/nuget/Newtonsoft.Json", true, "NugetPackageBadge")]
    [InlineData("GET", "/badges/packages/github/localstack-dotnet/localstack.client", true, "GithubPackagesBadge")]
    [InlineData("GET", "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main", true, "TestsBadge")]
    [InlineData("GET", "/redirect/test-results/linux/localstack-dotnet/dotnet-aspire-for-localstack/main", true, "BadgeRedirect")]
    public void TryResolve_Should_MatchValidRoutes(string method, string path, bool expectedMatch, string expectedRouteName)
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var result = resolver.TryResolve(method, path, out var match);

        Assert.Equal(expectedMatch, result);

        if (expectedMatch)
        {
            Assert.Equal(expectedRouteName, match.Descriptor.Name);
            Assert.Equal(method.ToUpperInvariant(), match.Descriptor.Method.ToUpperInvariant());
        }
    }

    [Theory]
    [InlineData("GET", "/nonexistent")]
    [InlineData("POST", "/health")] // Wrong method for health
    [InlineData("GET", "/tests/results")] // Wrong method for test ingestion
    [InlineData("DELETE", "/health")] // Unsupported method
    [InlineData("GET", "/badges")] // Incomplete path
    [InlineData("GET", "/badges/packages")] // Incomplete path
    [InlineData("GET", "/badges/packages/nuget")] // Incomplete path
    [InlineData("GET", "/badges/tests")] // Incomplete path
    [InlineData("GET", "/badges/tests/linux")] // Incomplete path
    [InlineData("GET", "/badges/tests/linux/owner")] // Incomplete path
    [InlineData("GET", "/badges/tests/linux/owner/repo")] // Incomplete path
    [InlineData("GET", "")] // Empty path
    public void TryResolve_Should_ReturnFalseForInvalidRoutes(string method, string path)
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var result = resolver.TryResolve(method, path, out _);

        Assert.False(result);
        // For ref struct, when no match is found, we just verify the result is false
    }

    [Fact]
    public void TryResolve_Should_ExtractParametersForNuGetPackage()
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var result = resolver.TryResolve("GET", "/badges/packages/nuget/Newtonsoft.Json", out var match);

        Assert.True(result);
        Assert.Equal("NugetPackageBadge", match.Descriptor.Name);

        var parameters = match.Values.ToDictionary();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("nuget", parameters["provider"]);
        Assert.Equal("Newtonsoft.Json", parameters["package"]);
    }

    [Fact]
    public void TryResolve_Should_ExtractParametersForGitHubPackage()
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var result = resolver.TryResolve("GET", "/badges/packages/github/localstack-dotnet/localstack.client", out var match);

        Assert.True(result);
        Assert.Equal("GithubPackagesBadge", match.Descriptor.Name);

        var parameters = match.Values.ToDictionary();
        Assert.Equal(3, parameters.Count);
        Assert.Equal("github", parameters["provider"]);
        Assert.Equal("localstack-dotnet", parameters["org"]);
        Assert.Equal("localstack.client", parameters["package"]);
    }

    [Fact]
    public void TryResolve_Should_ExtractParametersForTestBadge()
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var result = resolver.TryResolve("GET", "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main", out var match);

        Assert.True(result);
        Assert.Equal("TestsBadge", match.Descriptor.Name);

        var parameters = match.Values.ToDictionary();
        Assert.Equal(4, parameters.Count);
        Assert.Equal("linux", parameters["platform"]);
        Assert.Equal("localstack-dotnet", parameters["owner"]);
        Assert.Equal("dotnet-aspire-for-localstack", parameters["repo"]);
        Assert.Equal("main", parameters["branch"]);
    }

    [Fact]
    public void TryResolve_Should_HandleUrlEncodedBranches()
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var result = resolver.TryResolve("GET", "/badges/tests/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge", out var match);

        Assert.True(result);
        Assert.Equal("TestsBadge", match.Descriptor.Name);

        var parameters = match.Values.ToDictionary();
        Assert.Equal("feature/awesome-badge", parameters["branch"]);
    }

    [Theory]
    [InlineData("HEAD", "GET")] // HEAD should be normalized to GET
    [InlineData("head", "GET")] // Case insensitive HEAD
    [InlineData("Head", "GET")] // Mixed case HEAD
    [InlineData("GET", "GET")] // GET stays GET
    [InlineData("POST", "POST")] // Other methods stay unchanged
    [InlineData("PUT", "PUT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("OPTIONS", "OPTIONS")]
    public void TryResolve_Should_NormalizeHeadToGet(string inputMethod, string expectedNormalizedMethod)
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var result = resolver.TryResolve(inputMethod, "/health", out var match);

        if (expectedNormalizedMethod == "GET")
        {
            // HEAD and GET should match routes that accept GET
            Assert.True(result);
            Assert.Equal("Health", match.Descriptor.Name);
        }
        else if (inputMethod == "POST" && inputMethod == expectedNormalizedMethod)
        {
            // POST should work for POST routes
            result = resolver.TryResolve(inputMethod, "/tests/results", out match);
            Assert.True(result);
            Assert.Equal("TestIngestion", match.Descriptor.Name);
        }
        else
        {
            // Other methods should not match GET-only routes
            Assert.False(result);
        }
    }

    [Fact]
    public void TryResolve_Should_PrioritizeExactPatternsOverTemplates()
    {
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("SpecificHealth", "GET", RouteTestBuilder.CreateExactPattern("/health/specific")),
            RouteTestBuilder.CreateRouteDescriptor("TemplateHealth", "GET", RouteTestBuilder.CreateTemplatePattern("/health/{action}")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var result = resolver.TryResolve("GET", "/health/specific", out var match);

        Assert.True(result);
        Assert.Equal("SpecificHealth", match.Descriptor.Name); // Exact pattern should win
    }

    [Theory]
    [InlineData("/health", new[] { "GET", "HEAD", "OPTIONS" })]
    [InlineData("/tests/results", new[] { "POST", "OPTIONS" })] // POST routes don't support HEAD per HTTP standards
    [InlineData("/badges/packages/nuget/Newtonsoft.Json", new[] { "GET", "HEAD", "OPTIONS" })]
    [InlineData("/badges/tests/linux/owner/repo/main", new[] { "GET", "HEAD", "OPTIONS" })]
    [InlineData("/nonexistent/path", new[] { "OPTIONS" })] // Non-matching paths still get OPTIONS
    public void GetAllowedMethods_Should_ReturnCorrectMethods(string path, string[] expectedMethods)
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var allowedMethods = resolver.GetAllowedMethods(path);

        Assert.Equal(expectedMethods.Length, allowedMethods.Count);
        foreach (var expectedMethod in expectedMethods)
        {
            Assert.Contains(expectedMethod, allowedMethods);
        }
    }

    [Fact]
    public void GetAllowedMethods_Should_AddHeadForGetRoutes()
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var allowedMethods = resolver.GetAllowedMethods("/health");

        Assert.Contains("GET", allowedMethods);
        Assert.Contains("HEAD", allowedMethods);
        Assert.Contains("OPTIONS", allowedMethods);
    }

    [Fact]
    public void GetAllowedMethods_Should_AlwaysIncludeOptions()
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var allowedMethods = resolver.GetAllowedMethods("/some/random/path");

        Assert.Contains("OPTIONS", allowedMethods);
    }

    [Fact]
    public void GetAllowedMethods_Should_NotDuplicateHeadIfAlreadyPresent()
    {
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("GetHealth", "GET", RouteTestBuilder.CreateExactPattern("/health")),
            RouteTestBuilder.CreateRouteDescriptor("HeadHealth", "HEAD", RouteTestBuilder.CreateExactPattern("/health")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var allowedMethods = resolver.GetAllowedMethods("/health");

        var headCount = allowedMethods.Count(m => m.Equals("HEAD", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, headCount);
    }

    [Theory]
    [InlineData("POST", "/api/data")]
    [InlineData("PUT", "/api/data/123")]
    [InlineData("DELETE", "/api/data/123")]
    [InlineData("PATCH", "/api/data/123")]
    public void GetAllowedMethods_Should_NotIncludeHeadForNonGetMethods(string method, string path)
    {
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("PostData", "POST", RouteTestBuilder.CreateExactPattern("/api/data")),
            RouteTestBuilder.CreateRouteDescriptor("PutData", "PUT", RouteTestBuilder.CreateTemplatePattern("/api/data/{id}")),
            RouteTestBuilder.CreateRouteDescriptor("DeleteData", "DELETE", RouteTestBuilder.CreateTemplatePattern("/api/data/{id}")),
            RouteTestBuilder.CreateRouteDescriptor("PatchData", "PATCH", RouteTestBuilder.CreateTemplatePattern("/api/data/{id}")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var allowedMethods = resolver.GetAllowedMethods(path);

        Assert.DoesNotContain("HEAD", allowedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("OPTIONS", allowedMethods, StringComparer.OrdinalIgnoreCase); // Should still have OPTIONS
        Assert.Contains(method, allowedMethods, StringComparer.OrdinalIgnoreCase); // Should have the actual method
    }

    [Fact]
    public void GetAllowedMethods_Should_OnlyAddHeadForGetRoutes()
    {
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("GetUser", "GET", RouteTestBuilder.CreateTemplatePattern("/users/{id}")),
            RouteTestBuilder.CreateRouteDescriptor("PostUser", "POST", RouteTestBuilder.CreateExactPattern("/users")),
            RouteTestBuilder.CreateRouteDescriptor("PutUser", "PUT", RouteTestBuilder.CreateTemplatePattern("/users/{id}")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var getRouteAllowedMethods = resolver.GetAllowedMethods("/users/123");
        Assert.Contains("GET", getRouteAllowedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("HEAD", getRouteAllowedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("PUT", getRouteAllowedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("OPTIONS", getRouteAllowedMethods, StringComparer.OrdinalIgnoreCase);

        var postRouteAllowedMethods = resolver.GetAllowedMethods("/users");
        Assert.Contains("POST", postRouteAllowedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("HEAD", postRouteAllowedMethods, StringComparer.OrdinalIgnoreCase); // No GET route matches this path
        Assert.Contains("OPTIONS", postRouteAllowedMethods, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(GetRealWorldRoutingScenarios))]
    public void TryResolve_Should_HandleRealWorldScenarios(string method, string path, bool expectedMatch, string? expectedRouteName,
        IDictionary<string, string>? expectedParameters)
    {
        var routes = CreateTestRoutes();
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        var result = resolver.TryResolve(method, path, out var match);

        Assert.Equal(expectedMatch, result);

        if (expectedMatch)
        {
            Assert.Equal(expectedRouteName, match.Descriptor.Name);

            if (expectedParameters?.Count > 0)
            {
                var actualParameters = match.Values.ToDictionary();
                Assert.Equal(expectedParameters.Count, actualParameters.Count);

                foreach (var (key, expectedValue) in expectedParameters)
                {
                    Assert.True(actualParameters.ContainsKey(key), $"Parameter '{key}' not found");
                    Assert.Equal(expectedValue, actualParameters[key]);
                }
            }
        }
    }

    [SuppressMessage("Design", "MA0051:Method is too long")]
    public static IEnumerable<object?[]> GetRealWorldRoutingScenarios()
    {
        // Valid scenarios with parameters
        yield return
        [
            "GET", "/badges/packages/nuget/Newtonsoft.Json", true, "NugetPackageBadge",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "Newtonsoft.Json"
            },
        ];

        yield return
        [
            "GET", "/badges/packages/nuget/Microsoft.Extensions.Http", true, "NugetPackageBadge",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "Microsoft.Extensions.Http"
            },
        ];

        yield return
        [
            "GET", "/badges/packages/github/localstack-dotnet/localstack.client", true, "GithubPackagesBadge",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "github",
                ["org"] = "localstack-dotnet",
                ["package"] = "localstack.client"
            },
        ];

        yield return
        [
            "GET", "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main", true, "TestsBadge",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "dotnet-aspire-for-localstack",
                ["branch"] = "main"
            },
        ];

        // Exact pattern scenarios
        yield return
        [
            "GET", "/health", true, "Health", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ];

        yield return
        [
            "POST", "/tests/results", true, "TestIngestion", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ];

        // HEAD normalization scenarios
        yield return
        [
            "HEAD", "/health", true, "Health", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ];

        yield return
        [
            "HEAD", "/badges/packages/nuget/AutoMapper", true, "NugetPackageBadge",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "AutoMapper"
            },
        ];

        // Invalid scenarios
        yield return
        [
            "POST", "/health", false, null, null,
        ];

        yield return
        [
            "GET", "/badges", false, null, null,
        ];

        yield return
        [
            "GET", "/badges/packages", false, null, null,
        ];

        yield return
        [
            "DELETE", "/tests/results", false, null, null,
        ];

        yield return
        [
            "GET", "/nonexistent/path", false, null, null,
        ];
    }

    private static RouteDescriptor[] CreateTestRoutes()
    {
        return
        [
            RouteTestBuilder.CreateRouteDescriptor(
                "Health",
                "GET",
                RouteTestBuilder.CreateExactPattern("/health")),

            RouteTestBuilder.CreateRouteDescriptor(
                "NugetPackageBadge",
                "GET",
                RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),

            RouteTestBuilder.CreateRouteDescriptor(
                "GithubPackagesBadge",
                "GET",
                RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{org}/{package}")),

            RouteTestBuilder.CreateRouteDescriptor(
                "TestsBadge",
                "GET",
                RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}")),

            RouteTestBuilder.CreateRouteDescriptor(
                "TestIngestion",
                "POST",
                RouteTestBuilder.CreateExactPattern("/tests/results")),

            RouteTestBuilder.CreateRouteDescriptor(
                "BadgeRedirect",
                "GET",
                RouteTestBuilder.CreateTemplatePattern("/redirect/test-results/{platform}/{owner}/{repo}/{branch}")),
        ];
    }
}
