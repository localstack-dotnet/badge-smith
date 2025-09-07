using NuGet.Versioning;
using OneOf;

namespace BadgeSmith.Api.Core.Versioning;

internal record InvalidVersionRange(string Reason) : ValidationFailure(Reason, "PACKAGE_RANGE_INVALID", "versionRange");

internal record LastVersionNotFound(string Reason) : NotFoundFailure(Reason);

[GenerateOneOf]
internal partial class NuGetVersionResult : OneOfBase<NuGetVersion, InvalidVersionRange, LastVersionNotFound>
{
    public bool IsSuccess => IsT0 && AsT0 != null;

    public bool IsFailure => !IsSuccess;

    public NuGetVersion? NuGetVersion => IsT0 ? AsT0 : null;

    public OneOf<InvalidVersionRange, LastVersionNotFound> Failure
    {
        get
        {
            if (TryPickT1(out var invalidVersionRange, out _))
            {
                return invalidVersionRange;
            }

            if (TryPickT2(out var versionNotFound, out _))
            {
                return versionNotFound;
            }

            throw new InvalidOperationException("Failure was not found");
        }
    }
}
