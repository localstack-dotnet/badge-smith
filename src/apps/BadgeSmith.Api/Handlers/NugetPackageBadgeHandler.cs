using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using BadgeSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal interface INugetPackageBadgeHandler : IRouteHandler;

internal class NugetPackageBadgeHandler : INugetPackageBadgeHandler
{
    private readonly ILogger<NugetPackageBadgeHandler> _logger;

    public NugetPackageBadgeHandler(ILogger<NugetPackageBadgeHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NugetPackageBadgeHandler)}.{nameof(HandleAsync)}");
        _logger.LogInformation("Nuget packages badge request received");

        routeContext.Request.Headers.TryGetValue("if-none-match", out var ifNoneMatch);

        var cache = new ResponseHelper.CacheSettings(
            SMaxAgeSeconds: 10, // CloudFront caches 60s
            MaxAgeSeconds: 5, // browsers 10s
            StaleWhileRevalidateSeconds: 15,
            StaleIfErrorSeconds: 60);

        var shieldsBadgeResponse = new ShieldsBadgeResponse(1, "nuget", "1.0.0", "blue", NamedLogo: "nuget");

        await Task.Yield();

        return ResponseHelper.OkCached(
            shieldsBadgeResponse,
            LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
            ifNoneMatchHeader: ifNoneMatch,
            cache: cache,
            lastModifiedUtc: null // set to a real value when you have ‘updatedAt’
        );
    }
}
