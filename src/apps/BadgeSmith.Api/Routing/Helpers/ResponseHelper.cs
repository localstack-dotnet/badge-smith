using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Json;
using BadgeSmith.Domain.Models;

namespace BadgeSmith.Api.Routing.Helpers;

/// <summary>
/// Helper methods for creating standardized API Gateway HTTP responses with Native AOT support
/// </summary>
internal static class ResponseHelper
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct CacheSettings(int SMaxAgeSeconds = 60, int MaxAgeSeconds = 10, int StaleWhileRevalidateSeconds = 30, int StaleIfErrorSeconds = 24 * 60 * 60);

    /// <summary>
    /// Creates a custom HTTP response with the specified status code and optional body/headers.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with the specified parameters.</returns>
    public static APIGatewayHttpApiV2ProxyResponse CreateResponse(HttpStatusCode statusCode, string? responseBody = null, Func<Dictionary<string, string>>? customHeaders = null)
    {
        var headers = customHeaders?.Invoke();

        var apiGatewayHttpApiV2ProxyResponse = new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
        };

        if (headers != null && headers.Count != 0)
        {
            apiGatewayHttpApiV2ProxyResponse.Headers = headers;
        }

        if (responseBody != null)
        {
            apiGatewayHttpApiV2ProxyResponse.Body = responseBody;
        }

        return apiGatewayHttpApiV2ProxyResponse;
    }

    /// <summary>
    /// Creates a custom HTTP response with a serialized object body.
    /// </summary>
    /// <typeparam name="T">The type of the response object.</typeparam>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseObject">The object to serialize as the response body.</param>
    /// <param name="jsonTypeInfo">The JSON type info for AOT serialization.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with the serialized object body.</returns>
    public static APIGatewayHttpApiV2ProxyResponse CreateResponse<T>(
        HttpStatusCode statusCode,
        T responseObject,
        JsonTypeInfo<T> jsonTypeInfo,
        Func<Dictionary<string, string>>? customHeaders = null)
    {
        return CreateResponse(statusCode, JsonSerializer.Serialize(responseObject, jsonTypeInfo), customHeaders);
    }

    public static APIGatewayHttpApiV2ProxyResponse OkCached<T>(
        T responseObject,
        JsonTypeInfo<T> jsonTypeInfo,
        string? ifNoneMatchHeader = null,
        CacheSettings? cache = null,
        DateTimeOffset? lastModifiedUtc = null)
    {
        var settings = cache ?? new CacheSettings();
        var body = JsonSerializer.Serialize(responseObject, jsonTypeInfo);
        var etag = ComputeStrongEtag(body);

        if (IfNoneMatchMatches(ifNoneMatchHeader, etag))
        {
            return CreateResponse(HttpStatusCode.NotModified, responseBody: null,
                customHeaders: () => BuildCacheHeaders(etag, settings, lastModifiedUtc));
        }

        return CreateResponse(HttpStatusCode.OK, body,
            customHeaders: () => BuildCacheHeaders(etag, settings, lastModifiedUtc));
    }

    /// <summary>
    /// Creates a successful 200 OK response with additional headers.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Ok(string? responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.OK, responseBody, customHeaders);

    /// <summary>
    /// Creates a successful 200 OK response with a serialized object.
    /// </summary>
    /// <typeparam name="T">The type of the response object.</typeparam>
    /// <param name="responseObject">The object to serialize as the response body.</param>
    /// <param name="jsonTypeInfo">The JSON type info for AOT serialization.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK containing the serialized object.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Ok<T>(T responseObject, JsonTypeInfo<T> jsonTypeInfo, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.OK, responseObject, jsonTypeInfo, customHeaders);

    /// <summary>
    /// Creates a successful 200 OK response with health check information.
    /// </summary>
    /// <param name="status">The health status message.</param>
    /// <param name="timestamp">The timestamp when the health check was performed.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK containing health information.</returns>
    public static APIGatewayHttpApiV2ProxyResponse OkHealthWithNoCache(string status, DateTimeOffset timestamp)
    {
        var response = new HealthCheckResponse(status, timestamp);
        return CreateResponse(HttpStatusCode.OK, response, LambdaFunctionJsonSerializerContext.Default.HealthCheckResponse, () => NoCacheHeaders());
    }

    /// <summary>
    /// Creates a 201 Created response with additional headers.
    /// </summary>
    /// <param name="responseBody">The response body content.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 201 Created.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Created(string responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.Created, responseBody, customHeaders);

    /// <summary>
    /// Creates a 201 Created response with a serialized object.
    /// </summary>
    /// <typeparam name="T">The type of the response object.</typeparam>
    /// <param name="responseObject">The object to serialize as the response body.</param>
    /// <param name="jsonTypeInfo">The JSON type info for AOT serialization.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 201 Created containing the serialized object.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Created<T>(T responseObject, JsonTypeInfo<T> jsonTypeInfo,
        Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.Created, responseObject, jsonTypeInfo, customHeaders);

    /// <summary>
    /// Creates a 400 Bad Request response with additional headers.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 400 Bad Request.</returns>
    public static APIGatewayHttpApiV2ProxyResponse BadRequest(string? responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.BadRequest, responseBody, customHeaders);

    /// <summary>
    /// Creates a 400 Bad Request response with structured error information and additional headers.
    /// </summary>
    /// <param name="errorResponse">The error response object containing error details to serialize in the response body.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 400 Bad Request containing the serialized error response.</returns>
    public static APIGatewayHttpApiV2ProxyResponse BadRequest(ErrorResponse errorResponse, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.BadRequest, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse, customHeaders);

    /// <summary>
    /// Creates a 404 Not Found response with additional headers.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with the status 404 Not Found.</returns>
    public static APIGatewayHttpApiV2ProxyResponse NotFound(string? responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.NotFound, responseBody, customHeaders);

    /// <summary>
    /// Creates a 500 Internal Server Error response with additional headers.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 500 Internal Server Error.</returns>
    public static APIGatewayHttpApiV2ProxyResponse InternalServerError(string? responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.InternalServerError, responseBody, customHeaders);

    /// <summary>
    /// Creates a 500 Internal Server Error response with structured error information and additional headers.
    /// </summary>
    /// <param name="errorResponse">The error response object containing error details to serialize in the response body.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 500 Internal Server Error containing the serialized error response.</returns>
    public static APIGatewayHttpApiV2ProxyResponse InternalServerError(ErrorResponse errorResponse, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.InternalServerError, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse, customHeaders);

    /// <summary>
    /// Creates a 302 Found redirect response to the specified location.
    /// </summary>
    /// <param name="location">The URL to redirect to. Cannot be null or empty.</param>
    /// <param name="cacheControl">Optional cache control header value for the redirect response.</param>
    /// <returns>An API Gateway HTTP response with status 302 Found and Location header set to the specified URL.</returns>
    /// <exception cref="ArgumentException">Thrown when the location is null, empty, or whitespace.</exception>
    public static APIGatewayHttpApiV2ProxyResponse Redirect(string location, string? cacheControl = null)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location cannot be null, empty, or whitespace.", nameof(location));
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = location,
        };

        if (!string.IsNullOrEmpty(cacheControl))
        {
            headers["Cache-Control"] = cacheControl;
        }

        return CreateResponse(HttpStatusCode.Found, responseBody: null, customHeaders: () => headers);
    }

    public static APIGatewayHttpApiV2ProxyResponse Redirect(
        string location,
        HttpStatusCode status = HttpStatusCode.Found,
        int? sMaxAge = null,
        int? maxAge = null,
        int? staleWhileRevalidate = null,
        int? staleIfError = null,
        bool noStore = false)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location cannot be null/empty.", nameof(location));
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = location,
        };

        if (noStore)
        {
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        }
        else if (sMaxAge.HasValue || maxAge.HasValue || staleWhileRevalidate.HasValue || staleIfError.HasValue)
        {
            var cc = "public";
            if (sMaxAge.HasValue)
            {
                cc += $", s-maxage={sMaxAge.Value}";
            }

            if (maxAge.HasValue)
            {
                cc += $", max-age={maxAge.Value}";
            }

            if (staleWhileRevalidate.HasValue)
            {
                cc += $", stale-while-revalidate={staleWhileRevalidate.Value}";
            }

            if (staleIfError.HasValue)
            {
                cc += $", stale-if-error={staleIfError.Value}";
            }

            headers["Cache-Control"] = cc;
        }

        return CreateResponse(status, responseBody: null, () => headers);
    }

    /// <summary>
    /// Creates a CORS preflight response for OPTIONS requests with additional headers.
    /// </summary>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK and appropriate CORS headers.</returns>
    public static APIGatewayHttpApiV2ProxyResponse OptionsResponse(Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.NoContent, responseBody: null, customHeaders);

    public static Dictionary<string, string> NoCacheHeaders(string contentType = "application/json; charset=utf-8")
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = "no-store, no-cache, must-revalidate",
            ["Pragma"] = "no-cache",
            ["Expires"] = "0",
            ["Content-Type"] = contentType,
        };

    private static string ComputeStrongEtag(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var hex = Convert.ToHexString(hash);
        return $"\"{hex}\"";
    }

    private static bool IfNoneMatchMatches(string? ifNoneMatchHeader, string etag)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatchHeader))
        {
            return false;
        }

        var v = ifNoneMatchHeader.Trim();

        if (string.Equals(v, "*", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var token in v.Split(','))
        {
            var t = token.Trim();

            if (t.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            {
                t = t[2..].Trim();
            }

            var isQuoted = t is ['"', _, ..] && t[^1] == '"';
            if (!isQuoted)
            {
                t = $"\"{t}\"";
            }

            if (string.Equals(t, etag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string> BuildCacheHeaders(
        string etag,
        in CacheSettings s,
        DateTimeOffset? lastModifiedUtc = null,
        string contentType = "application/json; charset=utf-8")
    {
        var cacheControlValue =
            $"public, s-maxage={s.SMaxAgeSeconds}, max-age={s.MaxAgeSeconds}, stale-while-revalidate={s.StaleWhileRevalidateSeconds}, stale-if-error={s.StaleIfErrorSeconds}";

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = cacheControlValue,
            ["ETag"] = etag,
            ["Content-Type"] = contentType,
        };

        if (lastModifiedUtc.HasValue)
        {
            headers["Last-Modified"] = lastModifiedUtc.Value.ToUniversalTime().ToString("R");
        }

        return headers;
    }
}
