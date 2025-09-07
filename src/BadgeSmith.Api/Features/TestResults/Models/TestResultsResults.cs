using BadgeSmith.Api.Core;
using OneOf;

namespace BadgeSmith.Api.Features.TestResults.Models;

internal sealed record TestResultNotFound(string Reason) : NotFoundFailure(Reason);

internal sealed record InvalidTestPayload(string Reason) : ValidationFailure(Reason, "INVALID_TEST_PAYLOAD", "payload");

internal sealed record DuplicateTestResult(string Reason) : ValidationFailure(Reason, "DUPLICATE_TEST_RESULT", "run_id");

internal sealed record TestResultStored(string TestResultId, DateTimeOffset StoredAt);

[GenerateOneOf]
internal partial class TestResultStorageResult : OneOfBase<TestResultStored, InvalidTestPayload, DuplicateTestResult, Error>
{
    public bool IsSuccess => IsT0;
    public TestResultStored? TestResultStored => IsT0 ? AsT0 : null;

    public OneOf<InvalidTestPayload, DuplicateTestResult, Error> Failure => IsT0
        ? throw new InvalidOperationException("Result is successful")
        : Match<OneOf<InvalidTestPayload, DuplicateTestResult, Error>>(
            _ => throw new InvalidOperationException("Result is successful"),
            invalidPayload => invalidPayload,
            duplicate => duplicate,
            error => error
        );
}

[GenerateOneOf]
internal partial class TestResultQueryResult : OneOfBase<TestResultEntity, TestResultNotFound, Error>
{
    public bool IsSuccess => IsT0;
    public TestResultEntity? TestResultEntity => IsT0 ? AsT0 : null;

    public OneOf<TestResultNotFound, Error> Failure => IsT0
        ? throw new InvalidOperationException("Result is successful")
        : Match<OneOf<TestResultNotFound, Error>>(
            _ => throw new InvalidOperationException("Result is successful"),
            notFound => notFound,
            error => error
        );
}

internal sealed record StoreTestResultRequest(string Owner, string Repo, string Platform, string Branch, TestResultPayload? Payload);
