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
    public static APIGatewayHttpApiV2ProxyResponse OkHealth(string status, DateTimeOffset timestamp)
    {
        var response = new HealthCheckResponse(status, timestamp);
        return CreateResponse(HttpStatusCode.OK, response, LambdaFunctionJsonSerializerContext.Default.HealthCheckResponse);
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

    /// <summary>
    /// Creates a CORS preflight response for OPTIONS requests with additional headers.
    /// </summary>
    /// <param name="customHeaders">Optional function that returns custom headers to include in the response.</param>
    /// <returns>An API Gateway HTTP response with status 200 OK and appropriate CORS headers.</returns>
    public static APIGatewayHttpApiV2ProxyResponse OptionsResponse(Func<Dictionary<string, string>>? customHeaders = null) =>
        CreateResponse(HttpStatusCode.NoContent, responseBody: null, customHeaders);
}
