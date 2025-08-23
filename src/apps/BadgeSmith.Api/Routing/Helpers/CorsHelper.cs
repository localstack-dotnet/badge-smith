using System.Collections.Frozen;
using System.Globalization;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing.Contracts;
using ZLinq;

namespace BadgeSmith.Api.Routing.Helpers;

/// <summary>
/// Comprehensive CORS (Cross-Origin Resource Sharing) helper for handling preflight requests and response headers.
/// Provides secure, configurable CORS support with route-aware method advertisement and proper header validation.
/// Implements CORS specification (RFC 6454) with security best practices for production APIs.
/// </summary>
internal static class CorsHelper
{
    /// <summary>
    /// Configuration options for CORS behavior, allowing fine-grained control over cross-origin access policies.
    /// Supports both public APIs and credential-based authentication scenarios with appropriate security measures.
    /// </summary>
    internal sealed class Options
    {
        /// <summary>
        /// Indicates whether the API allows credentials (cookies, Authorization headers, TLS client certificates).
        /// When true, Access-Control-Allow-Origin cannot use wildcards and must specify exact origins.
        /// This is a security requirement per CORS specification to prevent credential leakage.
        /// </summary>
        public bool AllowCredentials { get; init; }

        /// <summary>
        /// Maximum time in seconds that browsers can cache preflight responses.
        /// Reduces preflight requests for repeated cross-origin calls from the same origin.
        /// Default is 600 seconds (10 minutes). The maximum recommended value is 86,400 seconds (24 hours).
        /// </summary>
        public int MaxAgeSeconds { get; init; } = 600;

        /// <summary>
        /// Controls whether to use wildcard "*" for Access-Control-Allow-Origin when credentials are not allowed.
        /// When false, the specific requesting origin is echoed back instead of using "*".
        /// Useful for APIs that need to support Origin-based logging or analytics while remaining public.
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
        /// </summary>
        public FrozenSet<string>? AllowedOrigins { get; init; }

        /// <summary>
        /// Whitelist of headers that clients are allowed to include in preflight Access-Control-Request-Headers.
        /// Only headers present in this set will be echoed back in Access-Control-Allow-Headers.
        /// Includes common headers needed for API authentication and content handling.
        /// Uses case-insensitive comparison per HTTP header specification.
        /// </summary>
        public FrozenSet<string> AllowedRequestHeaders { get; init; } =
            FrozenSet.ToFrozenSet([
                "content-type",
                "x-signature",
                "x-repo-secret",
                "x-timestamp",
                "x-nonce",
            ], StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Additional response headers to expose to client-side JavaScript via Access-Control-Expose-Headers.
        /// Only applies to actual responses, not preflight OPTIONS responses.
        /// Allows web applications to access custom headers that would otherwise be hidden by browser security.
        /// </summary>
        public FrozenSet<string>? ExposeHeaders { get; init; }
    }

    /// <summary>
    /// Default CORS configuration optimized for public APIs without credential requirements.
    /// Uses permissive settings appropriate for open badge/status APIs and public endpoints.
    /// </summary>
    public static readonly Options Default = new();

    /// <summary>
    /// Builds CORS preflight response headers for OPTIONS requests using route-aware method advertisement.
    /// Validates requested methods against actual available routes to provide accurate CORS responses.
    /// Implements proper security measures including origin validation and header filtering.
    /// </summary>
    /// <param name="resolver">Route resolver to determine which HTTP methods are actually supported for the given path.</param>
    /// <param name="path">The request path to check for supported methods (e.g., "/badges/packages/nuget/MyPackage").</param>
    /// <param name="requestedMethod">The HTTP method from the Access-Control-Request-Method header, if present.</param>
    /// <param name="requestedHeaders">Comma-separated header names from Access-Control-Request-Headers, if present.</param>
    /// <param name="origin">The requesting origin from the Origin header, if present.</param>
    /// <param name="options">CORS configuration options. Uses Default if not specified.</param>
    /// <returns>Dictionary of CORS headers to include in the preflight response.</returns>
    public static IDictionary<string, string> BuildPreflightHeaders(
        IRouteResolverV2 resolver,
        string path,
        string? requestedMethod,
        string? requestedHeaders,
        string? origin,
        Options? options = null)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(CorsHelper)}.{nameof(BuildPreflightHeaders)}");

        options ??= Default;

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(origin))
        {
            if (options.AllowCredentials)
            {
                if (IsOriginAllowed(origin, options))
                {
                    headers["Access-Control-Allow-Origin"] = origin;
                    headers["Access-Control-Allow-Credentials"] = "true";
                    AppendVary(headers, "Origin");
                }
                // else: omit ACAO => browser will block
            }
            else
            {
                headers["Access-Control-Allow-Origin"] = options.UseWildcardWhenNoCredentials ? "*" : origin;
                if (!options.UseWildcardWhenNoCredentials)
                {
                    AppendVary(headers, "Origin");
                }
            }
        }
        else
        {
            // no Origin header; public wildcard if no credentials
            if (options is { AllowCredentials: false, UseWildcardWhenNoCredentials: true })
            {
                headers["Access-Control-Allow-Origin"] = "*";
            }
        }

        var allowed = resolver.GetAllowedMethods(path); // e.g. ["GET","HEAD","OPTIONS"] or ["POST","OPTIONS"]

        if (allowed.Count > 0)
        {
            string? methodsHeader;
            if (!string.IsNullOrWhiteSpace(requestedMethod))
            {
                var req = NormalizeMethod(requestedMethod);
                // If the requested method is allowed, advertise ONLY that method (minimal, precise).
                // If not allowed, advertise the full list so the browser can reject.
                methodsHeader = allowed.Contains(req, StringComparer.OrdinalIgnoreCase)
                    ? req
                    : string.Join(", ", allowed);
            }
            else
            {
                // No requested method provided; advertise the full set
                methodsHeader = string.Join(", ", allowed);
            }

            if (!string.IsNullOrEmpty(methodsHeader))
            {
                headers["Access-Control-Allow-Methods"] = methodsHeader;
            }

            // Since response depends on the requested method, add Vary
            AppendVary(headers, "Access-Control-Request-Method");
        }

        // Vary on method because Allow-Methods differs per intended method/path
        AppendVary(headers, "Access-Control-Request-Method");

        // ----- Request headers (filter the requested list)
        var allowHeaders = BuildAllowHeaders(requestedHeaders, options.AllowedRequestHeaders);
        if (allowHeaders is not null)
        {
            headers["Access-Control-Allow-Headers"] = allowHeaders;
            AppendVary(headers, "Access-Control-Request-Headers");
        }

        headers["Access-Control-Max-Age"] = options.MaxAgeSeconds.ToString(CultureInfo.InvariantCulture);

        return headers;
    }

    /// <summary>
    /// Builds a complete API Gateway preflight response for CORS OPTIONS requests.
    /// Combines CORS header generation with proper HTTP response formatting using 204 No Content status.
    /// This is the primary method for handling CORS preflight requests in the API router.
    /// </summary>
    /// <param name="resolver">Route resolver to determine supported HTTP methods for the path.</param>
    /// <param name="path">The request path being checked for CORS preflight (e.g., "/badges/packages/nuget/MyPackage").</param>
    /// <param name="requestedMethod">The HTTP method from Access-Control-Request-Method header, if present.</param>
    /// <param name="requestedHeaders">Comma-separated header names from Access-Control-Request-Headers, if present.</param>
    /// <param name="origin">The requesting origin from the Origin header, if present.</param>
    /// <param name="options">CORS configuration options. Uses Default if not specified.</param>
    /// <returns>Complete API Gateway response with 204 No Content status and appropriate CORS headers.</returns>
    public static APIGatewayHttpApiV2ProxyResponse BuildPreflightResponse(
        IRouteResolverV2 resolver,
        string path,
        string? requestedMethod,
        string? requestedHeaders,
        string? origin,
        Options? options = null)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(CorsHelper)}.{nameof(BuildPreflightResponse)}");

        var hdrs = BuildPreflightHeaders(resolver, path, requestedMethod, requestedHeaders, origin, options);

        return ResponseHelper.OptionsResponse(hdrs);
    }

    /// <summary>
    /// Applies CORS headers to actual API responses (non-preflight requests).
    /// Handles Access-Control-Allow-Origin and Access-Control-Expose-Headers for simple and complex CORS requests.
    /// Should be called on all API responses that may be accessed from web browsers.
    /// </summary>
    /// <param name="responseHeaders">The response headers dictionary to modify with CORS headers.</param>
    /// <param name="origin">The requesting origin from the Origin header, if present in the original request.</param>
    /// <param name="options">CORS configuration options. Uses Default if not specified.</param>
    /// <example>
    /// <code>
    /// var response = ResponseHelper.Ok(jsonData);
    /// Cors.ApplySimpleResponseHeaders(response.Headers, request.Headers?["Origin"]);
    /// return response;
    /// </code>
    /// </example>
    public static void ApplySimpleResponseHeaders(IDictionary<string, string> responseHeaders, string? origin, Options? options = null)
    {
        options ??= Default;

        if (!string.IsNullOrEmpty(origin))
        {
            if (options.AllowCredentials)
            {
                if (IsOriginAllowed(origin, options))
                {
                    responseHeaders["Access-Control-Allow-Origin"] = origin;
                    responseHeaders["Access-Control-Allow-Credentials"] = "true";
                    AppendVary(responseHeaders, "Origin");
                }
            }
            else
            {
                responseHeaders["Access-Control-Allow-Origin"] = options.UseWildcardWhenNoCredentials ? "*" : origin;
                if (!options.UseWildcardWhenNoCredentials)
                {
                    AppendVary(responseHeaders, "Origin");
                }
            }
        }
        else if (!options.AllowCredentials && options.UseWildcardWhenNoCredentials)
        {
            responseHeaders["Access-Control-Allow-Origin"] = "*";
        }

        if (options.ExposeHeaders is { Count: > 0 })
        {
            responseHeaders["Access-Control-Expose-Headers"] = string.Join(", ", options.ExposeHeaders);
        }
    }

    /// <summary>
    /// Filters requested headers against the allowed headers whitelist and builds Access-Control-Allow-Headers value.
    /// Only headers present in the allowList will be included in the response.
    /// Handles proper parsing of comma-separated header names with trimming.
    /// </summary>
    /// <param name="requestedHeaders">Comma-separated list of headers from Access-Control-Request-Headers.</param>
    /// <param name="allowList">Set of headers that are permitted for cross-origin requests.</param>
    /// <returns>Comma-separated string of allowed headers, or null if none are allowed or requested.</returns>
    private static string? BuildAllowHeaders(string? requestedHeaders, FrozenSet<string> allowList)
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

        var accepted = parts.AsValueEnumerable().Where(allowList.Contains).ToArray();

        return accepted.Length > 0 ? string.Join(", ", accepted) : null;
    }

    /// <summary>
    /// Determines whether a specific origin is allowed based on the configured origin validation rules.
    /// Checks both explicit origin sets and custom validation predicates.
    /// Defaults to allowing all origins for public APIs when no restrictions are configured.
    /// </summary>
    /// <param name="origin">The origin to validate (e.g., "https://example.com").</param>
    /// <param name="options">CORS options containing origin validation configuration.</param>
    /// <returns>True if the origin is allowed; otherwise, false.</returns>
    private static bool IsOriginAllowed(string origin, Options options)
    {
        if (options.AllowedOrigins is { Count: > 0 } set && set.Contains(origin))
        {
            return true;
        }

        if (options.OriginAllowed is not null)
        {
            return options.OriginAllowed(origin);
        }

        return true; // default public API
    }

    /// <summary>
    /// Appends a token to the Vary header, handling existing values and preventing duplicates.
    /// The Vary header is crucial for proper caching behavior with CORS responses.
    /// Ensures cache keys include the specified request headers that affect the response.
    /// </summary>
    /// <param name="headers">Headers dictionary to modify.</param>
    /// <param name="token">The header name to add to the Vary header (e.g., "Origin", "Access-Control-Request-Method").</param>
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

    /// <summary>
    /// Normalizes HTTP method names to uppercase for consistent comparison.
    /// Handles null, empty, and whitespace-only method strings safely.
    /// </summary>
    /// <param name="method">The HTTP method to normalize.</param>
    /// <returns>Uppercase trimmed method name, or the original value if null/empty.</returns>
    private static string NormalizeMethod(string method) => string.IsNullOrWhiteSpace(method) ? method : method.Trim().ToUpperInvariant();
}
