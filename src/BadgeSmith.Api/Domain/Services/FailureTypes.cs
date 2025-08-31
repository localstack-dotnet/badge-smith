using BadgeSmith.Api.Domain.Models;

namespace BadgeSmith.Api.Domain.Services;

internal abstract record Failure(string Reason);

internal record ValidationFailure(string Reason, string Code, string PropertyName) : Failure(Reason)
{
    public ErrorResponse ToErrorResponse() => new(Reason, [new ErrorDetail(Code, PropertyName)]);
}

internal record NotFoundFailure(string Reason) : Failure(Reason)
{
    public ErrorResponse ToErrorResponse() => new(Reason);
}

internal record Error(string Reason) : Failure(Reason)
{
    public ErrorResponse ToErrorResponse() => new(Reason);
}
