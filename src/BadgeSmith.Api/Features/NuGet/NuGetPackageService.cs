#pragma warning disable CA1873 // Replace with LoggerMessage source-generated logging.

using System.Net;
using System.Text.Json;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Caching;
using BadgeSmith.Api.Core.Http;
using BadgeSmith.Api.Core.Versioning.Contracts;
using BadgeSmith.Api.Features.NuGet.Contracts;
using Microsoft.Extensions.Logging;
using LambdaFunctionJsonSerializerContext = BadgeSmith.Api.Core.Infrastructure.LambdaFunctionJsonSerializerContext;

namespace BadgeSmith.Api.Features.NuGet;

/// <summary>
/// Service for retrieving NuGet package information using the NuGet v3 flat container API
/// </summary>
internal sealed class NuGetPackageService : INuGetPackageService
{
    private readonly INuGetVersionService _nuGetVersionService;
    private readonly ILogger<NuGetPackageService> _logger;
    private readonly HttpClient _nugetClient;
    private readonly IAppCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public NuGetPackageService(INuGetVersionService nuGetVersionService, ILogger<NuGetPackageService> logger, HttpClient nugetClient, IAppCache cache)
    {
        _nuGetVersionService = nuGetVersionService ?? throw new ArgumentNullException(nameof(nuGetVersionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _nugetClient = nugetClient ?? throw new ArgumentNullException(nameof(nugetClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<NuGetResults> GetLatestVersionAsync(
        string packageId,
        string? versionRange = null,
        bool includePrerelease = false,
        CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NuGetPackageService)}.{nameof(GetLatestVersionAsync)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        _logger.LogInformation("Fetching NuGet package versions for {PackageId}", packageId);
        var packageIdNormalized = packageId.ToLowerInvariant();
        var url = new Uri($"v3-flatcontainer/{Uri.EscapeDataString(packageIdNormalized)}/index.json", UriKind.Relative);
        var cacheKey = $"nuget:index:{packageIdNormalized}";
        var hasCache = _cache.TryGetValue<UpstreamCacheEntry>(cacheKey, out var cached);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (hasCache)
        {
            if (!string.IsNullOrWhiteSpace(cached!.ETag))
            {
                request.Headers.IfNoneMatch.ParseAdd(cached.ETag);
            }

            if (cached!.LastModified.HasValue)
            {
                request.Headers.IfModifiedSince = cached.LastModified.Value;
            }
        }

        using var response = await _nugetClient.SendAsync(request, ct).ConfigureAwait(false);

        // Prepare unified variables for downstream processing/caching
        string content;
        string? etag;
        DateTimeOffset? lastMod;

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotModified when !hasCache:
                return new Error("Received 304 Not Modified without a cached entry");
            case HttpStatusCode.NotModified when hasCache:
                content = cached!.Payload;
                etag = response.Headers.ETag?.ToString() ?? cached!.ETag;
                lastMod = response.Content.Headers.LastModified ?? response.Headers.Date ?? cached!.LastModified;
                break;
            case HttpStatusCode.NotFound:
                return new PackageNotFound($"Package '{packageId}' not found");
            default:
                if (!response.IsSuccessStatusCode)
                {
                    return new Error($"NuGet API error: {response.StatusCode}");
                }

                content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                etag = response.Headers.ETag?.ToString();
                lastMod = response.Content.Headers.LastModified ?? response.Headers.Date;
                break;
        }

        _cache.Set(cacheKey, new UpstreamCacheEntry(content, etag, lastMod), CacheTtl);

        var indexResponse = JsonSerializer.Deserialize(content, LambdaFunctionJsonSerializerContext.Default.NuGetIndexResponse);

        if (indexResponse?.Versions == null || indexResponse.Versions.Length == 0)
        {
            return new PackageNotFound($"No versions found for package '{packageId}'");
        }

        var nuGetVersionResult = _nuGetVersionService.ParseAndFilterVersions(indexResponse.Versions, versionRange, includePrerelease);

        return nuGetVersionResult
            .Match<NuGetResults>
            (
                version => new NuGetPackageInfo(packageId, version.ToString(), version.IsPrerelease, lastMod),
                range => range,
                notfound => new PackageNotFound(notfound.Reason)
            );
    }
}

#pragma warning restore CA1873
