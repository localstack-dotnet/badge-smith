using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using BadgeSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal interface ITestResultsBadgeHandler : IRouteHandler;

internal class TestResultsBadgeHandler : ITestResultsBadgeHandler
{
    private readonly ILogger<TestResultsBadgeHandler> _logger;

    public TestResultsBadgeHandler(ILogger<TestResultsBadgeHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContextSnapshot routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultsBadgeHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Test results badge request received");

        var shieldsBadgeResponse = new ShieldsBadgeResponse(1, "tests", "100 passed", "green");

        await Task.Yield(); // Ensure we're truly async

        return ResponseHelper.Ok(shieldsBadgeResponse, LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse);
    }
}
