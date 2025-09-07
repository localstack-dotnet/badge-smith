using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Core.Security.Contracts;
using BadgeSmith.Api.Features.GitHub.Contracts;
using Microsoft.Extensions.Logging;
using LambdaFunctionJsonSerializerContext = BadgeSmith.Api.Core.Infrastructure.LambdaFunctionJsonSerializerContext;

namespace BadgeSmith.Api.Features.GitHub;

internal class GithubPackagesBadgeHandler : IGithubPackagesBadgeHandler
{
    private readonly ILogger<GithubPackagesBadgeHandler> _logger;
    private readonly IGitHubOrgSecretsService _gitHubOrgSecretsService;
    private readonly IGitHubPackageService _gitHubPackageService;

    private const string TokenType = "Package";

    public GithubPackagesBadgeHandler(ILogger<GithubPackagesBadgeHandler> logger, IGitHubOrgSecretsService gitHubOrgSecretsService, IGitHubPackageService gitHubPackageService)
    {
        _logger = logger;
        _gitHubOrgSecretsService = gitHubOrgSecretsService;
        _gitHubPackageService = gitHubPackageService;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(GithubPackagesBadgeHandler)}.{nameof(HandleAsync)}");

        try
        {
            if (!TryValidateRequest(routeContext, out var org, out var packageId, out var errorResponse))
            {
                return errorResponse!;
            }

            var tokenResult = await _gitHubOrgSecretsService.GetGitHubTokenAsync(org, TokenType, ct).ConfigureAwait(false);
            if (tokenResult is { IsSuccess: false, GithubSecret: null })
            {
                return tokenResult.Failure.Match(
                    _ => ResponseHelper.Unauthorized(),
                    error => ResponseHelper.InternalServerError(error.ToErrorResponse())
                );
            }

            var token = tokenResult.GithubSecret!;

            _logger.LogInformation("Github packages badge request received for {Org}/{Package}", org, packageId);

            var queryParams = routeContext.Request.QueryStringParameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var versionRange = queryParams.TryGetValue("version", out var version) ? version : null;
            var includePrerelease = queryParams.TryGetValue("prerelease", out var prerelease) &&
                                    bool.TryParse(prerelease, out var includePre) && includePre;

            var packageResult = await _gitHubPackageService.GetLatestVersionAsync(org, packageId, token, versionRange, includePrerelease, ct).ConfigureAwait(false);

            if (packageResult is not { IsSuccess: true, GitHubPackageInfo: not null })
            {
                return packageResult.Failure.Match
                (
                    notFound => ResponseHelper.NotFound(notFound.ToErrorResponse()),
                    validation => ResponseHelper.BadRequest(validation.ToErrorResponse()),
                    _ => ResponseHelper.Unauthorized(),
                    _ => ResponseHelper.Forbidden(),
                    error => ResponseHelper.InternalServerError(error.ToErrorResponse())
                );
            }

            var gitHubPackageInfo = packageResult.GitHubPackageInfo;
            var color = gitHubPackageInfo.IsPrerelease ? "orange" : "green";
            var badge = new ShieldsBadgeResponse(1, "github", gitHubPackageInfo.VersionString, color, NamedLogo: "github");

            routeContext.Request.Headers.TryGetValue("if-none-match", out var ifNoneMatch);
            var cache = new ResponseHelper.CacheSettings(SMaxAgeSeconds: 300, MaxAgeSeconds: 60, SwrSeconds: 900, SieSeconds: 3600);

            return ResponseHelper.OkCached(
                badge,
                LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
                ifNoneMatchHeader: ifNoneMatch,
                cache: cache,
                lastModifiedUtc: gitHubPackageInfo.LastModifiedUtc
            );
        }
        catch (Exception ex)
        {
            const string message = "Unexpected error processing Github badge request";

            _logger.LogError(ex, message);
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error);

            return ResponseHelper.InternalServerError(message);
        }
    }

    private bool TryValidateRequest(RouteContext routeContext, out string org, out string packageId, out APIGatewayHttpApiV2ProxyResponse? errorResponse)
    {
        org = string.Empty;
        packageId = string.Empty;
        errorResponse = null;

        // Validate provider is 'GitHub' for this route shape
        if (!routeContext.TryGetRouteValue("provider", out var provider) || string.IsNullOrWhiteSpace(provider))
        {
            var error = new ErrorResponse("Provider is required", [new ErrorDetail("INVALID_PROVIDER", "provider")]);
            errorResponse = ResponseHelper.BadRequest(error);
            return false;
        }

        if (!string.Equals(provider, "github", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(provider, "nuget", StringComparison.OrdinalIgnoreCase))
            {
                var hint = new ErrorResponse(
                    "Organization is not applicable for NuGet provider. Use /badges/packages/nuget/{package}",
                    [new ErrorDetail("ORG_NOT_APPLICABLE", "org")]);
                errorResponse = ResponseHelper.BadRequest(hint);
                return false;
            }

            var error = new ErrorResponse(
                $"Unsupported provider '{provider}'",
                [new ErrorDetail("INVALID_PROVIDER", "provider")]);
            errorResponse = ResponseHelper.BadRequest(error);
            return false;
        }

        if (!routeContext.TryGetRouteValue("org", out var organization) || string.IsNullOrWhiteSpace(organization))
        {
            _logger.LogWarning("Missing organization in GitHub package request");
            var error = new ErrorResponse(
                "Organization is required for GitHub provider",
                [new ErrorDetail("ORG_REQUIRED", "org")]);
            errorResponse = ResponseHelper.BadRequest(error);
            return false;
        }

        if (!routeContext.TryGetRouteValue("package", out var pkg) || string.IsNullOrWhiteSpace(pkg))
        {
            _logger.LogWarning("Missing package in GitHub package request");
            var error = new ErrorResponse(
                "Package is required",
                [new ErrorDetail("PACKAGE_REQUIRED", "package")]);
            errorResponse = ResponseHelper.BadRequest(error);
            return false;
        }

        org = organization;
        packageId = pkg;
        return true;
    }
}
