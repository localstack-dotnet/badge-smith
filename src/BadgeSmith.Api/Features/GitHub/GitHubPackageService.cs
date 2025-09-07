using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Caching;
using BadgeSmith.Api.Core.Versioning.Contracts;
using BadgeSmith.Api.Features.GitHub.Contracts;
using Microsoft.Extensions.Logging;
using ZLinq;
using LambdaFunctionJsonSerializerContext = BadgeSmith.Api.Core.Infrastructure.LambdaFunctionJsonSerializerContext;

namespace BadgeSmith.Api.Features.GitHub;

internal sealed class GitHubPackageService : IGitHubPackageService
{
    private readonly HttpClient _gitHubClient;
    private readonly INuGetVersionService _nuGetVersionService;
    private readonly IAppCache _cache;
    private readonly ILogger<GitHubPackageService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public GitHubPackageService(
        HttpClient gitHubClient,
        INuGetVersionService nuGetVersionService,
        IAppCache cache,
        ILogger<GitHubPackageService> logger)
    {
        _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
        _nuGetVersionService = nuGetVersionService ?? throw new ArgumentNullException(nameof(nuGetVersionService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GitHubPackageResult> GetLatestVersionAsync(
        string organization,
        string packageId,
        string token,
        string? versionRange = null,
        bool includePrerelease = false,
        CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(GitHubPackageService)}.{nameof(GetLatestVersionAsync)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrEmpty(token);

        _logger.LogInformation("Fetching GitHub package versions for {PackageId}", packageId);
        var orgNormalized = organization.ToLowerInvariant();
        var packageIdNormalized = packageId.ToLowerInvariant();
        var url = new Uri($"orgs/{HttpUtility.UrlEncode(orgNormalized)}/packages/nuget/{HttpUtility.UrlEncode(packageIdNormalized)}/versions", UriKind.Relative);
        var cacheKey = $"github_package:index:{orgNormalized}:{packageIdNormalized}";
        var hasCache = _cache.TryGetValue<(string Payload, string? ETag, DateTimeOffset? LastModified)>(cacheKey, out var cached);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (hasCache)
        {
            if (!string.IsNullOrWhiteSpace(cached.ETag))
            {
                request.Headers.IfNoneMatch.ParseAdd(cached.ETag);
            }

            if (cached.LastModified.HasValue)
            {
                request.Headers.IfModifiedSince = cached.LastModified.Value;
            }
        }

        using var response = await _gitHubClient.SendAsync(request, ct).ConfigureAwait(false);
        string content;
        string? etag;
        DateTimeOffset? lastMod;

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotModified when !hasCache:
                return new Error("Received 304 Not Modified without a cached entry");
            case HttpStatusCode.NotModified when hasCache:
                content = cached.Payload;
                etag = response.Headers.ETag?.Tag ?? cached.ETag;
                lastMod = response.Content.Headers.LastModified ?? response.Headers.Date ?? cached.LastModified;
                break;
            case HttpStatusCode.NotFound:
                return new PackageNotFound($"Package '{packageId}' not found");
            case HttpStatusCode.Forbidden:
                return new ForbiddenPackageAccess($"GitHub package {orgNormalized}/{packageIdNormalized} access forbidden");
            case HttpStatusCode.Unauthorized:
                return new UnauthorizedPackageAccess($"GitHub package {orgNormalized}/{packageIdNormalized} not authorized");
            default:
                if (!response.IsSuccessStatusCode)
                {
                    return new Error($"NuGet API error: {response.StatusCode}");
                }

                content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                etag = response.Headers.ETag?.Tag;
                lastMod = response.Content.Headers.LastModified ?? response.Headers.Date;
                break;
        }

        _cache.Set(cacheKey, (content, etag, lastMod), CacheTtl);
        var githubPackageVersions = JsonSerializer.Deserialize(content, LambdaFunctionJsonSerializerContext.Default.IReadOnlyListGithubPackageVersion);

        if (githubPackageVersions == null || githubPackageVersions.Count == 0)
        {
            return new PackageNotFound($"No versions found for package '{packageId}'");
        }

        var versions = githubPackageVersions.AsValueEnumerable().Select(version => version.Name).ToArray();
        var nuGetVersionResult = _nuGetVersionService.ParseAndFilterVersions(versions, versionRange, includePrerelease);

        return nuGetVersionResult
            .Match<GitHubPackageResult>
            (
                version => new GitHubPackageInfo(packageIdNormalized, orgNormalized, version.ToString(), version.IsPrerelease, lastMod),
                range => range,
                notfound => new PackageNotFound(notfound.Reason)
            );
    }
}
