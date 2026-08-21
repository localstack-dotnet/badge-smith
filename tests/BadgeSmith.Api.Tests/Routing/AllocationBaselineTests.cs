using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Infrastructure;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Features;
using BadgeSmith.Api.Features.TestResults.Models;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing;

[Trait("Category", TestCategories.Unit)]
public sealed class AllocationBaselineTests
{
    private const int WarmupIterations = 64;
    private const long CachedBadgeResponseAllocationBaselineBytes = 1208;
    private const long CachedRedirectAllocationBaselineBytes = 1096;
    private const long NoStoreRedirectAllocationBaselineBytes = 352;
    private const long TestResultRouteParameterExtractionAllocationBaselineBytes = 64;
    private const string RedirectLocation = "https://example.com/results/42";

    private static readonly ShieldsBadgeResponse CachedBadge = new(1, "NuGet", "1.2.3", "blue");

    private static readonly ResponseHelper.CacheSettings PublicCacheSettings = new(
        SMaxAgeSeconds: 600,
        MaxAgeSeconds: 300,
        SwrSeconds: 1200,
        SieSeconds: 3600);

    private static readonly RouteContext TestResultRouteContext = new(
        new APIGatewayHttpApiV2ProxyRequest(),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["platform"] = "linux",
            ["owner"] = "owner",
            ["repo"] = "repo",
            ["branch"] = "main"
        });

    [Fact]
    public void OkCached_Should_Not_Exceed_Allocation_Baseline_When_Warmed()
    {
        var allocatedBytes = MeasureAllocation(CreateCachedBadgeResponse);

        Assert.InRange(allocatedBytes, 0, CachedBadgeResponseAllocationBaselineBytes);
    }

    [Fact]
    public void Redirect_Should_Not_Exceed_Cached_Allocation_Baseline_When_Warmed()
    {
        var allocatedBytes = MeasureAllocation(CreateCachedRedirectResponse);

        Assert.InRange(allocatedBytes, 0, CachedRedirectAllocationBaselineBytes);
    }

    [Fact]
    public void Redirect_Should_Not_Exceed_NoStore_Allocation_Baseline_When_Warmed()
    {
        var allocatedBytes = MeasureAllocation(CreateNoStoreRedirectResponse);

        Assert.InRange(allocatedBytes, 0, NoStoreRedirectAllocationBaselineBytes);
    }

    [Fact]
    public void TestResultRouteParameterExtraction_Should_Not_Exceed_Allocation_Baseline_When_Warmed()
    {
        var allocatedBytes = MeasureAllocation(ExtractTestResultRouteParameters);

        Assert.True(
            allocatedBytes <= TestResultRouteParameterExtractionAllocationBaselineBytes,
            $"Measured {allocatedBytes} bytes; baseline is {TestResultRouteParameterExtractionAllocationBaselineBytes}.");
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateCachedBadgeResponse()
    {
        return ResponseHelper.OkCached(
            CachedBadge,
            LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
            cache: PublicCacheSettings);
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateCachedRedirectResponse()
    {
        return ResponseHelper.Redirect(
            RedirectLocation,
            sMaxAge: 600,
            maxAge: 300,
            staleWhileRevalidate: 1200,
            staleIfError: 3600);
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateNoStoreRedirectResponse()
    {
        return ResponseHelper.Redirect(RedirectLocation, noStore: true);
    }

    private static string ExtractTestResultRouteParameters()
    {
        var result = TestResultRouteParameters.Extract(TestResultRouteContext);
        return result.Parameters.Branch;
    }

    private static long MeasureAllocation<T>(Func<T> action)
        where T : class
    {
        for (var iteration = 0; iteration < WarmupIterations; iteration++)
        {
            GC.KeepAlive(action());
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = action();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(result);

        return allocatedBytes;
    }
}
