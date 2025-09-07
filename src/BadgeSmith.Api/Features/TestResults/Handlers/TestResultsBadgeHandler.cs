using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Features.TestResults.Contracts;
using Microsoft.Extensions.Logging;
using LambdaFunctionJsonSerializerContext = BadgeSmith.Api.Core.Infrastructure.LambdaFunctionJsonSerializerContext;

namespace BadgeSmith.Api.Features.TestResults.Handlers;

internal class TestResultsBadgeHandler : ITestResultsBadgeHandler
{
    private readonly ILogger<TestResultsBadgeHandler> _logger;

    public TestResultsBadgeHandler(ILogger<TestResultsBadgeHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultsBadgeHandler)}.{nameof(HandleAsync)}");
        _logger.LogInformation("Test results badge request received");

        routeContext.Request.Headers.TryGetValue("if-none-match", out var ifNoneMatch);

        var cache = new ResponseHelper.CacheSettings(
            SMaxAgeSeconds: 10, // CloudFront caches 60s
            MaxAgeSeconds: 5, // browsers 10s
            SwrSeconds: 15,
            SieSeconds: 60);

        var shieldsBadgeResponse = new ShieldsBadgeResponse(1, "tests", "100 passed", "green");

        await Task.Yield(); // Ensure we're truly async

        return ResponseHelper.OkCached(
            shieldsBadgeResponse,
            LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
            ifNoneMatchHeader: ifNoneMatch,
            cache: cache,
            lastModifiedUtc: null // set to a real value when you have ‘updatedAt’
        );
    }
}
