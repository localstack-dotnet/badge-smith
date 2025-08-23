using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;

namespace BadgeSmith.Api.Handlers;

internal interface ITestResultRedirectionHandler : IRouteHandler;

internal class TestResultRedirectionHandler : ITestResultRedirectionHandler
{
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContextV2 routeContext, ILambdaContext lambdaContext, CancellationToken ct = default)
    {
        var logger = lambdaContext.Logger;

        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultRedirectionHandler)}.{nameof(HandleAsync)}");

        logger.LogInformation("Test result redirection request received");

        return Task.FromResult(ResponseHelper.Redirect(
            location: "https://github.com/localstack-dotnet/localstack-dotnet-client/runs/46719603897",
            cacheControl: "public, max-age=60"));
    }
}
