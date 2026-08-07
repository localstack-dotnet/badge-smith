# W1.5 File-Based Tooling Migration Design

Date: 2026-07-06

Status: Completed/historical. Current CLI guidance lives in `tools/README.md`; current
workstream status lives in `docs/ROADMAP.md`.

## Purpose

Migrate BadgeSmith repository scripting from platform-specific `.sh` and `.ps1` files to
a single .NET 10 file-based CLI under `tools/`. The end state has no tracked shell or
PowerShell script files in the repository.

The migration removes duplicated script behavior, improves cross-platform consistency,
and moves BadgeSmith-specific workflow logic into typed C# code while leaving first-class
ecosystem CLIs such as `cdk`, `dotnet`, `docker`, `aws`, `k6`, and `npx` visible where
they remain simple one-line workflow calls.

## Constraints

- The repository uses .NET SDK `10.0.301` or newer so file-based apps support
  `#:include`.
- Unix-like environments run the tool through the shebang and executable bit, without
  `dotnet run`.
- Windows environments use `dotnet run --file tools/badgesmith.cs -- ...`.
- Analyzer policy should remain active. Any tool-specific analyzer friction is handled
  with targeted settings, not blanket analyzer removal.
- Package versions remain centrally managed through `Directory.Packages.props`.
- GitHub Actions installs the SDK from `global.json` via `actions/setup-dotnet`'s
  `global-json-file` input instead of duplicating a `10.0.x` version string.
- This work changes build and CI behavior, so implementation and commits remain
  approval-gated by `AGENTS.md`.

## Selected Approach

Use one file-based CLI entrypoint with modular includes:

```text
tools/
  badgesmith.cs
  Directory.Build.props
  Commands/
    LambdaBuildCommand.cs
    PerfBaselineCommand.cs
    TestRunCommand.cs
    TestIngestCommand.cs
    BadgeUpdateCommand.cs
    SecretsSeedCommand.cs
  Infrastructure/
    ProcessRunner.cs
    RepositoryPaths.cs
    GitHubActions.cs
    OrgSecretSeeder.cs
```

`tools/badgesmith.cs` is the only executable file:

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
```

Only `tools/badgesmith.cs` receives executable permissions in git:

```bash
git update-index --chmod=+x tools/badgesmith.cs
```

## Command Surface

The command tree is:

```text
badgesmith lambda build
badgesmith perf baseline
badgesmith tests run
badgesmith tests ingest
badgesmith badge update
badgesmith secrets seed
```

Existing behavior maps as follows:

| Existing file or logic | New command |
| --- | --- |
| `scripts/build-lambda.sh` and `scripts/build-lambda.ps1` | `tools/badgesmith.cs lambda build` |
| `scripts/perf-baseline.sh` and `scripts/perf-baseline.ps1` | `tools/badgesmith.cs perf baseline` |
| `scripts/perf-baseline-seed.sh` | internal step in `perf baseline` |
| `scripts/test-ingestion.sh` and `scripts/test-ingestion.ps1` | `tools/badgesmith.cs tests ingest` |
| `.github/workflows/run-dotnet-tests/run-unix.sh` and `run-win.ps1` | `tools/badgesmith.cs tests run` |
| Bash logic in `.github/workflows/update-test-badge/action.yml` | `tools/badgesmith.cs badge update` |
| `tests/seeders/BadgeSmith.DynamoDb.Seeders` project | `tools/badgesmith.cs secrets seed` |

`lambda build` keeps the existing knobs: `--target zip|image|both`, `--rid`,
`--image-tag`, `--dockerfile`, `--context`, `--out`, `--push`, `--clean`, and
`--verbose`. The default RID becomes `linux-arm64` to match the CDK artifact
expectation.

`perf baseline` keeps `--label`, `--upstream mock|real`, `--arch amd64|arm64`, and
environment support for `K6_VUS`, `K6_DURATION`, and `GITHUB_TOKEN`.

`tests run` discovers target frameworks and runs `dotnet test` once per framework with
TRX output.

`tests ingest` replaces the manual ingestion scripts. It accepts base URL, owner, repo,
platform, branch, secret, payload file or inline payload, verbosity, and `--dry-run`.

`badge update` replaces the composite action's Bash implementation. It builds the JSON
payload, signs it, posts it to BadgeSmith, displays badge URLs, writes GitHub step
summary when available, and supports `--dry-run`.

`secrets seed` replaces the local DynamoDB/Secrets Manager seeder project. It reads the
organization PAT mapping configuration, creates or updates Secrets Manager secrets, and
writes org-secret mapping rows to the configured DynamoDB table.

`secrets seed` defaults to `tools/organization-pat-mapping.json` when `--config` is not
specified. The `.dist` example is tracked, while the real local JSON file remains ignored
by the existing `**/organization-pat-mapping.json` rule. Direct command invocation fails
validation when the selected JSON file is missing.

## Invocation Rules

Unix, Linux, and macOS use the executable file directly:

```bash
${{ github.workspace }}/tools/badgesmith.cs lambda build --target zip --rid linux-arm64 --clean --verbose
```

Windows uses `dotnet run --file`:

```powershell
dotnet run --file "${{ github.workspace }}\tools\badgesmith.cs" -- lambda build --target zip --rid linux-arm64 --clean --verbose
```

The `${{ github.workspace }}` prefix is used inside composite actions so the action does
not depend on the caller's current working directory.

## Workflow Migration

Workflow YAML can keep one-line `run:` invocations. Multi-line Bash or PowerShell
business logic moves into the tool.

`ci-cd.yml` and `deploy.yml` replace `./scripts/build-lambda.sh ...` with
`tools/badgesmith.cs lambda build ...` on Unix runners.

`.github/workflows/run-dotnet-tests/action.yml` remains as a thin composite action, but
its OS-specific script files are removed. It calls the tool directly on Unix and through
`dotnet run --file` on Windows.

`.github/workflows/update-test-badge/action.yml` remains as a thin composite action to
preserve its caller-facing API. Its JSON creation, HMAC signing, HTTP POST, and URL
display logic move to `badge update`.

`src/BadgeSmith.Host` replaces the existing `AddProject<Projects.BadgeSmith_DynamoDb_Seeders>`
resource with Aspire 13's experimental `AddCSharpApp` support:

```csharp
#pragma warning disable ASPIRECSHARPAPPS001

var secretMappingConfigPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../tools/organization-pat-mapping.json"));

if (File.Exists(secretMappingConfigPath))
{
    var dynamoDbSeeder = builder
        .AddCSharpApp("BadgeSmithDynamoDbSeeders", "../../tools/badgesmith.cs")
        .WithArgs("secrets", "seed", "--config", secretMappingConfigPath, "--timeout-seconds", "300")
        .WithReference(awsConfig)
        .WithReference(badgeSmithStack)
        .WithEnvironment("AWS_RESOURCE_ORG_SECRETS_TABLE", badgeSmithStack.GetOutput(OrgSecretsOutputTableName))
        .ExcludeFromManifest();

    badgeSmithApi.WaitFor(dynamoDbSeeder);
}
```

This is an accepted local-development risk because `AddCSharpApp` is experimental and
does not affect the production Lambda artifact. `CSharpAppResource` derives from
`ProjectResource`, so LocalStack auto-wiring follows the same ProjectResource path as
`AddProject` when the app references the AWS stack resource. Keep `.WithReference(badgeSmithStack)`;
without an AWS resource reference, neither `AddProject` nor `AddCSharpApp` is discovered
as an AWS consumer automatically. If the API shape fails during implementation, fallback
to `AddExecutable("dotnet", ...)` with `run --file tools/badgesmith.cs -- secrets seed`.

The AppHost should not block startup when a developer has not created the ignored local
secret mapping file. Only add and wait for the seeder resource when the default config
file exists.

CDK and generic one-line commands are not wrapped. `cdk synth`, `cdk diff`, `cdk deploy`,
`cdk ls`, `dotnet restore`, and `dotnet build` remain direct workflow commands when they
are already clear one-liners.

## Behavior Decisions

- Secret names are standardized to the org-scoped format
  `badgesmith/github/{org}/{key}`. The existing standalone seeder used the older flat
  `badgesmith/github/{key}` format, while the performance baseline seeder already used
  org-scoped names. The migration must document this as an intentional local seeding
  correction so existing local environments can re-seed or move secrets deliberately.
- Badge/test result URLs lowercase `platform`, `owner`, and `repo`, but preserve branch
  casing.

## Implementation Boundaries

The tool owns BadgeSmith-specific orchestration, JSON generation, HMAC signing,
cross-platform path handling, local org-secret seeding, and external process invocation.

The tool does not own CDK semantics, Docker internals, AWS CLI semantics, the k6 scenario
file, or generic build and restore semantics.

External processes are launched with CliWrap without shell layers. Arguments are passed as
arrays or builders, long-running commands stream output, parse-oriented commands use
buffered capture, environment overrides are explicit, and per-command working directories
replace `pushd`/`popd` behavior.

## Dry-Run Behavior

`badge update --dry-run` prints or summarizes the target URL, payload, and generated
headers without sending the HTTP request. It must avoid printing raw secrets.

`tests ingest --dry-run` validates route inputs and payload loading, computes the target
URL and signature metadata, and does not POST.

Dry-run support exists to make local and CI-safe verification possible without mutating
BadgeSmith data.

## Research Inputs

The implementation plan uses these temporary research notes:

- `docs/research/2026-07-06-spectre-console-cli-usage.md`
- `docs/research/2026-07-06-cliwrap-script-replacement.md`

They are working references for W1.5 and can be removed later if the guidance becomes
stale or is fully incorporated into permanent project documentation.

## Documentation Updates

Update current-facing documentation:

- `AGENTS.md`: replace `scripts/build-lambda.*` references with `tools/badgesmith.cs lambda build`.
- `ARCHITECTURE.md`: update build, testing, badge update, and performance tooling sections.
- `README.md`: update workflow/action usage examples as needed.
- `docs/ROADMAP.md`: track W1.5 as active or completed.
- Script-specific README files: consolidate useful material into `tools/README.md` or current docs.

Historical plans and research remain historical unless they actively mislead current
instructions.

## Validation

Design-level validation targets:

- `dotnet --version` reports `10.0.301` or newer.
- `dotnet build tools/badgesmith.cs` compiles the file-based CLI.
- Unix help path works: `tools/badgesmith.cs --help`.
- Windows-compatible help path works: `dotnet run --file tools/badgesmith.cs -- --help`.
- Command help works for `lambda build`, `perf baseline`, `tests run`, `tests ingest`, and `badge update`.
- `git ls-files "*.sh" "*.ps1"` returns no files.
- Workflow references no longer point to `.sh` or `.ps1` files.
- Existing solution build and relevant tests still pass.
- Slopwatch runs after code changes if available.

Functional smoke checks are staged by cost. `tests run` can be checked against the unit
test project. `badge update --dry-run` and `tests ingest --dry-run` provide safe request
generation checks. Full Docker Lambda build and full performance baseline runs are heavier
and should be executed deliberately during implementation verification.

## Rejected Alternatives

Multiple file-based apps were rejected because discovery and shared behavior would fragment
across several entrypoints.

A traditional `tools/BadgeSmith.Tools.csproj` project was rejected for W1.5 because it adds
project ceremony that file-based apps avoid. It remains a future escape hatch if the tool
grows beyond a comfortable file-based structure.

Wrapping first-class CLIs such as `cdk synth` or `dotnet restore` was rejected when the
workflow step is already a clear one-liner.
