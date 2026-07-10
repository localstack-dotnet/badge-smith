using System.Diagnostics;
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Tools.Infrastructure;
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
        const string payload =
            "{\"platform\":\"Linux\",\"passed\":1,\"failed\":0,\"skipped\":0,\"total\":1,\"url_html\":\"https://example.com/run\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"commit\":\"abc123\",\"run_id\":\"1\",\"workflow_run_url\":\"https://example.com/workflow\"}";
        var result = await RunToolAsync("tests", "ingest", "--base-url", "https://example.com", "--owner", "LocalStack-DotNet", "--repo", "BadgeSmith", "--platform", "Linux",
            "--branch", "feature/tools", "--secret", "test-secret", "--payload", payload, "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DRY RUN", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.com/tests/results/linux/localstack-dotnet/badgesmith/feature%2Ftools", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestsIngest_Should_Return_Non_Zero_When_Payload_And_Payload_File_Missing()
    {
        var result = await RunToolAsync("tests", "ingest", "--base-url", "https://example.com", "--owner", "LocalStack-DotNet", "--repo", "BadgeSmith", "--platform", "Linux",
            "--branch", "Main", "--secret", "test-secret", "--dry-run");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("payload", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BadgeUpdate_Should_Dry_Run_Without_Posting_When_Dry_Run_Is_Set()
    {
        var result = await RunToolAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["BADGESMITH_HMAC_SECRET"] = "test-secret",
            },
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
            "--base-url", "https://api.example.com/prefix/",
            "--branch", "feature/tools",
            "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DRY RUN", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://api.example.com/prefix/tests/results/linux/localstack-dotnet/badge-smith/feature%2Ftools", result.Output, StringComparison.Ordinal);
        Assert.Contains("https://api.example.com/prefix/badges/tests/linux/localstack-dotnet/badge-smith/feature%2Ftools", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BadgeUpdate_Should_Return_Validation_When_Hmac_Environment_Variable_Is_Missing()
    {
        var result = await RunToolAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["BADGESMITH_HMAC_SECRET"] = null,
            },
            "badge", "update",
            "--platform", "Linux",
            "--test-passed", "1",
            "--test-failed", "0",
            "--test-skipped", "0",
            "--commit-sha", "abc123",
            "--run-id", "42",
            "--repository", "localstack-dotnet/badge-smith",
            "--server-url", "https://github.com",
            "--base-url", "https://api.example.com",
            "--branch", "main",
            "--dry-run");

        Assert.Equal(ToolExitCodes.ValidationFailure, result.ExitCode);
        Assert.Contains("BADGESMITH_HMAC_SECRET", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecretsSeed_Should_Print_Help_When_Invoked_With_Help()
    {
        var result = await RunToolAsync("secrets", "seed", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--config", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--table-name", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--dry-run", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecretsSeed_Should_Validate_Config_Without_Aws_Mutation_When_Dry_Run_Is_Set()
    {
        var configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await File.WriteAllTextAsync(configPath, """
                                                 {
                                                   "secrets": [
                                                     {
                                                       "org_name": "LocalStack-DotNet",
                                                       "name": "package",
                                                       "secret": "ghp_testtoken",
                                                       "type": "Package",
                                                       "description": "Package token"
                                                     }
                                                   ]
                                                 }
                                                 """, TestContext.Current.CancellationToken);

        try
        {
            var result = await RunToolAsync("secrets", "seed", "--config", configPath, "--dry-run");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("DRY RUN", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("localstack-dotnet", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CONST#GITHUB#package", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ghp_testtoken", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task SecretsSeed_Should_Return_Validation_When_Config_File_Is_Missing()
    {
        var missingConfigPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var result = await RunToolAsync("secrets", "seed", "--config", missingConfigPath, "--table-name", "badge-smith-github-org-secrets", "--dry-run");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("organization-pat-mapping.json.dist", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    private static Task<ToolRunResult> RunToolAsync(params string[] arguments)
    {
        return RunToolAsync(new Dictionary<string, string?>(StringComparer.Ordinal), arguments);
    }

    private static async Task<ToolRunResult> RunToolAsync(
        IReadOnlyDictionary<string, string?> environment,
        params string[] arguments)
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

        foreach (var (name, value) in environment)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

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
