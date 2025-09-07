using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Features.HealthCheck;

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

        await Task.Yield();

        return ResponseHelper.OkHealthWithNoCache("Healthy", DateTimeOffset.UtcNow);
    }
}
