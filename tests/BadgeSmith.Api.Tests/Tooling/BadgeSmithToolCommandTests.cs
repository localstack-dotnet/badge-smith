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
