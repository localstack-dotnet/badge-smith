using OneOf;

namespace BadgeSmith.Api.Domain.Services.Authentication;

internal sealed record InvalidSignature(string Reason) : ValidationFailure(Reason, "INVALID_SIGNATURE", "signature");

internal sealed record MissingAuthHeaders(string Reason) : ValidationFailure(Reason, "MISSING_AUTH_HEADERS", "headers");

internal sealed record InvalidTimestamp(string Reason) : ValidationFailure(Reason, "INVALID_TIMESTAMP", "timestamp");

internal sealed record NonceAlreadyUsed(string Reason) : ValidationFailure(Reason, "NONCE_ALREADY_USED", "nonce");

internal sealed record RepoSecretNotFound(string Reason) : NotFoundFailure(Reason);

internal sealed record AuthenticatedRequest(string RepoIdentifier, DateTimeOffset RequestTimestamp);

[GenerateOneOf]
internal partial class HmacAuthenticationResult
    : OneOfBase<AuthenticatedRequest, InvalidSignature, MissingAuthHeaders, InvalidTimestamp, NonceAlreadyUsed, RepoSecretNotFound, Error>
{
    public bool IsSuccess => IsT0;
    public AuthenticatedRequest? AuthenticatedRequest => IsT0 ? AsT0 : null;

    public OneOf<InvalidSignature, MissingAuthHeaders, InvalidTimestamp, NonceAlreadyUsed, RepoSecretNotFound, Error> Failure => IsT0
        ? throw new InvalidOperationException("Result is successful")
        : Match<OneOf<InvalidSignature, MissingAuthHeaders, InvalidTimestamp, NonceAlreadyUsed, RepoSecretNotFound, Error>>(
            _ => throw new InvalidOperationException("Result is successful"),
            invalidSig => invalidSig,
            missingHeaders => missingHeaders,
            invalidTimestamp => invalidTimestamp,
            nonceUsed => nonceUsed,
            secretNotFound => secretNotFound,
            error => error
        );
}

[GenerateOneOf]
internal partial class RepoSecretResult : OneOfBase<string, RepoSecretNotFound, Error>
{
    public bool IsSuccess => IsT0;
    public string? Secret => IsT0 ? AsT0 : null;

    public OneOf<RepoSecretNotFound, Error> Failure => IsT0
        ? throw new InvalidOperationException("Result is successful")
        : Match<OneOf<RepoSecretNotFound, Error>>(
            _ => throw new InvalidOperationException("Result is successful"),
            notFound => notFound,
            error => error
        );
}

[GenerateOneOf]
internal partial class NonceValidationResult : OneOfBase<ValidNonce, NonceAlreadyUsed, Error>
{
    public bool IsSuccess => IsT0;
    public ValidNonce? ValidNonce => IsT0 ? AsT0 : null;

    public OneOf<NonceAlreadyUsed, Error> Failure => IsT0
        ? throw new InvalidOperationException("Result is successful")
        : Match<OneOf<NonceAlreadyUsed, Error>>(
            _ => throw new InvalidOperationException("Result is successful"),
            alreadyUsed => alreadyUsed,
            error => error
        );
}

internal sealed record ValidNonce(string Nonce, DateTimeOffset MarkedAt);
