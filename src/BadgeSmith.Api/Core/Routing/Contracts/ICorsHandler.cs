using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Core.Routing.Contracts;

/// <summary>
/// Handles CORS (Cross-Origin Resource Sharing) for API requests.
/// Provides autonomous preflight handling with full header extraction from requests.
/// </summary>
internal interface ICorsHandler
{
    public APIGatewayHttpApiV2ProxyResponse HandlePreflight(IDictionary<string, string>? headers, string path);

    public void ApplyResponseHeaders(IDictionary<string, string> responseHeaders, string? origin = null);
}
