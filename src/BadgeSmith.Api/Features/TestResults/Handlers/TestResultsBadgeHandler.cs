using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Features.TestResults.Contracts;
using Microsoft.Extensions.Logging;
using LambdaFunctionJsonSerializerContext = BadgeSmith.Api.Core.Infrastructure.LambdaFunctionJsonSerializerContext;

namespace BadgeSmith.Api.Features.TestResults.Handlers;

internal class TestResultsBadgeHandler : ITestResultsBadgeHandler
{
    private readonly ILogger<TestResultsBadgeHandler> _logger;
    private readonly ITestResultsService _testResultsService;

    public TestResultsBadgeHandler(ILogger<TestResultsBadgeHandler> logger, ITestResultsService testResultsService)
    {
        _logger = logger;
        _testResultsService = testResultsService;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultsBadgeHandler)}.{nameof(HandleAsync)}");

        try
        {
            if (!TryExtractRouteParameters(routeContext, out var routeParams, out var errorResponse))
            {
                return errorResponse!;
            }

            var (owner, repo, platform, branch) = routeParams;

            _logger.LogInformation("Processing test badge request for {Owner}/{Repo} on {Platform}/{Branch}", owner, repo, platform, branch);

            var testResult = await _testResultsService.GetLatestTestResultAsync(owner, repo, platform, branch, ct).ConfigureAwait(false);
            if (testResult is { IsSuccess: false, TestResultEntity: null })
            {
                return testResult.Failure.Match(
                    notFound => ResponseHelper.NotFound(notFound.ToErrorResponse()),
                    error => ResponseHelper.InternalServerError(error.ToErrorResponse())
                );
            }

            var entity = testResult.TestResultEntity!;
            var badge = entity.ToBadge();

            _logger.LogInformation("Created test badge for {Owner}/{Repo}: {Message}",
                entity.Owner, entity.Repo, badge.Message);

            routeContext.Request.Headers.TryGetValue("if-none-match", out var ifNoneMatch);
            var cache = new ResponseHelper.CacheSettings(
                SMaxAgeSeconds: 600, // CloudFront cache
                MaxAgeSeconds: 300, // Browser's cache
                SwrSeconds: 1200, // Stale while revalidate
                SieSeconds: 3600 // Stale if error
            );

            return ResponseHelper.OkCached(
                badge,
                LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
                ifNoneMatchHeader: ifNoneMatch,
                cache: cache,
                lastModifiedUtc: entity.CreatedAt
            );
        }
        catch (Exception ex)
        {
            const string message = "Unexpected error processing test badge request";

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
}
