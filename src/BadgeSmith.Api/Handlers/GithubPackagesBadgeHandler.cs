using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Domain.Models;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal interface IGithubPackagesBadgeHandler : IRouteHandler;

internal class GithubPackagesBadgeHandler : IGithubPackagesBadgeHandler
{
    private readonly ILogger<GithubPackagesBadgeHandler> _logger;

    public GithubPackagesBadgeHandler(ILogger<GithubPackagesBadgeHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(GithubPackagesBadgeHandler)}.{nameof(HandleAsync)}");
        _logger.LogInformation("Github packages badge request received");

        routeContext.Request.Headers.TryGetValue("if-none-match", out var ifNoneMatch);

        var cache = new ResponseHelper.CacheSettings(
            SMaxAgeSeconds: 10, // CloudFront caches 60s
            MaxAgeSeconds: 5, // browsers 10s
            SwrSeconds: 15,
            SieSeconds: 60);

        var shieldsBadgeResponse = new ShieldsBadgeResponse(1, "github", "1.0.0", "green", NamedLogo: "github");

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
