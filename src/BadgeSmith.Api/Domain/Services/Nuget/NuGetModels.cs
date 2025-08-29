using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace BadgeSmith.Api.Domain.Services.Nuget;

/// <summary>
/// Represents a NuGet package with version information
/// </summary>
internal record NuGetPackageInfo
{
    public required string PackageId { get; init; }
    public required NuGetVersion Version { get; init; }
    public required string VersionString { get; init; }
    public bool IsPrerelease => Version.IsPrerelease;

    public bool IsListed { get; init; } = true;

    /// <summary>
    /// Creates a package info from a package ID and version string
    /// </summary>
    /// <param name="packageId">The NuGet package identifier</param>
    /// <param name="versionString">The version string to parse</param>
    /// <returns>A new NuGetPackageInfo instance</returns>
    public static NuGetPackageInfo Create(string packageId, string versionString)
    {
        var version = NuGetVersion.Parse(versionString);
        return new NuGetPackageInfo
        {
            PackageId = packageId,
            Version = version,
            VersionString = versionString,
        };
    }
}

internal record NuGetIndexResponse([property: JsonPropertyName("versions")] string[] Versions);
