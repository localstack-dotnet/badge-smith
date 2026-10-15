#pragma warning disable CA1812, CA1852, CA1515 // BenchmarkDotNet requires public, non-sealed types instantiated by generated code.

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
[BenchmarkCategory("Buffer")]
public class BufferAllocationBenchmarks
{
    private const string TestPath = "/badges/packages/github/localstack-dotnet/localstack.client";

    private RouteResolver _singleRouteResolver = null!;
    private RouteResolver _multiRouteResolver = null!;
    private RouteResolver _multiRouteMethodsResolver = null!;

    [GlobalSetup]
    public void Setup()
    {
        _singleRouteResolver = RouteTestBuilder.CreateRouteResolver(
            RouteTestBuilder.CreateRouteDescriptor("Test", "GET",
                RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")));

        var routes = new[]
        {
            RouteTestBuilder.CreateRouteDescriptor("Health", "GET", RouteTestBuilder.CreateExactPattern("/health")),
            RouteTestBuilder.CreateRouteDescriptor("NugetPackage", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("GithubPackage", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{org}/{package}")),
            RouteTestBuilder.CreateRouteDescriptor("TestsBadge", "GET", RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}")),
        };
        _multiRouteResolver = RouteTestBuilder.CreateRouteResolver(routes);
        _multiRouteMethodsResolver = RouteTestBuilder.CreateRouteResolver(
        [
            .. routes,
            RouteTestBuilder.CreateRouteDescriptor("TestIngestion", "POST",
                RouteTestBuilder.CreateTemplatePattern("/tests/results/{platform}/{owner}/{repo}/{branch}")),
        ]);
    }

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void RouteValues_Set_Should_Measure_Fixed_Array_When_2_Parameters()
    {
        var buffer = new (string, int, int)[8]; // Current strategy
        var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan());

        values.Set("provider", 17, 6); // "github"
        values.Set("package", 43, 16); // "localstack.client"
    }

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void RouteValues_Set_Should_Measure_Fixed_Array_When_4_Parameters()
    {
        var buffer = new (string, int, int)[8]; // Current strategy
        var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan());

        values.Set("platform", 0, 6);
        values.Set("owner", 7, 10);
        values.Set("repo", 18, 12);
        values.Set("branch", 31, 8);
    }

    [Benchmark]
    public void RouteValues_Set_Should_Measure_Fixed_Array_When_8_Parameters()
    {
        var buffer = new (string, int, int)[8]; // Current strategy - will fill exactly
        var values = new RouteValues(TestPath.AsSpan(), buffer.AsSpan());

        for (var i = 0; i < 8; i++)
        {
            values.Set($"param{i}", i * 5, 4);
        }
    }

    [Benchmark]
    public void RouteValues_Set_Should_Measure_ArrayPool_When_2_Parameters()
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
    public void RouteValues_Should_Measure_Parameter_Extraction_When_Using_String()
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
    public void RouteValues_Should_Measure_Parameter_Extraction_When_Using_Span()
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
    [BenchmarkCategory("Quick")]
    public bool RouteResolver_TryResolve_Should_Measure_Single_Route_Case()
    {
        return _singleRouteResolver.TryResolve("GET", "/badges/packages/nuget/TestPackage", out _);
    }

    [Benchmark]
    public IReadOnlyList<string> RouteResolver_GetAllowedMethods_Should_Measure_Single_Route_Case()
    {
        return _singleRouteResolver.GetAllowedMethods("/badges/packages/nuget/TestPackage");
    }

    [Benchmark]
    public void RouteValues_Should_Measure_Dictionary_Conversion()
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
    public IReadOnlyList<string> RouteResolver_GetAllowedMethods_Should_Measure_Multi_Route_Case()
    {
        return _multiRouteMethodsResolver.GetAllowedMethods("/badges/packages/nuget/TestPackage");
    }

    [Benchmark]
    public bool RouteResolver_TryResolve_Should_Measure_Multi_Route_Case()
    {
        return _multiRouteResolver.TryResolve("GET", "/badges/packages/nuget/TestPackage", out _);
    }

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void BufferAllocation_Should_Measure_Three_Independent_Buffers()
    {
        // Synthetic per-route buffers, not an alternate production resolver.
        for (var i = 0; i < 3; i++) // Simulate 3 route checks
        {
            var paramBuffer = new (string, int, int)[8]; // NEW buffer each time
            var values = new RouteValues(TestPath.AsSpan(), paramBuffer.AsSpan());
            values.Set("provider", 17, 6);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Quick")]
    public void BufferAllocation_Should_Measure_One_Buffer_Across_Three_Route_Checks()
    {
        // Synthetic shared buffer; the three route checks form one benchmark operation.
        var paramBuffer = new (string, int, int)[8]; // SHARED buffer
        for (var i = 0; i < 3; i++) // Simulate 3 route checks
        {
            var values = new RouteValues(TestPath.AsSpan(), paramBuffer.AsSpan());
            values.Set("provider", 17, 6);
        }
    }
}

#pragma warning restore CA1812, CA1852, CA1515
