#pragma warning disable CA1873 // Replace with LoggerMessage source-generated logging.

using System.Diagnostics;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Core.Security;
using BadgeSmith.Api.Core.Security.Contracts;
using BadgeSmith.Api.Features.TestResults.Contracts;
using BadgeSmith.Api.Features.TestResults.Models;
using Microsoft.Extensions.Logging;
using LambdaFunctionJsonSerializerContext = BadgeSmith.Api.Core.Infrastructure.LambdaFunctionJsonSerializerContext;

namespace BadgeSmith.Api.Features.TestResults.Handlers;

internal class TestResultIngestionHandler : ITestResultIngestionHandler
{
    private readonly ILogger<TestResultIngestionHandler> _logger;
    private readonly IHmacAuthenticationService _hmacAuthenticationService;
    private readonly ITestResultsService _testResultsService;

    public TestResultIngestionHandler(ILogger<TestResultIngestionHandler> logger, IHmacAuthenticationService hmacAuthenticationService, ITestResultsService testResultsService)
    {
        _logger = logger;
        _hmacAuthenticationService = hmacAuthenticationService;
        _testResultsService = testResultsService;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultIngestionHandler)}.{nameof(HandleAsync)}");

        try
        {
            var routeResult = TestResultRouteParameters.Extract(routeContext);
            if (!routeResult.IsSuccess)
            {
                return ResponseHelper.BadRequest(routeResult.Failure.ToErrorResponse());
            }

            if (!TryParseTestPayload(routeContext.Request.Body, out var payload, out var parseError))
            {
                return parseError!;
            }

            if (!TryExtractAuthHeaders(routeContext.Request.Headers, out var authHeaders, out var headerError))
            {
                return headerError!;
            }

            var parameters = routeResult.Parameters;
            var (signature, timestamp, nonce) = authHeaders;

            var hmacAuthRequest = new HmacAuthContext(
                Owner: parameters.Owner,
                Repo: parameters.Repo,
                Platform: parameters.Platform,
                Branch: parameters.Branch,
                Signature: signature,
                Timestamp: timestamp,
                Nonce: nonce,
                RequestBody: routeContext.Request.Body);

            _logger.LogInformation("Test result ingest request received");

            // Authenticate the request using HMAC
            var authResult = await _hmacAuthenticationService.ValidateRequestAsync(hmacAuthRequest, ct).ConfigureAwait(false);
            if (!authResult.IsSuccess)
            {
                return authResult.Failure.Match(
                    _ => ResponseHelper.Unauthorized(),
                    invalidTimestamp => ResponseHelper.BadRequest(invalidTimestamp.ToErrorResponse()),
                    nonceUsed => ResponseHelper.BadRequest(nonceUsed.ToErrorResponse()),
                    _ => ResponseHelper.Unauthorized(),
                    error => ResponseHelper.InternalServerError(error.ToErrorResponse())
                );
            }

            var authenticatedRequest = authResult.AuthenticatedRequest!;
            _logger.LogInformation("Successfully authenticated test result ingestion for repository {RepoIdentifier}", authenticatedRequest.RepoIdentifier);

            return await StoreTestResultAsync(parameters, payload, authenticatedRequest.RepoIdentifier, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            const string message = "Unexpected error processing Test results ingestion request";

            _logger.LogError(ex, message);
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error);

            return ResponseHelper.InternalServerError(message);
        }
    }

    private async Task<APIGatewayHttpApiV2ProxyResponse> StoreTestResultAsync(
        TestResultRouteParameters parameters,
        TestResultPayload? payload,
        string repoIdentifier,
        CancellationToken ct)
    {
        var testResultPayload = new StoreTestResultRequest(
            Owner: parameters.Owner,
            Repo: parameters.Repo,
            Platform: parameters.Platform,
            Branch: parameters.Branch,
            Payload: payload);

        var storeResult = await _testResultsService.StoreTestResultAsync(testResultPayload, ct).ConfigureAwait(false);
        if (!storeResult.IsSuccess)
        {
            return storeResult.Failure.Match(
                invalidPayload => ResponseHelper.BadRequest(invalidPayload.ToErrorResponse()),
                duplicate => ResponseHelper.Conflict(duplicate.ToErrorResponse()),
                error => ResponseHelper.InternalServerError(error.ToErrorResponse())
            );
        }

        var storedResult = storeResult.TestResultStored!;
        var response = new TestResultIngestionResponse(
            TestResultId: storedResult.TestResultId,
            Repository: repoIdentifier,
            Timestamp: storedResult.StoredAt
        );

        _logger.LogInformation("Successfully stored test result {TestResultId} for {RepoIdentifier}",
            storedResult.TestResultId, repoIdentifier);

        return ResponseHelper.Created(
            response,
            LambdaFunctionJsonSerializerContext.Default.TestResultIngestionResponse,
            () => ResponseHelper.NoCacheHeaders("application/json; charset=utf-8"));
    }

    private static bool TryExtractAuthHeaders(
        IDictionary<string, string>? headers,
        out (string Signature, string Timestamp, string Nonce) authHeaders,
        out APIGatewayHttpApiV2ProxyResponse? errorResponse)
    {
        authHeaders = default;
        errorResponse = null;

        if (headers == null)
        {
            errorResponse = ResponseHelper.BadRequest("Request headers are missing");
            return false;
        }

        if (!headers.TryGetValue("x-signature", out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            errorResponse = ResponseHelper.BadRequest("X-Signature header is required");
            return false;
        }

        if (!headers.TryGetValue("x-timestamp", out var timestampStr) || string.IsNullOrWhiteSpace(timestampStr))
        {
            errorResponse = ResponseHelper.BadRequest("X-Timestamp header is required");
            return false;
        }

        if (!headers.TryGetValue("x-nonce", out var nonce) || string.IsNullOrWhiteSpace(nonce))
        {
            errorResponse = ResponseHelper.BadRequest("X-Nonce header is required");
            return false;
        }

        authHeaders = (signature.Trim(), timestampStr.Trim(), nonce.Trim());
        return true;
    }

    private static bool TryParseTestPayload(string? requestBody, out TestResultPayload? payload, out APIGatewayHttpApiV2ProxyResponse? errorResponse)
    {
        payload = null;
        errorResponse = null;

        if (string.IsNullOrWhiteSpace(requestBody))
        {
            errorResponse = ResponseHelper.BadRequest("Request body is required");
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize(requestBody, LambdaFunctionJsonSerializerContext.Default.TestResultPayload)!;
            return true;
        }
        catch (JsonException)
        {
            errorResponse = ResponseHelper.BadRequest("Invalid JSON payload");
            return false;
        }
    }
}

#pragma warning restore CA1873
