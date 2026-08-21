#pragma warning disable CA1873 // Replace with LoggerMessage source-generated logging.

using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Features.TestResults.Contracts;
using BadgeSmith.Api.Features.TestResults.Models;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Features.TestResults.Handlers;

internal class TestResultRedirectionHandler : ITestResultRedirectionHandler
{
    private readonly ILogger<TestResultRedirectionHandler> _logger;
    private readonly ITestResultsService _testResultsService;

    public TestResultRedirectionHandler(ILogger<TestResultRedirectionHandler> logger, ITestResultsService testResultsService)
    {
        _logger = logger;
        _testResultsService = testResultsService;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultRedirectionHandler)}.{nameof(HandleAsync)}");

        try
        {
            var routeResult = TestResultRouteParameters.Extract(routeContext);
            if (!routeResult.IsSuccess)
            {
                return ResponseHelper.BadRequest(routeResult.Failure.ToErrorResponse());
            }

            var parameters = routeResult.Parameters;

            _logger.LogInformation("Processing test redirect request for {Owner}/{Repo} on {Platform}/{Branch}", parameters.Owner, parameters.Repo, parameters.Platform, parameters.Branch);

            var testResult = await _testResultsService.GetLatestTestResultAsync(parameters.Owner, parameters.Repo, parameters.Platform, parameters.Branch, ct).ConfigureAwait(false);
            if (testResult is { IsSuccess: false, TestResultEntity: null })
            {
                return testResult.Failure.Match(
                    notFound => ResponseHelper.NotFound(notFound.ToErrorResponse()),
                    error => ResponseHelper.InternalServerError(error.ToErrorResponse())
                );
            }

            var entity = testResult.TestResultEntity!;

            _logger.LogInformation("Redirecting to test result URL for {Owner}/{Repo}: {RunId}", entity.Owner, entity.Repo, entity.RunId);

            // Redirect to the GitHub Actions run page
            return ResponseHelper.Redirect(
                location: entity.UrlHtml,
                sMaxAge: 600, // CloudFront caches
                maxAge: 300, // Browser's cache
                staleWhileRevalidate: 1200,
                staleIfError: 3600
            );
        }
        catch (Exception ex)
        {
            const string message = "Unexpected error processing test redirect request";

            _logger.LogError(ex, message);
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error);

            return ResponseHelper.InternalServerError(message);
        }
    }
}

#pragma warning restore CA1873
