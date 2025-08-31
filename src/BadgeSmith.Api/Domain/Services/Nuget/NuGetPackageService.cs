using System.Net;
using System.Text.Json;
using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Json;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Domain.Services.Nuget;

/// <summary>
/// Service for retrieving NuGet package information using the NuGet v3 flat container API
/// </summary>
internal class NuGetPackageService : INuGetPackageService
{
    private readonly INuGetVersionService _nuGetVersionService;
    private readonly ILogger<NuGetPackageService> _logger;
    private readonly HttpClient _nugetClient;

    public NuGetPackageService(INuGetVersionService nuGetVersionService, ILogger<NuGetPackageService> logger, HttpClient nugetClient)
    {
        _nuGetVersionService = nuGetVersionService;
        _logger = logger;
        _nugetClient = nugetClient;
    }

    public async Task<NuGetResult> GetLatestVersionAsync(
        string packageId,
        string? versionRange = null,
        bool includePrerelease = false,
        CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NuGetPackageService)}.{nameof(GetLatestVersionAsync)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        _logger.LogInformation("Fetching NuGet package versions for {PackageId}", packageId);
        var normalizedPackageId = packageId.ToLowerInvariant();
        var url = new Uri($"v3-flatcontainer/{normalizedPackageId}/index.json", UriKind.Relative);

        using var response = await _nugetClient.GetAsync(url, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new PackageNotFound($"Package '{packageId}' not found");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new Error($"NuGet API error: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var indexResponse = JsonSerializer.Deserialize(content, LambdaFunctionJsonSerializerContext.Default.NuGetIndexResponse);

        if (indexResponse?.Versions == null || indexResponse.Versions.Length == 0)
        {
            return new PackageNotFound($"No versions found for package '{packageId}'");
        }

        var nuGetVersionResult = _nuGetVersionService.ParseAndFilterVersions(indexResponse.Versions, versionRange, includePrerelease);

        return nuGetVersionResult
            .Match<NuGetResult>
            (
                version => new NuGetPackageInfo(packageId, version.ToString(), version.IsPrerelease),
                range => range,
                notfound => new PackageNotFound(notfound.Reason)
            );
    }
}
