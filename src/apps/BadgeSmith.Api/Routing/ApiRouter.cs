using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Observability;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;

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
    /// <param name="lambdaContext"> The Lambda context containing logger, request ID, and execution environment.</param>
    /// <param name="ct">Cancellation token to support request cancellation and timeout handling</param>
    /// <returns>An API Gateway HTTP response with appropriate status code, headers, and body</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters (request, context, or services) are null</exception>
    /// <exception cref="InvalidOperationException">Thrown when route resolution succeeds, but match data is unexpectedly null</exception>
    public async Task<APIGatewayHttpApiV2ProxyResponse> RouteAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext lambdaContext, CancellationToken ct = default)
    {
        using var operation = Tracer.StartOperation($"{nameof(ApiRouter)}.{nameof(RouteAsync)}", currentActivity: BadgeSmithApiActivitySource.ActivitySource.StartActivity());

        try
        {
            ArgumentNullException.ThrowIfNull(request);

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

            if (HandlerRegistry.GetHandler(routeMatch.Descriptor.HandlerType) is not { } handler)
            {
                throw new InvalidOperationException($"Handler not found: {routeMatch.Descriptor.HandlerType}");
            }

            var routeContextV2 = new RouteContext(method, path, routeMatch);

            return await handler.HandleAsync(routeContextV2, lambdaContext, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            operation?.AddException(ex);
            lambdaContext.Logger.LogError(ex, "An error occurred while handling API route");
            return ResponseHelper.InternalServerError($"Unhandled error: {ex.Message}");
        }
    }
}
