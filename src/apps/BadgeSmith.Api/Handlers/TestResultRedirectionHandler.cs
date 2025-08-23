using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;

namespace BadgeSmith.Api.Handlers;

internal interface ITestResultRedirectionHandler : IRouteHandler;

internal class TestResultRedirectionHandler : ITestResultRedirectionHandler
{
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(routeContext);
        ArgumentNullException.ThrowIfNull(routeContext.LambdaContext);
        ArgumentNullException.ThrowIfNull(routeContext.Request);
        ArgumentNullException.ThrowIfNull(routeContext.RouteMatch);

        var (_, lambdaContext, _) = routeContext;
        var logger = lambdaContext.Logger;

        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultRedirectionHandler)}.{nameof(HandleAsync)}");

        logger.LogInformation("Test result redirection request received");

        return Task.FromResult(ResponseHelper.Redirect(
            location: "https://github.com/localstack-dotnet/localstack-dotnet-client/runs/46719603897",
            cacheControl: "public, max-age=60"));
    }
}
