#pragma warning disable CA1812,CA1852,CA1515

using System.Buffers;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Performance.Tests.TestHelpers;
using BenchmarkDotNet.Attributes;

namespace BadgeSmith.Api.Performance.Tests;

/// <summary>
/// Performance benchmarks focused specifically on buffer allocation patterns in routing.
/// Tests different buffer management strategies to optimize memory usage and allocation patterns.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
[BenchmarkCategory("Buffer")]
public class BufferAllocationBenchmarks
{
    private const string TestPath = "/badges/packages/github/localstack-dotnet/localstack.client";

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void RouteValues_Set_2Parameters_FixedArray()
    {
        var buffer = new (string, int, int)[8]; // Current strategy
        var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan());

        values.Set("provider", 17, 6); // "github"
        values.Set("package", 43, 16); // "localstack.client"
    }

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void RouteValues_Set_4Parameters_FixedArray()
    {
        var buffer = new (string, int, int)[8]; // Current strategy
        var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan());

        values.Set("platform", 0, 6);
        values.Set("owner", 7, 10);
        values.Set("repo", 18, 12);
        values.Set("branch", 31, 8);
    }

    [Benchmark]
    public void RouteValues_Set_8Parameters_FixedArray()
    {
        var buffer = new (string, int, int)[8]; // Current strategy - will fill exactly
        var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan());

        for (var i = 0; i < 8; i++)
        {
            values.Set($"param{i}", i * 5, 4);
        }
    }

    [Benchmark]
    public void RouteValues_Set_2Parameters_ArrayPool()
    {
        var buffer = ArrayPool<(string, int, int)>.Shared.Rent(8);
        try
        {
            var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan(0, 8));

            values.Set("provider", 17, 6);
            values.Set("package", 43, 16);
        }
        finally
        {
            ArrayPool<(string, int, int)>.Shared.Return(buffer);
        }
    }

    [Benchmark]
    public void RouteValues_ParameterExtraction_String()
    {
        var buffer = new (string, int, int)[8];
        var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan());

        values.Set("provider", 17, 6);
        values.Set("package", 43, 16);

        // String extraction (allocates due to URL decoding)
        _ = values.TryGetString("provider", out _);
        _ = values.TryGetString("package", out _);
    }

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void RouteValues_ParameterExtraction_Span()
    {
        var buffer = new (string, int, int)[8];
        var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan());

        values.Set("provider", 17, 6);
        values.Set("package", 43, 16);

        // Span extraction (zero allocation)
        _ = values.TryGetSpan("provider", out _);
        _ = values.TryGetSpan("package", out _);
    }

    [Benchmark]
    public void RouteResolver_TryResolve_Current()
    {
        // Simulate the current RouteResolver.TryResolve an allocation pattern
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("Test", "GET",
                RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        // This allocates a new buffer on every call (current issue)
        _ = resolver.TryResolve("GET", "/badges/packages/nuget/TestPackage", out _);
    }

    [Benchmark]
    public void RouteResolver_GetAllowedMethods_Current()
    {
        // Simulate the current RouteResolver.GetAllowedMethods allocation pattern
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("Test", "GET",
                RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        // This allocates a new buffer for EACH route check (major issue)
        _ = resolver.GetAllowedMethods("/badges/packages/nuget/TestPackage");
    }

    [Benchmark]
    public void RouteValues_Dictionary_Conversion()
    {
        var buffer = new (string, int, int)[8];
        var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan());

        values.Set("provider", 17, 6);
        values.Set("org", 24, 18);
        values.Set("package", 43, 16);

        // Test the allocation cost of dictionary conversion
        _ = values.ToImmutableDictionary();
    }

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void RouteResolver_TryResolve_Optimized()
    {
        // Test the optimized version with buffer sharing - same logic but tests our fix
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("OptimizedTest", "GET",
                RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        // This should now reuse buffer across route checks (optimized implementation)
        _ = resolver.TryResolve("GET", "/badges/packages/nuget/OptimizedPackage", out _);
    }

    [Benchmark]
    public void RouteResolver_MultiRoute_GetAllowedMethods()
    {
        // Test with multiple routes to see real-world buffer reuse impact
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("Health", "GET", RouteTestBuilder.CreateExactPattern("/health")),
            RouteTestBuilder.CreateRouteDescriptor("NugetPackage", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("GithubPackage", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{org}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("TestsBadge", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}")),
            RouteTestBuilder.CreateRouteDescriptor("TestIngestion", "POST", RouteTestBuilder.CreateExactPattern("/tests/results")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        // This tests buffer reuse across multiple route patterns
        _ = resolver.GetAllowedMethods("/badges/packages/nuget/TestPackage");
    }

    [Benchmark]
    public void RouteResolver_MultiRoute_TryResolve()
    {
        // Test TryResolve with multiple routes
        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("Health", "GET", RouteTestBuilder.CreateExactPattern("/health")),
            RouteTestBuilder.CreateRouteDescriptor("NugetPackage", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("GithubPackage", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{org}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("TestsBadge", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}")),
        };
        var resolver = RouteTestBuilder.CreateRouteResolver(routes);

        // This tests optimized buffer sharing across multiple route checks
        _ = resolver.TryResolve("GET", "/badges/packages/nuget/TestPackage", out _);
    }

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void BufferAllocation_Isolated_Current()
    {
        // Test JUST the buffer allocation (current approach - per route)
        for (var i = 0; i < 3; i++) // Simulate 3 route checks
        {
            var paramBuffer = new (string, int, int)[8]; // NEW buffer each time
            var values = new RouteValues(TestPath.AsSpan(), paramBuffer.AsSpan());
            values.Set("provider", 17, 6);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void BufferAllocation_Isolated_Optimized()
    {
        // Test JUST the buffer allocation (optimized approach - shared)
        var paramBuffer = new (string, int, int)[8]; // SHARED buffer
        for (var i = 0; i < 3; i++) // Simulate 3 route checks
        {
            var values = new RouteValues(TestPath.AsSpan(), paramBuffer.AsSpan());
            values.Set("provider", 17, 6);
        }
    }
}
