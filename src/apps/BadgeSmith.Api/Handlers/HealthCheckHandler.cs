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

    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext context, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(HealthCheckHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Health check request received");

        return Task.FromResult(ResponseHelper.OkHealth("Healthy", DateTimeOffset.UtcNow));
    }
}
