using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Domain.Models;
using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Infrastructure.Handlers.Contracts;
using BadgeSmith.Api.Infrastructure.Routing;
using BadgeSmith.Api.Infrastructure.Routing.Helpers;
using BadgeSmith.Api.Json;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Infrastructure.Handlers;

internal class GithubPackagesBadgeHandler : IGithubPackagesBadgeHandler
{
    private readonly ILogger<GithubPackagesBadgeHandler> _logger;
    private readonly IGitHubOrgSecretsService _gitHubOrgSecretsService;
    private readonly IGitHubPackageService _gitHubPackageService;

    public GithubPackagesBadgeHandler(ILogger<GithubPackagesBadgeHandler> logger, IGitHubOrgSecretsService gitHubOrgSecretsService, IGitHubPackageService gitHubPackageService)
    {
        _logger = logger;
        _gitHubOrgSecretsService = gitHubOrgSecretsService;
        _gitHubPackageService = gitHubPackageService;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(GithubPackagesBadgeHandler)}.{nameof(HandleAsync)}");
        if (!TryValidateRequest(routeContext, out var org, out var packageId, out var errorResponse))
        {
            return errorResponse!;
        }

        var orgLower = org.ToLowerInvariant();

        var tokenResult = await _gitHubOrgSecretsService.GetGitHubTokenAsync(orgLower, ct).ConfigureAwait(false);
        if (tokenResult is { IsSuccess: false, GithubSecret: null })
        {
            return tokenResult.Failure.Match(
                _ => ResponseHelper.Unauthorized(),
                error => ResponseHelper.InternalServerError(error.ToErrorResponse())
            );
        }

        var token = tokenResult.GithubSecret;

        _logger.LogInformation("Github packages badge request received for {Org}/{Package}", org, packageId);

        routeContext.Request.Headers.TryGetValue("if-none-match", out var ifNoneMatch);

        var cache = new ResponseHelper.CacheSettings(
            SMaxAgeSeconds: 10, // CloudFront caches 60s
            MaxAgeSeconds: 5, // browsers 10s
            SwrSeconds: 15,
            SieSeconds: 60);

        var packageResult = await _gitHubPackageService.GetLatestVersionAsync(org, packageId, ct: ct).ConfigureAwait(false);

        return packageResult.Match(
            success =>
            {
                var shieldsBadgeResponse = new ShieldsBadgeResponse(1, "github", success.VersionString, "green", NamedLogo: "github");
                return ResponseHelper.OkCached(
                    shieldsBadgeResponse,
                    LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
                    ifNoneMatchHeader: ifNoneMatch,
                    cache: cache,
                    lastModifiedUtc: success.LastModifiedUtc);
            },
            _ =>
            {
                _logger.LogWarning("GitHub package not found: {Org}/{Package}", org, packageId);
                var error = new ErrorResponse(
                    $"Package '{packageId}' not found in organization '{org}'",
                    [new ErrorDetail("PACKAGE_NOT_FOUND", "package")]);
                return ResponseHelper.NotFound(error);
            },
            gitHubError =>
            {
                _logger.LogError("GitHub API error for {Org}/{Package}: {Message}", org, packageId, gitHubError.Reason);
                var error = new ErrorResponse(
                    "GitHub API error occurred",
                    [new ErrorDetail("GITHUB_API_ERROR", "server")]);
                return ResponseHelper.InternalServerError(error);
            }
        );
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
