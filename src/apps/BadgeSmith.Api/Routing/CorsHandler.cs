using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Routing;

/// <summary>
/// Comprehensive CORS configuration options for AWS Lambda API Gateway routing.
/// Supports both public APIs and credential-based authentication scenarios with appropriate security measures.
/// Implements CORS specification (RFC 6454) with security best practices for production APIs.
/// </summary>
internal sealed class CorsOptions
{
    /// <summary>
    /// Indicates whether the API allows credentials (cookies, Authorization headers, TLS client certificates).
    /// When true, Access-Control-Allow-Origin cannot use wildcards and must specify exact origins.
    /// This is a security requirement per CORS specification to prevent credential leakage.
    /// Default: false (appropriate for public APIs).
    /// </summary>
    public bool AllowCredentials { get; init; }

    /// <summary>
    /// Maximum time in seconds that browsers can cache preflight responses.
    /// Reduces preflight requests for repeated cross-origin calls from the same origin.
    /// Default is 3600 seconds (1 hour). The maximum recommended value is 86,400 seconds (24 hours).
    /// </summary>
    public int MaxAgeSeconds { get; init; } = 3600;

    /// <summary>
    /// Controls whether to use wildcard "*" for Access-Control-Allow-Origin when credentials are not allowed.
    /// When false, the specific requesting origin is echoed back instead of using "*".
    /// Useful for APIs that need to support Origin-based logging or analytics while remaining public.
    /// Default: true (appropriate for public APIs).
    /// </summary>
    public bool UseWildcardWhenNoCredentials { get; init; } = true;

    /// <summary>
    /// Custom predicate function to determine if a specific origin is allowed access.
    /// Takes precedence over the AllowedOrigins set when both are provided.
    /// Useful for dynamic origin validation based on runtime conditions or external configuration.
    /// If null, all origins are allowed (appropriate for public APIs).
    /// </summary>
    public Func<string, bool>? OriginAllowed { get; init; }

    /// <summary>
    /// Explicit set of allowed origins for cross-origin requests.
    /// Only used when OriginAllowed predicate is null.
    /// Should contain full origin URLs including protocol (e.g., "https://example.com").
    /// If null, all origins are allowed (appropriate for public APIs).
    /// Uses case-sensitive comparison per RFC specification.
    /// </summary>
    public ISet<string>? AllowedOrigins { get; init; }

    /// <summary>
    /// Whitelist of headers that clients are allowed to include in preflight Access-Control-Request-Headers.
    /// Only headers present in this set will be echoed back in Access-Control-Allow-Headers.
    /// Includes common headers needed for API authentication and content handling.
    /// Uses case-insensitive comparison per HTTP header specification.
    /// </summary>
    public ISet<string> AllowedRequestHeaders { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "content-type",
        "authorization",
        "x-signature",
        "x-repo-secret",
        "x-timestamp",
        "x-nonce",
    };

    /// <summary>
    /// Additional response headers to expose to client-side JavaScript via Access-Control-Expose-Headers.
    /// Only applies to actual responses, not preflight OPTIONS responses.
    /// Allows web applications to access custom headers that would otherwise be hidden by browser security.
    /// </summary>
    public ISet<string>? ExposeHeaders { get; init; }

    /// <summary>
    /// Default CORS configuration optimized for public APIs without credential requirements.
    /// Uses permissive settings appropriate for open badge/status APIs and public endpoints.
    /// </summary>
    public static readonly CorsOptions Default = new();
}

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
    public void ApplyResponseHeaders(IDictionary<string, string> responseHeaders, string? origin);
}

/// <summary>
/// CORS handler that manages Cross-Origin Resource Sharing for the BadgeSmith API.
/// Handles preflight requests by extracting request headers and consulting the route resolver
/// to determine appropriate CORS responses.
/// </summary>
internal sealed class CorsHandler : ICorsHandler
{
    private readonly IRouteResolver _routeResolver;
    private readonly ILogger<CorsHandler> _logger;
    private readonly CorsOptions _options;

    public CorsHandler(IRouteResolver routeResolver, ILogger<CorsHandler> logger, CorsOptions options)
    {
        _routeResolver = routeResolver;
        _logger = logger;
        _options = options;
    }

    public APIGatewayHttpApiV2ProxyResponse HandlePreflight(IDictionary<string, string>? headers, string path)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(CorsHandler)}.{nameof(HandlePreflight)}");

        try
        {
            // Extract CORS headers from request
            var origin = GetHeaderValue(headers, "Origin");
            var requestMethod = GetHeaderValue(headers, "Access-Control-Request-Method");
            var requestHeaders = GetHeaderValue(headers, "Access-Control-Request-Headers");

            _logger.LogDebug("CORS preflight request: Origin={Origin}, Method={Method}, Headers={Headers}", origin, requestMethod, requestHeaders);

            // Build preflight headers using our comprehensive logic
            var corsHeaders = BuildPreflightHeaders(path, requestMethod, requestHeaders, origin);

            // For credentials-enabled APIs with rejected origins, return a response without default CORS headers
            if (_options.AllowCredentials && !string.IsNullOrEmpty(origin) && !corsHeaders.ContainsKey("Access-Control-Allow-Origin"))
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 204,
                    Body = string.Empty,
                    Headers = corsHeaders, // Only our explicitly set headers, no defaults
                    IsBase64Encoded = false,
                };
            }

            return ResponseHelper.OptionsResponse(corsHeaders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling CORS preflight for path: {Path}", path);

            // Return a fallback CORS response to avoid breaking browsers
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Body = string.Empty,
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Access-Control-Allow-Origin"] = "*",
                    ["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS",
                    ["Access-Control-Max-Age"] = _options.MaxAgeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                IsBase64Encoded = false,
            };
        }
    }

    public void ApplyResponseHeaders(IDictionary<string, string> responseHeaders, string? origin)
    {
        if (!string.IsNullOrEmpty(origin))
        {
            if (_options.AllowCredentials)
            {
                if (IsOriginAllowed(origin))
                {
                    responseHeaders["Access-Control-Allow-Origin"] = origin;
                    responseHeaders["Access-Control-Allow-Credentials"] = "true";
                    AppendVary(responseHeaders, "Origin");
                }
            }
            else
            {
                responseHeaders["Access-Control-Allow-Origin"] = _options.UseWildcardWhenNoCredentials ? "*" : origin;
                if (!_options.UseWildcardWhenNoCredentials)
                {
                    AppendVary(responseHeaders, "Origin");
                }
            }
        }
        else if (_options is { AllowCredentials: false, UseWildcardWhenNoCredentials: true })
        {
            responseHeaders["Access-Control-Allow-Origin"] = "*";
        }

        if (_options.ExposeHeaders is { Count: > 0 })
        {
            responseHeaders["Access-Control-Expose-Headers"] = string.Join(", ", _options.ExposeHeaders);
        }
    }

    private Dictionary<string, string> BuildPreflightHeaders(string path, string? requestedMethod, string? requestedHeaders, string? origin)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Handle origin validation and Access-Control-Allow-Origin
        if (!string.IsNullOrEmpty(origin))
        {
            if (_options.AllowCredentials)
            {
                if (IsOriginAllowed(origin))
                {
                    headers["Access-Control-Allow-Origin"] = origin;
                    headers["Access-Control-Allow-Credentials"] = "true";
                    AppendVary(headers, "Origin");
                }
                // else: omit ACAO => browser will block
            }
            else
            {
                headers["Access-Control-Allow-Origin"] = _options.UseWildcardWhenNoCredentials ? "*" : origin;
                if (!_options.UseWildcardWhenNoCredentials)
                {
                    AppendVary(headers, "Origin");
                }
            }
        }
        else
        {
            // no Origin header; public wildcard if no credentials
            if (_options is { AllowCredentials: false, UseWildcardWhenNoCredentials: true })
            {
                headers["Access-Control-Allow-Origin"] = "*";
            }
        }

        // Get allowed methods from route resolver
        var allowedMethods = _routeResolver.GetAllowedMethods(path);

        if (allowedMethods.Count > 0)
        {
            string? methodsHeader;
            if (!string.IsNullOrWhiteSpace(requestedMethod))
            {
                var req = NormalizeMethod(requestedMethod);
                // If the requested method is allowed, advertise ONLY that method (minimal, precise).
                // If not allowed, advertise the full list so the browser can reject.
                methodsHeader = allowedMethods.Contains(req, StringComparer.OrdinalIgnoreCase)
                    ? req
                    : string.Join(", ", allowedMethods);
            }
            else
            {
                // No requested method provided; advertise the full set
                methodsHeader = string.Join(", ", allowedMethods);
            }

            if (!string.IsNullOrEmpty(methodsHeader))
            {
                headers["Access-Control-Allow-Methods"] = methodsHeader;
            }

            // Since the response depends on the requested method, add Vary
            AppendVary(headers, "Access-Control-Request-Method");
        }

        // Handle request headers (filter the requested list)
        var allowHeaders = BuildAllowHeaders(requestedHeaders);
        if (allowHeaders is not null)
        {
            headers["Access-Control-Allow-Headers"] = allowHeaders;
            AppendVary(headers, "Access-Control-Request-Headers");
        }

        headers["Access-Control-Max-Age"] = _options.MaxAgeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return headers;
    }

    private string? BuildAllowHeaders(string? requestedHeaders)
    {
        if (string.IsNullOrWhiteSpace(requestedHeaders))
        {
            return null;
        }

        var parts = requestedHeaders.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var accepted = parts.Where(_options.AllowedRequestHeaders.Contains).ToArray();

        return accepted.Length > 0 ? string.Join(", ", accepted) : null;
    }

    private bool IsOriginAllowed(string origin)
    {
        if (_options.AllowedOrigins is { Count: > 0 } set)
        {
            return set.Contains(origin);
        }

        if (_options.OriginAllowed is not null)
        {
            return _options.OriginAllowed(origin);
        }

        return true; // default public API (no restrictions configured)
    }

    private static void AppendVary(IDictionary<string, string> headers, string token)
    {
        if (headers.TryGetValue("Vary", out var current) && !string.IsNullOrWhiteSpace(current))
        {
            var parts = current.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!parts.Contains(token, StringComparer.OrdinalIgnoreCase))
            {
                headers["Vary"] = current + ", " + token;
            }
        }
        else
        {
            headers["Vary"] = token;
        }
    }

    private static string NormalizeMethod(string method) => string.IsNullOrWhiteSpace(method) ? method : method.Trim().ToUpperInvariant();

    private static string? GetHeaderValue(IDictionary<string, string>? headers, string headerName)
    {
        if (headers == null)
        {
            return null;
        }

        return headers.TryGetValue(headerName, out var value) ? value.Trim() : null;
    }
}
