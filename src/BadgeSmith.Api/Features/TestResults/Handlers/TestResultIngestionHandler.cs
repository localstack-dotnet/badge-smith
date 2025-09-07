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
            if (!TryExtractRouteParameters(routeContext, out var routeParams, out var routeError))
            {
                return routeError!;
            }

            if (!TryParseTestPayload(routeContext.Request.Body, out var payload, out var parseError))
            {
                return parseError!;
            }

            if (!TryExtractAuthHeaders(routeContext.Request.Headers, out var authHeaders, out var headerError))
            {
                return headerError!;
            }

            var (owner, repo, platform, branch) = routeParams;
            var (signature, timestamp, nonce) = authHeaders;

            var hmacAuthRequest = new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, nonce, routeContext.Request.Body);

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

            var testResultPayload = new StoreTestResultRequest(owner, repo, platform, branch, payload);
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
                Repository: authenticatedRequest.RepoIdentifier,
                Timestamp: storedResult.StoredAt
            );

            _logger.LogInformation("Successfully stored test result {TestResultId} for {RepoIdentifier}",
                storedResult.TestResultId, authenticatedRequest.RepoIdentifier);

            return ResponseHelper.Created(
                response,
                LambdaFunctionJsonSerializerContext.Default.TestResultIngestionResponse,
                () => ResponseHelper.NoCacheHeaders("application/json; charset=utf-8"));
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

    private static bool TryExtractRouteParameters(
        RouteContext routeContext,
        out (string Owner, string Repo, string Platform, string Branch) routeParams,
        out APIGatewayHttpApiV2ProxyResponse? errorResponse)
    {
        routeParams = default;
        errorResponse = null;

        if (!routeContext.TryGetRouteValue("owner", out var owner) || string.IsNullOrWhiteSpace(owner))
        {
            errorResponse = ResponseHelper.BadRequest("Owner parameter is required");
            return false;
        }

        if (!routeContext.TryGetRouteValue("repo", out var repo) || string.IsNullOrWhiteSpace(repo))
        {
            errorResponse = ResponseHelper.BadRequest("Repo parameter is required");
            return false;
        }

        if (!routeContext.TryGetRouteValue("platform", out var platform) || string.IsNullOrWhiteSpace(platform))
        {
            errorResponse = ResponseHelper.BadRequest("Platform parameter is required");
            return false;
        }

        if (!routeContext.TryGetRouteValue("branch", out var branch) || string.IsNullOrWhiteSpace(branch))
        {
            errorResponse = ResponseHelper.BadRequest("Branch parameter is required");
            return false;
        }

        routeParams = (owner, repo, platform, branch);
        return true;
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
        catch (JsonException ex)
        {
            errorResponse = ResponseHelper.BadRequest($"Invalid JSON payload: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            errorResponse = ResponseHelper.InternalServerError($"Failed to parse payload: {ex.Message}");
            return false;
        }
    }
}
