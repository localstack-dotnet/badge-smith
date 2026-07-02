using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
[Trait("Category", TestCategories.AotContract)]
public sealed class TestResultsContractTests(BadgeSmithStackFixture stack)
{
    private const string IngestPath = "/tests/results/linux/test-org/test-repo/main";
    private const string BadgePath = "/badges/tests/linux/test-org/test-repo/main";

    private static string Payload(string runId, string ts) => $$"""
        {"platform":"linux","passed":10,"failed":0,"skipped":1,"total":11,
         "url_html":"https://github.com/test-org/test-repo/runs/1",
         "timestamp":"{{ts}}","commit":"abc1234","run_id":"{{runId}}",
         "workflow_run_url":"https://github.com/test-org/test-repo/actions/runs/1"}
        """;

    private static Dictionary<string, string> AuthHeaders(string body)
    {
        var (sig, ts, nonce) = HmacTestSigner.Sign(body, BadgeSmithStackFixture.HmacSecret);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x-signature"] = sig,
            ["x-timestamp"] = ts,
            ["x-nonce"] = nonce,
            ["content-type"] = "application/json",
        };
    }

    [Fact]
    public async Task Ingestion_Then_Badge_RoundTrip()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, AuthHeaders(body), body, TestContext.Current.CancellationToken);
        Assert.Equal(201, post.StatusCode);

        var badge = await stack.Lambda.InvokeAsync("GET", BadgePath, ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, badge.StatusCode);
        Assert.Contains("\"schemaVersion\":1", badge.Body, StringComparison.Ordinal);
        Assert.Contains("passed", badge.Body, StringComparison.Ordinal);
        Assert.NotNull(badge.Headers);
        Assert.StartsWith("\"", badge.Headers["ETag"], StringComparison.Ordinal);

        var cached = await stack.Lambda.InvokeAsync("GET", BadgePath,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["if-none-match"] = badge.Headers["ETag"] },
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(304, cached.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Reject_BadSignature_With401()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var headers = AuthHeaders(body);
        headers["x-signature"] = "sha256=" + new string('0', 64);
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, headers, body, TestContext.Current.CancellationToken);
        Assert.Equal(401, post.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Reject_StaleTimestamp_With400()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var (sig, ts, nonce) = HmacTestSigner.Sign(body, BadgeSmithStackFixture.HmacSecret,
            timestamp: DateTimeOffset.UtcNow.AddMinutes(-10));
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x-signature"] = sig,
            ["x-timestamp"] = ts,
            ["x-nonce"] = nonce,
        };
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, headers, body, TestContext.Current.CancellationToken);
        Assert.Equal(400, post.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Reject_NonceReplay_With400()
    {
        var nonce = Guid.NewGuid().ToString("N");
        var body1 = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var (sig1, ts1, _) = HmacTestSigner.Sign(body1, BadgeSmithStackFixture.HmacSecret, nonce: nonce);
        var first = await stack.Lambda.InvokeAsync("POST", IngestPath,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["x-signature"] = sig1, ["x-timestamp"] = ts1, ["x-nonce"] = nonce }, body1,
            TestContext.Current.CancellationToken);
        Assert.Equal(201, first.StatusCode);

        var body2 = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var (sig2, ts2, _) = HmacTestSigner.Sign(body2, BadgeSmithStackFixture.HmacSecret, nonce: nonce);
        var replay = await stack.Lambda.InvokeAsync("POST", IngestPath,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["x-signature"] = sig2, ["x-timestamp"] = ts2, ["x-nonce"] = nonce }, body2,
            TestContext.Current.CancellationToken);
        Assert.Equal(400, replay.StatusCode);
    }

    [Fact]
    public async Task Ingestion_MalformedHexSignature_PinsCurrentBehavior_500()
    {
        // Known bug (findings doc §2): malformed hex throws FormatException → 500.
        // Wave 1 will change this to 401 and update this assertion.
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var headers = AuthHeaders(body);
        headers["x-signature"] = "sha256=zzzz";
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, headers, body, TestContext.Current.CancellationToken);
        Assert.Equal(500, post.StatusCode);
    }

    [Fact]
    public async Task Ingestion_MissingAuthHeaders_Should_Return400()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, body: body, ct: TestContext.Current.CancellationToken);
        Assert.Equal(400, post.StatusCode);
    }

    [Fact]
    public async Task Badge_UnknownRepo_Should_Return404()
    {
        var badge = await stack.Lambda.InvokeAsync("GET", "/badges/tests/linux/test-org/no-such-repo/main", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, badge.StatusCode);
    }

    [Fact]
    public async Task Redirect_Should_Return302_WithLocation()
    {
        var body = Payload($"run-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.ToString("O"));
        var post = await stack.Lambda.InvokeAsync("POST", IngestPath, AuthHeaders(body), body, TestContext.Current.CancellationToken);
        Assert.Equal(201, post.StatusCode);

        var redirect = await stack.Lambda.InvokeAsync("GET", "/redirect/test-results/linux/test-org/test-repo/main", ct: TestContext.Current.CancellationToken);
        Assert.Equal(302, redirect.StatusCode);
        Assert.NotNull(redirect.Headers);
        Assert.Contains("github.com", redirect.Headers["Location"], StringComparison.Ordinal);
    }
}
