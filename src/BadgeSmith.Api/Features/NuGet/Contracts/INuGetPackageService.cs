namespace BadgeSmith.Api.Features.NuGet.Contracts;

/// <summary>
/// Service for retrieving NuGet package information using the NuGet v3 flat container API
/// </summary>
internal interface INuGetPackageService
{
    public Task<NuGetResults> GetLatestVersionAsync(string packageId, string? versionRange = null, bool includePrerelease = false, CancellationToken ct = default);
}
