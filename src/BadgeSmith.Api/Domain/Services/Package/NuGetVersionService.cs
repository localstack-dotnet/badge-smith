using BadgeSmith.Api.Domain.Services.Contracts;
using NuGet.Versioning;

namespace BadgeSmith.Api.Domain.Services.Package;

internal class NuGetVersionService : INuGetVersionService
{
    public NuGetVersionResult ParseAndFilterVersions(ReadOnlySpan<string> versionStrings, string? versionRange, bool includePrerelease)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(NuGetVersionService)}.{nameof(ParseAndFilterVersions)}");

        // if (string.IsNullOrWhiteSpace(versionRange) && versionStrings.Length > 0)
        // {
        //     for (var i = versionStrings.Length - 1; i >= 0; i--)
        //     {
        //         if (NuGetVersion.TryParse(versionStrings[i], out var version) && (includePrerelease || !version.IsPrerelease))
        //         {
        //             return version;
        //         }
        //     }
        //
        //     return new LastVersionNotFound("The latest version of the package could not be found");
        // }
        //
        VersionRange? range = null;
        if (!string.IsNullOrWhiteSpace(versionRange) && !VersionRange.TryParse(versionRange, out range))
        {
            var message = BuildCriteriaDescription(versionRange, includePrerelease);
            return new InvalidVersionRange($"The version range '{versionRange}' is invalid: {message}");
        }

        NuGetVersion? maxVersion = null;

        foreach (var versionString in versionStrings)
        {
            if (!NuGetVersion.TryParse(versionString, out var version))
            {
                continue;
            }

            if (!includePrerelease && version.IsPrerelease)
            {
                continue;
            }

            if (range?.Satisfies(version) == false)
            {
                continue;
            }

            if (maxVersion == null || version > maxVersion)
            {
                maxVersion = version;
            }
        }

        return maxVersion == null ? new LastVersionNotFound("The latest version of the package could not be found") : new NuGetVersionResult(maxVersion);
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
