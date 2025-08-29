using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Routing.Contracts;

internal interface IApiRouter
{
    public Task<APIGatewayHttpApiV2ProxyResponse> RouteAsync(APIGatewayHttpApiV2ProxyRequest request, CancellationToken ct = default);
}
