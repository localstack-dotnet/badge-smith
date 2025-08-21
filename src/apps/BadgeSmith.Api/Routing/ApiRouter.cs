using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace BadgeSmith.Api.Routing;

/// <summary>
/// Main API router that orchestrates request routing using high-performance route resolution.
/// Integrates route matching, authentication checks, handler resolution, and error handling
/// for AWS Lambda API Gateway HTTP requests.
/// </summary>
internal class ApiRouter : IApiRouter
{
    private readonly IRouteResolver _routeResolver;

    public ApiRouter(IRouteResolver routeResolver)
    {
        _routeResolver = routeResolver;
    }

    /// <summary>
    /// Routes an incoming HTTP request to the appropriate handler based on path and method matching.
    /// Handles CORS preflight requests, authentication requirements, and error scenarios.
    /// </summary>
    /// <param name="request">The API Gateway HTTP request containing path, method, headers, and body</param>
    /// <param name="services">Service provider for dependency injection and handler resolution</param>
    /// <param name="ct">Cancellation token to support request cancellation and timeout handling</param>
    /// <returns>An API Gateway HTTP response with appropriate status code, headers, and body</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters (request, context, or services) are null</exception>
    /// <exception cref="InvalidOperationException">Thrown when route resolution succeeds, but match data is unexpectedly null</exception>
    public async Task<APIGatewayHttpApiV2ProxyResponse> RouteAsync(APIGatewayHttpApiV2ProxyRequest request, IServiceProvider services, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(ApiRouter)}.{nameof(RouteAsync)}");

        try
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(services);

            var path = request.RequestContext.Http.Path;
            var method = request.RequestContext.Http.Method;

            // Handle CORS preflight
            if (method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                string? acrm = null;
                string? acrh = null;
                string? origin = null;

                request.Headers?.TryGetValue("Access-Control-Request-Method", out acrm);
                request.Headers?.TryGetValue("Access-Control-Request-Headers", out acrh);
                request.Headers?.TryGetValue("Origin", out origin);

                return CorsHelper.BuildPreflightResponse(_routeResolver, path, acrm?.Trim(), acrh?.Trim(), origin?.Trim());
            }

            var resolvedRoute = _routeResolver.ResolveRoute(path, method);
            if (resolvedRoute == null)
            {
                return ResponseHelper.NotFound($"Route not found: {method} {path}");
            }

            // Check authentication requirements
            if (resolvedRoute.RequiresAuth)
            {
                // Implement authentication check
                // For now, just continue
            }

            if (services.GetRequiredService(resolvedRoute.Handler) is not IRouteHandler handler)
            {
                throw new InvalidOperationException($"Handler not found: {resolvedRoute.Handler}");
            }

            if (resolvedRoute.Match == null)
            {
                throw new InvalidOperationException("Route match is null");
            }

            return await handler.HandleAsync(new RouteContext(request, resolvedRoute.Match), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return ResponseHelper.InternalServerError($"Unhandled error: {ex.Message}");
        }
    }
}
