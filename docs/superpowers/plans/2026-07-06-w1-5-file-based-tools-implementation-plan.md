# W1.5 File-Based Tools Implementation Plan

Status: Completed/historical. Do not execute this checklist. Current CLI commands live
in `tools/README.md`, CDK commands live in `build/BadgeSmith.CDK/README.md`, package
versions live in `Directory.Packages.props`, and current workstream status lives in
`docs/ROADMAP.md`. The deferred `perf baseline` command remains tracked there.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace every tracked `.sh` and `.ps1` file with one modular .NET 10 file-based BadgeSmith CLI under `tools/`.

**Architecture:** `tools/badgesmith.cs` is the only executable entrypoint and uses `#:include` to compose focused command and infrastructure files. Spectre.Console.Cli owns command parsing, validation, help, and exit codes; CliWrap owns shell-free external process execution. GitHub composite actions remain as thin wrappers, but their multi-line Bash/PowerShell business logic moves into the C# tool.

**Tech Stack:** .NET SDK 10.0.301+, C# 14 file-based apps, `#:include`, Spectre.Console.Cli, CliWrap, System.Text.Json, HMACSHA256, HttpClient, xUnit v3 on VSTest.

## Global Constraints

- `global.json` must require .NET SDK `10.0.301` or newer so file-based apps support `#:include`.
- Unix-like environments run `${{ github.workspace }}/tools/badgesmith.cs ...` directly through the shebang; do not use `dotnet run` on Unix workflow paths.
- Windows environments run `dotnet run --file "${{ github.workspace }}\tools\badgesmith.cs" -- ...`.
- The repository must end with no tracked `.sh` or `.ps1` files: `git ls-files "*.sh" "*.ps1"` must return no output.
- Root analyzer policy stays active; use only targeted tool-specific MSBuild overrides if a concrete analyzer/build issue appears.
- Package versions stay in `Directory.Packages.props`; do not hard-code package versions in `#:package` directives.
- GitHub Actions must install the SDK from `global.json` with `actions/setup-dotnet`'s `global-json-file` input instead of duplicating a `10.0.x` version string.
- Do not wrap first-class one-line workflow commands such as `cdk synth`, `cdk diff`, `cdk deploy`, `cdk ls`, `dotnet restore`, or `dotnet build` unless BadgeSmith-specific orchestration is required.
- `badge update` and `tests ingest` must support `--dry-run` and must not print raw secrets.
- Commit operations are approval-gated by `AGENTS.md`; when a task reaches a commit checkpoint, present a concise summary and proposed Conventional Commit message, then ask Deniz before committing.

---

## File Structure

Create these files:

- `tools/badgesmith.cs`: file-based CLI entrypoint, package/directive declarations, command registration, global exception handling.
- `tools/Directory.Build.props`: imports root build props and applies only targeted tool-specific settings.
- `tools/Commands/LambdaBuildCommand.cs`: `lambda build` command and settings.
- `tools/Commands/PerfBaselineCommand.cs`: `perf baseline` command and settings.
- `tools/Commands/TestRunCommand.cs`: `tests run` command and settings.
- `tools/Commands/TestIngestCommand.cs`: `tests ingest` command and settings.
- `tools/Commands/BadgeUpdateCommand.cs`: `badge update` command and settings.
- `tools/Commands/SecretsSeedCommand.cs`: `secrets seed` command and settings.
- `tools/Infrastructure/ProcessRunner.cs`: CliWrap wrapper for streaming and buffered process execution.
- `tools/Infrastructure/RepositoryPaths.cs`: repo-root discovery and common path helpers.
- `tools/Infrastructure/ToolExitCodes.cs`: stable exit-code constants.
- `tools/Infrastructure/HmacSigner.cs`: HMAC SHA-256 signature helper.
- `tools/Infrastructure/GitHubActions.cs`: GitHub Actions environment helpers and step-summary writer.
- `tools/Infrastructure/TestResultPayloads.cs`: JSON payload records used by `tests ingest` and `badge update`.
- `tools/Infrastructure/OrgSecretSeeder.cs`: AWS SDK seeding logic for GitHub org secret mappings.
- `tools/Infrastructure/LocalStackSeeder.cs`: DynamoDB and Secrets Manager seeding logic previously in `perf-baseline-seed.sh`.
- `tools/README.md`: current tooling usage documentation.
- `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`: process-level CLI smoke and dry-run tests.

Modify these files:

- `Directory.Packages.props`: add `Spectre.Console.Cli` and `CliWrap` package versions.
- `.github/workflows/ci-cd.yml`: call `tools/badgesmith.cs lambda build` directly.
- `.github/workflows/deploy.yml`: call `tools/badgesmith.cs lambda build` directly; keep CDK commands direct.
- `.github/workflows/run-dotnet-tests/action.yml`: replace OS-specific script invocations with tool invocations.
- `.github/workflows/update-test-badge/action.yml`: replace inline Bash logic with tool invocation while preserving inputs.
- `src/BadgeSmith.Host/Program.cs`: replace the seeder project resource with an Aspire `AddCSharpApp` resource that runs `tools/badgesmith.cs secrets seed`.
- `src/BadgeSmith.Host/BadgeSmith.Host.csproj`: remove the seeder project reference.
- `BadgeSmith.sln`: remove the old seeder project.
- `AGENTS.md`: replace build-lambda script references with the new tool command.
- `ARCHITECTURE.md`: update tooling descriptions.
- `README.md`: update action/tool examples.
- `docs/ROADMAP.md`: track W1.5 progress.
- `tests/BadgeSmith.Api.Tests/README.md`: mention the tooling smoke tests if a new category note is needed.

Delete these files:

- `.github/workflows/run-dotnet-tests/run-unix.sh`
- `.github/workflows/run-dotnet-tests/run-win.ps1`
- `scripts/build-lambda.sh`
- `scripts/build-lambda.ps1`
- `scripts/perf-baseline.sh`
- `scripts/perf-baseline.ps1`
- `scripts/perf-baseline-seed.sh`
- `scripts/test-ingestion.sh`
- `scripts/test-ingestion.ps1`
- `tests/seeders/BadgeSmith.DynamoDb.Seeders/BadgeSmith.DynamoDb.Seeders.csproj`
- `tests/seeders/BadgeSmith.DynamoDb.Seeders/Program.cs`
- `tests/seeders/BadgeSmith.DynamoDb.Seeders/OrgSecretSeeder.cs`
- `tests/seeders/BadgeSmith.DynamoDb.Seeders/Properties/launchSettings.json`

Replace this template with a fixed copy:

- `tests/seeders/BadgeSmith.DynamoDb.Seeders/organization-pat-mapping.json.dist` to `tools/organization-pat-mapping.json.dist`, fixing the invalid JSON while moving it.

The real local config remains `tools/organization-pat-mapping.json` and is ignored by the existing `**/organization-pat-mapping.json` rule. Do not commit the real config file.

Remove or consolidate these script-facing docs after their useful content is migrated:

- `scripts/README-TEST-INGESTION.md`
- `scripts/README-PERF-TESTING.md`

---

### Task 1: CLI Foundation And Process-Level Test Harness

**Files:**

- Modify: `Directory.Packages.props`
- Create: `tools/Directory.Build.props`
- Create: `tools/badgesmith.cs`
- Create: `tools/Infrastructure/ToolExitCodes.cs`
- Create: `tools/Infrastructure/RepositoryPaths.cs`
- Create: `tools/Infrastructure/ProcessRunner.cs`
- Create: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`

**Interfaces:**

- Produces: `internal static class ToolExitCodes` with `Success`, `GeneralFailure`, `ValidationFailure`, `ExternalProcessFailure`, `NetworkFailure`, and `Canceled` integer constants.
- Produces: `internal sealed class RepositoryPaths` with `RepositoryRoot`, `ArtifactsDirectory`, and `ResolveFromRoot(params string[] segments)`.
- Produces: `internal sealed class ProcessRunner` with `RunStreamingAsync(...)` and `RunBufferedAsync(...)` methods.
- Produces: `tools/badgesmith.cs` command app registration used by later command tasks.

- [ ] **Step 1: Add package versions**

Modify `Directory.Packages.props` by adding these package versions under the third-party packages item group:

```xml
<PackageVersion Include="CliWrap" Version="3.6.0" />
<PackageVersion Include="Spectre.Console.Cli" Version="0.55.0" />
```

- [ ] **Step 2: Create tool build props**

Create `tools/Directory.Build.props`:

```xml
<Project>
  <Import Project="..\Directory.Build.props" />

  <PropertyGroup>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <IsPackable>false</IsPackable>
    <PackAsTool>false</PackAsTool>
    <PublishAot>false</PublishAot>
  </PropertyGroup>
</Project>
```

This keeps the root analyzer and package policy by importing the root props, while disabling package/doc outputs that do not matter for source-controlled file-based tools.

- [ ] **Step 3: Write the failing CLI help tests**

Create `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`:

```csharp
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
```

- [ ] **Step 4: Run the new tests and verify they fail**

Run:

```bash
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeSmithToolCommandTests"
```

Expected: FAIL because `tools/badgesmith.cs` does not exist.

- [ ] **Step 5: Create common infrastructure**

Create `tools/Infrastructure/ToolExitCodes.cs`:

```csharp
namespace BadgeSmith.Tools.Infrastructure;

internal static class ToolExitCodes
{
    public const int Success = 0;
    public const int GeneralFailure = 1;
    public const int ValidationFailure = 2;
    public const int ExternalProcessFailure = 3;
    public const int NetworkFailure = 4;
    public const int Canceled = 130;
}
```

Create `tools/Infrastructure/RepositoryPaths.cs`:

```csharp
namespace BadgeSmith.Tools.Infrastructure;

internal sealed class RepositoryPaths
{
    public RepositoryPaths(string? startDirectory = null)
    {
        RepositoryRoot = FindRepositoryRoot(startDirectory ?? Directory.GetCurrentDirectory());
        ArtifactsDirectory = Path.Combine(RepositoryRoot, "artifacts");
    }

    public string RepositoryRoot { get; }

    public string ArtifactsDirectory { get; }

    public string ResolveFromRoot(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return Path.Combine([RepositoryRoot, .. segments]);
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
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
}
```

Create `tools/Infrastructure/ProcessRunner.cs`:

```csharp
using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using Spectre.Console;

namespace BadgeSmith.Tools.Infrastructure;

internal sealed class ProcessRunner
{
    private readonly IAnsiConsole _console;
    private readonly bool _verbose;

    public ProcessRunner(IAnsiConsole console, bool verbose)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _verbose = verbose;
    }

    public async Task<BufferedProcessResult> RunBufferedAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        CancellationToken cancellationToken = default)
    {
        var command = CreateCommand(executable, arguments, workingDirectory, environment, allowNonZeroExit);
        WriteCommand(executable, arguments, workingDirectory);

        var result = await command.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        if (_verbose && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            _console.WriteLine(result.StandardOutput.TrimEnd());
        }

        if (_verbose && !string.IsNullOrWhiteSpace(result.StandardError))
        {
            _console.MarkupLine($"[yellow]{Markup.Escape(result.StandardError.TrimEnd())}[/]");
        }

        return new BufferedProcessResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    public async Task<int> RunStreamingAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        CancellationToken cancellationToken = default)
    {
        var command = CreateCommand(executable, arguments, workingDirectory, environment, allowNonZeroExit);
        WriteCommand(executable, arguments, workingDirectory);

        await foreach (var commandEvent in command.ListenAsync(cancellationToken).ConfigureAwait(false))
        {
            switch (commandEvent)
            {
                case StandardOutputCommandEvent output:
                    _console.WriteLine(output.Text);
                    break;
                case StandardErrorCommandEvent error:
                    _console.WriteLine(error.Text);
                    break;
                case ExitedCommandEvent exited:
                    return exited.ExitCode;
            }
        }

        return ToolExitCodes.Success;
    }

    private Command CreateCommand(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string?>? environment,
        bool allowNonZeroExit)
    {
        var command = Cli.Wrap(executable)
            .WithArguments(arguments);

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            command = command.WithWorkingDirectory(workingDirectory);
        }

        if (environment is not null)
        {
            command = command.WithEnvironmentVariables(environment);
        }

        if (allowNonZeroExit)
        {
            command = command.WithValidation(CommandResultValidation.None);
        }

        return command;
    }

    private void WriteCommand(string executable, IReadOnlyList<string> arguments, string? workingDirectory)
    {
        if (!_verbose)
        {
            return;
        }

        var directory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory;
        _console.MarkupLine($"[grey]> ({Markup.Escape(directory)}) {Markup.Escape(executable)} {Markup.Escape(string.Join(' ', arguments))}[/]");
    }
}

internal readonly record struct BufferedProcessResult(int ExitCode, string StandardOutput, string StandardError);
```

- [ ] **Step 6: Create the file-based CLI entrypoint**

Create `tools/badgesmith.cs`:

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:package Spectre.Console.Cli
#:package CliWrap
#:package AWSSDK.DynamoDBv2
#:package AWSSDK.SecretsManager
#:include Commands/**/*.cs
#:include Infrastructure/**/*.cs

using BadgeSmith.Tools.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("badgesmith");
    config.Settings.ShowOptionDefaultValues = true;
    config.Settings.CaseSensitivity = CaseSensitivity.None;
    config.Settings.CancellationExitCode = ToolExitCodes.Canceled;
    config.SetExceptionHandler((exception, _) =>
    {
        if (exception is OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Operation canceled.[/]");
            return ToolExitCodes.Canceled;
        }

        AnsiConsole.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
        return ToolExitCodes.GeneralFailure;
    });
});

return await app.RunAsync(args).ConfigureAwait(false);
```

- [ ] **Step 7: Mark the entrypoint executable in git**

Run:

```bash
git update-index --chmod=+x tools/badgesmith.cs
```

Expected: `git ls-files --stage tools/badgesmith.cs` shows mode `100755` for the file.

- [ ] **Step 8: Run foundation verification**

Run:

```bash
dotnet build tools/badgesmith.cs
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeSmithToolCommandTests"
```

Expected: `dotnet build` succeeds with zero warnings and both tests pass. If root analyzers produce concrete tool-only warnings, add targeted `tools/Directory.Build.props` suppressions with a short justification; do not disable analyzers globally.

- [ ] **Step 9: Commit checkpoint**

Present this summary and proposed commit message, then ask for approval before committing:

```text
Summary: Add the file-based BadgeSmith CLI foundation, package versions, executable entrypoint, process runner, and process-level help tests.
Proposed commit: build: add file-based BadgeSmith tool foundation
```

---

### Task 2: Lambda Build Command

**Files:**

- Create: `tools/Commands/LambdaBuildCommand.cs`
- Modify: `tools/badgesmith.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`

**Interfaces:**

- Consumes: `RepositoryPaths`, `ProcessRunner`, `ToolExitCodes`.
- Produces: `LambdaBuildCommand` registered as `lambda build`.
- Produces: default RID `linux-arm64`.

- [ ] **Step 1: Write failing tests for command help and validation**

Append these tests to `BadgeSmithToolCommandTests`:

```csharp
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
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```bash
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~LambdaBuild"
```

Expected: FAIL because `lambda build` is not registered.

- [ ] **Step 3: Implement the command**

Create `tools/Commands/LambdaBuildCommand.cs`:

```csharp
using BadgeSmith.Tools.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace BadgeSmith.Tools.Commands;

internal sealed class LambdaBuildCommand : AsyncCommand<LambdaBuildSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, LambdaBuildSettings settings)
    {
        var paths = new RepositoryPaths();
        var outputDirectory = paths.ResolveFromRoot(settings.OutDir);

        if (settings.Clean && Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);

        var platform = settings.Rid == "linux-arm64" ? "linux/arm64" : "linux/amd64";
        var runner = new ProcessRunner(AnsiConsole.Console, settings.Verbose);

        if (settings.Target is "zip" or "both")
        {
            await runner.RunStreamingAsync("docker", [
                "buildx", "build",
                "-f", settings.Dockerfile,
                "--target", "export-zip",
                "--build-arg", $"RID={settings.Rid}",
                "--platform", platform,
                "--output", $"type=local,dest={settings.OutDir}",
                settings.Context
            ], paths.RepositoryRoot).ConfigureAwait(false);

            var expectedZip = paths.ResolveFromRoot(settings.OutDir, $"badge-lambda-{settings.Rid}.zip");
            if (!File.Exists(expectedZip))
            {
                AnsiConsole.MarkupLine($"[red]ZIP not found: {Markup.Escape(expectedZip)}[/]");
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
            await runner.RunStreamingAsync("docker", imageArgs, paths.RepositoryRoot).ConfigureAwait(false);
        }

        AnsiConsole.MarkupLine($"[green]Done. Artifacts in '{Markup.Escape(settings.OutDir)}'.[/]");
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
```

- [ ] **Step 4: Register the command**

Modify `tools/badgesmith.cs` by adding this using:

```csharp
using BadgeSmith.Tools.Commands;
```

Inside `app.Configure`, before the exception handler, add:

```csharp
config.AddBranch("lambda", lambda =>
{
    lambda.SetDescription("Lambda build and artifact commands.");
    lambda.AddCommand<LambdaBuildCommand>("build")
        .WithDescription("Build the BadgeSmith Lambda ZIP or container image.")
        .WithExample("lambda", "build", "--target", "zip", "--rid", "linux-arm64", "--clean");
});
```

- [ ] **Step 5: Run validation**

Run:

```bash
dotnet build tools/badgesmith.cs
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~LambdaBuild"
```

Expected: both pass.

- [ ] **Step 6: Optional heavy verification**

Run only when Docker is available and Deniz explicitly agrees to run a Lambda build:

```bash
tools/badgesmith.cs lambda build --target zip --rid linux-arm64 --clean --verbose
```

Expected: `artifacts/badge-lambda-linux-arm64.zip` exists.

- [ ] **Step 7: Commit checkpoint**

Present this summary and proposed commit message, then ask for approval before committing:

```text
Summary: Add lambda build command with arm64 default, Docker Buildx invocation, ZIP existence check, help, and validation tests.
Proposed commit: build: migrate lambda build script to file-based tool
```

---

### Task 3: Test Runner Command

**Files:**

- Create: `tools/Commands/TestRunCommand.cs`
- Modify: `tools/badgesmith.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`

**Interfaces:**

- Consumes: `ProcessRunner`, `ToolExitCodes`.
- Produces: `TestRunCommand` registered as `tests run`.

- [ ] **Step 1: Write failing tests for `tests run` help and validation**

Append these tests:

```csharp
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
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```bash
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~TestsRun"
```

Expected: FAIL because `tests run` is not registered.

- [ ] **Step 3: Implement `tests run`**

Create `tools/Commands/TestRunCommand.cs`:

```csharp
using BadgeSmith.Tools.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace BadgeSmith.Tools.Commands;

internal sealed class TestRunCommand : AsyncCommand<TestRunSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, TestRunSettings settings)
    {
        var paths = new RepositoryPaths();
        var projectPath = Path.GetFullPath(Path.Combine(paths.RepositoryRoot, settings.ProjectPath));
        var resultsDir = Path.GetFullPath(Path.Combine(paths.RepositoryRoot, settings.ResultsDir));
        Directory.CreateDirectory(resultsDir);

        var runner = new ProcessRunner(AnsiConsole.Console, settings.Verbose);
        var tfmRaw = await GetMsBuildPropertyAsync(runner, projectPath, "TargetFrameworks", paths.RepositoryRoot).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tfmRaw))
        {
            tfmRaw = await GetMsBuildPropertyAsync(runner, projectPath, "TargetFramework", paths.RepositoryRoot).ConfigureAwait(false);
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
            ], paths.RepositoryRoot, allowNonZeroExit: true).ConfigureAwait(false);

            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        return ToolExitCodes.Success;
    }

    private static async Task<string> GetMsBuildPropertyAsync(ProcessRunner runner, string projectPath, string propertyName, string repositoryRoot)
    {
        var result = await runner.RunBufferedAsync("dotnet", [
            "msbuild", projectPath,
            $"-getProperty:{propertyName}",
            "-nologo",
            "-v:q"
        ], repositoryRoot).ConfigureAwait(false);

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
```

- [ ] **Step 4: Register the branch and command**

In `tools/badgesmith.cs`, add this inside `app.Configure`:

```csharp
config.AddBranch("tests", tests =>
{
    tests.SetDescription("Test execution and ingestion commands.");
    tests.AddCommand<TestRunCommand>("run")
        .WithDescription("Run a .NET test project once per target framework.")
        .WithExample("tests", "run", "--project-path", "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj", "--results-dir", "test-results");
});
```

If the `tests` branch already exists from a later merge, add `TestRunCommand` to that branch instead of creating a second branch.

- [ ] **Step 5: Run tests and targeted command verification**

Run:

```bash
dotnet build tools/badgesmith.cs
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~TestsRun"
dotnet build --configuration Release
tools/badgesmith.cs tests run --project-path tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --results-dir artifacts/tool-test-results --configuration Release
```

Expected: build passes, targeted tests pass, and `tests run` writes TRX files under `artifacts/tool-test-results`.

- [ ] **Step 6: Commit checkpoint**

Present this summary and proposed commit message, then ask for approval before committing:

```text
Summary: Add tests run command to replace run-dotnet-tests shell and PowerShell helper scripts.
Proposed commit: build: migrate test runner helper to file-based tool
```

---

### Task 4: HMAC Ingestion And Badge Update Commands

**Files:**

- Create: `tools/Infrastructure/HmacSigner.cs`
- Create: `tools/Infrastructure/GitHubActions.cs`
- Create: `tools/Infrastructure/TestResultPayloads.cs`
- Create: `tools/Commands/TestIngestCommand.cs`
- Create: `tools/Commands/BadgeUpdateCommand.cs`
- Modify: `tools/badgesmith.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`

**Interfaces:**

- Consumes: `ToolExitCodes` and `RepositoryPaths`.
- Produces: `HmacSigner.CreateSignature(string payload, string secret)` returning `sha256=<hex>`.
- Produces: `tests ingest` and `badge update` commands with `--dry-run` support.

- [ ] **Step 1: Write failing dry-run tests**

Append these tests:

```csharp
[Fact]
public async Task TestsIngest_Should_Dry_Run_Without_Posting_When_Dry_Run_Is_Set()
{
    var payload = "{\"platform\":\"Linux\",\"passed\":1,\"failed\":0,\"skipped\":0,\"total\":1,\"url_html\":\"https://example.com/run\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"commit\":\"abc123\",\"run_id\":\"1\",\"workflow_run_url\":\"https://example.com/workflow\"}";
    var result = await RunToolAsync("tests", "ingest", "--base-url", "https://example.com", "--owner", "LocalStack-DotNet", "--repo", "BadgeSmith", "--platform", "Linux", "--branch", "Main", "--secret", "test-secret", "--payload", payload, "--dry-run");

    Assert.Equal(0, result.ExitCode);
    Assert.Contains("DRY RUN", result.Output, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("https://example.com/tests/results/linux/localstack-dotnet/badgesmith/Main", result.Output, StringComparison.Ordinal);
    Assert.DoesNotContain("test-secret", result.Output, StringComparison.Ordinal);
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
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```bash
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~TestsIngest|FullyQualifiedName~BadgeUpdate"
```

Expected: FAIL because commands are not registered.

- [ ] **Step 3: Implement HMAC and payload infrastructure**

Create `tools/Infrastructure/HmacSigner.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace BadgeSmith.Tools.Infrastructure;

internal static class HmacSigner
{
    public static string CreateSignature(string payload, string secret)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(secret);

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return "sha256=" + Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
```

Create `tools/Infrastructure/TestResultPayloads.cs`:

```csharp
using System.Text.Json.Serialization;

namespace BadgeSmith.Tools.Infrastructure;

internal sealed record TestResultPayload(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("skipped")] int Skipped,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("url_html")] string UrlHtml,
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("commit")] string Commit,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("workflow_run_url")] string WorkflowRunUrl);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TestResultPayload))]
internal sealed partial class ToolJsonSerializerContext : JsonSerializerContext;
```

Create `tools/Infrastructure/GitHubActions.cs`:

```csharp
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
```

- [ ] **Step 4: Implement `tests ingest`**

Create `tools/Commands/TestIngestCommand.cs` with a settings class that accepts `--base-url`, `--owner`, `--repo`, `--platform`, `--branch`, `--secret`, `--payload-file`, `--payload`, `--dry-run`, and `--verbose`. Use this exact execution behavior:

```csharp
var payloadJson = settings.PayloadFile is { Length: > 0 }
    ? await File.ReadAllTextAsync(settings.PayloadFile, cancellationToken).ConfigureAwait(false)
    : settings.Payload!;

var owner = settings.Owner.ToLowerInvariant();
var repo = settings.Repo.ToLowerInvariant();
var platform = settings.Platform.ToLowerInvariant();
var branch = settings.Branch;
var url = $"{settings.BaseUrl.TrimEnd('/')}/tests/results/{platform}/{owner}/{repo}/{branch}";
var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
var nonce = Guid.NewGuid().ToString("N");
var signature = HmacSigner.CreateSignature(payloadJson, settings.Secret);

if (settings.DryRun)
{
    AnsiConsole.MarkupLine("[yellow]DRY RUN: request was not sent.[/]");
    AnsiConsole.WriteLine(url);
    AnsiConsole.WriteLine($"X-Timestamp: {timestamp}");
    AnsiConsole.WriteLine($"X-Nonce: {nonce}");
    AnsiConsole.WriteLine($"X-Signature: {signature}");
    return ToolExitCodes.Success;
}
```

For non-dry-run, use `HttpClient` and POST the exact payload string with `Content-Type: application/json`; return `ToolExitCodes.NetworkFailure` when the response is not successful and print the status code plus response body.

- [ ] **Step 5: Implement `badge update`**

Create `tools/Commands/BadgeUpdateCommand.cs`. It must:

- Parse repository as `owner/repo`.
- Compute `total = passed + failed + skipped`.
- Resolve branch from `--branch`, `GITHUB_HEAD_REF`, or `GITHUB_REF_NAME`.
- Build `TestResultPayload` and serialize with `ToolJsonSerializerContext.Default.TestResultPayload`.
- Lowercase platform, owner, and repo for URLs; preserve branch casing.
- Sign with `HmacSigner.CreateSignature`.
- In `--dry-run`, print the target URL, payload, badge URL, redirect URL, and signature metadata without printing the secret.
- In normal mode, POST with `HttpClient`.
- Preserve current behavior where badge update failure does not fail CI by default. Add `--fail-on-error` to opt into non-zero failure.

- [ ] **Step 6: Register commands**

In `tools/badgesmith.cs`, add `TestIngestCommand` to the existing `tests` branch:

```csharp
tests.AddCommand<TestIngestCommand>("ingest")
    .WithDescription("Post a test result payload to BadgeSmith.")
    .WithExample("tests", "ingest", "--base-url", "https://api.example.com", "--owner", "localstack-dotnet", "--repo", "badge-smith", "--platform", "linux", "--branch", "main", "--secret", "secret", "--payload-file", "payload.json", "--dry-run");
```

Add a `badge` branch:

```csharp
config.AddBranch("badge", badge =>
{
    badge.SetDescription("BadgeSmith badge update commands.");
    badge.AddCommand<BadgeUpdateCommand>("update")
        .WithDescription("Post GitHub Actions test results to BadgeSmith.")
        .WithExample("badge", "update", "--platform", "Linux", "--test-passed", "1", "--test-failed", "0", "--test-skipped", "0", "--repository", "localstack-dotnet/badge-smith", "--hmac-secret", "secret", "--dry-run");
});
```

- [ ] **Step 7: Run verification**

Run:

```bash
dotnet build tools/badgesmith.cs
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~TestsIngest|FullyQualifiedName~BadgeUpdate"
tools/badgesmith.cs tests ingest --help
tools/badgesmith.cs badge update --help
```

Expected: all pass and help output renders.

- [ ] **Step 8: Commit checkpoint**

Present this summary and proposed commit message, then ask for approval before committing:

```text
Summary: Add HMAC signing, dry-run ingestion, and badge update commands to replace script and workflow Bash logic.
Proposed commit: build: migrate badge update and ingestion tooling to C#
```

---

### Task 5: Org Secret Seeder Command And AppHost Migration

Task 4.5 inserts the hosted DI / LocalStack.Client refactor before continuing
`secrets seed`; see
`docs/superpowers/plans/2026-07-06-w1-5-task-4-5-tool-hosting-di-implementation-plan.md`.

**Files:**

- Create: `tools/Commands/SecretsSeedCommand.cs`
- Create: `tools/Infrastructure/OrgSecretSeeder.cs`
- Create: `tools/organization-pat-mapping.json.dist`
- Modify: `tools/badgesmith.cs`
- Modify: `src/BadgeSmith.Host/Program.cs`
- Modify: `src/BadgeSmith.Host/BadgeSmith.Host.csproj`
- Modify: `BadgeSmith.sln`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`
- Delete: `tests/seeders/BadgeSmith.DynamoDb.Seeders/BadgeSmith.DynamoDb.Seeders.csproj`
- Delete: `tests/seeders/BadgeSmith.DynamoDb.Seeders/Program.cs`
- Delete: `tests/seeders/BadgeSmith.DynamoDb.Seeders/OrgSecretSeeder.cs`
- Delete: `tests/seeders/BadgeSmith.DynamoDb.Seeders/organization-pat-mapping.json.dist`
- Delete: `tests/seeders/BadgeSmith.DynamoDb.Seeders/Properties/launchSettings.json`

**Interfaces:**

- Consumes: `ToolExitCodes` and AWS SDK packages referenced by `tools/badgesmith.cs`.
- Produces: `SecretsSeedCommand` registered as `secrets seed`.
- Produces: `OrgSecretSeeder.SeedAsync(...)` for creating/updating Secrets Manager secrets and DynamoDB org mapping rows.
- Produces: AppHost `AddCSharpApp("BadgeSmithDynamoDbSeeders", "../../tools/badgesmith.cs")` resource.

- [ ] **Step 1: Write failing dry-run and help tests**

Append these tests to `BadgeSmithToolCommandTests`:

```csharp
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
        """);

    try
    {
        var result = await RunToolAsync("secrets", "seed", "--config", configPath, "--table-name", "badge-smith-github-org-secrets", "--dry-run");

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
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```bash
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~SecretsSeed"
```

Expected: FAIL because `secrets seed` is not registered.

- [ ] **Step 3: Create the valid config template**

Create `tools/organization-pat-mapping.json.dist`:

```json
{
  "secrets": [
    {
      "org_name": "<org-name>",
      "name": "package",
      "secret": "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
      "type": "Package",
      "description": "Example GitHub Packages PAT"
    },
    {
      "org_name": "<org-name>",
      "name": "testdata",
      "secret": "your-hmac-secret-here",
      "type": "TestData",
      "description": "Example HMAC secret for test result ingestion"
    }
  ]
}
```

The actual local config file is `tools/organization-pat-mapping.json`. It remains ignored by the repository's existing `**/organization-pat-mapping.json` rule and must not be committed.

Secret names intentionally use the org-scoped format `badgesmith/github/{org}/{key}`. The old standalone seeder used the flat `badgesmith/github/{key}` format, while `scripts/perf-baseline-seed.sh` already uses the org-scoped format. W1.5 standardizes the persistent seeder on the org-scoped format; document this in `tools/README.md` so developers with old LocalStack data know to re-seed or move local secrets.

- [ ] **Step 4: Implement org secret seeding infrastructure**

Create `tools/Infrastructure/OrgSecretSeeder.cs`:

```csharp
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BadgeSmith.Tools.Infrastructure;

internal sealed class OrgSecretSeeder
{
    private readonly IAnsiConsole _console;
    private readonly bool _dryRun;

    public OrgSecretSeeder(IAnsiConsole console, bool dryRun)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _dryRun = dryRun;
    }

    public async Task<int> SeedAsync(string configPath, string tableName, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigurationAsync(configPath, cancellationToken).ConfigureAwait(false);
        if (config.Secrets.Length == 0)
        {
            _console.MarkupLine("[yellow]No secrets found in config.[/]");
            return ToolExitCodes.Success;
        }

        using var dynamoDb = CreateDynamoDbClient();
        using var secretsManager = CreateSecretsManagerClient();

        foreach (var secret in config.Secrets)
        {
            var normalized = Normalize(secret);
            if (_dryRun)
            {
                _console.MarkupLine("[yellow]DRY RUN: org secret would be seeded.[/]");
                _console.WriteLine($"PK: ORG#{normalized.OrgName}");
                _console.WriteLine($"SK: CONST#GITHUB#{normalized.Type}");
                _console.WriteLine($"SecretName: {normalized.SecretName}");
                continue;
            }

            await CreateOrUpdateSecretAsync(secretsManager, normalized.SecretName, secret.Secret, cancellationToken).ConfigureAwait(false);
            await PutMappingAsync(dynamoDb, tableName, normalized.OrgName, normalized.Type, normalized.SecretName, cancellationToken).ConfigureAwait(false);
            _console.MarkupLine($"[green]Seeded org mapping for {Markup.Escape(normalized.OrgName)} / {Markup.Escape(normalized.Type)}.[/]");
        }

        return ToolExitCodes.Success;
    }

    private static async Task<SecretConfig> LoadConfigurationAsync(string configPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Secret mapping config file was not found.", configPath);
        }

        var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, OrgSecretSeederJsonContext.Default.SecretConfig)
            ?? new SecretConfig([]);
    }

    private static NormalizedSecret Normalize(SecretInfo secret)
    {
        if (string.IsNullOrWhiteSpace(secret.OrgName))
        {
            throw new InvalidOperationException("Secret entry is missing org_name.");
        }

        if (string.IsNullOrWhiteSpace(secret.Name))
        {
            throw new InvalidOperationException("Secret entry is missing name.");
        }

        if (string.IsNullOrWhiteSpace(secret.Secret))
        {
            throw new InvalidOperationException($"Secret entry '{secret.Name}' is missing secret.");
        }

        if (string.IsNullOrWhiteSpace(secret.Type))
        {
            throw new InvalidOperationException($"Secret entry '{secret.Name}' is missing type.");
        }

        var orgName = secret.OrgName.ToLowerInvariant();
        var type = secret.Type.ToLowerInvariant();
        var keyName = secret.Name.ToLowerInvariant();
        return new NormalizedSecret(orgName, type, $"badgesmith/github/{orgName}/{keyName}");
    }

    private static IAmazonDynamoDB CreateDynamoDbClient()
    {
        var config = new AmazonDynamoDBConfig { RegionEndpoint = ResolveRegion() };
        ApplyServiceUrl(config);
        return new AmazonDynamoDBClient(ResolveCredentials(), config);
    }

    private static IAmazonSecretsManager CreateSecretsManagerClient()
    {
        var config = new AmazonSecretsManagerConfig { RegionEndpoint = ResolveRegion() };
        ApplyServiceUrl(config);
        return new AmazonSecretsManagerClient(ResolveCredentials(), config);
    }

    private static AWSCredentials ResolveCredentials()
    {
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
        {
            return new BasicAWSCredentials(accessKey, secretKey);
        }

        return FallbackCredentialsFactory.GetCredentials();
    }

    private static RegionEndpoint ResolveRegion()
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? "us-east-1";
        return RegionEndpoint.GetBySystemName(region);
    }

    private static void ApplyServiceUrl(ClientConfig config)
    {
        var endpoint = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            config.ServiceURL = endpoint;
            config.AuthenticationRegion = ResolveRegion().SystemName;
        }
    }

    private static async Task CreateOrUpdateSecretAsync(IAmazonSecretsManager secretsManager, string secretName, string value, CancellationToken cancellationToken)
    {
        try
        {
            await secretsManager.CreateSecretAsync(new CreateSecretRequest
            {
                Name = secretName,
                SecretString = value,
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ResourceExistsException)
        {
            await secretsManager.PutSecretValueAsync(new PutSecretValueRequest
            {
                SecretId = secretName,
                SecretString = value,
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task PutMappingAsync(IAmazonDynamoDB dynamoDb, string tableName, string orgName, string type, string secretName, CancellationToken cancellationToken)
    {
        return dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = tableName,
            Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new($"ORG#{orgName}"),
                ["SK"] = new($"CONST#GITHUB#{type}"),
                ["SecretName"] = new(secretName),
                ["CreatedAt"] = new(DateTime.UtcNow.ToString("O")),
            },
        }, cancellationToken);
    }

    private sealed record NormalizedSecret(string OrgName, string Type, string SecretName);
}

internal sealed record SecretConfig([property: JsonPropertyName("secrets")] SecretInfo[] Secrets);

internal sealed record SecretInfo(
    [property: JsonPropertyName("org_name")] string OrgName,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("secret")] string Secret,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description);

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SecretConfig))]
[JsonSerializable(typeof(SecretInfo))]
internal sealed partial class OrgSecretSeederJsonContext : JsonSerializerContext;
```

- [ ] **Step 5: Implement `secrets seed` command**

Create `tools/Commands/SecretsSeedCommand.cs`:

```csharp
using BadgeSmith.Tools.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace BadgeSmith.Tools.Commands;

internal sealed class SecretsSeedCommand : AsyncCommand<SecretsSeedSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SecretsSeedSettings settings)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var paths = new RepositoryPaths();
        var configPath = Path.IsPathRooted(settings.Config)
            ? settings.Config
            : paths.ResolveFromRoot(settings.Config);

        if (!File.Exists(configPath))
        {
            AnsiConsole.MarkupLine($"[red]Secret mapping config file was not found: {Markup.Escape(configPath)}[/]");
            AnsiConsole.MarkupLine("[yellow]Copy tools/organization-pat-mapping.json.dist to tools/organization-pat-mapping.json and fill in local secrets.[/]");
            return ToolExitCodes.ValidationFailure;
        }

        var tableName = settings.TableName;
        if (string.IsNullOrWhiteSpace(tableName))
        {
            tableName = Environment.GetEnvironmentVariable("AWS_RESOURCE_ORG_SECRETS_TABLE");
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            AnsiConsole.MarkupLine("[red]Org secrets table name is required. Use --table-name or AWS_RESOURCE_ORG_SECRETS_TABLE.[/]");
            return ToolExitCodes.ValidationFailure;
        }

        var seeder = new OrgSecretSeeder(AnsiConsole.Console, settings.DryRun);
        return await seeder.SeedAsync(configPath, tableName, cts.Token).ConfigureAwait(false);
    }
}

internal sealed class SecretsSeedSettings : CommandSettings
{
    [CommandOption("--config")]
    [Description("Path to organization PAT mapping JSON, relative to the repository root unless absolute.")]
    public string Config { get; init; } = "tools/organization-pat-mapping.json";

    [CommandOption("--table-name")]
    [Description("DynamoDB org secrets table name. Defaults to AWS_RESOURCE_ORG_SECRETS_TABLE.")]
    public string? TableName { get; init; }

    [CommandOption("--timeout-seconds")]
    [Description("Maximum seed duration in seconds.")]
    public int TimeoutSeconds { get; init; } = 300;

    [CommandOption("--dry-run")]
    [Description("Validate and display planned changes without writing to AWS.")]
    public bool DryRun { get; init; }

    public override ValidationResult Validate()
    {
        if (TimeoutSeconds <= 0)
        {
            return ValidationResult.Error("--timeout-seconds must be greater than zero.");
        }

        return ValidationResult.Success();
    }
}
```

- [ ] **Step 6: Register the command**

In `tools/badgesmith.cs`, add:

```csharp
config.AddBranch("secrets", secrets =>
{
    secrets.SetDescription("Secrets Manager and org mapping commands.");
    secrets.AddCommand<SecretsSeedCommand>("seed")
        .WithDescription("Seed GitHub org secret mappings into AWS resources.")
        .WithExample("secrets", "seed", "--config", "tools/organization-pat-mapping.json", "--table-name", "badge-smith-github-org-secrets", "--dry-run");
});
```

- [ ] **Step 7: Replace Aspire seeder project with `AddCSharpApp`**

Modify `src/BadgeSmith.Host/Program.cs`.

Add near the top after using directives:

```csharp
#pragma warning disable ASPIRECSHARPAPPS001 // AddCSharpApp is experimental in Aspire 13.
```

Replace the existing `builder.AddProject<Projects.BadgeSmith_DynamoDb_Seeders>` seeder block with:

```csharp
var secretMappingConfigPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../tools/organization-pat-mapping.json"));
```

Remove `.WaitFor(dynamoDbSeeder)` from the `badgeSmithApi` declaration chain. After `badgeSmithApi` is created, add the seeder only when the ignored local config exists:

```csharp
if (File.Exists(secretMappingConfigPath))
{
    var dynamoDbSeeder = builder.AddCSharpApp("BadgeSmithDynamoDbSeeders", "../../tools/badgesmith.cs")
        .WithArgs("secrets", "seed", "--config", secretMappingConfigPath, "--timeout-seconds", "300")
        .WithReference(awsConfig)
        .WithReference(badgeSmithStack)
        .WithEnvironment("AWS_RESOURCE_ORG_SECRETS_TABLE", badgeSmithStack.GetOutput(OrgSecretsOutputTableName))
        .ExcludeFromManifest();

    badgeSmithApi.WaitFor(dynamoDbSeeder);
}
```

`CSharpAppResource` derives from `ProjectResource`, so LocalStack auto-wiring follows the same ProjectResource path as `AddProject` when the resource has `.WithReference(badgeSmithStack)`. Do not remove the AWS stack reference; without it, neither `AddProject` nor `AddCSharpApp` is discovered as an AWS consumer automatically.

If `AddCSharpApp` does not accept this file path or argument shape during implementation, use this fallback executable resource inside the same `File.Exists(secretMappingConfigPath)` block and record why in the implementation notes:

```csharp
var dynamoDbSeeder = builder.AddExecutable("BadgeSmithDynamoDbSeeders", "dotnet", builder.AppHostDirectory)
    .WithArgs("run", "--file", "../../tools/badgesmith.cs", "--", "secrets", "seed", "--config", secretMappingConfigPath, "--timeout-seconds", "300")
    .WithReference(awsConfig)
    .WithReference(badgeSmithStack)
    .WithEnvironment("AWS_RESOURCE_ORG_SECRETS_TABLE", badgeSmithStack.GetOutput(OrgSecretsOutputTableName))
    .ExcludeFromManifest();

badgeSmithApi.WaitFor(dynamoDbSeeder);
```

- [ ] **Step 8: Remove old seeder project references**

Remove this line from `src/BadgeSmith.Host/BadgeSmith.Host.csproj`:

```xml
<ProjectReference Include="..\..\tests\seeders\BadgeSmith.DynamoDb.Seeders\BadgeSmith.DynamoDb.Seeders.csproj" />
```

Remove the `BadgeSmith.DynamoDb.Seeders` project from `BadgeSmith.sln` using a non-interactive command:

```bash
dotnet sln BadgeSmith.sln remove tests/seeders/BadgeSmith.DynamoDb.Seeders/BadgeSmith.DynamoDb.Seeders.csproj
```

- [ ] **Step 9: Delete old seeder project files**

Before deleting the old seeder directory, check whether `tests/seeders/BadgeSmith.DynamoDb.Seeders/organization-pat-mapping.json` exists locally. If it exists, move or copy it to `tools/organization-pat-mapping.json` and keep it untracked. Do not print or commit its contents.

Delete the files listed in this task's delete list. Do not delete a local `organization-pat-mapping.json` if it exists and is ignored; it may contain local secrets and must not be committed.

- [ ] **Step 10: Run verification**

Run:

```bash
dotnet build tools/badgesmith.cs
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~SecretsSeed"
tools/badgesmith.cs secrets seed --help
dotnet build --configuration Release
```

Expected: all pass.

- [ ] **Step 11: Optional Aspire smoke**

Run only when Docker/LocalStack local dev dependencies are available and Deniz agrees to start the AppHost:

```bash
aspire start --apphost src/BadgeSmith.Host/BadgeSmith.Host.csproj --non-interactive
```

Expected: when `tools/organization-pat-mapping.json` exists, Aspire starts `BadgeSmithDynamoDbSeeders` as a C# file-based app resource and `BadgeSmithApi` waits for it. When the ignored local config is absent, Aspire starts without the seeder resource and does not block API startup.

- [ ] **Step 12: Commit checkpoint**

Present this summary and proposed commit message, then ask for approval before committing:

```text
Summary: Move the local org secret seeder into the file-based BadgeSmith tool and wire Aspire to run it with AddCSharpApp.
Proposed commit: build: migrate DynamoDB seeder to file-based tool
```

---

### Task 6: Performance Baseline Command And LocalStack Seeder

**Files:**

- Create: `tools/Infrastructure/LocalStackSeeder.cs`
- Create: `tools/Commands/PerfBaselineCommand.cs`
- Modify: `tools/badgesmith.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`

**Interfaces:**

- Consumes: `ProcessRunner`, `RepositoryPaths`, `ToolExitCodes`.
- Produces: `perf baseline` command that replaces `perf-baseline.sh`, `perf-baseline.ps1`, and `perf-baseline-seed.sh`.

- [ ] **Step 1: Write failing help and validation tests**

Append these tests:

```csharp
[Fact]
public async Task PerfBaseline_Should_Print_Help_When_Invoked_With_Help()
{
    var result = await RunToolAsync("perf", "baseline", "--help");

    Assert.Equal(0, result.ExitCode);
    Assert.Contains("--label", result.Output, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("--upstream", result.Output, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("--arch", result.Output, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public async Task PerfBaseline_Should_Reject_Invalid_Upstream_When_Value_Is_Not_Supported()
{
    var result = await RunToolAsync("perf", "baseline", "--upstream", "invalid");

    Assert.NotEqual(0, result.ExitCode);
    Assert.Contains("mock", result.Output, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("real", result.Output, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```bash
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~PerfBaseline"
```

Expected: FAIL because `perf baseline` is not registered.

- [ ] **Step 3: Implement LocalStack seeder**

Create `tools/Infrastructure/LocalStackSeeder.cs` with methods:

```csharp
internal sealed class LocalStackSeeder
{
    public LocalStackSeeder(ProcessRunner runner, string networkName, string githubToken);
    public Task SeedAsync(CancellationToken cancellationToken = default);
}
```

The implementation must preserve these seed records from `scripts/perf-baseline-seed.sh`:

```text
create table badge-smith-hmac-nonce with PK/SK
create table badge-smith-github-org-secrets with PK/SK
create table badge-smith-test-result with PK/SK and GSI1(GSI1PK/GSI1SK)
create secret badgesmith/github/test-org/testdata = contract-test-secret
create secret badgesmith/github/test-org/package = $GITHUB_TOKEN or dummy-github-pat
create secret badgesmith/github/localstack-dotnet/package = $GITHUB_TOKEN or dummy-github-pat
put org secret rows for test-org/testdata, test-org/package, localstack-dotnet/package
put five test result rows for localstack-dotnet/localstack.client, microsoft/vscode, facebook/react, dotnet/aspnetcore, AutoMapper/AutoMapper
```

Use AWS CLI through CliWrap. Prefer host AWS CLI when `docker port bs-perf-ls 4566/tcp` returns a port and `aws` is available; otherwise run `amazon/aws-cli:2.17.62` inside the benchmark Docker network.

- [ ] **Step 4: Implement `perf baseline` command**

Create `tools/Commands/PerfBaselineCommand.cs`. It must preserve these behaviors from the current script:

- Options: `--label`, `--upstream mock|real`, `--arch amd64|arm64`.
- Environment: `K6_VUS` default `1`, `K6_DURATION` default `60s`, `GITHUB_TOKEN` required for `--upstream real`.
- Build artifacts with Docker targets `export-mstat` and `export-zip`.
- Start LocalStack container `bs-perf-ls` on network `bs-perf-net`.
- Start WireMock container `bs-perf-wm` for mock upstream.
- Deploy the local performance CDK stack with `npx -y -p aws-cdk-local@3.0.4 -p aws-cdk@2.1129.0 cdklocal` from the `build` directory.
- Read `BadgeSmithApiUrl`; fallback to `BadgeSmithLambdaFunctionUrl` when API Gateway URL is `unknown`.
- Seed DynamoDB and Secrets Manager using `LocalStackSeeder`.
- Wait for `/health`.
- Run k6 with `scripts/k6-perf-test.js` and `--summary-export artifacts/k6-summary.json`.
- Validate k6 aggregate checks rate and fail count.
- Write `docs/research/baselines/<UTC-date>-<label>.json` with image, boot, k6, and memory fields matching current baseline schema.
- Write LocalStack/WireMock logs under `artifacts/` on failure.
- Clean up containers and Docker network in `finally`.

Keep `scripts/k6-perf-test.js` as the k6 scenario file; do not migrate it in W1.5.

- [ ] **Step 5: Register the command**

In `tools/badgesmith.cs`, add:

```csharp
config.AddBranch("perf", perf =>
{
    perf.SetDescription("Performance baseline commands.");
    perf.AddCommand<PerfBaselineCommand>("baseline")
        .WithDescription("Run a LocalStack-backed Lambda performance baseline.")
        .WithExample("perf", "baseline", "--label", "localstack-smoke", "--upstream", "mock", "--arch", "arm64");
});
```

- [ ] **Step 6: Run cheap verification**

Run:

```bash
dotnet build tools/badgesmith.cs
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~PerfBaseline"
tools/badgesmith.cs perf baseline --help
```

Expected: all pass.

- [ ] **Step 7: Optional heavy smoke**

Run only when Docker is available and Deniz agrees to execute local performance infrastructure:

```bash
K6_DURATION=10s K6_VUS=1 tools/badgesmith.cs perf baseline --label w1-5-localstack-smoke --upstream mock --arch arm64
```

Expected: command exits `0`, writes a baseline JSON file under `docs/research/baselines/`, and cleans up `bs-perf-net`, `bs-perf-ls`, and `bs-perf-wm`.

- [ ] **Step 8: Commit checkpoint**

Present this summary and proposed commit message, then ask for approval before committing:

```text
Summary: Add the perf baseline command and LocalStack seeder to replace performance shell scripts.
Proposed commit: build: migrate performance baseline tooling to C#
```

---

### Task 7: Workflow Migration And Script Deletion

**Files:**

- Modify: `.github/workflows/ci-cd.yml`
- Modify: `.github/workflows/deploy.yml`
- Modify: `.github/workflows/run-dotnet-tests/action.yml`
- Modify: `.github/workflows/update-test-badge/action.yml`
- Delete: `.github/workflows/run-dotnet-tests/run-unix.sh`
- Delete: `.github/workflows/run-dotnet-tests/run-win.ps1`
- Delete: `scripts/build-lambda.sh`
- Delete: `scripts/build-lambda.ps1`
- Delete: `scripts/perf-baseline.sh`
- Delete: `scripts/perf-baseline.ps1`
- Delete: `scripts/perf-baseline-seed.sh`
- Delete: `scripts/test-ingestion.sh`
- Delete: `scripts/test-ingestion.ps1`
- Delete: remaining tracked files under `tests/seeders/BadgeSmith.DynamoDb.Seeders/`

**Interfaces:**

- Consumes: all commands implemented in Tasks 2 through 5.
- Produces: workflows with no tracked `.sh` or `.ps1` dependencies.

- [ ] **Step 1: Update CI Lambda build invocation**

In `.github/workflows/ci-cd.yml`, update the SDK setup step to install from `global.json`:

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v5
  with:
    global-json-file: ./global.json
```

In `.github/workflows/ci-cd.yml`, replace:

```yaml
run: |
  ./scripts/build-lambda.sh --target zip --rid linux-arm64 --clean --verbose
```

with:

```yaml
run: ${{ github.workspace }}/tools/badgesmith.cs lambda build --target zip --rid linux-arm64 --clean --verbose
```

- [ ] **Step 2: Update deploy Lambda build invocation**

In `.github/workflows/deploy.yml`, update the SDK setup step to install from `global.json`:

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v5
  with:
    global-json-file: ./global.json
```

In `.github/workflows/deploy.yml`, replace:

```yaml
run: |
  ./scripts/build-lambda.sh --target zip --rid linux-arm64 --clean --verbose
```

with:

```yaml
run: ${{ github.workspace }}/tools/badgesmith.cs lambda build --target zip --rid linux-arm64 --clean --verbose
```

- [ ] **Step 3: Keep CDK commands direct but remove unnecessary multiline blocks**

In `.github/workflows/deploy.yml`, keep direct CDK commands. Convert CDK diff and `cdk ls` to single-line commands where possible:

```yaml
- name: CDK diff
  if: inputs.show_diff
  working-directory: build
  run: cdk diff --all
  continue-on-error: true
```

```yaml
- name: Get deployment outputs
  working-directory: build
  run: cdk ls --long
  continue-on-error: true
```

Keep `cdk deploy --all --require-approval never` direct.

- [ ] **Step 4: Replace run-dotnet-tests action internals**

Replace `.github/workflows/run-dotnet-tests/action.yml` steps with:

```yaml
runs:
  using: "composite"
  steps:
    - if: runner.os == 'Windows'
      shell: pwsh
      run: dotnet run --file "${{ github.workspace }}\tools\badgesmith.cs" -- tests run --project-path "${{ inputs.project-path }}" --results-dir "${{ inputs.results-dir }}" --configuration "${{ inputs.configuration }}"

    - if: runner.os != 'Windows'
      shell: bash
      run: ${{ github.workspace }}/tools/badgesmith.cs tests run --project-path "${{ inputs.project-path }}" --results-dir "${{ inputs.results-dir }}" --configuration "${{ inputs.configuration }}"
```

- [ ] **Step 5: Replace update-test-badge action internals**

Replace the composite action steps in `.github/workflows/update-test-badge/action.yml` with OS-specific thin wrappers. Preserve existing inputs.

Unix step:

```yaml
- if: runner.os != 'Windows'
  name: 'Post Test Results to BadgeSmith API'
  shell: bash
  run: ${{ github.workspace }}/tools/badgesmith.cs badge update --platform "${{ inputs.platform }}" --test-passed "${{ inputs.test_passed }}" --test-failed "${{ inputs.test_failed }}" --test-skipped "${{ inputs.test_skipped }}" --test-url-html "${{ inputs.test_url_html }}" --commit-sha "${{ inputs.commit_sha }}" --run-id "${{ inputs.run_id }}" --repository "${{ inputs.repository }}" --server-url "${{ inputs.server_url }}" --api-domain "${{ inputs.api_domain }}" --hmac-secret "${{ inputs.hmac_secret }}"
```

Windows step:

```yaml
- if: runner.os == 'Windows'
  name: 'Post Test Results to BadgeSmith API'
  shell: pwsh
  run: dotnet run --file "${{ github.workspace }}\tools\badgesmith.cs" -- badge update --platform "${{ inputs.platform }}" --test-passed "${{ inputs.test_passed }}" --test-failed "${{ inputs.test_failed }}" --test-skipped "${{ inputs.test_skipped }}" --test-url-html "${{ inputs.test_url_html }}" --commit-sha "${{ inputs.commit_sha }}" --run-id "${{ inputs.run_id }}" --repository "${{ inputs.repository }}" --server-url "${{ inputs.server_url }}" --api-domain "${{ inputs.api_domain }}" --hmac-secret "${{ inputs.hmac_secret }}"
```

- [ ] **Step 6: Delete script files**

Delete all files listed in this task's delete list. Do not delete `scripts/k6-perf-test.js`, `scripts/localstack.yml`, or JSON sample files in this task unless they are separately addressed by docs cleanup.

- [ ] **Step 7: Verify there are no tracked shell or PowerShell files**

Run:

```bash
git ls-files "*.sh" "*.ps1"
```

Expected: no output.

- [ ] **Step 8: Verify workflows no longer reference deleted files**

Run:

```bash
rg -n "\.sh|\.ps1|scripts/build-lambda|scripts/perf-baseline|scripts/test-ingestion|run-unix|run-win" .github scripts docs AGENTS.md ARCHITECTURE.md README.md
```

Expected: no current-facing references. Historical docs under `docs/plans/`, `docs/research/`, and `docs/agents/handover-prompts/` may still contain historical references; review results and only edit current-facing docs in Task 8.

- [ ] **Step 9: Run workflow-facing smoke tests**

Run:

```bash
dotnet build tools/badgesmith.cs
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeSmithToolCommandTests"
```

Expected: build and tests pass.

- [ ] **Step 10: Commit checkpoint**

Present this summary and proposed commit message, then ask for approval before committing:

```text
Summary: Update workflows to call the file-based BadgeSmith tool and remove tracked shell/PowerShell scripts.
Proposed commit: build: replace shell workflow helpers with file-based tool
```

---

### Task 8: Current Documentation And Final Verification

**Files:**

- Create: `tools/README.md`
- Modify: `AGENTS.md`
- Modify: `ARCHITECTURE.md`
- Modify: `README.md`
- Modify: `docs/ROADMAP.md`
- Modify or delete: `scripts/README-TEST-INGESTION.md`
- Modify or delete: `scripts/README-PERF-TESTING.md`

**Interfaces:**

- Consumes: final CLI commands and workflow migration.
- Produces: current documentation that points to `tools/badgesmith.cs` instead of `.sh` or `.ps1` files.

- [ ] **Step 1: Create tool README**

Create `tools/README.md`:

````markdown
# BadgeSmith Tools

BadgeSmith repository tooling is implemented as a .NET 10 file-based app.

## Requirements

- .NET SDK 10.0.301 or newer.
- Docker for Lambda artifact builds and local performance baselines.
- k6 for performance baseline runs.
- AWS CLI for host-side LocalStack seeding when available; otherwise the tool uses the AWS CLI container.

## Unix, Linux, And macOS

Run the executable directly:

```bash
./tools/badgesmith.cs --help
./tools/badgesmith.cs lambda build --target zip --rid linux-arm64 --clean
./tools/badgesmith.cs tests run --project-path tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --results-dir test-results --configuration Release
./tools/badgesmith.cs secrets seed --config tools/organization-pat-mapping.json --dry-run
```

## Windows

Run through `dotnet run --file`:

```powershell
dotnet run --file tools/badgesmith.cs -- --help
dotnet run --file tools/badgesmith.cs -- lambda build --target zip --rid linux-arm64 --clean
dotnet run --file tools/badgesmith.cs -- secrets seed --config tools\organization-pat-mapping.json --dry-run
```

## Safe Dry Runs

Use dry-run modes to validate request generation without mutating BadgeSmith data:

```bash
./tools/badgesmith.cs tests ingest --base-url https://api.example.com --owner localstack-dotnet --repo badge-smith --platform linux --branch main --secret example --payload-file scripts/sample-test-payload.json --dry-run
./tools/badgesmith.cs badge update --platform Linux --test-passed 1 --test-failed 0 --test-skipped 0 --repository localstack-dotnet/badge-smith --hmac-secret example --dry-run
./tools/badgesmith.cs secrets seed --config tools/organization-pat-mapping.json --table-name badge-smith-github-org-secrets --dry-run
```

## Local Secret Mapping Config

`tools/organization-pat-mapping.json.dist` is the tracked example. Copy it to
`tools/organization-pat-mapping.json` for local development and keep the real file
untracked; the repository ignores `**/organization-pat-mapping.json`.

`secrets seed` defaults to `tools/organization-pat-mapping.json` when `--config` is not
provided. The command fails validation if the selected JSON file is missing.

Seeded secret names use `badgesmith/github/{org}/{key}`. If an older LocalStack setup
has secrets named `badgesmith/github/{key}`, re-run `secrets seed` or move those local
secrets to the org-scoped names.

````

- [ ] **Step 2: Update `AGENTS.md` current instructions**

Replace current `scripts/build-lambda.*` references with `tools/badgesmith.cs lambda build`. Preserve approval gates for Lambda publish/deploy and AWS mutation.

- [ ] **Step 3: Update architecture and README references**

In `ARCHITECTURE.md`, replace tooling bullets for `build-lambda.sh/.ps1`, `test-ingestion.sh/.ps1`, and `perf-baseline.sh` with `tools/badgesmith.cs` commands.

In `README.md`, update composite action examples only if the caller-facing API changed. If the composite action API is preserved, add a short note that the action delegates to `tools/badgesmith.cs`.

- [ ] **Step 4: Consolidate stale script docs**

Move still-current ingestion examples to `tools/README.md` and delete `scripts/README-TEST-INGESTION.md` if all useful content is represented.

Move still-current performance usage to `tools/README.md` or current performance docs and delete `scripts/README-PERF-TESTING.md` if all useful content is represented.

- [ ] **Step 5: Update roadmap**

Update `docs/ROADMAP.md` so W1.5 appears under the active Wave 1 workstream or as a completed row after implementation verification. When `lambda build` defaults to `linux-arm64` and workflow builds use it, remove or mark complete the Wave 1 backlog item about the build-script RID default vs. CDK arm64 expectation.

- [ ] **Step 6: Run final verification**

Run:

```bash
dotnet --version
dotnet build tools/badgesmith.cs
dotnet build --configuration Release
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category=Unit"
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category=Functional"
git ls-files "*.sh" "*.ps1"
rg -n "\.sh|\.ps1|scripts/build-lambda|scripts/perf-baseline|scripts/test-ingestion|run-unix|run-win|BadgeSmith\.DynamoDb\.Seeders|BadgeSmith_DynamoDb_Seeders|tests/seeders" AGENTS.md ARCHITECTURE.md README.md .github scripts tools docs/ROADMAP.md src/BadgeSmith.Host BadgeSmith.sln
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"
```

Expected:

- `dotnet --version` is `10.0.301` or newer.
- Tool and solution builds pass.
- Unit and functional tests pass.
- `git ls-files "*.sh" "*.ps1"` prints no files.
- `rg` finds no current-facing stale references. Historical docs under `docs/plans/`, `docs/research/`, and `docs/agents/handover-prompts/` can retain historical references; if the command above reports historical files, rerun a narrower current-doc command and record the historical exception.
- Slopwatch reports no warnings, or if Slopwatch is unavailable, record the command failure and reason.

- [ ] **Step 7: Commit checkpoint**

Present this summary and proposed commit message, then ask for approval before committing:

```text
Summary: Update current documentation for the file-based BadgeSmith tool and verify no tracked shell or PowerShell scripts remain.
Proposed commit: docs: document file-based BadgeSmith tooling
```

---

## Self-Review Notes

- Spec coverage: the plan covers the single file-based CLI, `#:include`, Spectre.Console.Cli, CliWrap, Unix shebang execution, Windows `dotnet run --file`, workflow thin wrappers, no tracked `.sh`/`.ps1`, dry-run support, current docs, and final verification.
- Scope check: this is one coherent tooling migration. CDK command semantics and k6 scenario migration are explicitly out of scope.
- Type consistency: command and infrastructure names are defined before use in later tasks.
- Approval gate: commit steps are written as approval checkpoints rather than automatic commits, matching `AGENTS.md`.
