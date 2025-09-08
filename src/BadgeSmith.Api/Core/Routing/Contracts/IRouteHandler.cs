using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Core.Routing.Contracts;

/// <summary>
/// Base interface that all route handlers must implement to process HTTP requests.
/// Provides a standardized contract for handling requests and returning API Gateway responses.
/// </summary>
internal interface IRouteHandler
{
    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default);
}
