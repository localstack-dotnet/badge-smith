namespace BadgeSmith.Api.Features.GitHub.Contracts;

/// <summary>
/// Service for retrieving GitHub package information and versions.
/// </summary>
internal interface IGitHubPackageService
{
    /// <summary>
    /// Retrieves the latest version information for a GitHub package.
    /// </summary>
    /// <param name="organization">The GitHub organization name.</param>
    /// <param name="packageId">The package name.</param>
    /// <param name="token">The GitHub Personal Access Token.</param>
    /// <param name="versionRange">Optional version range constraint.</param>
    /// <param name="includePrerelease">Whether to include prerelease versions.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>
    /// A result containing package information if successful, or failure details.
    /// </returns>
    public Task<GitHubPackageResult> GetLatestVersionAsync(
        string organization,
        string packageId,
        string token,
        string? versionRange = null,
        bool includePrerelease = false,
        CancellationToken ct = default);
}
