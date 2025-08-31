using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Infrastructure.Handlers.Contracts;
using BadgeSmith.Api.Infrastructure.Routing;
using BadgeSmith.Api.Infrastructure.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Infrastructure.Handlers;

internal class HealthCheckHandler : IHealthCheckHandler
{
    private readonly ILogger<HealthCheckHandler> _logger;

    public HealthCheckHandler(ILogger<HealthCheckHandler> logger)
    {
        _logger = logger;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(HealthCheckHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Health check request received");

        await Task.Yield(); // Ensure we're truly async

        return ResponseHelper.OkHealthWithNoCache("Healthy", DateTimeOffset.UtcNow);
    }
}
