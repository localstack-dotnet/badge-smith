using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;

namespace BadgeSmith.Api.Handlers;

internal interface ITestResultIngestionHandler : IRouteHandler;

internal class TestResultIngestionHandler : ITestResultIngestionHandler
{
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(routeContext);
        ArgumentNullException.ThrowIfNull(routeContext.LambdaContext);
        ArgumentNullException.ThrowIfNull(routeContext.Request);
        ArgumentNullException.ThrowIfNull(routeContext.RouteMatch);

        var (_, lambdaContext, _) = routeContext;
        var logger = lambdaContext.Logger;

        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultIngestionHandler)}.{nameof(HandleAsync)}");

        logger.LogInformation("Test result ingest badge request received");

        return Task.FromResult(ResponseHelper.Created("""{"test_result_id":"badge-smith-test-result-id"}"""));
    }
}
