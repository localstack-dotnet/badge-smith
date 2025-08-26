using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal interface ITestResultIngestionHandler : IRouteHandler;

internal class TestResultIngestionHandler : ITestResultIngestionHandler
{
    private readonly ILogger<TestResultIngestionHandler> _logger;

    public TestResultIngestionHandler(ILogger<TestResultIngestionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContextSnapshot routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultIngestionHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Test result ingest badge request received");

        await Task.Yield(); // Ensure we're truly async

        return ResponseHelper.Created("""{"test_result_id":"badge-smith-test-result-id"}""");
    }
}
