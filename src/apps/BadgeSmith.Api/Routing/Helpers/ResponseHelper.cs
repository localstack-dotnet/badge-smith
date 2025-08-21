using System.Net;
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
    private static readonly Dictionary<string, string> DefaultHeaders = new(StringComparer.Ordinal)
    {
        ["Content-Type"] = "application/json",
        ["Access-Control-Allow-Origin"] = "*",
    };

    /// <summary>
    /// Creates a custom HTTP response with the specified status code and optional body/headers.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="additionalHeaders">Additional headers to merge with default headers. These override defaults if keys match.</param>
    /// <returns>An API Gateway HTTP response with the specified parameters.</returns>
    public static APIGatewayHttpApiV2ProxyResponse CreateResponse(HttpStatusCode statusCode, string? responseBody = null, IDictionary<string, string>? additionalHeaders = null)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
            Body = responseBody,
            Headers = MergeHeaders(additionalHeaders),
        };
    }

    /// <summary>
    /// Creates a custom HTTP response with a serialized object body.
    /// </summary>
    /// <typeparam name="T">The type of the response object.</typeparam>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseObject">The object to serialize as the response body.</param>
    /// <param name="jsonTypeInfo">The JSON type info for AOT serialization.</param>
    /// <param name="additionalHeaders">Additional headers to merge with default headers.</param>
    /// <returns>An API Gateway HTTP response with the serialized object body.</returns>
    public static APIGatewayHttpApiV2ProxyResponse CreateResponse<T>(
        HttpStatusCode statusCode,
        T responseObject,
        JsonTypeInfo<T> jsonTypeInfo,
        IDictionary<string, string>? additionalHeaders = null)
    {
        return CreateResponse(statusCode, JsonSerializer.Serialize(responseObject, jsonTypeInfo), additionalHeaders);
    }

    /// <summary>
    /// Create a response with a specific content type (handy for OPTIONS/204 or HTML/CSV).
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="contentType">The content type of the response.</param>
    /// <param name="additionalHeaders">Additional headers to merge with default headers. These override defaults if keys match.</param>
    public static APIGatewayHttpApiV2ProxyResponse CreateRawResponse(
        HttpStatusCode statusCode,
        string? responseBody = null,
        string contentType = "text/plain",
        IDictionary<string, string>? additionalHeaders = null)
    {
        var headers = MergeHeaders(additionalHeaders);
        headers["Content-Type"] = contentType;
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
            Body = responseBody,
            Headers = headers,
        };
    }

    /// <summary>
    /// Creates a successful 200 OK response.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Ok(string? responseBody = null) =>
        CreateResponse(HttpStatusCode.OK, responseBody);

    /// <summary>
    /// Creates a successful 200 OK response with additional headers.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Ok(string? responseBody, IDictionary<string, string> additionalHeaders) =>
        CreateResponse(HttpStatusCode.OK, responseBody, additionalHeaders);

    /// <summary>
    /// Creates a successful 200 OK response with a serialized object.
    /// </summary>
    /// <typeparam name="T">The type of the response object.</typeparam>
    /// <param name="responseObject">The object to serialize as the response body.</param>
    /// <param name="jsonTypeInfo">The JSON type info for AOT serialization.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK containing the serialized object.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Ok<T>(T responseObject, JsonTypeInfo<T> jsonTypeInfo, IDictionary<string, string>? additionalHeaders = null) =>
        CreateResponse(HttpStatusCode.OK, responseObject, jsonTypeInfo, additionalHeaders);

    /// <summary>
    /// Creates a successful 200 OK response with health check information.
    /// </summary>
    /// <param name="status">The health status message.</param>
    /// <param name="timestamp">The timestamp when the health check was performed.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK containing health information.</returns>
    public static APIGatewayHttpApiV2ProxyResponse OkHealth(string status, DateTimeOffset timestamp)
    {
        var response = new HealthCheckResponse(status, timestamp);
        return CreateResponse(HttpStatusCode.OK, response, LambdaFunctionJsonSerializerContext.Default.HealthCheckResponse);
    }

    /// <summary>
    /// Creates a 201 Created response.
    /// </summary>
    /// <param name="responseBody">The response body content.</param>
    /// <returns>An API Gateway HTTP response with status 201 Created.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Created(string responseBody) =>
        CreateResponse(HttpStatusCode.Created, responseBody);

    /// <summary>
    /// Creates a 201 Created response with additional headers.
    /// </summary>
    /// <param name="responseBody">The response body content.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 201 Created.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Created(string responseBody, IDictionary<string, string> additionalHeaders) =>
        CreateResponse(HttpStatusCode.Created, responseBody, additionalHeaders);

    /// <summary>
    /// Creates a 201 Created response with a serialized object.
    /// </summary>
    /// <typeparam name="T">The type of the response object.</typeparam>
    /// <param name="responseObject">The object to serialize as the response body.</param>
    /// <param name="jsonTypeInfo">The JSON type info for AOT serialization.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 201 Created containing the serialized object.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Created<T>(T responseObject, JsonTypeInfo<T> jsonTypeInfo, IDictionary<string, string>? additionalHeaders = null) =>
        CreateResponse(HttpStatusCode.Created, responseObject, jsonTypeInfo, additionalHeaders);

    /// <summary>
    /// Creates a 400 Bad Request response.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <returns>An API Gateway HTTP response with status 400 Bad Request.</returns>
    public static APIGatewayHttpApiV2ProxyResponse BadRequest(string? responseBody = null) =>
        CreateResponse(HttpStatusCode.BadRequest, responseBody);

    /// <summary>
    /// Creates a 400 Bad Request response with additional headers.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 400 Bad Request.</returns>
    public static APIGatewayHttpApiV2ProxyResponse BadRequest(string? responseBody, IDictionary<string, string> additionalHeaders) =>
        CreateResponse(HttpStatusCode.BadRequest, responseBody, additionalHeaders);

    /// <summary>
    /// Creates a 400 Bad Request response with structured error information.
    /// </summary>
    /// <param name="errorResponse">The error response object containing error details to serialize in the response body.</param>
    /// <returns>An API Gateway HTTP response with status 400 Bad Request containing the serialized error response.</returns>
    public static APIGatewayHttpApiV2ProxyResponse BadRequest(ErrorResponse errorResponse) =>
        CreateResponse(HttpStatusCode.BadRequest, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse);

    /// <summary>
    /// Creates a 400 Bad Request response with structured error information and additional headers.
    /// </summary>
    /// <param name="errorResponse">The error response object containing error details to serialize in the response body.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 400 Bad Request containing the serialized error response.</returns>
    public static APIGatewayHttpApiV2ProxyResponse BadRequest(ErrorResponse errorResponse, IDictionary<string, string> additionalHeaders) =>
        CreateResponse(HttpStatusCode.BadRequest, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse, additionalHeaders);

    /// <summary>
    /// Creates a 404 Not Found response.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <returns>An API Gateway HTTP response with the status 404 Not Found.</returns>
    public static APIGatewayHttpApiV2ProxyResponse NotFound(string? responseBody = null) =>
        CreateResponse(HttpStatusCode.NotFound, responseBody);

    /// <summary>
    /// Creates a 404 Not Found response with additional headers.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with the status 404 Not Found.</returns>
    public static APIGatewayHttpApiV2ProxyResponse NotFound(string? responseBody, IDictionary<string, string> additionalHeaders) =>
        CreateResponse(HttpStatusCode.NotFound, responseBody, additionalHeaders);

    /// <summary>
    /// Creates a 500 Internal Server Error response.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <returns>An API Gateway HTTP response with status 500 Internal Server Error.</returns>
    public static APIGatewayHttpApiV2ProxyResponse InternalServerError(string? responseBody = null) =>
        CreateResponse(HttpStatusCode.InternalServerError, responseBody);

    /// <summary>
    /// Creates a 500 Internal Server Error response with additional headers.
    /// </summary>
    /// <param name="responseBody">The optional response body content.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 500 Internal Server Error.</returns>
    public static APIGatewayHttpApiV2ProxyResponse InternalServerError(string? responseBody, IDictionary<string, string> additionalHeaders) =>
        CreateResponse(HttpStatusCode.InternalServerError, responseBody, additionalHeaders);

    /// <summary>
    /// Creates a 500 Internal Server Error response with structured error information.
    /// </summary>
    /// <param name="errorResponse">The error response object containing error details to serialize in the response body.</param>
    /// <returns>An API Gateway HTTP response with status 500 Internal Server Error containing the serialized error response.</returns>
    public static APIGatewayHttpApiV2ProxyResponse InternalServerError(ErrorResponse errorResponse) =>
        CreateResponse(HttpStatusCode.InternalServerError, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse);

    /// <summary>
    /// Creates a 500 Internal Server Error response with structured error information and additional headers.
    /// </summary>
    /// <param name="errorResponse">The error response object containing error details to serialize in the response body.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 500 Internal Server Error containing the serialized error response.</returns>
    public static APIGatewayHttpApiV2ProxyResponse InternalServerError(ErrorResponse errorResponse, IDictionary<string, string> additionalHeaders) =>
        CreateResponse(HttpStatusCode.InternalServerError, errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse, additionalHeaders);

    /// <summary>
    /// Creates a 302 Found redirect response to the specified location.
    /// </summary>
    /// <param name="location">The URL to redirect to. Cannot be null or empty.</param>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <param name="cacheControl">Optional cache control header value for the redirect response.</param>
    /// <returns>An API Gateway HTTP response with status 302 Found and Location header set to the specified URL.</returns>
    /// <exception cref="ArgumentException">Thrown when the location is null, empty, or whitespace.</exception>
    public static APIGatewayHttpApiV2ProxyResponse Redirect(string location, IDictionary<string, string>? additionalHeaders = null, string? cacheControl = null)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location cannot be null, empty, or whitespace.", nameof(location));
        }

        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Location"] = location,
        };

        if (!string.IsNullOrEmpty(cacheControl))
        {
            headers["Cache-Control"] = cacheControl;
        }

        if (additionalHeaders != null)
        {
            foreach (var (key, value) in additionalHeaders)
            {
                headers[key] = value;
            }
        }

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)HttpStatusCode.Found,
            Body = string.Empty,
            Headers = headers,
        };
    }

    /// <summary>
    /// Creates a CORS preflight response for OPTIONS requests.
    /// </summary>
    /// <returns>An API Gateway HTTP response with status 200 OK and appropriate CORS headers.</returns>
    public static APIGatewayHttpApiV2ProxyResponse OptionsResponse() =>
        CreateRawResponse(HttpStatusCode.NoContent, string.Empty, contentType: "text/plain");

    /// <summary>
    /// Creates a CORS preflight response for OPTIONS requests with additional headers.
    /// </summary>
    /// <param name="additionalHeaders">Additional headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK and appropriate CORS headers.</returns>
    public static APIGatewayHttpApiV2ProxyResponse OptionsResponse(IDictionary<string, string> additionalHeaders) =>
        CreateRawResponse(HttpStatusCode.NoContent, string.Empty, contentType: "text/plain", additionalHeaders);

    /// <summary>
    /// Merges additional headers with default headers, with additional headers taking precedence.
    /// </summary>
    /// <param name="additionalHeaders">Additional headers to merge with defaults.</param>
    /// <returns>A new dictionary containing the merged headers.</returns>
    private static Dictionary<string, string> MergeHeaders(IDictionary<string, string>? additionalHeaders)
    {
        var headers = new Dictionary<string, string>(DefaultHeaders, StringComparer.Ordinal);

        if (additionalHeaders == null)
        {
            return headers;
        }

        foreach (var (key, value) in additionalHeaders)
        {
            headers[key] = value;
        }

        return headers;
    }
}
