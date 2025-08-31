using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Handlers.Contracts;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal class TestResultRedirectionHandler : ITestResultRedirectionHandler
{
    private readonly ILogger<TestResultRedirectionHandler> _logger;

    public TestResultRedirectionHandler(ILogger<TestResultRedirectionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultRedirectionHandler)}.{nameof(HandleAsync)}");
        _logger.LogInformation("Test result redirection request received");

        await Task.Yield();

        return ResponseHelper.Redirect(
            location: "https://github.com/localstack-dotnet/localstack-dotnet-client/runs/46719603897",
            sMaxAge: 10,                    // CloudFront caches 60s
            maxAge: 5,                     // browsers 10s
            staleWhileRevalidate: 15,
            staleIfError: 60
        );
    }
}
