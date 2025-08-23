using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;

namespace BadgeSmith.Api.Handlers;

internal interface IHealthCheckHandler : IRouteHandler;

internal class HealthCheckHandler : IHealthCheckHandler
{
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContextV2 routeContext, ILambdaContext lambdaContext, CancellationToken ct = default)
    {
        var logger = lambdaContext.Logger;

        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(HealthCheckHandler)}.{nameof(HandleAsync)}");

        logger.LogInformation("Health check request received");

        return Task.FromResult(ResponseHelper.OkHealth("Healthy", DateTimeOffset.UtcNow));
    }
}
