namespace BadgeSmith.Api.Features.NuGet.Contracts;

/// <summary>
/// Service for retrieving NuGet package information
/// </summary>
internal interface INuGetPackageService
{
    /// <summary>
    /// Gets the latest version of a NuGet package that satisfies the given version range
    /// </summary>
    /// <param name="packageId">The NuGet package identifier (case-insensitive)</param>
    /// <param name="versionRange">Optional version range constraint (e.g., "[1.0,2.0)", "1.*", etc.)</param>
    /// <param name="includePrerelease">Whether to include prerelease versions</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Package information if found, otherwise an error result</returns>
    public Task<NuGetResults> GetLatestVersionAsync(
        string packageId,
        string? versionRange = null,
        bool includePrerelease = false,
        CancellationToken ct = default);
}
