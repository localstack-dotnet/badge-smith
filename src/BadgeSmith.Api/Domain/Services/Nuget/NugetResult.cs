using BadgeSmith.Api.Domain.Services.Results;
using OneOf;
using Error = BadgeSmith.Api.Domain.Services.Results.Error;

namespace BadgeSmith.Api.Domain.Services.Nuget;

[GenerateOneOf]
internal partial class NugetResult : OneOfBase<NuGetPackageInfo, NotFoundFailure, ValidationFailure, Error>
{
    public bool IsSuccess => IsT0 && AsT0 != null;

    public bool IsFailure => !IsSuccess;

    public NuGetPackageInfo? NuGetPackageInfo => AsT0;

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
