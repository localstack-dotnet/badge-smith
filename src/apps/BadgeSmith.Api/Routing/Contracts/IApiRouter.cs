using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Routing.Contracts;

internal interface IApiRouter
{
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
    public Task<APIGatewayHttpApiV2ProxyResponse> RouteAsync(APIGatewayHttpApiV2ProxyRequest request, IServiceProvider services, CancellationToken ct = default);
}
