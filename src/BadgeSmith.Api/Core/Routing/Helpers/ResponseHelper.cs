using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Infrastructure;

namespace BadgeSmith.Api.Core.Routing.Helpers;

/// <summary>
/// Helper methods for creating standardized API Gateway HTTP responses
/// </summary>
internal static class ResponseHelper
{
    private const string DefaultContentType = "application/json; charset=utf-8";
    private const string NoStoreValue = "no-store";
    private const string UpperHexDigits = "0123456789ABCDEF";
    private const int StackallocThresholdBytes = 512;

    /// <summary>
    /// Creates a custom HTTP response with the specified status code and optional body/headers.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with the specified parameters.</returns>
    public static APIGatewayHttpApiV2ProxyResponse CreateResponse(HttpStatusCode statusCode, string? responseBody = null, Func<Dictionary<string, string>>? customHeaders = null)
    {
        var headers = customHeaders?.Invoke() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        headers.TryAdd("Content-Type", DefaultContentType);

        var apiGatewayHttpApiV2ProxyResponse = new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
            Headers = headers,
        };

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
        PublicCachePolicy cachePolicy,
        string? ifNoneMatchHeader = null,
        DateTimeOffset? lastModifiedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(cachePolicy);
        var body = JsonSerializer.Serialize(responseObject, jsonTypeInfo);
        var etag = ComputeStrongEtag(body);

        if (IfNoneMatchMatches(ifNoneMatchHeader, etag))
        {
            return CreateResponse(HttpStatusCode.NotModified, responseBody: null,
                customHeaders: () => BuildCacheHeaders(etag, cachePolicy, lastModifiedUtc));
        }

        return CreateResponse(HttpStatusCode.OK, body,
            customHeaders: () => BuildCacheHeaders(etag, cachePolicy, lastModifiedUtc));
    }

    public static APIGatewayHttpApiV2ProxyResponse Ok(string? responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.OK, responseBody, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse Ok<T>(T responseObject, JsonTypeInfo<T> jsonTypeInfo, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.OK, responseObject, jsonTypeInfo, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse Created(string responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.Created, responseBody, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse Created<T>(T responseObject, JsonTypeInfo<T> jsonTypeInfo,
        Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.Created, responseObject, jsonTypeInfo, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse BadRequest(string? responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.BadRequest, responseBody, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse BadRequest(ErrorResponse errorResponse, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.BadRequest, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse NotFound(string? responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.NotFound, responseBody, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse NotFound(ErrorResponse errorResponse, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.NotFound, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse Conflict(ErrorResponse errorResponse, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.Conflict, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse InternalServerError(string? responseBody, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.InternalServerError, responseBody, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse InternalServerError(ErrorResponse errorResponse, Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.InternalServerError, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse Unauthorized(Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.Unauthorized, responseBody: null, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse Forbidden(Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.Forbidden, responseBody: null, customHeaders);

    public static APIGatewayHttpApiV2ProxyResponse RedirectCached(string location, PublicCachePolicy cachePolicy) =>
        RedirectCached(location, RedirectStatus.Found, cachePolicy);

    public static APIGatewayHttpApiV2ProxyResponse RedirectCached(string location, RedirectStatus status, PublicCachePolicy cachePolicy)
    {
        ArgumentNullException.ThrowIfNull(cachePolicy);
        return CreateRedirect(location, status, cachePolicy.CacheControl);
    }

    public static APIGatewayHttpApiV2ProxyResponse RedirectNoStore(string location) =>
        RedirectNoStore(location, RedirectStatus.Found);

    public static APIGatewayHttpApiV2ProxyResponse RedirectNoStore(string location, RedirectStatus status) =>
        CreateRedirect(location, status, NoStoreValue);

    public static APIGatewayHttpApiV2ProxyResponse OptionsResponse(Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.NoContent, responseBody: null, customHeaders);

    public static Dictionary<string, string> NoStoreHeaders(string contentType = DefaultContentType)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = NoStoreValue,
            ["Content-Type"] = contentType,
        };

    private static APIGatewayHttpApiV2ProxyResponse CreateRedirect(string location, RedirectStatus status, string cacheControl)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location cannot be null, empty, or whitespace.", nameof(location));
        }

        if (status.Code is < 300 or > 399)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status.Code, "Redirect status must be a 3xx redirect code.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = location,
            ["Cache-Control"] = cacheControl,
        };

        return CreateResponse((HttpStatusCode)status.Code, responseBody: null, customHeaders: () => headers);
    }

    private static string ComputeStrongEtag(string payload)
    {
        var byteCount = Encoding.UTF8.GetByteCount(payload);

        return byteCount <= StackallocThresholdBytes
            ? HashToEtag(payload, byteCount, stackalloc byte[StackallocThresholdBytes])
            : HashToEtagPooled(payload, byteCount);
    }

    private static string HashToEtagPooled(string payload, int byteCount)
    {
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            return HashToEtag(payload, byteCount, rentedBuffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static string HashToEtag(string payload, int byteCount, Span<byte> utf8Buffer)
    {
        var bytesWritten = Encoding.UTF8.GetBytes(payload, utf8Buffer);
        if (bytesWritten != byteCount)
        {
            throw new InvalidOperationException($"UTF-8 encoding wrote {bytesWritten} bytes but expected {byteCount}.");
        }

        Span<byte> hash = stackalloc byte[32];
        var hashBytesWritten = SHA256.HashData(utf8Buffer[..bytesWritten], hash);
        if (hashBytesWritten != 32)
        {
            throw new InvalidOperationException($"SHA-256 wrote {hashBytesWritten} bytes but expected 32.");
        }

        return string.Create(66, hash, static (span, digest) =>
        {
            span[0] = '"';
            for (var index = 0; index < digest.Length; index++)
            {
                var value = digest[index];
                span[1 + (index * 2)] = UpperHexDigits[(value >> 4) & 0xF];
                span[2 + (index * 2)] = UpperHexDigits[value & 0xF];
            }

            span[65] = '"';
        });
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

    private static Dictionary<string, string> BuildCacheHeaders(string etag, PublicCachePolicy cachePolicy, DateTimeOffset? lastModifiedUtc = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = cachePolicy.CacheControl,
            ["ETag"] = etag,
        };

        if (lastModifiedUtc.HasValue)
        {
            headers["Last-Modified"] = lastModifiedUtc.Value.ToUniversalTime().ToString("R");
        }

        return headers;
    }
}
