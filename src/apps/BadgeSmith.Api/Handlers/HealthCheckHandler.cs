using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal interface IHealthCheckHandler : IRouteHandler;

internal class HealthCheckHandler : IHealthCheckHandler
{
    private readonly ILogger<HealthCheckHandler> _logger;

    public HealthCheckHandler(ILogger<HealthCheckHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContextSnapshot routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(HealthCheckHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Health check request received");

        await Task.Yield(); // Ensure we're truly async

        return ResponseHelper.OkHealth("Healthy", DateTimeOffset.UtcNow);
    }
}
