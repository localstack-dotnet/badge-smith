using System.Diagnostics;
using System.Net;
using System.Text.Json;
using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Domain.Services.Results;
using BadgeSmith.Api.Json;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using ZLinq;

namespace BadgeSmith.Api.Domain.Services.Nuget;

/// <summary>
/// Service for retrieving NuGet package information using the NuGet v3 flat container API
/// </summary>
internal class NuGetPackageService : INuGetPackageService
{
    private readonly ILogger<NuGetPackageService> _logger;
    private readonly HttpClient _nugetClient;

    public NuGetPackageService(ILogger<NuGetPackageService> logger, HttpClient nugetClient)
    {
        _logger = logger;
        _nugetClient = nugetClient;
    }

    public async Task<NugetResult> GetLatestVersionAsync(
        string packageId,
        string? versionRange = null,
        bool includePrerelease = false,
        CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NuGetPackageService)}.{nameof(GetLatestVersionAsync)}");

        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        var normalizedPackageId = packageId.ToLowerInvariant();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Fetching NuGet package versions for {PackageId}", packageId);

            var url = new Uri($"v3-flatcontainer/{normalizedPackageId}/index.json", UriKind.Relative);
            using var response = await _nugetClient.GetAsync(url, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("NuGet package not found: {PackageId}", packageId);
                return new NotFoundFailure($"Package '{packageId}' not found");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NuGet API returned {StatusCode} for package {PackageId}", response.StatusCode, packageId);
                return new Error($"NuGet API error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var indexResponse = JsonSerializer.Deserialize<NuGetIndexResponse>(content, LambdaFunctionJsonSerializerContext.Default.NuGetIndexResponse);

            if (indexResponse?.Versions == null || indexResponse.Versions.Length == 0)
            {
                _logger.LogInformation("No versions found for NuGet package: {PackageId}", packageId);
                return new NotFoundFailure($"No versions found for package '{packageId}'");
            }

            var stopwatchElapsedMilliseconds1 = stopwatch.ElapsedMilliseconds;
            var latestVersion = ParseAndFilterVersions(indexResponse.Versions, versionRange, includePrerelease);
            var stopwatchElapsedMilliseconds2 = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation("Parsed and filtered versions in {ElapsedMilliseconds}ms", stopwatchElapsedMilliseconds2 - stopwatchElapsedMilliseconds1);

            if (latestVersion == null)
            {
                var criteria = BuildCriteriaDescription(versionRange, includePrerelease);
                _logger.LogInformation("No versions matching criteria for {PackageId}: {Criteria}", packageId, criteria);
                return new NotFoundFailure($"No versions found for package '{packageId}' matching criteria: {criteria}");
            }

            var packageInfo = NuGetPackageInfo.Create(packageId, latestVersion.ToString());

            _logger.LogInformation("Found NuGet package {PackageId} version {Version}", packageId, latestVersion);
            return packageInfo;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error retrieving NuGet package {PackageId}", packageId);
            return new Error($"Network error retrieving NuGet package {packageId}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse NuGet API response for package {PackageId}", packageId);
            return new Error("Invalid response from NuGet API");
        }
        catch (ArgumentException ex) when (ex.Message.Contains("version", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Invalid version range specified for package {PackageId}: {VersionRange}", packageId, versionRange);
            return new ValidationFailure($"Invalid version range: {versionRange}", "VERSION_RANGE_INVALID", nameof(versionRange));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving NuGet package {PackageId}", packageId);
            return new Error($"Unexpected error retrieving NuGet package {packageId}");
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation("Retrieved NuGet package {PackageId} in {ElapsedMilliseconds}ms", packageId, stopwatch.ElapsedMilliseconds);
        }
    }

    private static NuGetVersion? ParseAndFilterVersions(string[] versionStrings, string? versionRange, bool includePrerelease)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NuGetPackageService)}.{nameof(ParseAndFilterVersions)}");
        switch (versionRange)
        {
            case null when includePrerelease && NuGetVersion.TryParse(versionStrings[^1], out var lastVersion):
                return lastVersion;
            case null when !includePrerelease:
                for (var i = versionStrings.Length - 1; i > 0; i--)
                {
                    var versionString = versionStrings[i];

                    if (NuGetVersion.TryParse(versionString, out var version) && !version.IsPrerelease)
                    {
                        return version;
                    }
                }

                break;
        }

        var versions = new List<NuGetVersion>();

        foreach (var versionString in versionStrings)
        {
            if (NuGetVersion.TryParse(versionString, out var version))
            {
                versions.Add(version);
            }
        }

        if (!includePrerelease)
        {
            versions = [.. versions.AsValueEnumerable().Where(v => !v.IsPrerelease)];
        }

        if (string.IsNullOrWhiteSpace(versionRange))
        {
            return versions.Count == 0 ? null : versions.AsValueEnumerable().Order().Last();
        }

        if (VersionRange.TryParse(versionRange, out var range))
        {
            versions = [.. versions.AsValueEnumerable().Where(range.Satisfies)];
        }
        else
        {
            throw new ArgumentException($"Invalid version range format: {versionRange}", nameof(versionRange));
        }

        return versions.Count == 0 ? null : versions.AsValueEnumerable().Order().Last();
    }

    private static string BuildCriteriaDescription(string? versionRange, bool includePrerelease)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(versionRange))
        {
            parts.Add($"version range '{versionRange}'");
        }

        parts.Add(includePrerelease ? "including prerelease" : "stable versions only");

        return string.Join(", ", parts);
    }
}
