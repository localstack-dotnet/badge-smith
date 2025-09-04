using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Domain.Models;
using BadgeSmith.Api.Domain.Services.Authentication.Contracts;
using BadgeSmith.Api.Infrastructure.Handlers.Contracts;
using BadgeSmith.Api.Infrastructure.Routing;
using BadgeSmith.Api.Infrastructure.Routing.Helpers;
using BadgeSmith.Api.Json;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Infrastructure.Handlers;

internal class TestResultIngestionHandler : ITestResultIngestionHandler
{
    private readonly ILogger<TestResultIngestionHandler> _logger;
    private readonly IHmacAuthenticationService _hmacAuthenticationService;

    public TestResultIngestionHandler(ILogger<TestResultIngestionHandler> logger, IHmacAuthenticationService hmacAuthenticationService)
    {
        _logger = logger;
        _hmacAuthenticationService = hmacAuthenticationService;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultIngestionHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Test result ingest request received");

        // Authenticate the request using HMAC
        var authResult = await _hmacAuthenticationService.ValidateRequestAsync(routeContext.Request, ct).ConfigureAwait(false);
        if (!authResult.IsSuccess)
        {
            return authResult.Failure.Match(
                invalidSig => ResponseHelper.BadRequest(invalidSig.ToErrorResponse()),
                missingHeaders => ResponseHelper.BadRequest(missingHeaders.ToErrorResponse()),
                invalidTimestamp => ResponseHelper.BadRequest(invalidTimestamp.ToErrorResponse()),
                nonceUsed => ResponseHelper.BadRequest(nonceUsed.ToErrorResponse()),
                _ => ResponseHelper.Unauthorized(),
                error => ResponseHelper.InternalServerError(error.ToErrorResponse())
            );
        }

        var authenticatedRequest = authResult.AuthenticatedRequest!;
        _logger.LogInformation("Successfully authenticated test result ingestion for repository {RepoIdentifier}",
            authenticatedRequest.RepoIdentifier);

        // Parse and validate test result payload
        // Store test results in DynamoDB
        // For now, return a success response

        var response = new TestResultIngestionResponse(
            TestResultId: $"badge-smith-{Guid.NewGuid():N}",
            Repository: authenticatedRequest.RepoIdentifier,
            Timestamp: authenticatedRequest.RequestTimestamp
        );

        return ResponseHelper.Created(
            response,
            LambdaFunctionJsonSerializerContext.Default.TestResultIngestionResponse,
            () => ResponseHelper.NoCacheHeaders("application/json; charset=utf-8"));
    }
}
