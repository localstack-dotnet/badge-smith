# Spectre.Console.Cli Usage For BadgeSmith Tools

Date: 2026-07-06

Historical research note for the completed W1.5 file-based tooling migration. Current
command behavior lives in `tools/README.md` and source.

## Sources

- Spectre.Console.Cli documentation: `https://spectreconsole.net/cli/`
- Spectre.Console.Cli multi-command tutorial and command app configuration guidance.
- Spectre.Console.Cli command lifecycle, async command, validation, help text, error handling, and testing guidance.
- Context7 library lookup: `/spectreconsole/spectre.console.cli`.

## Recommended Patterns

Use `CommandApp` with branches rather than one default command. BadgeSmith needs a command
tree with `lambda`, `perf`, `tests`, and `badge` branches.

Use `AsyncCommand<TSettings>` for every command because most work involves file I/O,
process execution, or HTTP requests.

Use `CommandSettings` classes for command arguments and options. Keep simple validation in
`CommandSettings.Validate()` and command-aware validation in the command's `Validate()`
override.

Configure help and examples on registration. Use descriptions, examples, and default value
display so the CLI replaces the help text that currently lives in shell scripts.

Use a central exception handler for consistent exit codes and user-facing errors. Command
implementations should catch only expected, local conditions they can handle.

Prefer injected `IAnsiConsole` or a thin console abstraction over static `AnsiConsole` in
command bodies when it improves testability. Do not introduce a broad dependency injection
framework unless command construction needs it.

If DI is needed, use a small local `ITypeRegistrar` implementation. Avoid adding
`Spectre.Console.Cli.Extensions.DependencyInjection` unless it provides a concrete benefit.

## File-Based App Shape

The selected W1.5 shape is not a traditional `.csproj` tool. It is a .NET 10 file-based app
with includes:

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:package Spectre.Console.Cli
#:include Commands/**/*.cs
#:include Infrastructure/**/*.cs
```

This keeps the tool lightweight while avoiding a single oversized source file.

## Example Command Registration

```csharp
var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("badgesmith");
    config.Settings.ShowOptionDefaultValues = true;
    config.Settings.CaseSensitivity = CaseSensitivity.None;

    config.AddBranch("lambda", lambda =>
    {
        lambda.AddCommand<LambdaBuildCommand>("build")
            .WithDescription("Build the BadgeSmith Lambda ZIP or container image.")
            .WithExample("lambda", "build", "--target", "zip", "--rid", "linux-arm64", "--clean");
    });

    config.AddBranch("tests", tests =>
    {
        tests.AddCommand<TestRunCommand>("run")
            .WithDescription("Run a test project once per target framework.");
        tests.AddCommand<TestIngestCommand>("ingest")
            .WithDescription("Post test result payloads to BadgeSmith.");
    });
});

return await app.RunAsync(args);
```

## Example Settings Validation

```csharp
public sealed class LambdaBuildSettings : CommandSettings
{
    [CommandOption("--target")]
    [Description("Build target: zip, image, or both.")]
    public string Target { get; init; } = "zip";

    [CommandOption("--rid")]
    [Description("Runtime identifier: linux-arm64 or linux-x64.")]
    public string Rid { get; init; } = "linux-arm64";

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

## Exit Codes

Use small, stable exit-code conventions:

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | General command failure |
| `2` | Input or validation failure |
| `3` | External process failure |
| `4` | Network or HTTP failure |
| `130` | Cancellation |

## Anti-Patterns To Avoid

- Do not put all validation inside `ExecuteAsync`.
- Do not use synchronous `.Result` or `.Wait()` over async APIs.
- Do not use static console calls everywhere if a command needs testability.
- Do not hide many unrelated behaviors behind one command with mode flags when a branch
  and subcommand would be clearer.
- Do not convert W1.5 into a traditional `.csproj` tool unless file-based structure becomes
  a proven maintenance problem.

## Notes For Implementation

Add `Spectre.Console.Cli` to `Directory.Packages.props`. Add testing-specific Spectre
packages only if command parsing tests are included in this work.

Keep generated help output as the replacement for removed shell-script help text.

Treat the traditional project recommendation from generic Spectre examples as rejected for
W1.5. BadgeSmith's selected design is file-based app plus `#:include`.
