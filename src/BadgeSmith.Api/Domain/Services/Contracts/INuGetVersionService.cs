using BadgeSmith.Api.Domain.Services.Package;

namespace BadgeSmith.Api.Domain.Services.Contracts;

internal interface INuGetVersionService
{
    public NuGetVersionResult ParseAndFilterVersions(ReadOnlySpan<string> versionStrings, string? versionRange, bool includePrerelease);
}
