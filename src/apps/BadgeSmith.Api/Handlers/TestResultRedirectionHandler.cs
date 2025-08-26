using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal interface ITestResultRedirectionHandler : IRouteHandler;

internal class TestResultRedirectionHandler : ITestResultRedirectionHandler
{
    private readonly ILogger<TestResultRedirectionHandler> _logger;

    public TestResultRedirectionHandler(ILogger<TestResultRedirectionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContextSnapshot routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultRedirectionHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Test result redirection request received");

        await Task.Yield();

        return ResponseHelper.Redirect(
            location: "https://github.com/localstack-dotnet/localstack-dotnet-client/runs/46719603897",
            cacheControl: "public, max-age=60");
    }
}
