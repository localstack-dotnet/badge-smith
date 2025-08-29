using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Routing.Contracts;

/// <summary>
/// Base interface that all route handlers must implement to process HTTP requests.
/// Provides a standardized contract for handling requests and returning API Gateway responses.
/// </summary>
internal interface IRouteHandler
{
    /// <summary>
    /// Handles an HTTP request asynchronously and returns an API Gateway response.
    /// </summary>
    /// <param name="routeContext">Route context snapshot containing request data and route parameters</param>
    /// <param name="ct">Cancellation token to support request cancellation and timeout handling</param>
    /// <returns>A task that resolves to an API Gateway HTTP response with status code, headers, and body</returns>
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default);
}
