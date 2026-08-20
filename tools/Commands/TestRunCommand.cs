using System.ComponentModel;
using BadgeSmith.Tools.Infrastructure;
using BadgeSmith.Tools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BadgeSmith.Tools.Commands;

internal sealed class TestRunCommand : AsyncCommand<TestRunSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IProcessRunner _runner;
    private readonly RepositoryPaths _paths;

    public TestRunCommand(IProcessRunner runner, RepositoryPaths paths, IAnsiConsole console, IToolLogger logger)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        ArgumentNullException.ThrowIfNull(logger);
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, TestRunSettings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(Path.Combine(_paths.RepositoryRoot, settings.ProjectPath));
        if (!File.Exists(projectPath))
        {
            _console.MarkupLine($"[red]Project file not found: {Markup.Escape(settings.ProjectPath)}[/]");
            return ToolExitCodes.ValidationFailure;
        }

        var resultsDir = Path.GetFullPath(Path.Combine(_paths.RepositoryRoot, settings.ResultsDir));
        Directory.CreateDirectory(resultsDir);

        var tfmRaw = await GetMsBuildPropertyAsync(_runner, projectPath, "TargetFrameworks", _paths.RepositoryRoot, settings.Verbose, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tfmRaw))
        {
            tfmRaw = await GetMsBuildPropertyAsync(_runner, projectPath, "TargetFramework", _paths.RepositoryRoot, settings.Verbose, cancellationToken).ConfigureAwait(false);
        }

        var frameworks = tfmRaw
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (frameworks.Length == 0)
        {
            _console.MarkupLine($"[red]Unable to determine target frameworks for {Markup.Escape(projectPath)}[/]");
            return ToolExitCodes.ValidationFailure;
        }

        _console.MarkupLine($"[cyan]Target frameworks: {Markup.Escape(string.Join(", ", frameworks))}[/]");

        foreach (var framework in frameworks)
        {
            _console.MarkupLine($"[cyan]Testing {Markup.Escape(framework)}...[/]");
            var exitCode = await _runner.RunStreamingAsync("dotnet", [
                "test", projectPath,
                "-c", settings.Configuration,
                "-f", framework,
                "--no-build",
                "--logger", $"trx;LogFileName=testResults-{framework}.trx",
                "--results-directory", resultsDir
            ], _paths.RepositoryRoot, allowNonZeroExit: true, verbose: settings.Verbose, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        return ToolExitCodes.Success;
    }

    private static async Task<string> GetMsBuildPropertyAsync(IProcessRunner runner, string projectPath, string propertyName, string repositoryRoot, bool verbose, CancellationToken cancellationToken)
    {
        var result = await runner.RunBufferedAsync("dotnet", [
            "msbuild", projectPath,
            $"-getProperty:{propertyName}",
            "-nologo",
            "-v:q"
        ], repositoryRoot, verbose: verbose, cancellationToken: cancellationToken).ConfigureAwait(false);

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

    public override ValidationResult Validate() => ValidationResult.Success();
}
