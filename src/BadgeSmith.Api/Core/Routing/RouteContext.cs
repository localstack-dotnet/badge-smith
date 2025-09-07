using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Core.Routing;

internal sealed class RouteContext
{
    public APIGatewayHttpApiV2ProxyRequest Request { get; }

    public IReadOnlyDictionary<string, string> RouteValues { get; }

    public RouteContext(APIGatewayHttpApiV2ProxyRequest request, IReadOnlyDictionary<string, string> routeValues)
    {
        Request = request;
        RouteValues = routeValues;
    }

    public bool TryGetRouteValue(string name, out string? value) => RouteValues.TryGetValue(name, out value);
}
