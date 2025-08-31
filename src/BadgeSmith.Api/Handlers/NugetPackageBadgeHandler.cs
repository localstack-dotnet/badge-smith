using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Domain.Models;
using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Observability.Performance;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal interface INugetPackageBadgeHandler : IRouteHandler;

internal class NugetPackageBadgeHandler : INugetPackageBadgeHandler
{
    public static readonly Dictionary<string, string> ContentTypeHeader = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Content-Type", "application/json; charset=utf-8" },
    };

    private readonly ILogger<NugetPackageBadgeHandler> _logger;
    private readonly INuGetPackageService _nugetPackageService;

    public NugetPackageBadgeHandler(ILogger<NugetPackageBadgeHandler> logger, INugetPackageServiceFactory nugetPackageServiceFactory)
    {
        _logger = logger;
        _nugetPackageService = nugetPackageServiceFactory.NuGetPackageService;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NugetPackageBadgeHandler)}.{nameof(HandleAsync)}");
        using var perfScope = PerfTracker.StartScope($"{nameof(NugetPackageBadgeHandler)}.{nameof(HandleAsync)}", typeof(NugetPackageBadgeHandler).FullName);

        try
        {
            if (!routeContext.TryGetRouteValue("package", out var packageId) || string.IsNullOrWhiteSpace(packageId))
            {
                _logger.LogWarning("Missing package ID in request");
                return CreateInvalidPackageIdentifierResponse();
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
                    notFound => ResponseHelper.NotFound(notFound.ToErrorResponse(), () => ContentTypeHeader),
                    validation => ResponseHelper.BadRequest(validation.ToErrorResponse(), () => ContentTypeHeader),
                    error => ResponseHelper.InternalServerError(error.ToErrorResponse(), () => ContentTypeHeader)
                );
            }

            var nuGetPackageInfo = result.NuGetPackageInfo;
            var color = nuGetPackageInfo.IsPrerelease ? "orange" : "blue";
            var badge = new ShieldsBadgeResponse(1, "nuget", nuGetPackageInfo.VersionString, color, NamedLogo: "nuget");

            _logger.LogInformation("Created badge for {PackageId} version {Version}", packageId, nuGetPackageInfo.VersionString);

            routeContext.Request.Headers.TryGetValue("if-none-match", out var ifNoneMatch);
            var cache = new ResponseHelper.CacheSettings(SMaxAgeSeconds: 300, MaxAgeSeconds: 60, SwrSeconds: 900, SieSeconds: 3600);

            return ResponseHelper.OkCached(
                badge,
                LambdaFunctionJsonSerializerContext.Default.ShieldsBadgeResponse,
                ifNoneMatchHeader: ifNoneMatch,
                cache: cache,
                lastModifiedUtc: null
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

    private static APIGatewayHttpApiV2ProxyResponse CreateInvalidPackageIdentifierResponse()
    {
        var errorResponse = new ErrorResponse("Package identifier is required", [new ErrorDetail("PACKAGE_ID_REQUIRED", "packageId")]);
        return ResponseHelper.BadRequest(errorResponse);
    }
}
