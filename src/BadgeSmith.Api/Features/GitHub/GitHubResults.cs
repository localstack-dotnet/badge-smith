using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Versioning;
using OneOf;

namespace BadgeSmith.Api.Features.GitHub;

internal sealed record PackageNotFound(string Reason) : NotFoundFailure(Reason);

internal sealed record UnauthorizedPackageAccess(string Reason) : Error(Reason);

internal sealed record ForbiddenPackageAccess(string Reason) : Error(Reason);

internal sealed record GitHubPackageInfo(string PackageName, string Organization, string VersionString, bool IsPrerelease, DateTimeOffset? LastModifiedUtc = null);

[GenerateOneOf]
internal sealed partial class GitHubPackageResult : OneOfBase<GitHubPackageInfo, PackageNotFound, InvalidVersionRange, UnauthorizedPackageAccess, ForbiddenPackageAccess, Error>
{
    public bool IsSuccess => IsT0 && AsT0 != null;
    public GitHubPackageInfo? GitHubPackageInfo => IsT0 ? AsT0 : null;

    public OneOf<PackageNotFound, InvalidVersionRange, UnauthorizedPackageAccess, ForbiddenPackageAccess, Error> Failure => IsT0
        ? throw new InvalidOperationException("Result is successful")
        : Match<OneOf<PackageNotFound, InvalidVersionRange, UnauthorizedPackageAccess, ForbiddenPackageAccess, Error>>(
            _ => throw new InvalidOperationException("Result is successful"),
            notFound => notFound,
            range => range,
            unAuth => unAuth,
            forbidden => forbidden,
            error => error
        );
}
