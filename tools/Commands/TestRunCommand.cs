using BadgeSmith.Tools.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace BadgeSmith.Tools.Commands;

internal sealed class TestRunCommand : AsyncCommand<TestRunSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, TestRunSettings settings, CancellationToken cancellationToken)
    {
        var paths = new RepositoryPaths();
        var projectPath = Path.GetFullPath(Path.Combine(paths.RepositoryRoot, settings.ProjectPath));
        var resultsDir = Path.GetFullPath(Path.Combine(paths.RepositoryRoot, settings.ResultsDir));
        Directory.CreateDirectory(resultsDir);

        var runner = new ProcessRunner(AnsiConsole.Console, settings.Verbose);
        var tfmRaw = await GetMsBuildPropertyAsync(runner, projectPath, "TargetFrameworks", paths.RepositoryRoot, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tfmRaw))
        {
            tfmRaw = await GetMsBuildPropertyAsync(runner, projectPath, "TargetFramework", paths.RepositoryRoot, cancellationToken).ConfigureAwait(false);
        }

        var frameworks = tfmRaw
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (frameworks.Length == 0)
        {
            AnsiConsole.MarkupLine($"[red]Unable to determine target frameworks for {Markup.Escape(projectPath)}[/]");
            return ToolExitCodes.ValidationFailure;
        }

        AnsiConsole.MarkupLine($"[cyan]Target frameworks: {Markup.Escape(string.Join(", ", frameworks))}[/]");

        foreach (var framework in frameworks)
        {
            AnsiConsole.MarkupLine($"[cyan]Testing {Markup.Escape(framework)}...[/]");
            var exitCode = await runner.RunStreamingAsync("dotnet", [
                "test", projectPath,
                "-c", settings.Configuration,
                "-f", framework,
                "--no-build",
                "--logger", $"trx;LogFileName=testResults-{framework}.trx",
                "--results-directory", resultsDir
            ], paths.RepositoryRoot, allowNonZeroExit: true, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        return ToolExitCodes.Success;
    }

    private static async Task<string> GetMsBuildPropertyAsync(ProcessRunner runner, string projectPath, string propertyName, string repositoryRoot, CancellationToken cancellationToken)
    {
        var result = await runner.RunBufferedAsync("dotnet", [
            "msbuild", projectPath,
            $"-getProperty:{propertyName}",
            "-nologo",
            "-v:q"
        ], repositoryRoot, cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.StandardOutput.Trim();
    }
}

internal sealed class TestRunSettings : CommandSettings
{
    [CommandOption("--project-path")]
    [Description("Path to the test project file, relative to the repository root.")]
    public string ProjectPath { get; init; } = "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj";

    [CommandOption("--results-dir")]
    [Description("Directory for TRX results, relative to the repository root.")]
    public string ResultsDir { get; init; } = "test-results";

    [CommandOption("--configuration")]
    [Description("Build configuration.")]
    public string Configuration { get; init; } = "Release";

    [CommandOption("-v|--verbose")]
    [Description("Print external commands before running them.")]
    public bool Verbose { get; init; }

    public override ValidationResult Validate()
    {
        RepositoryPaths paths;
        try
        {
            paths = new RepositoryPaths();
        }
        catch (DirectoryNotFoundException ex)
        {
            return ValidationResult.Error(ex.Message);
        }

        var projectPath = Path.GetFullPath(Path.Combine(paths.RepositoryRoot, ProjectPath));
        if (!File.Exists(projectPath))
        {
            return ValidationResult.Error($"Project file not found: {ProjectPath}");
        }

        return ValidationResult.Success();
    }
}
