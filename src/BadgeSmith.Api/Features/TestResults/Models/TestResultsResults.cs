using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Routing;
using OneOf;

namespace BadgeSmith.Api.Features.TestResults.Models;

internal sealed record TestResultNotFound(string Reason) : NotFoundFailure(Reason);

internal sealed record InvalidTestPayload(string Reason) : ValidationFailure(Reason, "INVALID_TEST_PAYLOAD", "payload");

internal sealed record DuplicateTestResult(string Reason) : ValidationFailure(Reason, "DUPLICATE_TEST_RESULT", "run_id");

internal sealed record MissingRouteParameter(string Reason, string ParameterName)
    : ValidationFailure(Reason, "MISSING_ROUTE_PARAMETER", ParameterName);

internal readonly record struct TestResultRouteParameters(
    string Platform,
    string Owner,
    string Repo,
    string Branch)
{
    public static TestResultRouteParametersResult Extract(RouteContext routeContext)
    {
        if (!TryReadRequired(routeContext, "platform", out var platform))
        {
            return new TestResultRouteParametersResult(new MissingRouteParameter("Platform parameter is required", "platform"));
        }

        if (!TryReadRequired(routeContext, "owner", out var owner))
        {
            return new TestResultRouteParametersResult(new MissingRouteParameter("Owner parameter is required", "owner"));
        }

        if (!TryReadRequired(routeContext, "repo", out var repo))
        {
            return new TestResultRouteParametersResult(new MissingRouteParameter("Repo parameter is required", "repo"));
        }

        if (!TryReadRequired(routeContext, "branch", out var branch))
        {
            return new TestResultRouteParametersResult(new MissingRouteParameter("Branch parameter is required", "branch"));
        }

        return new TestResultRouteParametersResult(new TestResultRouteParameters(platform, owner, repo, branch));

        static bool TryReadRequired(RouteContext context, string name, out string value)
        {
            var isPresent = context.TryGetRouteValue(name, out var candidate) && !string.IsNullOrWhiteSpace(candidate);
            value = isPresent ? candidate! : string.Empty;
            return isPresent;
        }
    }
}

[GenerateOneOf]
internal sealed partial class TestResultRouteParametersResult
    : OneOfBase<TestResultRouteParameters, MissingRouteParameter>
{
    public bool IsSuccess => IsT0;

    public TestResultRouteParameters Parameters => IsT0
        ? AsT0
        : throw new InvalidOperationException("Result is successful");

    public MissingRouteParameter Failure => IsT1
        ? AsT1
        : throw new InvalidOperationException("Result is successful");
}

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
