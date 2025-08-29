using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Routing.Contracts;

/// <summary>
/// Handles CORS (Cross-Origin Resource Sharing) for API requests.
/// Provides autonomous preflight handling with full header extraction from requests.
/// </summary>
internal interface ICorsHandler
{
    /// <summary>
    /// Handles CORS preflight (OPTIONS) requests autonomously.
    /// Extracts all necessary headers from the request and determines allowed methods
    /// by consulting the route resolver.
    /// </summary>
    /// <param name="headers">Request headers dictionary</param>
    /// <param name="path">The request path to check for allowed methods</param>
    /// <returns>Complete CORS preflight response</returns>
    public APIGatewayHttpApiV2ProxyResponse HandlePreflight(IDictionary<string, string>? headers, string path);

    /// <summary>
    /// Applies CORS headers to actual API responses (non-preflight requests).
    /// Handles Access-Control-Allow-Origin and Access-Control-Expose-Headers for simple and complex CORS requests.
    /// Should be called on all API responses that may be accessed from web browsers.
    /// </summary>
    /// <param name="responseHeaders">The response headers dictionary to modify with CORS headers</param>
    /// <param name="origin">The requesting origin from the Origin header, if present in the original request</param>
    public void ApplyResponseHeaders(IDictionary<string, string> responseHeaders, string? origin = null);
}
