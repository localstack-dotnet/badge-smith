using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using BadgeSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal interface INugetPackageBadgeHandler : IRouteHandler;

internal class NugetPackageBadgeHandler : INugetPackageBadgeHandler
{
    private readonly ILogger<NugetPackageBadgeHandler> _logger;

    public NugetPackageBadgeHandler(ILogger<NugetPackageBadgeHandler> logger)
    {
        _logger = logger;
    }

    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext context, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NugetPackageBadgeHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Nuget packages badge request received");

        var shieldsBadgeResponse = new ShieldsBadgeResponse(1, "nuget", "1.0.0", "blue", NamedLogo: "nuget");

        return Task.FromResult(ResponseHelper.Ok(shieldsBadgeResponse, LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse));
    }
}
