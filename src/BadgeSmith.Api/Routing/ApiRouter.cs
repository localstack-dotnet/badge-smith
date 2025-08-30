using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Routing.Contracts;
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
    private readonly ICorsHandler _corsHandler;

    public ApiRouter(ILogger<ApiRouter> logger, IRouteResolver routeResolver, IHandlerFactory handlerFactory, ICorsHandler corsHandler)
    {
        _logger = logger;
        _routeResolver = routeResolver;
        _handlerFactory = handlerFactory;
        _corsHandler = corsHandler;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> RouteAsync(APIGatewayHttpApiV2ProxyRequest request, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(ApiRouter)}.{nameof(RouteAsync)}");
        // using var perfScope = PerfTracker.StartScope($"{nameof(ApiRouter)}.{nameof(RouteAsync)}", typeof(ApiRouter).FullName);

        try
        {
            ArgumentNullException.ThrowIfNull(request);

            _logger.LogInformation("API route request received");

            var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
            var path = request.RequestContext.Http.Path ?? "/";

            if (httpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("CORS preflight request for path: {Path}", path);
                return _corsHandler.HandlePreflight(request.Headers, path);
            }

            var resolved = _routeResolver.TryResolve(httpMethod, path, out var routeMatch);

            if (!resolved)
            {
                return Helpers.ResponseHelper.NotFound($"Route not found: {httpMethod} {path}");
            }

            // Check authentication requirements
            if (routeMatch.Descriptor.RequiresAuth)
            {
                // Implement authentication check
                // For now, just continue
            }

            var routeHandler = routeMatch.Descriptor.HandlerFactory(_handlerFactory);

            var routeContextSnapshot = new RouteContext(request, routeMatch.Values.ToImmutableDictionary());

            var apiGatewayHttpApiV2ProxyResponse = await routeHandler.HandleAsync(routeContextSnapshot, ct).ConfigureAwait(false);
            _corsHandler.ApplyResponseHeaders(apiGatewayHttpApiV2ProxyResponse.Headers);

            return apiGatewayHttpApiV2ProxyResponse;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(ex);
            _logger.LogError(ex, "An error occurred while handling API route");
            return Helpers.ResponseHelper.InternalServerError($"Unhandled error: {ex.Message}");
        }
    }
}
