using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Features.NuGet.Contracts;
using Microsoft.Extensions.Logging;
using LambdaFunctionJsonSerializerContext = BadgeSmith.Api.Core.Infrastructure.LambdaFunctionJsonSerializerContext;

namespace BadgeSmith.Api.Features.NuGet;

internal class NuGetPackageBadgeHandler : INugetPackageBadgeHandler
{
    private readonly ILogger<NuGetPackageBadgeHandler> _logger;
    private readonly INuGetPackageService _nugetPackageService;

    public NuGetPackageBadgeHandler(ILogger<NuGetPackageBadgeHandler> logger, INuGetPackageService nuGetPackageService)
    {
        _logger = logger;
        _nugetPackageService = nuGetPackageService;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NuGetPackageBadgeHandler)}.{nameof(HandleAsync)}");

        try
        {
            if (!TryValidateRequest(routeContext, out var packageId, out var errorResponse))
            {
                return errorResponse!;
            }

            _logger.LogInformation("Processing NuGet badge request for package: {PackageId}", packageId);

            var queryParams = routeContext.Request.QueryStringParameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var versionRange = queryParams.TryGetValue("version", out var version) ? version : null;
            var includePrerelease = queryParams.TryGetValue("prerelease", out var prerelease) &&
                                    bool.TryParse(prerelease, out var includePre) && includePre;

            var result = await _nugetPackageService.GetLatestVersionAsync(packageId, versionRange, includePrerelease, ct).ConfigureAwait(false);

            if (result is not { IsSuccess: true, NuGetPackageInfo: not null })
            {
                return result.Failure.Match
                (
                    notFound => ResponseHelper.NotFound(notFound.ToErrorResponse()),
                    validation => ResponseHelper.BadRequest(validation.ToErrorResponse()),
                    error => ResponseHelper.InternalServerError(error.ToErrorResponse())
                );
            }

            var nuGetPackageInfo = result.NuGetPackageInfo;
            var color = nuGetPackageInfo.IsPrerelease ? "orange" : "blue";
            var badge = new ShieldsBadgeResponse(1, "nuget", nuGetPackageInfo.VersionString, color, NamedLogo: "nuget");

            _logger.LogInformation("Created badge for {PackageId} version {Version}", packageId, nuGetPackageInfo.VersionString);

            routeContext.Request.Headers.TryGetValue("if-none-match", out var ifNoneMatch);
            var cache = new ResponseHelper.CacheSettings(SMaxAgeSeconds: 600, MaxAgeSeconds: 300, SwrSeconds: 1200, SieSeconds: 3600);

            return ResponseHelper.OkCached(
                badge,
                LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
                ifNoneMatchHeader: ifNoneMatch,
                cache: cache,
                lastModifiedUtc: nuGetPackageInfo.LastModifiedUtc
            );
        }
        catch (Exception ex)
        {
            const string message = "Unexpected error processing NuGet badge request";

            _logger.LogError(ex, message);
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error);

            return ResponseHelper.InternalServerError(message);
        }
    }

    private bool TryValidateRequest(RouteContext routeContext, out string packageId, out APIGatewayHttpApiV2ProxyResponse? errorResponse)
    {
        packageId = string.Empty;
        errorResponse = null;

        // Validate provider is 'nuget' for this route shape
        if (!routeContext.TryGetRouteValue("provider", out var provider) || string.IsNullOrWhiteSpace(provider))
        {
            var error = new ErrorResponse(
                "Provider is required",
                [new ErrorDetail("INVALID_PROVIDER", "provider")]);
            errorResponse = ResponseHelper.BadRequest(error);
            return false;
        }

        if (!string.Equals(provider, "nuget", StringComparison.OrdinalIgnoreCase))
        {
            // Helpful guidance if someone hits the nuget-shaped route with GitHub provider
            if (string.Equals(provider, "github", StringComparison.OrdinalIgnoreCase))
            {
                var errorOrg = new ErrorResponse(
                    "Organization is required for GitHub provider. Use /badges/packages/github/{org}/{package}",
                    [new ErrorDetail("ORG_REQUIRED", "org")]);
                errorResponse = ResponseHelper.BadRequest(errorOrg);
                return false;
            }

            var error = new ErrorResponse($"Unsupported provider '{provider}'", [new ErrorDetail("INVALID_PROVIDER", "provider")]);
            errorResponse = ResponseHelper.BadRequest(error);
            return false;
        }

        if (!routeContext.TryGetRouteValue("package", out var pkg) || string.IsNullOrWhiteSpace(pkg))
        {
            _logger.LogWarning("Missing package ID in request");
            var error = new ErrorResponse("Package identifier is required", [new ErrorDetail("PACKAGE_ID_REQUIRED", "packageId")]);
            errorResponse = ResponseHelper.BadRequest(error);
            return false;
        }

        packageId = pkg;
        return true;
    }
}
