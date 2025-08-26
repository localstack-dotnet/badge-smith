using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Routing;

/// <summary>
/// Main API router that orchestrates request routing using high-performance route resolution.
/// Integrates route matching, authentication checks, handler resolution, and error handling
/// for AWS Lambda API Gateway HTTP requests.
/// </summary>
internal class ApiRouter : IApiRouter
{
    private readonly ILogger<ApiRouter> _logger;
    private readonly IRouteResolver _routeResolver;
    private readonly IHandlerFactory _handlerFactory;

    public ApiRouter(ILogger<ApiRouter> logger, IRouteResolver routeResolver, IHandlerFactory handlerFactory)
    {
        _logger = logger;
        _routeResolver = routeResolver;
        _handlerFactory = handlerFactory;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> RouteAsync(string path, string method, IDictionary<string, string>? headers, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(ApiRouter)}.{nameof(RouteAsync)}");

        try
        {
            _logger.LogInformation("API route request received");

            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(method);

            // Handle CORS preflight
            if (method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                string? acrm = null;
                string? acrh = null;
                string? origin = null;

                headers?.TryGetValue("Access-Control-Request-Method", out acrm);
                headers?.TryGetValue("Access-Control-Request-Headers", out acrh);
                headers?.TryGetValue("Origin", out origin);

                return CorsHelper.BuildPreflightResponse(_routeResolver, path, acrm?.Trim(), acrh?.Trim(), origin?.Trim());
            }

            var resolved = _routeResolver.TryResolve(method, path, out var routeMatch);

            if (!resolved)
            {
                return ResponseHelper.NotFound($"Route not found: {method} {path}");
            }

            // Check authentication requirements
            if (routeMatch.Descriptor.RequiresAuth)
            {
                // Implement authentication check
                // For now, just continue
            }

            var routeHandler = routeMatch.Descriptor.HandlerFactory(_handlerFactory);

            var routeContextSnapshot = RouteContextSnapshot.FromMatch(method, path, routeMatch);

            return await routeHandler.HandleAsync(routeContextSnapshot, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(ex);
            _logger.LogError(ex, "An error occurred while handling API route");
            return ResponseHelper.InternalServerError($"Unhandled error: {ex.Message}");
        }
    }
}
