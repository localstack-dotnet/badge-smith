namespace BadgeSmith.Api.Core.Versioning.Contracts;

internal interface INuGetVersionService
{
    public NuGetVersionResult ParseAndFilterVersions(ReadOnlySpan<string> versionStrings, string? versionRange, bool includePrerelease);
}
