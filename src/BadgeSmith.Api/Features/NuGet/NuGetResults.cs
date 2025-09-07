using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Versioning;
using OneOf;

namespace BadgeSmith.Api.Features.NuGet;

internal record NuGetPackageInfo(string PackageId, string VersionString, bool IsPrerelease, DateTimeOffset? LastModifiedUtc = null);

internal record PackageNotFound(string Reason) : NotFoundFailure(Reason);

[GenerateOneOf]
internal partial class NuGetResults : OneOfBase<NuGetPackageInfo, PackageNotFound, InvalidVersionRange, Error>
{
    public bool IsSuccess => IsT0 && AsT0 != null;

    public bool IsFailure => !IsSuccess;

    public NuGetPackageInfo? NuGetPackageInfo => IsT0 ? AsT0 : null;

    public bool TryGetFailure(out OneOf<NotFoundFailure, ValidationFailure, Error>? failure)
    {
        if (TryPickT1(out var notFound, out _))
        {
            failure = notFound;
            return true;
        }

        if (TryPickT2(out var validation, out _))
        {
            failure = validation;
            return true;
        }

        if (TryPickT3(out var error, out _))
        {
            failure = error;
            return true;
        }

        failure = null;
        return false;
    }

    public OneOf<NotFoundFailure, ValidationFailure, Error> Failure
    {
        get
        {
            if (TryPickT1(out var notFound, out _))
            {
                return notFound;
            }

            if (TryPickT2(out var validation, out _))
            {
                return validation;
            }

            if (TryPickT3(out var error, out _))
            {
                return error;
            }

            throw new InvalidOperationException("Failure was not found");
        }
    }
}
