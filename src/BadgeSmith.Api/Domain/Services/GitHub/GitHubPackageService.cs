using System.Net.Http.Headers;
using System.Text.Json;
using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Infrastructure.Caching;
using BadgeSmith.Api.Json;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Domain.Services.GitHub;

internal sealed class GitHubPackageService : IGitHubPackageService
{
    private readonly HttpClient _httpClient;
    private readonly INuGetVersionService _nuGetVersionService;
    private readonly IAppCache _cache;
    private readonly ILogger<GitHubPackageService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public GitHubPackageService(
        HttpClient httpClient,
        INuGetVersionService nuGetVersionService,
        IAppCache cache,
        ILogger<GitHubPackageService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _nuGetVersionService = nuGetVersionService ?? throw new ArgumentNullException(nameof(nuGetVersionService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GitHubPackageResult> GetLatestVersionAsync(
        string organization,
        string packageName,
        string token,
        string? versionRange = null,
        bool includePrerelease = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrEmpty(token);

        var orgLower = organization.ToLowerInvariant();
        var packageLower = packageName.ToLowerInvariant();

        var cacheKey = $"github_package:{orgLower}:{packageLower}:{versionRange ?? "latest"}:{includePrerelease}";

        // Try cache first
        if (_cache.TryGetValue<GitHubPackageInfo>(cacheKey, out var cachedPackage))
        {
            _logger.LogDebug("Retrieved cached GitHub package info for {Org}/{Package}", orgLower, packageLower);
            return cachedPackage;
        }

        try
        {
            // Fetch package versions from GitHub Packages API
            var packageVersions = await FetchPackageVersionsAsync(orgLower, packageLower, token, ct).ConfigureAwait(false);

            if (packageVersions == null || packageVersions.Count == 0)
            {
                _logger.LogWarning("No versions found for GitHub package {Org}/{Package}", orgLower, packageLower);
                return new PackageNotFound($"Package '{packageLower}' not found in organization '{orgLower}'");
            }

            // Filter and select the appropriate version
            var selectedVersion = SelectVersion(packageVersions, versionRange, includePrerelease);
            if (selectedVersion == null)
            {
                var criteria = string.IsNullOrWhiteSpace(versionRange) ? "latest" : versionRange;
                return new PackageNotFound($"No matching version found for package '{packageLower}' with criteria '{criteria}' (prerelease: {includePrerelease})");
            }

            var packageInfo = new GitHubPackageInfo(
                PackageName: packageLower,
                Organization: orgLower,
                VersionString: selectedVersion.Name,
                IsPrerelease: selectedVersion.Prerelease,
                LastModifiedUtc: selectedVersion.UpdatedAt
            );

            // Cache the result
            _cache.Set(cacheKey, packageInfo, CacheTtl);
            _logger.LogDebug("Cached GitHub package info for {Org}/{Package} version {Version}", orgLower, packageLower, selectedVersion.Name);

            return packageInfo;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching GitHub package {Org}/{Package}", orgLower, packageLower);
            return new Error($"Failed to fetch package information: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout while fetching GitHub package {Org}/{Package}", orgLower, packageLower);
            return new Error("Request timeout while fetching package information");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching GitHub package {Org}/{Package}", orgLower, packageLower);
            return new Error($"An unexpected error occurred: {ex.Message}");
        }
    }

    private async Task<IReadOnlyList<GithubPackageVersion>?> FetchPackageVersionsAsync(string org, string packageName, string token, CancellationToken ct)
    {
        // GitHub Packages API endpoint for package versions
        var url = new Uri($"orgs/{org}/packages/nuget/{packageName}/versions", UriKind.Relative);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("BadgeSmith", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        _logger.LogDebug("Fetching GitHub package versions from {Url}", url);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("GitHub package {Org}/{Package} not found (404)", org, packageName);
            return null;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Access forbidden for GitHub package {Org}/{Package} (403)", org, packageName);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var versions = JsonSerializer.Deserialize(content, LambdaFunctionJsonSerializerContext.Default.IReadOnlyListGithubPackageVersion);

        _logger.LogDebug("Retrieved {Count} versions for GitHub package {Org}/{Package}", versions?.Count ?? 0, org, packageName);

        return versions;
    }
}
