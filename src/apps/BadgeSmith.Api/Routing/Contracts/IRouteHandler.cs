using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

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
    /// <param name="routeContext">Route context containing request data, Lambda context, route parameters, and services</param>
    /// <param name="lambdaContext"> The Lambda context containing logger, request ID, and execution environment.</param>
    /// <param name="ct">Cancellation token to support request cancellation and timeout handling</param>
    /// <returns>A task that resolves to an API Gateway HTTP response with status code, headers, and body</returns>
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContextV2 routeContext, ILambdaContext lambdaContext, CancellationToken ct = default);
}
