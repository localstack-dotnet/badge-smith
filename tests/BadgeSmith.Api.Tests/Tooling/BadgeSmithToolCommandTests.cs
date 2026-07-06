using System.Diagnostics;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Trait("Category", TestCategories.Unit)]
public sealed class BadgeSmithToolCommandTests
{
    [Fact]
    public async Task BadgeSmithTool_Should_Print_Help_When_Invoked_With_Help()
    {
        var result = await RunToolAsync("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("USAGE", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("badgesmith", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BadgeSmithTool_Should_Return_Non_Zero_When_Command_Is_Unknown()
    {
        var result = await RunToolAsync("unknown-command");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("unknown-command", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LambdaBuild_Should_Print_Help_When_Invoked_With_Help()
    {
        var result = await RunToolAsync("lambda", "build", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("linux-arm64", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--target", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--rid", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LambdaBuild_Should_Reject_Invalid_Rid_When_Rid_Is_Not_Supported()
    {
        var result = await RunToolAsync("lambda", "build", "--rid", "windows-x64");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("linux-arm64", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("linux-x64", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestsRun_Should_Print_Help_When_Invoked_With_Help()
    {
        var result = await RunToolAsync("tests", "run", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--project-path", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--results-dir", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestsRun_Should_Reject_Missing_Project_File_When_Project_Path_Does_Not_Exist()
    {
        var result = await RunToolAsync("tests", "run", "--project-path", "missing.csproj", "--results-dir", "artifacts/test-results");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Project file not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestsIngest_Should_Dry_Run_Without_Posting_When_Dry_Run_Is_Set()
    {
        const string payload = "{\"platform\":\"Linux\",\"passed\":1,\"failed\":0,\"skipped\":0,\"total\":1,\"url_html\":\"https://example.com/run\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"commit\":\"abc123\",\"run_id\":\"1\",\"workflow_run_url\":\"https://example.com/workflow\"}";
        var result = await RunToolAsync("tests", "ingest", "--base-url", "https://example.com", "--owner", "LocalStack-DotNet", "--repo", "BadgeSmith", "--platform", "Linux", "--branch", "Main", "--secret", "test-secret", "--payload", payload, "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DRY RUN", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.com/tests/results/linux/localstack-dotnet/badgesmith/Main", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestsIngest_Should_Return_Non_Zero_When_Payload_And_Payload_File_Missing()
    {
        var result = await RunToolAsync("tests", "ingest", "--base-url", "https://example.com", "--owner", "LocalStack-DotNet", "--repo", "BadgeSmith", "--platform", "Linux", "--branch", "Main", "--secret", "test-secret", "--dry-run");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("payload", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BadgeUpdate_Should_Dry_Run_Without_Posting_When_Dry_Run_Is_Set()
    {
        var result = await RunToolAsync(
            "badge", "update",
            "--platform", "Linux",
            "--test-passed", "2",
            "--test-failed", "0",
            "--test-skipped", "1",
            "--test-url-html", "https://example.com/tests",
            "--commit-sha", "abc123",
            "--run-id", "42",
            "--repository", "localstack-dotnet/badge-smith",
            "--server-url", "https://github.com",
            "--api-domain", "api.example.com",
            "--hmac-secret", "test-secret",
            "--branch", "feature/tools",
            "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DRY RUN", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://api.example.com/tests/results/linux/localstack-dotnet/badge-smith/feature/tools", result.Output, StringComparison.Ordinal);
        Assert.Contains("badges/tests/linux/localstack-dotnet/badge-smith/feature/tools", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", result.Output, StringComparison.Ordinal);
    }

    private static async Task<ToolRunResult> RunToolAsync(params string[] arguments)
    {
        var root = FindRepositoryRoot();
        var toolPath = Path.Combine(root, "tools", "badgesmith.cs");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--file");
        startInfo.ArgumentList.Add(toolPath);
        startInfo.ArgumentList.Add("--");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process.");
        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        return new ToolRunResult(process.ExitCode, stdout + stderr);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) && (File.Exists(gitPath) || Directory.Exists(gitPath)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find BadgeSmith repository root.");
    }

    private sealed record ToolRunResult(int ExitCode, string Output);
}
