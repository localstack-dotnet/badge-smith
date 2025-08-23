using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;

namespace BadgeSmith.Api.Handlers;

internal interface IHealthCheckHandler : IRouteHandler;

internal class HealthCheckHandler : IHealthCheckHandler
{
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(routeContext);
        ArgumentNullException.ThrowIfNull(routeContext.LambdaContext);
        ArgumentNullException.ThrowIfNull(routeContext.Request);
        ArgumentNullException.ThrowIfNull(routeContext.RouteMatch);

        var (_, lambdaContext, _) = routeContext;
        var logger = lambdaContext.Logger;

        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(HealthCheckHandler)}.{nameof(HandleAsync)}");

        logger.LogInformation("Health check request received");

        return Task.FromResult(ResponseHelper.OkHealth("Healthy", DateTimeOffset.UtcNow));
    }
}
