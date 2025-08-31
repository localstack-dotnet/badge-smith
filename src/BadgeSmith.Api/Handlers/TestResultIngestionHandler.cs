using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Handlers.Contracts;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal class TestResultIngestionHandler : ITestResultIngestionHandler
{
    private readonly ILogger<TestResultIngestionHandler> _logger;

    public TestResultIngestionHandler(ILogger<TestResultIngestionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultIngestionHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Test result ingest badge request received");

        await Task.Yield(); // Ensure we're truly async

        var noCacheHeaders = ResponseHelper.NoCacheHeaders("application/json; charset=utf-8");

        return ResponseHelper.Created("""{"test_result_id":"badge-smith-test-result-id"}""", () => noCacheHeaders);
    }
}
