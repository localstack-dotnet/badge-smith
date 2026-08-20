#pragma warning disable CA1812, CA1852, CA1515 // BenchmarkDotNet requires public, non-sealed types instantiated by generated code.

using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Patterns;
using BadgeSmith.Api.Performance.Tests.TestHelpers;
using BenchmarkDotNet.Attributes;

namespace BadgeSmith.Api.Performance.Tests;

/// <summary>
/// Performance benchmarks for routing components to ensure zero-allocation goals.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class RoutingBenchmarks
{
    private RouteResolver _resolver = null!;
    private TemplatePattern _nugetPattern = null!;
    private TemplatePattern _githubPattern = null!;
    private TemplatePattern _testPattern = null!;
    private ExactPattern _healthPattern = null!;

    [GlobalSetup]
    public void Setup()
    {
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("Health", "GET", RouteTestBuilder.CreateExactPattern("/health")),
            RouteTestBuilder.CreateRouteDescriptor("NugetPackageBadge", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("GithubPackagesBadge", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{org}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("TestsBadge", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}")),
            RouteTestBuilder.CreateRouteDescriptor("TestIngestion", "POST", RouteTestBuilder.CreateExactPattern("/tests/results")),
            RouteTestBuilder.CreateRouteDescriptor("BadgeRedirect", "GET", RouteTestBuilder.CreateTemplatePattern("/redirect/test-results/{platform}/{owner}/{repo}/{branch}")),
        };

        _resolver = RouteTestBuilder.CreateRouteResolver(routes);
        _nugetPattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        _githubPattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{org}/{package}");
        _testPattern = RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}");
        _healthPattern = RouteTestBuilder.CreateExactPattern("/health");
    }

    [Benchmark]
    [Arguments("/health")]
    [Arguments("/badges/packages/nuget/Newtonsoft.Json")]
    [Arguments("/badges/packages/github/localstack-dotnet/localstack.client")]
    [Arguments("/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main")]
    [Arguments("/redirect/test-results/linux/localstack-dotnet/dotnet-aspire-for-localstack/main")]
    public bool RouteResolver_TryResolve_Should_Measure_Route_Resolution(string path)
    {
        return _resolver.TryResolve("GET", path, out _);
    }

    [Benchmark]
    [Arguments("/badges/packages/nuget/Newtonsoft.Json")]
    [Arguments("/badges/packages/nuget/Microsoft.Extensions.Http")]
    [Arguments("/badges/packages/nuget/AutoMapper")]
    [Arguments("/badges/packages/nuget/FluentValidation")]
    public bool TemplatePattern_TryMatch_Should_Measure_NuGet_Package_Route(string path)
    {
        var values = RouteTestBuilder.CreateRouteValues(path);
        return _nugetPattern.TryMatch(path.AsSpan(), ref values);
    }

    [Benchmark]
    [Arguments("/badges/packages/github/localstack-dotnet/localstack.client")]
    [Arguments("/badges/packages/github/microsoft/vscode")]
    [Arguments("/badges/packages/github/facebook/react")]
    [Arguments("/badges/packages/github/AutoMapper/AutoMapper")]
    public bool TemplatePattern_TryMatch_Should_Measure_GitHub_Package_Route(string path)
    {
        var values = RouteTestBuilder.CreateRouteValues(path);
        return _githubPattern.TryMatch(path.AsSpan(), ref values);
    }

    [Benchmark]
    [Arguments("/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main")]
    [Arguments("/badges/tests/windows/microsoft/vscode/main")]
    [Arguments("/badges/tests/macos/facebook/react/main")]
    [Arguments("/badges/tests/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge")]
    public bool TemplatePattern_TryMatch_Should_Measure_Test_Badge_Route(string path)
    {
        var values = RouteTestBuilder.CreateRouteValues(path);
        return _testPattern.TryMatch(path.AsSpan(), ref values);
    }

    [Benchmark]
    [Arguments("/health")]
    [Arguments("/tests/results")]
    public bool ExactPattern_TryMatch_Should_Measure_Exact_Route(string path)
    {
        var values = RouteTestBuilder.CreateRouteValues(path);
        return _healthPattern.TryMatch(path.AsSpan(), ref values);
    }

    [Benchmark]
    public void RouteValues_Should_Measure_Parameter_Extraction()
    {
        const string path = "/badges/packages/github/localstack-dotnet/localstack.client";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Simulate parameter extraction like TemplatePattern does
        values.Set("provider", 17, 6); // "github"
        values.Set("org", 24, 18); // "localstack-dotnet"
        values.Set("package", 43, 16); // "localstack.client"

        // Extract parameters
        _ = values.TryGetString("provider", out _);
        _ = values.TryGetString("org", out _);
        _ = values.TryGetString("package", out _);
    }

    [Benchmark]
    public void RouteValues_Should_Measure_Span_Extraction()
    {
        const string path = "/badges/packages/github/localstack-dotnet/localstack.client";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Simulate parameter extraction using spans (more efficient)
        values.Set("provider", 17, 6);
        values.Set("org", 24, 18);
        values.Set("package", 43, 16);

        // Extract as spans (no allocation)
        _ = values.TryGetSpan("provider", out _);
        _ = values.TryGetSpan("org", out _);
        _ = values.TryGetSpan("package", out _);
    }

    [Benchmark]
    [Arguments("/health")]
    [Arguments("/badges/packages/nuget/Newtonsoft.Json")]
    [Arguments("/badges/tests/linux/owner/repo/main")]
    [Arguments("/nonexistent/path")]
    public IReadOnlyCollection<string> RouteResolver_GetAllowedMethods_Should_Measure_Allowed_Method_Discovery(string path)
    {
        return [.. _resolver.GetAllowedMethods(path)];
    }

    [Benchmark]
    public void TemplatePattern_Should_Measure_Complex_Parameter_Extraction()
    {
        const string path = "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/feature%2Fawesome-badge";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // This simulates the most complex pattern with URL-encoded branch names
        var result = _testPattern.TryMatch(path.AsSpan(), ref values);

        if (result)
        {
            _ = values.TryGetString("platform", out _);
            _ = values.TryGetString("owner", out _);
            _ = values.TryGetString("repo", out _);
            _ = values.TryGetString("branch", out _);
        }
    }

    [Benchmark]
    public void RouteResolver_Should_Measure_Full_Pipeline_When_Route_Matches()
    {
        const string method = "GET";
        const string path = "/badges/packages/nuget/Newtonsoft.Json";

        // This tests the complete routing pipeline for a successful match
        var resolved = _resolver.TryResolve(method, path, out var match);

        if (!resolved)
        {
            return;
        }

        // Access the matched route information
        _ = match.Descriptor.Name;
        _ = match.Descriptor.Method;

        // Extract parameters
        _ = match.Values.TryGetString("provider", out _);
        _ = match.Values.TryGetString("package", out _);
    }

    [Benchmark]
    public void RouteResolver_Should_Measure_Full_Pipeline_When_Route_Is_Not_Found()
    {
        const string method = "GET";
        const string path = "/nonexistent/path/that/wont/match";

        // This tests the complete routing pipeline for a failed match
        var resolved = _resolver.TryResolve(method, path, out var match);

        // Should be false, but we still access to ensure no shortcuts
        if (resolved)
        {
            _ = match.Descriptor.Name;
        }
    }
}

#pragma warning restore CA1812, CA1852, CA1515
