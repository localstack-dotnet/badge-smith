using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using BadgeSmith.Domain.Models;
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

    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext context, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(GithubPackagesBadgeHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Github packages badge request received");

        var shieldsBadgeResponse = new ShieldsBadgeResponse(1, "github", "1.0.0", "green", NamedLogo: "github");

        return Task.FromResult(ResponseHelper.Ok(shieldsBadgeResponse, LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse));
    }
}
