#pragma warning disable CA1873 // Replace with LoggerMessage source-generated logging.

using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Features.TestResults.Contracts;
using BadgeSmith.Api.Features.TestResults.Models;
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
            var routeResult = TestResultRouteParameters.Extract(routeContext);
            if (!routeResult.IsSuccess)
            {
                return ResponseHelper.BadRequest(routeResult.Failure.ToErrorResponse());
            }

            var parameters = routeResult.Parameters;

            _logger.LogInformation("Processing test badge request for {Owner}/{Repo} on {Platform}/{Branch}", parameters.Owner, parameters.Repo, parameters.Platform, parameters.Branch);

            var testResult = await _testResultsService.GetLatestTestResultAsync(parameters.Owner, parameters.Repo, parameters.Platform, parameters.Branch, ct).ConfigureAwait(false);
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

            return ResponseHelper.OkCached(
                badge,
                LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
                cachePolicy: BadgeResponsePolicy.PublicCache,
                ifNoneMatchHeader: ifNoneMatch,
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
}

#pragma warning restore CA1873
