# CliWrap Script Replacement For BadgeSmith Tools

Date: 2026-07-06

Historical research note for the completed W1.5 file-based tooling migration. Current
command behavior lives in `tools/README.md` and source.

## Sources

- CliWrap README: `https://github.com/Tyrrrz/CliWrap`
- CliWrap v3 usage and migration guidance.
- Context7 library lookup: `/tyrrrz/cliwrap`.
- BadgeSmith script inventory under `scripts/` and `.github/workflows/`.

## Recommended Patterns

Use CliWrap for external process execution instead of `Process.Start`, shell scripts, or
manual command strings.

Pass arguments as arrays or builders. This avoids shell quoting rules and command injection
risks.

Use streaming execution for long-running commands such as `docker buildx build`, `k6 run`,
and CDK local performance-stack steps that need live diagnostics.

Use buffered execution for commands whose output is parsed, such as `git rev-parse`,
`docker port`, `docker stats --format`, and AWS CLI commands that emit JSON.

Use `WithWorkingDirectory` instead of `pushd` and `popd`.

Use `WithEnvironmentVariables` for AWS, CDK, LocalStack, and test-specific environment
overrides. Only clear inherited environment values when there is a concrete reason.

Use C# native APIs instead of process calls where practical. Replace `curl` with
`HttpClient`, `date` with `DateTimeOffset.UtcNow`, `uuidgen` with `Guid.NewGuid()`, and
inline Python/JQ JSON manipulation with `System.Text.Json`.

## Example Streaming Command

```csharp
await Cli.Wrap("docker")
    .WithArguments([
        "buildx", "build",
        "-f", dockerfile,
        "--target", "export-zip",
        "--build-arg", $"RID={rid}",
        "--platform", platform,
        "--output", $"type=local,dest={outDir}",
        context
    ])
    .ExecuteAsync(cancellationToken);
```

For commands with important real-time output, wrap streaming in a `ProcessRunner` helper so
stdout and stderr are consistently written to the console and optional log files.

## Example Buffered Command

```csharp
var result = await Cli.Wrap("git")
    .WithArguments(["rev-parse", "--short", "HEAD"])
    .ExecuteBufferedAsync(cancellationToken);

var shortSha = result.StandardOutput.Trim();
```

## Example Environment And Working Directory

```csharp
await Cli.Wrap("npx")
    .WithWorkingDirectory(Path.Combine(repoRoot, "build"))
    .WithArguments([
        "-y",
        "-p", "aws-cdk-local@3.0.4",
        "-p", "aws-cdk@2.1129.0",
        "cdklocal",
        "deploy",
        "BadgeSmithPerformanceStack",
        "--require-approval", "never"
    ])
    .WithEnvironmentVariables(env => env
        .Set("AWS_ACCESS_KEY_ID", "test")
        .Set("AWS_SECRET_ACCESS_KEY", "test")
        .Set("AWS_DEFAULT_REGION", "us-east-1")
        .Set("AWS_REGION", "us-east-1")
        .Set("AWS_ENDPOINT_URL", $"http://localhost:{localStackPort}")
        .Set("CDK_DEFAULT_ACCOUNT", "000000000000")
        .Set("CDK_DEFAULT_REGION", "us-east-1")
        .Set("LOCALSTACK_HOST", $"localhost:{localStackPort}"))
    .ExecuteAsync(cancellationToken);
```

This pattern is for BadgeSmith-specific orchestration such as local performance-stack
deployment. Generic production `cdk synth`, `cdk diff`, and `cdk deploy` workflow steps do
not need wrapping when they are already one-line commands.

## ProcessRunner Shape

A small `ProcessRunner` should provide two paths:

- `RunStreamingAsync` for long-running commands where logs matter.
- `RunBufferedAsync` for commands whose output is parsed.

The helper should support executable name, argument collection, optional working directory,
optional environment overrides, verbose logging, cancellation, and clear exit-code handling.

Default CliWrap validation should fail on non-zero exit codes. Opt out with explicit
validation only for commands where non-zero status is an expected branch.

## Path Handling

Use `Path.Combine`, absolute repository paths, and `Directory.CreateDirectory` rather than
shell path manipulation. The .NET tool runs natively on Windows and Unix, so much of the
existing Bash WSL path translation can disappear.

When a child tool requires a host path in a specific format, isolate that conversion in one
helper and keep call sites clean.

## Temporary Files And Artifacts

Use `Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())` for process coordination
paths when the file should not be created immediately. `Path.GetTempFileName()` creates a
0-byte file as a side effect, so reserve it for cases where that behavior is required. Use
the repository `artifacts/` directory for intentional outputs such as k6 summaries,
LocalStack logs, and Lambda ZIP artifacts.

Always clean transient files in `finally` blocks. Keep diagnostic artifacts on failure when
they help troubleshooting.

## Anti-Patterns To Avoid

- Do not use `bash -c`, `cmd /C`, or PowerShell as a generic process wrapper.
- Do not build raw argument strings that rely on shell escaping.
- Do not set `CommandResultValidation.None` globally.
- Do not ignore stderr; Docker and similar tools often write useful progress or warnings
  there.
- Do not parse human output if a tool offers JSON or `--format` output.
- Do not call external tools for behavior that has a simple .NET API equivalent.

## BadgeSmith Migration Mapping

| Current script behavior | Preferred C# replacement |
| --- | --- |
| `docker buildx build ...` | CliWrap streaming command |
| `k6 run --summary-export ...` | CliWrap streaming command with summary path |
| `git rev-parse --short HEAD` | CliWrap buffered command |
| `docker port` and `docker stats --format` | CliWrap buffered command |
| AWS CLI table and secret operations | CliWrap buffered or streaming command with explicit LocalStack env |
| `curl -X POST` for signed ingestion | `HttpClient` |
| shell `date` | `DateTimeOffset.UtcNow` |
| `uuidgen` | `Guid.NewGuid().ToString("N")` |
| inline Python/JQ JSON processing | `System.Text.Json` |
| `trap cleanup EXIT` | `try/finally` |
