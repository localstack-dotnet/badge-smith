using OneOf;

namespace BadgeSmith.Api.Domain.Services.Github;

internal record SecretNotFound(string Reason) : NotFoundFailure(Reason);

[GenerateOneOf]
internal partial class GithubSecretResult : OneOfBase<string, SecretNotFound, Error>
{
    public bool IsSuccess => IsT0 && AsT0 != null;

    public bool IsFailure => !IsSuccess;

    public string? GithubSecret => AsT0;

    public OneOf<SecretNotFound, Error> Failure
    {
        get
        {
            if (TryPickT1(out var notFound, out _))
            {
                return notFound;
            }

            if (TryPickT2(out var error, out _))
            {
                return error;
            }

            throw new InvalidOperationException("Failure was not found");
        }
    }
}
