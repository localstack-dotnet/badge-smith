# W1.5 Task 4.5 Tool Hosting, DI, And AWS Client Implementation Plan

Status: Completed/historical. Do not execute this checklist. Current CLI commands live
in `tools/README.md`, package versions live in `Directory.Packages.props`, and current
workstream status lives in `docs/ROADMAP.md`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the BadgeSmith file-based tool to hosted DI with PathSmith-style Spectre logging, LocalStack.Client-backed AWS clients, and linked-source command tests while keeping `tools/badgesmith.cs` a true file-based app.

**Architecture:** `tools/badgesmith.cs` remains the executable file-based app and composes implementation files with `#:include`. `BadgeSmithTool.RunAsync` builds a hosted service graph, wires Spectre command creation through DI, and keeps command implementation testable through linked source files in `tests/BadgeSmith.Api.Tests`. AWS-aware commands resolve effective LocalStack/live AWS options after Spectre parses command settings, then obtain AWS clients from a LocalStack.Client-backed factory.

**Tech Stack:** .NET SDK 10.0.301+, C# 14 file-based apps, Microsoft.Extensions.Hosting, Spectre.Console.Cli, Spectre.Console.Cli.Testing, LocalStack.Client.Extensions 2.0.0, AWS SDK v4, CliWrap, xUnit v3 on VSTest.

## Global Constraints

- Keep `tools/badgesmith.cs` as the executable file-based app, not a project shim.
- Do not add `tools/BadgeSmith.Tools.csproj` or any other production tool project.
- Command implementation, services, configuration, logging, and AWS client wiring remain source files included by `tools/badgesmith.cs` through `#:include` directives.
- Use `Microsoft.Extensions.Hosting` and DI as the single composition model.
- Use `LocalStack.Client.Extensions` for AWS client registration and configuration.
- `LocalStack:UseLocalStack=true` wins over live AWS profile settings.
- Live AWS uses `AWS:Profile`, `AWS:Region`, and the normal AWS SDK credential chain when LocalStack is disabled.
- Commands do not create `HttpClient`, AWS SDK clients, process runners, or consoles directly.
- `OrgSecretSeeder` must not construct `AmazonDynamoDBClient` or `AmazonSecretsManagerClient` manually.
- `badge update`, `tests ingest`, and `secrets seed --dry-run` must not print raw secrets.
- `tests/BadgeSmith.Api.Tests` links tool implementation source files for in-process tests, but it must not link `tools/badgesmith.cs`.
- Root analyzer policy stays active; use only targeted tool-specific MSBuild overrides if a concrete analyzer/build issue appears.
- Package versions stay in `Directory.Packages.props`; do not hard-code package versions in `#:package` directives.
- Package changes must be made with `dotnet add package`, not manual XML edits.
- Commit operations are approval-gated by `AGENTS.md`; when a task reaches a commit checkpoint, present a concise summary and proposed Conventional Commit message, then ask Deniz before committing.

---

## File Structure

Create these files:

- `tools/Configuration/AwsCommandSettings.cs`: common AWS command settings interface/base class and validation for `--aws-profile`, `--aws-region`, `--localstack`, and `--no-localstack`.
- `tools/Configuration/EffectiveAwsOptions.cs`: immutable resolved AWS/LocalStack option record.
- `tools/Services/BadgeSmithTool.cs`: hosted tool entrypoint, Spectre command registration, command app factory, and exception handling.
- `tools/Services/BadgeSmithToolServiceCollectionExtensions.cs`: DI registration for console, logger, process runner, HTTP client, AWS option resolver, AWS client factory, and command services.
- `tools/Services/HostTypeRegistrar.cs`: Spectre `ITypeRegistrar` bridge that registers command types into the host `IServiceCollection` and resolves from the built host `IServiceProvider`.
- `tools/Services/IToolLogger.cs`: logging abstraction for tool output.
- `tools/Services/SpectreConsoleLogger.cs`: PathSmith-style `IAnsiConsole` logger adapted to BadgeSmith secret hygiene and UTC time.
- `tools/Services/IAwsOptionsResolver.cs`: interface for resolving effective AWS options from configuration and parsed command settings.
- `tools/Services/AwsOptionsResolver.cs`: implementation of LocalStack/live AWS precedence.
- `tools/Services/IToolAwsClientFactory.cs`: interface for creating scoped AWS clients after settings are parsed.
- `tools/Services/ToolAwsClientFactory.cs`: LocalStack.Client-backed client factory for `IAmazonDynamoDB` and `IAmazonSecretsManager`.
- `tools/Services/ToolAwsClientScope.cs`: disposable holder for AWS clients and the temporary service provider used by the factory.
- `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`: Spectre/TestConsole in-process tests for DI and configuration behavior.

Modify these files:

- `tools/badgesmith.cs`: add package/include directives and delegate to `BadgeSmithTool.RunAsync`.
- `tools/Commands/BadgeUpdateCommand.cs`: constructor-inject console/logger, HTTP client factory, signer helpers, and GitHub Actions helpers.
- `tools/Commands/TestIngestCommand.cs`: constructor-inject console/logger, HTTP client factory, and signer helpers.
- `tools/Commands/LambdaBuildCommand.cs`: constructor-inject `IProcessRunner`, `RepositoryPaths`, and logger.
- `tools/Commands/TestRunCommand.cs`: constructor-inject `IProcessRunner`, `RepositoryPaths`, and logger.
- `tools/Commands/SecretsSeedCommand.cs`: inherit AWS settings, resolve clients after settings parse, and skip client creation for dry-run.
- `tools/Infrastructure/OrgSecretSeeder.cs`: accept AWS clients as method parameters and keep dry-run free of AWS client use.
- `tools/Infrastructure/ProcessRunner.cs`: expose an interface and receive logger/console through DI; remove constructor-level `verbose` state.
- `tools/Infrastructure/GitHubActions.cs`: expose an interface if command tests need to fake step-summary behavior.
- `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj`: add package references and linked `Compile` items for tool source files.
- `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`: keep process-level smoke tests only; move in-process command behavior to the new test file.
- `docs/superpowers/plans/2026-07-06-w1-5-file-based-tools-implementation-plan.md`: insert Task 4.5 status/notes after this plan is approved if the parent W1.5 plan needs to remain the ledger source.

---

### Task 1: Add Package References And Linked-Source Test Boundary

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj`
- Test: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`

**Interfaces:**
- Consumes: existing tool source under `tools/Commands` and `tools/Infrastructure`.
- Produces: test project can compile `BadgeSmith.Tools.*` implementation types without linking `tools/badgesmith.cs`.

- [ ] **Step 1: Add package references with dotnet CLI**

Run these commands from the repository root:

```powershell
dotnet add "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" package Spectre.Console.Cli.Testing --version 0.55.0
dotnet add "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" package Spectre.Console.Cli.Extensions.DependencyInjection --version 0.26.0
dotnet add "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" package Microsoft.Extensions.Hosting
dotnet add "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" package LocalStack.Client.Extensions
```

Expected: restore succeeds and package versions are centralized in `Directory.Packages.props`. Do not manually add package version XML.

- [ ] **Step 2: Link tool source into the test project**

Add this `ItemGroup` to `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj` after the existing project references:

```xml
  <ItemGroup>
    <Compile Include="..\..\tools\Commands\**\*.cs" Link="Tooling\Linked\Commands\%(RecursiveDir)%(Filename)%(Extension)" />
    <Compile Include="..\..\tools\Configuration\**\*.cs" Link="Tooling\Linked\Configuration\%(RecursiveDir)%(Filename)%(Extension)" />
    <Compile Include="..\..\tools\Infrastructure\**\*.cs" Link="Tooling\Linked\Infrastructure\%(RecursiveDir)%(Filename)%(Extension)" />
    <Compile Include="..\..\tools\Services\**\*.cs" Link="Tooling\Linked\Services\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>
```

Do not link `..\..\tools\badgesmith.cs`.

- [ ] **Step 3: Add an empty in-process test file that proves the linked boundary compiles**

Create `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`:

```csharp
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Tools.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Trait("Category", TestCategories.Unit)]
public sealed class BadgeSmithToolInProcessTests
{
    [Fact]
    public void Linked_Tool_Source_Should_Expose_Tool_Exit_Codes()
    {
        Assert.Equal(0, ToolExitCodes.Success);
        Assert.Equal(2, ToolExitCodes.ValidationFailure);
    }
}
```

- [ ] **Step 4: Run the targeted compile/test and capture expected failures**

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeSmithToolInProcessTests"
```

Expected before later tasks: compile can fail because `tools/Configuration` and `tools/Services` do not exist yet. If it fails only for missing directories, continue to Task 2. If it fails for package restore or duplicate entrypoint linkage, fix this task before proceeding.

- [ ] **Step 5: Commit checkpoint**

Do not commit automatically. Present summary and proposed commit message:

```text
build: add linked-source test boundary for file-based tool
```

Ask Deniz for commit approval.

---

### Task 2: Add Hosted Spectre Composition Without Changing Command Behavior

**Files:**
- Create: `tools/Services/HostTypeRegistrar.cs`
- Create: `tools/Services/BadgeSmithTool.cs`
- Create: `tools/Services/BadgeSmithToolServiceCollectionExtensions.cs`
- Modify: `tools/badgesmith.cs`
- Test: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`

**Interfaces:**
- Produces: `BadgeSmith.Tools.BadgeSmithTool.RunAsync(string[] args, Action<HostApplicationBuilder>? configureHost = null, IAnsiConsole? console = null)`.
- Produces: `BadgeSmith.Tools.BadgeSmithTool.CreateCommandApp(IServiceCollection services, HostTypeRegistrar registrar)`.
- Consumes: existing command types and settings.

- [ ] **Step 1: Write a failing in-process help test**

Append this test to `BadgeSmithToolInProcessTests.cs`:

```csharp
using BadgeSmith.Tools;
using Spectre.Console.Testing;

[Fact]
public async Task BadgeSmithTool_Should_Run_Help_In_Process_When_Using_TestConsole()
{
    using var console = new TestConsole().Width(200);

    var exitCode = await BadgeSmithTool.RunAsync(["--help"], console: console);

    Assert.Equal(ToolExitCodes.Success, exitCode);
    Assert.Contains("USAGE", console.Output, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("badgesmith", console.Output, StringComparison.OrdinalIgnoreCase);
}
```

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeSmithTool_Should_Run_Help_In_Process"
```

Expected: FAIL because `BadgeSmithTool` does not exist.

- [ ] **Step 2: Implement `HostTypeRegistrar`**

Create `tools/Services/HostTypeRegistrar.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace BadgeSmith.Tools.Services;

internal sealed class HostTypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;
    private IServiceProvider? _serviceProvider;

    public HostTypeRegistrar(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public void UseServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public ITypeResolver Build()
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("The host service provider must be assigned before running the command app.");
        }

        return new HostTypeResolver(_serviceProvider);
    }

    public void Register(Type service, Type implementation)
    {
        _services.AddTransient(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _services.AddSingleton(service, _ => factory());
    }

    private sealed class HostTypeResolver : ITypeResolver, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;

        public HostTypeResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object? Resolve(Type? type)
        {
            return type is null ? null : _serviceProvider.GetService(type);
        }

        public void Dispose()
        {
        }
    }
}
```

- [ ] **Step 3: Implement service collection extension with existing command registrations**

Create `tools/Services/BadgeSmithToolServiceCollectionExtensions.cs`:

```csharp
using BadgeSmith.Tools.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace BadgeSmith.Tools.Services;

internal static class BadgeSmithToolServiceCollectionExtensions
{
    public static IServiceCollection AddBadgeSmithToolServices(this IServiceCollection services, IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(console);

        services.AddSingleton(console);
        services.AddSingleton<RepositoryPaths>();

        return services;
    }
}
```

This task intentionally keeps the service list minimal. Later tasks add logger, process runner, HTTP, and AWS services.

- [ ] **Step 4: Implement `BadgeSmithTool` and command registration**

Create `tools/Services/BadgeSmithTool.cs`:

```csharp
using BadgeSmith.Tools.Commands;
using BadgeSmith.Tools.Infrastructure;
using BadgeSmith.Tools.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BadgeSmith.Tools;

internal static class BadgeSmithTool
{
    public static async Task<int> RunAsync(
        string[] args,
        Action<HostApplicationBuilder>? configureHost = null,
        IAnsiConsole? console = null)
    {
        var hostBuilder = Host.CreateApplicationBuilder(args);
        var ansiConsole = console ?? AnsiConsole.Console;
        hostBuilder.Services.AddBadgeSmithToolServices(ansiConsole);

        var registrar = new HostTypeRegistrar(hostBuilder.Services);
        var app = CreateCommandApp(registrar);
        configureHost?.Invoke(hostBuilder);

        using var host = hostBuilder.Build();
        registrar.UseServiceProvider(host.Services);
        return await app.RunAsync(args).ConfigureAwait(false);
    }

    internal static CommandApp CreateCommandApp(ITypeRegistrar registrar)
    {
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName("badgesmith");
            config.Settings.ShowOptionDefaultValues = true;
            config.Settings.CaseSensitivity = CaseSensitivity.None;
            config.Settings.CancellationExitCode = ToolExitCodes.Canceled;

            config.AddBranch("lambda", lambda =>
            {
                lambda.SetDescription("Lambda build and artifact commands.");
                lambda.AddCommand<LambdaBuildCommand>("build")
                    .WithDescription("Build the BadgeSmith Lambda ZIP or container image.")
                    .WithExample("lambda", "build", "--target", "zip", "--rid", "linux-arm64", "--clean");
            });

            config.AddBranch("tests", tests =>
            {
                tests.SetDescription("Test execution and ingestion commands.");
                tests.AddCommand<TestRunCommand>("run")
                    .WithDescription("Run a .NET test project once per target framework.")
                    .WithExample("tests", "run", "--project-path", "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj", "--results-dir", "test-results");
                tests.AddCommand<TestIngestCommand>("ingest")
                    .WithDescription("Post a test result payload to BadgeSmith.")
                    .WithExample("tests", "ingest", "--base-url", "https://api.example.com", "--owner", "localstack-dotnet", "--repo", "badge-smith", "--platform", "linux", "--branch", "main", "--secret", "secret", "--payload-file", "payload.json", "--dry-run");
            });

            config.AddBranch("secrets", secrets =>
            {
                secrets.SetDescription("Secrets Manager and org mapping commands.");
                secrets.AddCommand<SecretsSeedCommand>("seed")
                    .WithDescription("Seed GitHub org secret mappings into AWS resources.")
                    .WithExample("secrets", "seed", "--config", "tools/organization-pat-mapping.json", "--table-name", "badge-smith-github-org-secrets", "--dry-run");
            });

            config.AddBranch("badge", badge =>
            {
                badge.SetDescription("BadgeSmith badge update commands.");
                badge.AddCommand<BadgeUpdateCommand>("update")
                    .WithDescription("Post GitHub Actions test results to BadgeSmith.")
                    .WithExample("badge", "update", "--platform", "Linux", "--test-passed", "1", "--test-failed", "0", "--test-skipped", "0", "--repository", "localstack-dotnet/badge-smith", "--hmac-secret", "secret", "--dry-run");
            });

            config.SetExceptionHandler((exception, resolver) =>
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

        return app;
    }
}
```

- [ ] **Step 5: Convert `tools/badgesmith.cs` to the hosted entrypoint while preserving file-based shape**

Replace `tools/badgesmith.cs` with:

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:package Spectre.Console.Cli
#:package CliWrap
#:package AWSSDK.DynamoDBv2
#:package AWSSDK.SecretsManager
#:package Microsoft.Extensions.Hosting
#:package Spectre.Console.Cli.Extensions.DependencyInjection
#:package LocalStack.Client.Extensions
#:include Commands/**/*.cs
#:include Configuration/**/*.cs
#:include Infrastructure/**/*.cs
#:include Services/**/*.cs

return await BadgeSmith.Tools.BadgeSmithTool.RunAsync(args).ConfigureAwait(false);
```

- [ ] **Step 6: Run help tests**

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeSmithTool_Should_Run_Help_In_Process|FullyQualifiedName~BadgeSmithTool_Should_Print_Help_When_Invoked_With_Help"
```

Expected: both help tests pass after later command constructors have required DI services. If command constructors are still parameterless, this task should pass now.

- [ ] **Step 7: Commit checkpoint**

Do not commit automatically. Present summary and proposed commit message:

```text
refactor: add hosted composition for file-based tool
```

Ask Deniz for commit approval.

---

### Task 3: Add Spectre Logger And Replace Static Console Access

**Files:**
- Create: `tools/Services/IToolLogger.cs`
- Create: `tools/Services/SpectreConsoleLogger.cs`
- Modify: `tools/Services/BadgeSmithToolServiceCollectionExtensions.cs`
- Modify: command files under `tools/Commands/*.cs`
- Modify: `tools/Infrastructure/ProcessRunner.cs`
- Test: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`

**Interfaces:**
- Produces: `IToolLogger` with `Trace`, `Debug`, `Info`, `Warning`, and `Error` methods.
- Produces: `SpectreConsoleLogger(IAnsiConsole console, TimeProvider timeProvider)`.
- Consumes: `IAnsiConsole` from DI.

- [ ] **Step 1: Write failing logger capture test**

Append this test:

```csharp
using BadgeSmith.Tools.Services;

[Fact]
public void SpectreConsoleLogger_Should_Write_To_Injected_TestConsole()
{
    using var console = new TestConsole().Width(200);
    var logger = new SpectreConsoleLogger(console, TimeProvider.System);

    logger.Info("hello [tool]");

    Assert.Contains("INFO", console.Output, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("hello", console.Output, StringComparison.Ordinal);
    Assert.Contains("[[tool]]", console.Output, StringComparison.Ordinal);
}
```

Expected: FAIL because logger types do not exist.

- [ ] **Step 2: Add logger abstraction**

Create `tools/Services/IToolLogger.cs`:

```csharp
namespace BadgeSmith.Tools.Services;

internal interface IToolLogger
{
    void Trace(string message);

    void Debug(string message);

    void Info(string message);

    void Warning(string message);

    void Error(string message);
}
```

- [ ] **Step 3: Add PathSmith-style Spectre logger with UTC time and markup escaping**

Create `tools/Services/SpectreConsoleLogger.cs`:

```csharp
using Spectre.Console;

namespace BadgeSmith.Tools.Services;

internal sealed class SpectreConsoleLogger : IToolLogger
{
    private readonly IAnsiConsole _console;
    private readonly TimeProvider _timeProvider;

    public SpectreConsoleLogger(IAnsiConsole console, TimeProvider timeProvider)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public void Trace(string message) => Log("TRACE", "grey", message);

    public void Debug(string message) => Log("DEBUG", "blue", message);

    public void Info(string message) => Log("INFO", "green", message);

    public void Warning(string message) => Log("WARN", "yellow", message);

    public void Error(string message) => Log("ERROR", "red", message);

    private void Log(string level, string color, string message)
    {
        var timestamp = _timeProvider.GetUtcNow().ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        _console.MarkupLine($"[grey]{timestamp}Z[/] [{color}]{level}[/]: {Markup.Escape(message)}");
    }
}
```

- [ ] **Step 4: Register logger and time provider**

Update `AddBadgeSmithToolServices`:

```csharp
services.AddSingleton(TimeProvider.System);
services.AddSingleton<IToolLogger, SpectreConsoleLogger>();
```

- [ ] **Step 5: Replace static `AnsiConsole` in commands**

For each command, add constructor parameters and replace direct static calls:

```csharp
private readonly IAnsiConsole _console;
private readonly IToolLogger _logger;

public CommandName(IAnsiConsole console, IToolLogger logger)
{
    _console = console ?? throw new ArgumentNullException(nameof(console));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

Replace:

```csharp
AnsiConsole.MarkupLine("[yellow]DRY RUN: request was not sent.[/]");
AnsiConsole.WriteLine(errorBody);
```

with:

```csharp
_console.MarkupLine("[yellow]DRY RUN: request was not sent.[/]");
_console.WriteLine(errorBody);
```

Do not replace user-facing output with logger unless it is diagnostic output. Keep user-facing dry-run output on `_console`.

- [ ] **Step 6: Run logger and help tests**

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~SpectreConsoleLogger_Should_Write_To_Injected_TestConsole|FullyQualifiedName~BadgeSmithTool_Should_Run_Help_In_Process"
```

Expected: PASS.

- [ ] **Step 7: Commit checkpoint**

Do not commit automatically. Present summary and proposed commit message:

```text
refactor: route tool output through injected Spectre logger
```

Ask Deniz for commit approval.

---

### Task 4: Add AWS Option Resolution And LocalStack.Client Factory

**Files:**
- Create: `tools/Configuration/AwsCommandSettings.cs`
- Create: `tools/Configuration/EffectiveAwsOptions.cs`
- Create: `tools/Services/IAwsOptionsResolver.cs`
- Create: `tools/Services/AwsOptionsResolver.cs`
- Create: `tools/Services/IToolAwsClientFactory.cs`
- Create: `tools/Services/ToolAwsClientFactory.cs`
- Create: `tools/Services/ToolAwsClientScope.cs`
- Modify: `tools/Services/BadgeSmithToolServiceCollectionExtensions.cs`
- Test: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`

**Interfaces:**
- Produces: `IAwsCommandSettings` for parsed command settings.
- Produces: `EffectiveAwsOptions` immutable options record.
- Produces: `IAwsOptionsResolver.Resolve(IAwsCommandSettings settings): EffectiveAwsOptions`.
- Produces: `IToolAwsClientFactory.Create(EffectiveAwsOptions options): ToolAwsClientScope`.

- [ ] **Step 1: Write failing LocalStack precedence tests**

Append these tests:

```csharp
using BadgeSmith.Tools.Configuration;
using BadgeSmith.Tools.Services;
using Microsoft.Extensions.Configuration;

[Fact]
public void AwsOptionsResolver_Should_Enable_LocalStack_When_Environment_Config_Is_True()
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalStack:UseLocalStack"] = "true",
            ["LocalStack:Session:RegionName"] = "eu-central-1",
            ["AWS:Profile"] = "production",
        })
        .Build();
    var resolver = new AwsOptionsResolver(configuration);
    var settings = new TestAwsSettings();

    var options = resolver.Resolve(settings);

    Assert.True(options.UseLocalStack);
    Assert.Equal("eu-central-1", options.Region);
    Assert.Null(options.Profile);
}

[Fact]
public void AwsOptionsResolver_Should_Let_LocalStack_Command_Option_Win_Over_Profile()
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalStack:UseLocalStack"] = "false",
            ["AWS:Profile"] = "production",
            ["AWS:Region"] = "eu-central-1",
        })
        .Build();
    var resolver = new AwsOptionsResolver(configuration);
    var settings = new TestAwsSettings { LocalStack = true, AwsProfile = "dev", AwsRegion = "us-east-1" };

    var options = resolver.Resolve(settings);

    Assert.True(options.UseLocalStack);
    Assert.Equal("us-east-1", options.Region);
    Assert.Null(options.Profile);
}

[Fact]
public void AwsOptionsResolver_Should_Use_Profile_When_LocalStack_Is_Disabled()
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalStack:UseLocalStack"] = "true",
            ["AWS:Profile"] = "production",
            ["AWS:Region"] = "eu-central-1",
        })
        .Build();
    var resolver = new AwsOptionsResolver(configuration);
    var settings = new TestAwsSettings { NoLocalStack = true, AwsProfile = "ci", AwsRegion = "eu-west-1" };

    var options = resolver.Resolve(settings);

    Assert.False(options.UseLocalStack);
    Assert.Equal("ci", options.Profile);
    Assert.Equal("eu-west-1", options.Region);
}

private sealed class TestAwsSettings : IAwsCommandSettings
{
    public string? AwsProfile { get; init; }

    public string? AwsRegion { get; init; }

    public bool LocalStack { get; init; }

    public bool NoLocalStack { get; init; }
}
```

Expected: FAIL because the AWS configuration types do not exist.

- [ ] **Step 2: Add AWS command settings interface and base class**

Create `tools/Configuration/AwsCommandSettings.cs`:

```csharp
using Spectre.Console.Cli;
using System.ComponentModel;

namespace BadgeSmith.Tools.Configuration;

internal interface IAwsCommandSettings
{
    string? AwsProfile { get; }

    string? AwsRegion { get; }

    bool LocalStack { get; }

    bool NoLocalStack { get; }
}

internal abstract class AwsCommandSettings : CommandSettings, IAwsCommandSettings
{
    [CommandOption("--aws-profile")]
    [Description("AWS profile name for live AWS calls when LocalStack is disabled.")]
    public string? AwsProfile { get; init; }

    [CommandOption("--aws-region")]
    [Description("AWS region for live AWS calls, and default LocalStack region when LocalStack-specific region is not configured.")]
    public string? AwsRegion { get; init; }

    [CommandOption("--localstack")]
    [Description("Use LocalStack for AWS service clients.")]
    public bool LocalStack { get; init; }

    [CommandOption("--no-localstack")]
    [Description("Use live AWS SDK clients even if LocalStack configuration is present.")]
    public bool NoLocalStack { get; init; }

    protected ValidationResult ValidateAwsSettings()
    {
        return LocalStack && NoLocalStack
            ? ValidationResult.Error("Use either --localstack or --no-localstack, not both.")
            : ValidationResult.Success();
    }
}
```

- [ ] **Step 3: Add effective options record**

Create `tools/Configuration/EffectiveAwsOptions.cs`:

```csharp
namespace BadgeSmith.Tools.Configuration;

internal sealed record EffectiveAwsOptions(
    bool UseLocalStack,
    string Region,
    string? Profile,
    string LocalStackHost,
    int LocalStackEdgePort,
    bool LocalStackUseSsl,
    bool LocalStackUseLegacyPorts,
    string LocalStackAccessKeyId,
    string LocalStackSecretAccessKey,
    string LocalStackSessionToken);
```

- [ ] **Step 4: Add AWS options resolver**

Create `tools/Services/IAwsOptionsResolver.cs`:

```csharp
using BadgeSmith.Tools.Configuration;

namespace BadgeSmith.Tools.Services;

internal interface IAwsOptionsResolver
{
    EffectiveAwsOptions Resolve(IAwsCommandSettings settings);
}
```

Create `tools/Services/AwsOptionsResolver.cs`:

```csharp
using BadgeSmith.Tools.Configuration;
using LocalStack.Client.Models;
using Microsoft.Extensions.Configuration;

namespace BadgeSmith.Tools.Services;

internal sealed class AwsOptionsResolver : IAwsOptionsResolver
{
    private readonly IConfiguration _configuration;

    public AwsOptionsResolver(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public EffectiveAwsOptions Resolve(IAwsCommandSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var configuredLocalStack = _configuration.GetValue<bool?>("LocalStack:UseLocalStack") ?? false;
        var useLocalStack = settings.LocalStack || (!settings.NoLocalStack && configuredLocalStack);
        var awsRegion = FirstNonBlank(settings.AwsRegion, _configuration["AWS:Region"], Environment.GetEnvironmentVariable("AWS_REGION"), Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION"), Constants.RegionName);
        var localStackRegion = FirstNonBlank(settings.AwsRegion, _configuration["LocalStack:Session:RegionName"], awsRegion, Constants.RegionName);
        var region = useLocalStack ? localStackRegion : awsRegion;
        var profile = useLocalStack ? null : FirstNonBlank(settings.AwsProfile, _configuration["AWS:Profile"], Environment.GetEnvironmentVariable("AWS_PROFILE"));

        return new EffectiveAwsOptions(
            UseLocalStack: useLocalStack,
            Region: region,
            Profile: profile,
            LocalStackHost: FirstNonBlank(_configuration["LocalStack:Config:LocalStackHost"], Constants.LocalStackHost),
            LocalStackEdgePort: GetInt("LocalStack:Config:EdgePort", Constants.EdgePort),
            LocalStackUseSsl: _configuration.GetValue<bool?>("LocalStack:Config:UseSsl") ?? Constants.UseSsl,
            LocalStackUseLegacyPorts: _configuration.GetValue<bool?>("LocalStack:Config:UseLegacyPorts") ?? Constants.UseLegacyPorts,
            LocalStackAccessKeyId: FirstNonBlank(_configuration["LocalStack:Session:AwsAccessKeyId"], Constants.AwsAccessKeyId),
            LocalStackSecretAccessKey: FirstNonBlank(_configuration["LocalStack:Session:AwsAccessKey"], Constants.AwsAccessKey),
            LocalStackSessionToken: FirstNonBlank(_configuration["LocalStack:Session:AwsSessionToken"], Constants.AwsSessionToken));
    }

    private int GetInt(string key, int fallback)
    {
        var value = _configuration.GetValue<int?>(key);
        return value is > 0 ? value.Value : fallback;
    }

    private static string FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
```

- [ ] **Step 5: Add AWS client scope and factory**

Create `tools/Services/ToolAwsClientScope.cs`:

```csharp
using Amazon.DynamoDBv2;
using Amazon.SecretsManager;

namespace BadgeSmith.Tools.Services;

internal sealed class ToolAwsClientScope : IAsyncDisposable, IDisposable
{
    private readonly IDisposable _serviceProvider;

    public ToolAwsClientScope(IDisposable serviceProvider, IAmazonDynamoDB dynamoDb, IAmazonSecretsManager secretsManager)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        DynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        SecretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
    }

    public IAmazonDynamoDB DynamoDb { get; }

    public IAmazonSecretsManager SecretsManager { get; }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        _serviceProvider.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

Create `tools/Services/IToolAwsClientFactory.cs`:

```csharp
using BadgeSmith.Tools.Configuration;

namespace BadgeSmith.Tools.Services;

internal interface IToolAwsClientFactory
{
    ToolAwsClientScope Create(EffectiveAwsOptions options);
}
```

Create `tools/Services/ToolAwsClientFactory.cs`:

```csharp
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SecretsManager;
using BadgeSmith.Tools.Configuration;
using LocalStack.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BadgeSmith.Tools.Services;

internal sealed class ToolAwsClientFactory : IToolAwsClientFactory
{
    public ToolAwsClientScope Create(EffectiveAwsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var services = new ServiceCollection();
        var configuration = BuildConfiguration(options);

        services.AddLocalStack(configuration);
        services.AddDefaultAwsOptions(BuildAwsOptions(options));
        services.AddAwsService<IAmazonDynamoDB>();
        services.AddAwsService<IAmazonSecretsManager>();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        return new ToolAwsClientScope(
            serviceProvider,
            serviceProvider.GetRequiredService<IAmazonDynamoDB>(),
            serviceProvider.GetRequiredService<IAmazonSecretsManager>());
    }

    private static IConfiguration BuildConfiguration(EffectiveAwsOptions options)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["LocalStack:UseLocalStack"] = options.UseLocalStack.ToString(),
            ["LocalStack:Session:AwsAccessKeyId"] = options.LocalStackAccessKeyId,
            ["LocalStack:Session:AwsAccessKey"] = options.LocalStackSecretAccessKey,
            ["LocalStack:Session:AwsSessionToken"] = options.LocalStackSessionToken,
            ["LocalStack:Session:RegionName"] = options.Region,
            ["LocalStack:Config:LocalStackHost"] = options.LocalStackHost,
            ["LocalStack:Config:EdgePort"] = options.LocalStackEdgePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["LocalStack:Config:UseSsl"] = options.LocalStackUseSsl.ToString(),
            ["LocalStack:Config:UseLegacyPorts"] = options.LocalStackUseLegacyPorts.ToString(),
            ["AWS:Region"] = options.Region,
        };

        if (!string.IsNullOrWhiteSpace(options.Profile))
        {
            values["AWS:Profile"] = options.Profile;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static AWSOptions BuildAwsOptions(EffectiveAwsOptions options)
    {
        var awsOptions = new AWSOptions
        {
            Region = RegionEndpoint.GetBySystemName(options.Region),
        };

        if (!string.IsNullOrWhiteSpace(options.Profile))
        {
            awsOptions.Profile = options.Profile;
        }

        return awsOptions;
    }
}
```

- [ ] **Step 6: Register AWS services**

Update `AddBadgeSmithToolServices`:

```csharp
services.AddSingleton<IAwsOptionsResolver, AwsOptionsResolver>();
services.AddSingleton<IToolAwsClientFactory, ToolAwsClientFactory>();
```

- [ ] **Step 7: Run AWS option tests**

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~AwsOptionsResolver_Should"
```

Expected: PASS.

- [ ] **Step 8: Commit checkpoint**

Do not commit automatically. Present summary and proposed commit message:

```text
refactor: add LocalStack-aware AWS client factory for tool
```

Ask Deniz for commit approval.

---

### Task 5: Refactor Commands To Constructor-Injected Dependencies

**Files:**
- Modify: `tools/Commands/LambdaBuildCommand.cs`
- Modify: `tools/Commands/TestRunCommand.cs`
- Modify: `tools/Commands/TestIngestCommand.cs`
- Modify: `tools/Commands/BadgeUpdateCommand.cs`
- Modify: `tools/Infrastructure/ProcessRunner.cs`
- Modify: `tools/Infrastructure/GitHubActions.cs`
- Modify: `tools/Services/BadgeSmithToolServiceCollectionExtensions.cs`
- Test: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`

**Interfaces:**
- Produces: `IProcessRunner` abstraction for command tests.
- Produces: `IGitHubActions` abstraction if badge update tests need step-summary fakes.
- Consumes: `IToolLogger`, `IAnsiConsole`, `IHttpClientFactory`, `RepositoryPaths`.

- [ ] **Step 1: Add in-process dry-run tests for HTTP commands**

Append tests that run commands in-process and assert secret redaction:

```csharp
[Fact]
public async Task BadgeUpdate_Should_Dry_Run_In_Process_Without_Printing_Hmac_Secret()
{
    using var console = new TestConsole().Width(300);

    var exitCode = await BadgeSmithTool.RunAsync([
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
        "--dry-run"
    ], console: console);

    Assert.Equal(ToolExitCodes.Success, exitCode);
    Assert.Contains("DRY RUN", console.Output, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("https://api.example.com/tests/results/linux/localstack-dotnet/badge-smith/feature/tools", console.Output, StringComparison.Ordinal);
    Assert.DoesNotContain("test-secret", console.Output, StringComparison.Ordinal);
}

[Fact]
public async Task TestsIngest_Should_Dry_Run_In_Process_Without_Printing_Secret()
{
    using var console = new TestConsole().Width(300);
    const string payload = "{\"platform\":\"Linux\",\"passed\":1,\"failed\":0,\"skipped\":0,\"total\":1,\"url_html\":\"https://example.com/run\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"commit\":\"abc123\",\"run_id\":\"1\",\"workflow_run_url\":\"https://example.com/workflow\"}";

    var exitCode = await BadgeSmithTool.RunAsync([
        "tests", "ingest",
        "--base-url", "https://example.com",
        "--owner", "LocalStack-DotNet",
        "--repo", "BadgeSmith",
        "--platform", "Linux",
        "--branch", "Main",
        "--secret", "test-secret",
        "--payload", payload,
        "--dry-run"
    ], console: console);

    Assert.Equal(ToolExitCodes.Success, exitCode);
    Assert.Contains("DRY RUN", console.Output, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("https://example.com/tests/results/linux/localstack-dotnet/badgesmith/Main", console.Output, StringComparison.Ordinal);
    Assert.DoesNotContain("test-secret", console.Output, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Add `IProcessRunner` and update `ProcessRunner`**

Update `tools/Infrastructure/ProcessRunner.cs` so the file begins with:

```csharp
using BadgeSmith.Tools.Services;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using Spectre.Console;

namespace BadgeSmith.Tools.Infrastructure;

internal interface IProcessRunner
{
    Task<BufferedProcessResult> RunBufferedAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        bool verbose = false,
        CancellationToken cancellationToken = default);

    Task<int> RunStreamingAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool allowNonZeroExit = false,
        bool verbose = false,
        CancellationToken cancellationToken = default);
}
```

Change `ProcessRunner` constructor to:

```csharp
internal sealed class ProcessRunner : IProcessRunner
{
    private readonly IAnsiConsole _console;
    private readonly IToolLogger _logger;

    public ProcessRunner(IAnsiConsole console, IToolLogger logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
```

Move verbose from constructor state into method parameters and update `WriteCommand` to accept `verbose`.

- [ ] **Step 3: Register process runner and HTTP client factory**

Update `AddBadgeSmithToolServices`:

```csharp
services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddHttpClient("badgesmith-api");
```

- [ ] **Step 4: Refactor `LambdaBuildCommand` and `TestRunCommand`**

Constructor-inject `IProcessRunner`, `RepositoryPaths`, `IAnsiConsole`, and `IToolLogger`. Replace all `new ProcessRunner(...)` and `new RepositoryPaths()` calls with injected fields.

Use this constructor shape:

```csharp
private readonly IProcessRunner _processRunner;
private readonly RepositoryPaths _paths;
private readonly IAnsiConsole _console;
private readonly IToolLogger _logger;

public LambdaBuildCommand(IProcessRunner processRunner, RepositoryPaths paths, IAnsiConsole console, IToolLogger logger)
{
    _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    _console = console ?? throw new ArgumentNullException(nameof(console));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

For `TestRunCommand`, use the same shape with the command type name.

- [ ] **Step 5: Refactor HTTP commands**

Constructor-inject `IHttpClientFactory`, `IAnsiConsole`, and `IToolLogger` into `BadgeUpdateCommand` and `TestIngestCommand`:

```csharp
private readonly IHttpClientFactory _httpClientFactory;
private readonly IAnsiConsole _console;
private readonly IToolLogger _logger;

public BadgeUpdateCommand(IHttpClientFactory httpClientFactory, IAnsiConsole console, IToolLogger logger)
{
    _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    _console = console ?? throw new ArgumentNullException(nameof(console));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

Replace:

```csharp
using var client = new HttpClient();
```

with:

```csharp
var client = _httpClientFactory.CreateClient("badgesmith-api");
```

Do not dispose `HttpClient` instances from `IHttpClientFactory`.

- [ ] **Step 6: Run in-process dry-run tests**

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeUpdate_Should_Dry_Run_In_Process|FullyQualifiedName~TestsIngest_Should_Dry_Run_In_Process"
```

Expected: PASS.

- [ ] **Step 7: Run existing process-level dry-run tests**

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeUpdate_Should_Dry_Run_Without_Posting|FullyQualifiedName~TestsIngest_Should_Dry_Run_Without_Posting"
```

Expected: PASS.

- [ ] **Step 8: Commit checkpoint**

Do not commit automatically. Present summary and proposed commit message:

```text
refactor: inject tool command dependencies
```

Ask Deniz for commit approval.

---

### Task 6: Refactor Secrets Seed To LocalStack.Client Factory

**Files:**
- Modify: `tools/Commands/SecretsSeedCommand.cs`
- Modify: `tools/Infrastructure/OrgSecretSeeder.cs`
- Test: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`
- Test: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`

**Interfaces:**
- Consumes: `AwsCommandSettings`, `IAwsOptionsResolver`, `IToolAwsClientFactory`, `ToolAwsClientScope`.
- Produces: `OrgSecretSeeder.SeedAsync(string configPath, string tableName, IAmazonDynamoDB? dynamoDb, IAmazonSecretsManager? secretsManager, bool dryRun, CancellationToken cancellationToken)`.

- [ ] **Step 1: Write dry-run test proving no AWS clients are constructed**

Append this test. It overrides the AWS client factory with a throwing fake; dry-run must still pass.

```csharp
[Fact]
public async Task SecretsSeed_Should_Not_Create_Aws_Clients_When_Dry_Run_Is_Set()
{
    using var console = new TestConsole().Width(300);
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
        var exitCode = await BadgeSmithTool.RunAsync([
            "secrets", "seed",
            "--config", configPath,
            "--table-name", "badge-smith-github-org-secrets",
            "--localstack",
            "--dry-run"
        ], builder => builder.Services.AddSingleton<IToolAwsClientFactory, ThrowingAwsClientFactory>(), console);

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.Contains("DRY RUN", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ghp_testtoken", console.Output, StringComparison.Ordinal);
    }
    finally
    {
        File.Delete(configPath);
    }
}

private sealed class ThrowingAwsClientFactory : IToolAwsClientFactory
{
    public ToolAwsClientScope Create(EffectiveAwsOptions options)
    {
        throw new InvalidOperationException("AWS clients should not be created during dry-run.");
    }
}
```

Expected: FAIL until `SecretsSeedCommand` skips client factory use for dry-run.

- [ ] **Step 2: Refactor `SecretsSeedSettings` to inherit AWS settings**

Change:

```csharp
internal sealed class SecretsSeedSettings : CommandSettings
```

to:

```csharp
internal sealed class SecretsSeedSettings : AwsCommandSettings
```

Update `Validate()` to include AWS validation:

```csharp
public override ValidationResult Validate()
{
    var awsValidation = ValidateAwsSettings();
    if (!awsValidation.Successful)
    {
        return awsValidation;
    }

    if (TimeoutSeconds <= 0)
    {
        return ValidationResult.Error("--timeout-seconds must be greater than zero.");
    }

    return ValidationResult.Success();
}
```

- [ ] **Step 3: Inject seeder dependencies into `SecretsSeedCommand`**

Add constructor fields:

```csharp
private readonly RepositoryPaths _paths;
private readonly OrgSecretSeeder _seeder;
private readonly IAwsOptionsResolver _awsOptionsResolver;
private readonly IToolAwsClientFactory _awsClientFactory;
private readonly IAnsiConsole _console;

public SecretsSeedCommand(
    RepositoryPaths paths,
    OrgSecretSeeder seeder,
    IAwsOptionsResolver awsOptionsResolver,
    IToolAwsClientFactory awsClientFactory,
    IAnsiConsole console)
{
    _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    _seeder = seeder ?? throw new ArgumentNullException(nameof(seeder));
    _awsOptionsResolver = awsOptionsResolver ?? throw new ArgumentNullException(nameof(awsOptionsResolver));
    _awsClientFactory = awsClientFactory ?? throw new ArgumentNullException(nameof(awsClientFactory));
    _console = console ?? throw new ArgumentNullException(nameof(console));
}
```

Replace `new RepositoryPaths()` and `new OrgSecretSeeder(...)` with fields.

- [ ] **Step 4: Update `SecretsSeedCommand.ExecuteAsync` client lifecycle**

Use this dry-run/client flow:

```csharp
if (settings.DryRun)
{
    return await _seeder.SeedAsync(configPath, tableName, dynamoDb: null, secretsManager: null, dryRun: true, cts.Token).ConfigureAwait(false);
}

var effectiveAwsOptions = _awsOptionsResolver.Resolve(settings);
await using var awsClientScope = _awsClientFactory.Create(effectiveAwsOptions);
return await _seeder.SeedAsync(configPath, tableName, awsClientScope.DynamoDb, awsClientScope.SecretsManager, dryRun: false, cts.Token).ConfigureAwait(false);
```

- [ ] **Step 5: Refactor `OrgSecretSeeder` method signature**

Change `OrgSecretSeeder` so its constructor receives `IAnsiConsole` only:

```csharp
public OrgSecretSeeder(IAnsiConsole console)
```

Change the public method signature:

```csharp
public async Task<int> SeedAsync(
    string configPath,
    string tableName,
    IAmazonDynamoDB? dynamoDb,
    IAmazonSecretsManager? secretsManager,
    bool dryRun,
    CancellationToken cancellationToken = default)
```

Inside `SeedAsync`, keep the existing dry-run loop before checking clients. After the dry-run branch, add:

```csharp
if (dynamoDb is null || secretsManager is null)
{
    throw new InvalidOperationException("AWS clients are required when --dry-run is not set.");
}
```

Delete `CreateDynamoDbClient`, `CreateSecretsManagerClient`, `ResolveCredentials`, `ResolveRegion`, and `ApplyServiceUrl` from `OrgSecretSeeder`.

- [ ] **Step 6: Register seeder**

Update `AddBadgeSmithToolServices`:

```csharp
services.AddSingleton<OrgSecretSeeder>();
```

- [ ] **Step 7: Run secrets dry-run tests**

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~SecretsSeed"
```

Expected: all `SecretsSeed` tests pass. Output must not contain raw secret values.

- [ ] **Step 8: Run help smoke through file-based entrypoint**

Run:

```powershell
dotnet run --file tools/badgesmith.cs -- secrets seed --help
```

Expected: exit code 0 and output includes `--localstack`, `--no-localstack`, `--aws-profile`, `--aws-region`, `--config`, `--table-name`, and `--dry-run`.

- [ ] **Step 9: Commit checkpoint**

Do not commit automatically. Present summary and proposed commit message:

```text
refactor: route secrets seeding through LocalStack client factory
```

Ask Deniz for commit approval.

---

### Task 7: Verification, Slopwatch, And Parent Plan Update

**Files:**
- Modify: `docs/superpowers/plans/2026-07-06-w1-5-file-based-tools-implementation-plan.md` if it needs Task 4.5 insertion notes.
- Modify: `.superpowers/sdd/progress.md` only after review/verification, because it is ignored session scratch.

**Interfaces:**
- Consumes: all Task 4.5 changes.
- Produces: verified baseline before resuming original Task 5.

- [ ] **Step 1: Run targeted in-process tool tests**

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeSmithToolInProcessTests"
```

Expected: PASS.

- [ ] **Step 2: Run process-level tool smoke tests**

Run:

```powershell
dotnet test "tests\BadgeSmith.Api.Tests\BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~BadgeSmithToolCommandTests"
```

Expected: PASS.

- [ ] **Step 3: Build the file-based tool**

Run:

```powershell
dotnet build tools/badgesmith.cs
```

Expected: exit code 0, no warnings.

- [ ] **Step 4: Build the solution**

Run:

```powershell
dotnet build --configuration Release
```

Expected: exit code 0, no warnings.

- [ ] **Step 5: Run Slopwatch if available**

Run:

```powershell
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"
```

Expected: exit code 0. If `slopwatch` is unavailable, record the exact command failure and continue only after noting that verification gap.

- [ ] **Step 6: Update W1.5 plan/progress notes**

If the parent plan still presents Task 5 as directly following Task 4, add a short note near the Task 5 section:

```markdown
Task 4.5 inserts the hosted DI / LocalStack.Client refactor before continuing `secrets seed`; see `docs/superpowers/plans/2026-07-06-w1-5-task-4-5-tool-hosting-di-implementation-plan.md`.
```

Do not alter already-completed task requirements.

- [ ] **Step 7: Commit checkpoint**

Do not commit automatically. Present summary and proposed commit message:

```text
refactor: complete hosted DI conversion for file-based tool
```

Ask Deniz for commit approval.

---

## Self-Review

- Spec coverage: the plan keeps `tools/badgesmith.cs` pure file-based, adds hosted DI, adds PathSmith-style logger, uses LocalStack.Client, supports live AWS profile/region, implements command-line LocalStack precedence, links tool source into tests, and keeps process-level entrypoint smoke tests.
- Placeholder scan: no `TBD`, `TODO`, `implement later`, or undefined task references are intentionally left in the task steps.
- Type consistency: AWS settings use `IAwsCommandSettings`; commands consume `IAwsOptionsResolver` and `IToolAwsClientFactory`; `OrgSecretSeeder` receives clients as method parameters after settings parse.
