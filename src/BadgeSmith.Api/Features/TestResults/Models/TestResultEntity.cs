namespace BadgeSmith.Api.Features.TestResults.Models;

internal record TestResultEntity(
    // DynamoDB Keys
    string Pk, // TEST#{owner}#{repo}
    string Sk, // RESULT#{platform}#{branch}#{timestamp}
    string Gsi1Pk, // LATEST#{owner}#{repo}#{platform}#{branch}
    string Gsi1Sk, // {timestamp} (for sorting latest first)

    // Route Context (enriched from URL)
    string Owner,
    string Repo,
    string Platform,
    string Branch,

    // Test Results (from payload)
    int Passed,
    int Failed,
    int Skipped,
    int Total,
    DateTimeOffset Timestamp,

    // GitHub Context (from payload)
    string Commit,
    string RunId,
    string UrlHtml,
    string WorkflowRunUrl,

    // Metadata
    DateTimeOffset CreatedAt,
    long Ttl // Auto-expire old test results
)
{
    public static TestResultEntity FromPayload(
        string owner,
        string repo,
        string platform,
        string branch,
        TestResultPayload payload,
        DateTimeOffset createdAt)
    {
        var pk = $"TEST#{owner}#{repo}";
        var sk = $"RESULT#{platform}#{branch}#{payload.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture)}";
        var gsi1Pk = $"LATEST#{owner}#{repo}#{platform}#{branch}";
        var gsi1Sk = payload.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);

        // TTL: Keep test results for 90 days
        var ttl = createdAt.AddDays(90).ToUnixTimeSeconds();

        return new TestResultEntity(
            Pk: pk,
            Sk: sk,
            Gsi1Pk: gsi1Pk,
            Gsi1Sk: gsi1Sk,
            Owner: owner,
            Repo: repo,
            Platform: platform,
            Branch: branch,
            Passed: payload.Passed,
            Failed: payload.Failed,
            Skipped: payload.Skipped,
            Total: payload.Total,
            Timestamp: payload.Timestamp,
            Commit: payload.Commit,
            RunId: payload.RunId,
            UrlHtml: payload.UrlHtml,
            WorkflowRunUrl: payload.WorkflowRunUrl,
            CreatedAt: createdAt,
            Ttl: ttl
        );
    }

    /// <summary>
    /// Converts to a Shields.io badge response.
    /// </summary>
    public ShieldsBadgeResponse ToBadge()
    {
        var (message, color) = Failed switch
        {
            0 => ($"{Passed} passed", "brightgreen"),
            _ when Failed > Passed => ($"{Failed} failed", "red"),
            _ => ($"{Passed} passed, {Failed} failed", "yellow"),
        };

        return new ShieldsBadgeResponse(
            SchemaVersion: 1,
            Label: "tests",
            Message: message,
            Color: color
        );
    }
}
