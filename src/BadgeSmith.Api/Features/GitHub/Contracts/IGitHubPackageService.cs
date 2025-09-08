namespace BadgeSmith.Api.Features.GitHub.Contracts;

/// <summary>
/// Service for retrieving GitHub package information and versions.
/// </summary>
internal interface IGitHubPackageService
{
    public Task<GitHubPackageResult> GetLatestVersionAsync(
        string organization,
        string packageId,
        string token,
        string? versionRange = null,
        bool includePrerelease = false,
        CancellationToken ct = default);
}
