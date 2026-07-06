using BadgeSmith.Tools.Infrastructure;
using BadgeSmith.Tools.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace BadgeSmith.Tools.Commands;

internal sealed class LambdaBuildCommand : AsyncCommand<LambdaBuildSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IProcessRunner _runner;
    private readonly RepositoryPaths _paths;

    public LambdaBuildCommand(IProcessRunner runner, RepositoryPaths paths, IAnsiConsole console, IToolLogger logger)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        ArgumentNullException.ThrowIfNull(logger);
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, LambdaBuildSettings settings, CancellationToken cancellationToken)
    {
        var outputDirectory = _paths.ResolveFromRoot(settings.OutDir);

        if (settings.Clean && Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);

        var platform = settings.Rid == "linux-arm64" ? "linux/arm64" : "linux/amd64";
        if (settings.Target is "zip" or "both")
        {
            await _runner.RunStreamingAsync("docker", [
                "buildx", "build",
                "-f", settings.Dockerfile,
                "--target", "export-zip",
                "--build-arg", $"RID={settings.Rid}",
                "--platform", platform,
                "--output", $"type=local,dest={settings.OutDir}",
                settings.Context
            ], _paths.RepositoryRoot, verbose: settings.Verbose, cancellationToken: cancellationToken).ConfigureAwait(false);

            var expectedZip = _paths.ResolveFromRoot(settings.OutDir, $"badge-lambda-{settings.Rid}.zip");
            if (!File.Exists(expectedZip))
            {
                _console.MarkupLine($"[red]ZIP not found: {Markup.Escape(expectedZip)}[/]");
                return ToolExitCodes.ExternalProcessFailure;
            }
        }

        if (settings.Target is "image" or "both")
        {
            var imageArgs = new List<string>
            {
                "buildx", "build",
                "-f", settings.Dockerfile,
                "--target", "lambda-image",
                "--build-arg", $"RID={settings.Rid}",
                "--platform", platform,
                "-t", settings.ImageTag,
            };

            if (settings.Push)
            {
                imageArgs.Add("--push");
            }

            imageArgs.Add(settings.Context);
            await _runner.RunStreamingAsync("docker", imageArgs, _paths.RepositoryRoot, verbose: settings.Verbose, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        _console.MarkupLine($"[green]Done. Artifacts in '{Markup.Escape(settings.OutDir)}'.[/]");
        return ToolExitCodes.Success;
    }
}

internal sealed class LambdaBuildSettings : CommandSettings
{
    [CommandOption("-t|--target")]
    [Description("Build target: zip, image, or both.")]
    public string Target { get; init; } = "zip";

    [CommandOption("-r|--rid")]
    [Description("Runtime identifier: linux-arm64 or linux-x64.")]
    public string Rid { get; init; } = "linux-arm64";

    [CommandOption("-i|--image-tag")]
    [Description("Docker image tag.")]
    public string ImageTag { get; init; } = "badgesmith-lambda:local";

    [CommandOption("-f|--dockerfile")]
    [Description("Path to the Lambda Dockerfile.")]
    public string Dockerfile { get; init; } = "src/BadgeSmith.Api/Dockerfile";

    [CommandOption("-c|--context")]
    [Description("Docker build context.")]
    public string Context { get; init; } = ".";

    [CommandOption("-o|--out")]
    [Description("Output directory for artifacts.")]
    public string OutDir { get; init; } = "artifacts";

    [CommandOption("--push")]
    [Description("Push the image after build.")]
    public bool Push { get; init; }

    [CommandOption("--clean")]
    [Description("Clean the output directory before writing.")]
    public bool Clean { get; init; }

    [CommandOption("-v|--verbose")]
    [Description("Print external commands before running them.")]
    public bool Verbose { get; init; }

    public override ValidationResult Validate()
    {
        if (Target is not "zip" and not "image" and not "both")
        {
            return ValidationResult.Error("--target must be zip, image, or both.");
        }

        if (Rid is not "linux-arm64" and not "linux-x64")
        {
            return ValidationResult.Error("--rid must be linux-arm64 or linux-x64.");
        }

        return ValidationResult.Success();
    }
}
