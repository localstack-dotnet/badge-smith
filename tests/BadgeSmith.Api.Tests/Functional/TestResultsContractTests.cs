using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager.Model;
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("aspire-contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
public sealed class TestResultsContractTests(AspireContractFixture stack)
{
    private const string Owner = "test-org";
    private const string AlternateOwner = "test-org-alt";
    private const string Platform = "linux";
    private const string OrgSecretsTableName = "badge-smith-github-org-secrets";
    private static readonly DateTimeOffset TimestampSeed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TestResultCase CreateCase(string slug, int timestampOffsetSeconds)
    {
        var repo = $"test-repo-{slug}";
        var branch = $"main-{slug}";
        var runId = $"run-{slug}";
        var timestamp = TimestampSeed.AddSeconds(timestampOffsetSeconds).ToString("O");
        var urlHtml = $"https://github.com/{Owner}/{repo}/runs/{timestampOffsetSeconds}";
        var workflowRunUrl = $"https://github.com/{Owner}/{repo}/actions/runs/{timestampOffsetSeconds}";

        return new TestResultCase(
            IngestPath: CreateIngestPath(Owner, repo, Platform, branch),
            BadgePath: CreateBadgePath(Owner, repo, Platform, branch),
            RedirectPath: $"/redirect/test-results/{Platform}/{Owner}/{repo}/{branch}",
            Repo: repo,
            Branch: branch,
            RunId: runId,
            Timestamp: timestamp,
            UrlHtml: urlHtml,
            WorkflowRunUrl: workflowRunUrl);
    }

    private static Dictionary<string, string> AuthHeaders(TestResultCase testCase, string body)
    {
        var (sig, ts, nonce) = HmacTestSigner.Sign(Owner, testCase.Repo, Platform, testCase.Branch, body, AwsTestSeeder.HmacSecret);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x-signature"] = sig,
            ["x-timestamp"] = ts,
            ["x-nonce"] = nonce,
            ["content-type"] = "application/json",
        };
    }

    public static TheoryData<string> TamperedFields =>
    [
        "owner",
        "repo",
        "platform",
        "branch",
        "timestamp",
        "nonce",
        "body",
    ];

    [Fact]
    public async Task Ingestion_Should_Round_Trip_Badge_When_Accepted()
    {
        var testCase = CreateCase("roundtrip", 1);
        var body = testCase.CreatePayload();
        var post = await stack.Api.InvokeAsync("POST", testCase.IngestPath, AuthHeaders(testCase, body), body, TestContext.Current.CancellationToken);
        Assert.Equal(201, post.StatusCode);

        var badge = await stack.Api.InvokeAsync("GET", testCase.BadgePath, ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, badge.StatusCode);
        Assert.Contains("\"schemaVersion\":1", badge.Body, StringComparison.Ordinal);
        Assert.Contains("passed", badge.Body, StringComparison.Ordinal);
        Assert.NotNull(badge.Headers);
        Assert.StartsWith("\"", badge.Headers["ETag"], StringComparison.Ordinal);
        Assert.True(badge.Headers.ContainsKey("Last-Modified"));

        var cached = await stack.Api.InvokeAsync("GET", testCase.BadgePath,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["if-none-match"] = badge.Headers["ETag"]
            },
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(304, cached.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Return_401_When_Signature_Is_Invalid()
    {
        var testCase = CreateCase("bad-signature", 2);
        var body = testCase.CreatePayload();
        var headers = AuthHeaders(testCase, body);
        headers["x-signature"] = "sha256=" + new string('0', 64);
        var post = await stack.Api.InvokeAsync("POST", testCase.IngestPath, headers, body, TestContext.Current.CancellationToken);
        Assert.Equal(401, post.StatusCode);
    }

    [Theory]
    [MemberData(nameof(TamperedFields))]
    public async Task Ingestion_Should_Return_401_And_Not_Store_Result_When_Canonical_Field_Is_Tampered(string fieldName)
    {
        if (string.Equals(fieldName, "owner", StringComparison.Ordinal))
        {
            await EnsureAlternateOwnerUsesTestSecretAsync(TestContext.Current.CancellationToken);
        }

        var testCase = CreateCase($"tamper-{fieldName}", 20 + fieldName.Length);
        var body = testCase.CreatePayload();
        var headers = AuthHeaders(testCase, body);

        var owner = Owner;
        var repo = testCase.Repo;
        var platform = Platform;
        var branch = testCase.Branch;
        var sentBody = body;

        switch (fieldName)
        {
            case "owner":
                owner = AlternateOwner;
                break;
            case "repo":
                repo += "-alt";
                break;
            case "platform":
                platform = "windows";
                break;
            case "branch":
                branch += "-alt";
                break;
            case "timestamp":
                headers["x-timestamp"] = DateTimeOffset
                    .Parse(headers["x-timestamp"], CultureInfo.InvariantCulture)
                    .AddSeconds(1)
                    .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
                break;
            case "nonce":
                headers["x-nonce"] = Guid.NewGuid().ToString("N");
                break;
            case "body":
                sentBody = body.Replace("\"passed\":10", "\"passed\":11", StringComparison.Ordinal);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unknown field.");
        }

        var targetIngestPath = CreateIngestPath(owner, repo, platform, branch);
        var targetBadgePath = CreateBadgePath(owner, repo, platform, branch);

        var post = await stack.Api.InvokeAsync("POST", targetIngestPath, headers, sentBody, TestContext.Current.CancellationToken);
        Assert.Equal(401, post.StatusCode);

        var badge = await stack.Api.InvokeAsync("GET", targetBadgePath, ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, badge.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Return_400_When_Timestamp_Is_Stale()
    {
        var testCase = CreateCase("stale-timestamp", 3);
        var body = testCase.CreatePayload();
        var (sig, ts, nonce) = HmacTestSigner.Sign(Owner, testCase.Repo, Platform, testCase.Branch, body, AwsTestSeeder.HmacSecret,
            timestamp: DateTimeOffset.UtcNow.AddMinutes(-10));
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x-signature"] = sig,
            ["x-timestamp"] = ts,
            ["x-nonce"] = nonce,
        };
        var post = await stack.Api.InvokeAsync("POST", testCase.IngestPath, headers, body, TestContext.Current.CancellationToken);
        Assert.Equal(400, post.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Return_400_When_Timestamp_Is_Future()
    {
        var testCase = CreateCase("future-timestamp", 9);
        var body = testCase.CreatePayload();
        var (sig, ts, nonce) = HmacTestSigner.Sign(Owner, testCase.Repo, Platform, testCase.Branch, body, AwsTestSeeder.HmacSecret,
            timestamp: DateTimeOffset.UtcNow.AddMinutes(10));
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x-signature"] = sig,
            ["x-timestamp"] = ts,
            ["x-nonce"] = nonce,
        };
        var post = await stack.Api.InvokeAsync("POST", testCase.IngestPath, headers, body, TestContext.Current.CancellationToken);
        Assert.Equal(400, post.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Return_400_When_Nonce_Is_Replayed()
    {
        var testCase = CreateCase("nonce-replay", 4);
        var nonce = Guid.NewGuid().ToString("N");
        var body1 = testCase.CreatePayload();
        var (sig1, ts1, _) = HmacTestSigner.Sign(Owner, testCase.Repo, Platform, testCase.Branch, body1, AwsTestSeeder.HmacSecret, nonce: nonce);
        var first = await stack.Api.InvokeAsync("POST", testCase.IngestPath,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["x-signature"] = sig1,
                ["x-timestamp"] = ts1,
                ["x-nonce"] = nonce
            }, body1,
            TestContext.Current.CancellationToken);
        Assert.Equal(201, first.StatusCode);

        var replay = await stack.Api.InvokeAsync("POST", testCase.IngestPath,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["x-signature"] = sig1,
                ["x-timestamp"] = ts1,
                ["x-nonce"] = nonce
            }, body1,
            TestContext.Current.CancellationToken);
        Assert.Equal(400, replay.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Return_400_When_Same_Signed_Request_Is_Replayed_With_Case_Only_Route_Changes()
    {
        var testCase = CreateCase("case-replay", 10);
        var body = testCase.CreatePayload();
        var headers = AuthHeaders(testCase, body);

        var first = await stack.Api.InvokeAsync(
            "POST",
            testCase.IngestPath,
            headers,
            body,
            TestContext.Current.CancellationToken);
        Assert.Equal(201, first.StatusCode);

        var replay = await stack.Api.InvokeAsync(
            "POST",
            CreateIngestPath(Owner.ToUpperInvariant(), testCase.Repo.ToUpperInvariant(), Platform.ToUpperInvariant(), testCase.Branch),
            headers,
            body,
            TestContext.Current.CancellationToken);
        Assert.Equal(400, replay.StatusCode);

        var badge = await stack.Api.InvokeAsync("GET", testCase.BadgePath, ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, badge.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Return_401_When_Signature_Hex_Is_Malformed()
    {
        var testCase = CreateCase("malformed-hex", 6);
        var body = testCase.CreatePayload();
        var headers = AuthHeaders(testCase, body);
        headers["x-signature"] = "sha256=" + new string('z', 64);

        var post = await stack.Api.InvokeAsync(
            "POST",
            testCase.IngestPath,
            headers,
            body,
            TestContext.Current.CancellationToken);

        Assert.Equal(401, post.StatusCode);
    }

    [Fact]
    public async Task Ingestion_Should_Return_400_When_Auth_Headers_Are_Missing()
    {
        var testCase = CreateCase("missing-auth", 7);
        var body = testCase.CreatePayload();
        var post = await stack.Api.InvokeAsync("POST", testCase.IngestPath, body: body, ct: TestContext.Current.CancellationToken);
        Assert.Equal(400, post.StatusCode);
    }

    [Fact]
    public async Task Badge_Should_Return_404_When_Repo_Is_Unknown()
    {
        var badge = await stack.Api.InvokeAsync("GET", "/badges/tests/linux/test-org/no-such-repo/main", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, badge.StatusCode);
    }

    [Fact]
    public async Task Redirect_Should_Return_302_With_Location()
    {
        var testCase = CreateCase("redirect", 8);
        var body = testCase.CreatePayload();
        var post = await stack.Api.InvokeAsync("POST", testCase.IngestPath, AuthHeaders(testCase, body), body, TestContext.Current.CancellationToken);
        Assert.Equal(201, post.StatusCode);

        var redirect = await stack.Api.InvokeAsync("GET", testCase.RedirectPath, ct: TestContext.Current.CancellationToken);
        Assert.Equal(302, redirect.StatusCode);
        Assert.NotNull(redirect.Headers);
        Assert.Equal(testCase.UrlHtml, redirect.Headers["Location"]);
        Assert.True(redirect.Headers.ContainsKey("Cache-Control"));
        Assert.Contains("public", redirect.Headers["Cache-Control"], StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TestResultCase(
        string IngestPath,
        string BadgePath,
        string RedirectPath,
        string Repo,
        string Branch,
        string RunId,
        string Timestamp,
        string UrlHtml,
        string WorkflowRunUrl)
    {
        public string CreatePayload() => $$"""
                                           {"platform":"linux","passed":10,"failed":0,"skipped":1,"total":11,
                                            "url_html":"{{UrlHtml}}",
                                            "timestamp":"{{Timestamp}}","commit":"abc1234","run_id":"{{RunId}}",
                                            "workflow_run_url":"{{WorkflowRunUrl}}"}
                                           """;
    }

    private static string CreateIngestPath(string owner, string repo, string platform, string branch) => $"/tests/results/{platform}/{owner}/{repo}/{branch}";

    private static string CreateBadgePath(string owner, string repo, string platform, string branch) => $"/badges/tests/{platform}/{owner}/{repo}/{branch}";

    private async Task EnsureAlternateOwnerUsesTestSecretAsync(CancellationToken cancellationToken)
    {
        const string secretName = "badgesmith/github/test-org-alt/testdata";

        try
        {
            await stack.Secrets.DescribeSecretAsync(new DescribeSecretRequest
            {
                SecretId = secretName
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Amazon.SecretsManager.Model.ResourceNotFoundException)
        {
            await stack.Secrets.CreateSecretAsync(new CreateSecretRequest
            {
                Name = secretName,
                SecretString = AwsTestSeeder.HmacSecret,
            }, cancellationToken).ConfigureAwait(false);
        }

        await stack.DynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = OrgSecretsTableName,
            Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new($"ORG#{AlternateOwner}"),
                ["SK"] = new("CONST#GITHUB#testdata"),
                ["SecretName"] = new(secretName),
            },
        }, cancellationToken).ConfigureAwait(false);
    }
}
