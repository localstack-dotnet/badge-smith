using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using BadgeSmith.Domain.Models;

namespace BadgeSmith.Api.Handlers;

internal interface INugetPackageBadgeHandler : IRouteHandler;

internal class NugetPackageBadgeHandler : INugetPackageBadgeHandler
{
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, ILambdaContext lambdaContext, CancellationToken ct = default)
    {
        var logger = lambdaContext.Logger;

        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NugetPackageBadgeHandler)}.{nameof(HandleAsync)}");

        logger.LogInformation("Nuget packages badge request received");

        var shieldsBadgeResponse = new ShieldsBadgeResponse(1, "nuget", "1.0.0", "blue", NamedLogo: "nuget");

        return Task.FromResult(ResponseHelper.Ok(shieldsBadgeResponse, LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse));
    }
}
