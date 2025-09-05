namespace BadgeSmith.Api.Infrastructure.Routing.Cors;

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
        "x-signature",
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
