using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Infrastructure.Routing.Contracts;
using BadgeSmith.Api.Infrastructure.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Infrastructure.Routing.Cors;

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

        var origin = GetHeaderValue(headers, "Origin");
        var requestMethod = GetHeaderValue(headers, "Access-Control-Request-Method");
        var requestHeaders = GetHeaderValue(headers, "Access-Control-Request-Headers");

        _logger.LogDebug("CORS preflight request: Origin={Origin}, Method={Method}, Headers={Headers}", origin, requestMethod, requestHeaders);

        var corsHeaders = BuildPreflightHeaders(path, requestMethod, requestHeaders, origin);

        return ResponseHelper.OptionsResponse(() => corsHeaders);
    }

    public void ApplyResponseHeaders(IDictionary<string, string> responseHeaders, string? origin = null)
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
        headers["Content-Type"] = "text/plain";

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
