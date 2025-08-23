using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;

namespace BadgeSmith.Api.Handlers;

internal interface ITestResultIngestionHandler : IRouteHandler;

internal class TestResultIngestionHandler : ITestResultIngestionHandler
{
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContextV2 routeContext, ILambdaContext lambdaContext, CancellationToken ct = default)
    {
        var logger = lambdaContext.Logger;

        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultIngestionHandler)}.{nameof(HandleAsync)}");

        logger.LogInformation("Test result ingest badge request received");

        return Task.FromResult(ResponseHelper.Created("""{"test_result_id":"badge-smith-test-result-id"}"""));
    }
}
