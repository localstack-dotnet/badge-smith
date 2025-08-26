using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Routing.Contracts;

internal interface IApiRouter
{
    public Task<APIGatewayHttpApiV2ProxyResponse> RouteAsync(string path, string method, IDictionary<string, string>? headers, CancellationToken ct = default);
}
