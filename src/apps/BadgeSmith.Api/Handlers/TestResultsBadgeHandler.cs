using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using BadgeSmith.Domain.Models;

namespace BadgeSmith.Api.Handlers;

internal interface ITestResultsBadgeHandler : IRouteHandler;

internal class TestResultsBadgeHandler : ITestResultsBadgeHandler
{
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(routeContext);
        ArgumentNullException.ThrowIfNull(routeContext.LambdaContext);
        ArgumentNullException.ThrowIfNull(routeContext.Request);
        ArgumentNullException.ThrowIfNull(routeContext.RouteMatch);

        var (_, lambdaContext, _) = routeContext;
        var logger = lambdaContext.Logger;

        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultsBadgeHandler)}.{nameof(HandleAsync)}");

        logger.LogInformation("Test results badge request received");

        var shieldsBadgeResponse = new ShieldsBadgeResponse(1, "tests", "100 passed", "green");

        return Task.FromResult(ResponseHelper.Ok(shieldsBadgeResponse, LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse));
    }
}
