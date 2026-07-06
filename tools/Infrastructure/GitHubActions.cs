namespace BadgeSmith.Tools.Infrastructure;

internal static class GitHubActions
{
    public static string? StepSummaryPath => Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");

    public static string ResolveBranch(string? explicitBranch)
    {
        if (!string.IsNullOrWhiteSpace(explicitBranch))
        {
            return explicitBranch;
        }

        var headRef = Environment.GetEnvironmentVariable("GITHUB_HEAD_REF");
        if (!string.IsNullOrWhiteSpace(headRef))
        {
            return headRef;
        }

        var refName = Environment.GetEnvironmentVariable("GITHUB_REF_NAME");
        if (!string.IsNullOrWhiteSpace(refName))
        {
            return refName;
        }

        return "unknown";
    }

    public static async Task AppendStepSummaryAsync(string markdown, CancellationToken cancellationToken = default)
    {
        var path = StepSummaryPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await File.AppendAllTextAsync(path, markdown + Environment.NewLine, cancellationToken).ConfigureAwait(false);
    }
}
