# W1.5 Task 4.5 Tool Hosting, DI, And AWS Client Design

Date: 2026-07-06

## Context

BadgeSmith W1.5 is replacing tracked shell and PowerShell scripts with a single .NET 10
file-based CLI at `tools/badgesmith.cs`. Tasks 1-4 added the entrypoint and several
commands directly under `tools/Commands` and `tools/Infrastructure`. Task 5 then exposed
an integration flaw: Aspire `AddCSharpApp` correctly treats the file-based tool as a
`ProjectResource`, but LocalStack Aspire Hosting injects `LocalStack__...` configuration
variables into project resources, while `OrgSecretSeeder` currently looks only at raw AWS
environment variables such as `AWS_ENDPOINT_URL`.

Task 4.5 inserts a full tool composition refactor before continuing Task 5. This is not a
workaround. The tool will remain a true file-based app, move to hosted dependency
injection, use LocalStack.Client for AWS service clients, and expose testable seams
through linked-source tests.

## Source Evidence

- `env-variable-tools` uses `ServiceCollection`, `DependencyInjectionRegistrar`,
  `IAnsiConsole`, `TestConsole`, and a `SpectreConsoleLogger` wrapper as the CLI
  composition and command testing pattern.
- `external/aspire/v13.1.0` confirms `AddCSharpApp` resolves relative paths against the
  AppHost directory and `CSharpAppResource` derives from `ProjectResource`.
- `external/dotnet-aspire-for-localstack/13.1.0` confirms LocalStack Aspire Hosting
  configures project resources with `LocalStack__UseLocalStack`,
  `LocalStack__Session__...`, and `LocalStack__Config__...` environment variables.
- `external/localstack-dotnet-client/v2.0.0` confirms `LocalStack.Client.Extensions`
  provides `AddLocalStack(configuration)` and `AddAwsService<T>()`. When
  `LocalStack:UseLocalStack` is true, AWS service clients are created through
  LocalStack.Client; otherwise they are created through `AWSSDK.Extensions.NETCore.Setup`.
- GitHub Actions currently use OIDC assume-role for deploy workflow AWS access. Current
  `badge update` and `tests ingest` commands are HTTP + HMAC and do not need AWS SDK
  credentials.

## Goals

- Keep `tools/badgesmith.cs` as the executable file-based app, not a project shim.
- Keep command implementation, services, configuration, logging, and AWS client wiring
  as source files included by `tools/badgesmith.cs` through `#:include` directives.
- Use `Microsoft.Extensions.Hosting` and DI as the single composition model.
- Use `LocalStack.Client.Extensions` for AWS client registration and configuration.
- Support live AWS profile/region configuration and LocalStack configuration from
  environment variables and command-line options.
- Make command behavior testable in-process with Spectre testing and fake services.
- Retain a small process-level smoke test for the file-based entrypoint itself.

## Non-Goals

- Do not convert BadgeSmith.Api to DI. The Native AOT Lambda remains on
  `ApplicationRegistry` and direct environment-variable configuration.
- Do not add deployment behavior or mutate real AWS resources during tests.
- Do not run CDK deploy, Lambda publish, or LocalStack-backed smoke tests without an
  explicit separate approval.
- Do not keep parallel manual AWS client construction paths after the conversion.

## File-Based Structure

`tools/badgesmith.cs` remains the real tool definition. It keeps package, property, and
include directives at the top and calls the hosted tool entrypoint in included source.

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:package Spectre.Console.Cli
#:package Spectre.Console.Cli.Extensions.DependencyInjection
#:package Microsoft.Extensions.Hosting
#:package CliWrap
#:package LocalStack.Client.Extensions
#:package AWSSDK.DynamoDBv2
#:package AWSSDK.SecretsManager
#:include Commands/**/*.cs
#:include Configuration/**/*.cs
#:include Infrastructure/**/*.cs
#:include Services/**/*.cs

return await BadgeSmith.Tools.BadgeSmithTool.RunAsync(args).ConfigureAwait(false);
```

The entrypoint remains in `tools/` so Unix workflows can keep invoking
`${{ github.workspace }}/tools/badgesmith.cs` directly and Windows workflows can keep
using `dotnet run --file "${{ github.workspace }}\tools\badgesmith.cs" -- ...`.

Expected implementation layout:

```text
tools/
  badgesmith.cs
  Commands/
  Configuration/
  Infrastructure/
  Services/
```

No production `BadgeSmith.Tools.csproj` is added. If pure file-based composition becomes
unworkable after direct verification, a project-backed shim can be reconsidered as a new
design decision rather than silently introduced as part of Task 4.5.

## Hosting And DI Design

`BadgeSmithTool.RunAsync(args)` creates a `HostApplicationBuilder` and configures the
tool through extension methods:

- `AddBadgeSmithTool()` registers command services, infrastructure services, HTTP
  clients, AWS clients, logging, path resolution, and command app construction support.
- `AddBadgeSmithAws()` registers LocalStack/AWS option resolution and an AWS client
  factory that creates `IAmazonDynamoDB` and `IAmazonSecretsManager` through
  LocalStack.Client after command settings have been parsed.
- `CreateCommandApp(serviceProvider)` creates a Spectre `CommandApp` backed by
  `DependencyInjectionRegistrar`.

The top-level tool keeps Spectre as the CLI parser. Hosting owns configuration,
lifetime, service provider validation, and dependency graph construction. Commands do
not create `HttpClient`, AWS SDK clients, process runners, or consoles directly.

AWS clients are not injected directly into command constructors because command-line AWS
overrides such as `--aws-profile`, `--aws-region`, `--localstack`, and `--no-localstack`
are not available until after Spectre parses the command settings. AWS-aware commands
instead inject an `IToolAwsClientFactory`, resolve effective AWS options from the parsed
settings, and then request clients from the factory inside `ExecuteAsync`.

## Logging And Console Output

BadgeSmith adopts the PathSmith logger pattern with repo-specific cleanup:

- Introduce an `IToolLogger` abstraction backed by `SpectreConsoleLogger`.
- Register `IAnsiConsole` in DI. Production uses `AnsiConsole.Console`; tests use
  `TestConsole`.
- Avoid `DateTime.Now`; timestamps use `DateTimeOffset.UtcNow` or injected time where a
  timestamp is required.
- Do not swallow file logging errors silently unless the operation is explicitly
  best-effort and documented in the message path.
- Keep secret hygiene: log URLs, resource names, and non-sensitive identifiers; never
  log raw HMAC secrets, PATs, AWS secret keys, or full local config file contents.

Commands may still write user-facing output, but they do so through injected
`IAnsiConsole` or `IToolLogger`, not through static `AnsiConsole` or `Console.Out`.

## AWS And LocalStack Configuration

AWS-aware commands use a single configuration precedence model:

1. Command-line options override environment/configuration values.
2. `LocalStack:UseLocalStack=true` wins over live AWS profile settings.
3. When LocalStack is enabled, AWS clients are created by LocalStack.Client using
   `LocalStack:Session` and `LocalStack:Config` values.
4. When LocalStack is disabled, AWS clients use `AWS:Profile`, `AWS:Region`, and the
   normal AWS SDK credential chain. This supports local named profiles and GitHub
   Actions OIDC-provided environment credentials.

Supported command options for AWS-aware commands:

- `--aws-profile <name>` sets `AWS:Profile` for live AWS.
- `--aws-region <region>` sets `AWS:Region` for live AWS and may also default
  `LocalStack:Session:RegionName` when LocalStack-specific region is absent.
- `--localstack` sets `LocalStack:UseLocalStack=true`.
- `--no-localstack` sets `LocalStack:UseLocalStack=false`.

Aspire already supplies `LocalStack__...` environment variables to the file-based app,
so `secrets seed` should work under AppHost without custom endpoint construction. Manual
local usage can set the same variables or pass `--localstack` and the relevant options.

## Command Refactor Scope

All existing command types become DI-created commands:

- `lambda build` receives `ProcessRunner`, `RepositoryPaths`, and logger dependencies.
- `tests run` receives `ProcessRunner`, `RepositoryPaths`, and logger dependencies.
- `tests ingest` receives an HTTP client abstraction or named `HttpClient`, signer, and
  console/logger dependencies.
- `badge update` receives an HTTP client abstraction or named `HttpClient`, signer,
  GitHub Actions helper, and console/logger dependencies.
- `secrets seed` receives `OrgSecretSeeder`, `IToolAwsClientFactory`, path/config
  services, and logger dependencies. It creates AWS clients only after settings are
  parsed and only when not running in `--dry-run` mode.

`OrgSecretSeeder` no longer constructs `AmazonDynamoDBClient` or
`AmazonSecretsManagerClient` manually. It accepts `IAmazonDynamoDB` and
`IAmazonSecretsManager` as method parameters from the LocalStack.Client-backed factory.

## Testing Strategy

File-based apps cannot be referenced by a test project as a normal `ProjectReference`.
Task 4.5 keeps the production tool pure file-based and gives tests access to the
implementation through linked source files:

- `tests/BadgeSmith.Api.Tests` links `tools/Commands/**/*.cs`,
  `tools/Configuration/**/*.cs`, `tools/Infrastructure/**/*.cs`, and
  `tools/Services/**/*.cs` into the test assembly.
- `tools/badgesmith.cs` is not linked into the test assembly because it is the executable
  file-based entrypoint.
- Command tests use `Spectre.Console.Cli.Testing` and `Spectre.Console.Testing`
  `TestConsole` for in-process tests.
- Tests build a `HostApplicationBuilder` or `ServiceCollection` with fake services and
  call the same `BadgeSmithTool` command registration used by production.
- Process-level tests remain only for entrypoint behavior such as `dotnet run --file
  tools/badgesmith.cs -- --help` and directive/package resolution.

Test coverage required for Task 4.5:

- `--help` still succeeds through the file-based entrypoint.
- DI command app resolves each command from the service provider.
- `IAnsiConsole` output is captured through `TestConsole`.
- LocalStack env variables produce LocalStack-enabled options.
- `--localstack` overrides live AWS profile options.
- `--no-localstack --aws-profile <name>` produces live AWS options without LocalStack.
- `secrets seed --dry-run` validates config without constructing or calling real AWS
  clients.
- HTTP commands preserve dry-run secret redaction.

## Package Changes

Package changes must be made with `dotnet add package` and Central Package Management,
not manual XML editing. Expected package additions:

- `Spectre.Console.Cli.Extensions.DependencyInjection` as a `#:package` in
  `tools/badgesmith.cs` and as a test project package reference for linked-source tests.
- `Microsoft.Extensions.Hosting` as a `#:package` in `tools/badgesmith.cs` and as a test
  project package reference for linked-source tests.
- `LocalStack.Client.Extensions` as a `#:package` in `tools/badgesmith.cs` and as a test
  project package reference for linked-source tests.
- `AWSSDK.Extensions.NETCore.Setup` only if direct reference is needed beyond the
  transitive reference from LocalStack.Client.Extensions.
- `Spectre.Console.Cli.Testing` for the test project.

Existing package versions in `Directory.Packages.props` should be reused when present.
New versions should use the latest stable package compatible with the repository's .NET
10 SDK unless a source-verified compatibility reason requires otherwise.

## Error Handling

Command validation failures return `ToolExitCodes.ValidationFailure`. Network failures
return `ToolExitCodes.NetworkFailure` unless the command intentionally treats a failure
as non-blocking, such as `badge update` without `--fail-on-error`.

The Spectre exception handler resolves `IToolLogger` from DI when available and prints a
sanitized error message. It must not print raw secrets or config file contents.

## Migration Order

1. Keep `tools/badgesmith.cs` as the file-based entrypoint and add the required
   `#:package` and `#:include` directives.
2. Add hosted DI composition and command registration in included source files.
3. Add `SpectreConsoleLogger` and replace static console usage.
4. Add LocalStack.Client-based AWS registration and options precedence.
5. Refactor `OrgSecretSeeder` to injected AWS clients.
6. Link tool implementation source files into `tests/BadgeSmith.Api.Tests` and add
   in-process Spectre tests.
7. Keep minimal file-based entrypoint smoke tests.
8. Re-run Task 5 implementation on top of this composition model.

## Open Risks

- `LocalStack.Client.Extensions` uses reflection in the live AWS client wrapper. This is
  acceptable for the non-AOT file-based tool but must not leak into `src/BadgeSmith.Api`.
- Linked-source tests can drift from file-based package directives if package references
  are not kept aligned. The entrypoint smoke tests must catch directive/package
  resolution failures.
- Spectre testing APIs must be verified against the selected package version before broad
  test rewrites.
- The pure file-based entrypoint must be tested on Windows and Unix invocation forms
  because W1.5 workflows depend on both.
