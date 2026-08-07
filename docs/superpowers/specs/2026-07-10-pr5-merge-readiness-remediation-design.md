# PR #5 Merge-Readiness Remediation Design

Date: 2026-07-10

Status: Implemented in `34fe5f7`. This document is historical; the second-pass design
and active operator guides supersede it where behavior changed.

## Context

PR #5 completed its first end-to-end pipeline run. The `build-and-test` job passed,
but the ARM64 Native AOT artifact job failed with `IL3053` from
`Amazon.Lambda.Serialization.SystemTextJson` 3.0.0. A full-PR review also found
runtime, CLI, GitHub Actions, and agent-documentation issues that should be resolved
before the PR is marked ready for review.

Rider additionally reports `bootstrap 1.0.0 contains vulnerabilities`. Rider MCP and
NuGet audit evidence show that this is an identity collision between BadgeSmith's AWS
Lambda executable and the unrelated `twbs/bootstrap` browser library.

All remediation stays on the existing draft PR and is split into independently
verifiable commits. Backward compatibility is not required because the current users
are repositories controlled by the project owner.

## Goals

- Restore a warning-free .NET 10 Native AOT ARM64 Lambda build.
- Resolve every validated full-PR review finding.
- Make BadgeSmith's badge update integration genuinely reusable and white-label.
- Keep the internal test runner action repository-local.
- Reject the Rider/Mend `bootstrap` finding without suppressing real package audits.
- Finish with a green PR pipeline and no Critical or Important review findings.

## Non-Goals

- Opening an upstream AWS issue in this workstream.
- Preserving the `api_domain` input or CLI option.
- Adding compatibility aliases for old action or CLI arguments.
- Replacing AWS's serializer with a BadgeSmith-owned serializer.
- Renaming the Lambda `bootstrap` executable.
- Suppressing trim, AOT, NuGet, Rider, or Mend warnings broadly.
- Making `run-dotnet-tests` a general-purpose public action.

## Decision Summary

| Area | Decision |
| --- | --- |
| Lambda serializer | Temporarily pin `Amazon.Lambda.Serialization.SystemTextJson` to 2.4.5 |
| HMAC failures | Treat malformed digest input as an invalid signature and return 401 |
| White-label URL | Require an absolute `api_base_url` / `--base-url`; remove `api_domain` |
| Badge action | Make `update-test-badge` remotely reusable and self-contained |
| Test action | Keep `run-dotnet-tests` repository-local |
| Shell safety | Pass expressions through step-level environment variables, not script interpolation |
| Rider warning | Reject only the `bootstrap@1.0.0` identity collision as a false positive |
| Merge state | Keep PR #5 draft until all verification gates pass |

## Workstream 1: Native AOT Package Compatibility

### Root Cause

`Amazon.Lambda.Serialization.SystemTextJson` 3.0.0 contains `net8.0` and `net10.0`
assets. Its upstream project applies `IsTrimmable` and the trim analyzer only when the
target is `net8.0`. BadgeSmith targets `net10.0`, so NuGet selects the unmarked
`net10.0` asset. Native AOT then analyzes reflection-based serializers that BadgeSmith
does not use and emits `IL3050`; the SDK collapses the assembly warnings into `IL3053`.

BadgeSmith already uses the correct source-generated serializer:

```csharp
SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>
```

The application serializer and JSON source-generation context do not change.

### Package Decision

- Pin `Amazon.Lambda.Serialization.SystemTextJson` to 2.4.5 through Central Package
  Management.
- Add a concise self-contained XML comment explaining that 3.0.0's `net10.0` asset
  lacks the trimmability metadata required by BadgeSmith's Native AOT build.
- Do not suppress `IL3053` or `IL3050`.
- Do not weaken warnings-as-errors.
- Do not switch to `DefaultLambdaJsonSerializer` or another reflection serializer.
- Do not open an upstream issue in this workstream.

### Package Verification

- Restore and inspect the resolved dependency graph.
- Publish with `TrimmerSingleWarn=false` so hidden warnings cannot pass under an
  aggregate warning.
- Require zero trim and AOT warnings.
- Require the PR's ARM64 ZIP job to upload the expected artifact.

## Workstream 2: Runtime Security And White-Label URLs

### Malformed HMAC Digests

HMAC validation must treat all malformed client input as authentication failure rather
than an internal exception.

- Require the `sha256=` prefix followed by exactly 64 hexadecimal characters.
- Decode without throwing on malformed, odd-length, short, or long input.
- Preserve constant-time comparison for valid-length decoded digests.
- Return `InvalidSignature`, which maps to HTTP 401.
- Do not mark the nonce when signature validation fails.

Tests cover non-hex input, odd-length input, short and long digests, a valid-length
incorrect digest, nonce behavior, and the functional HTTP status.

### Base URL Contract

BadgeSmith is white-label software. No reusable action or CLI command may assume the
LocalStack.NET production domain.

- Replace `api_domain` with required action input `api_base_url`.
- Replace the badge command's domain option with required `--base-url`.
- Keep one `--base-url` concept across `badge update` and `tests ingest`.
- Do not provide a default BadgeSmith deployment URL.
- Accept only absolute HTTP or HTTPS URLs.
- Support ports and path prefixes.
- Reject embedded credentials, query strings, and fragments.
- Normalize trailing slashes before appending BadgeSmith routes.

All route segments are encoded independently with `Uri.EscapeDataString`. This
includes platform, owner, repository, and branch. A branch such as `feature/tools`
therefore appears as `feature%2Ftools`; the existing server route-value decoder turns
it back into `feature/tools`.

A shared tool URL builder owns validation, normalization, and construction of ingest,
badge, and redirect URLs. HMAC payload and signature formats do not change.

## Workstream 3: CLI And GitHub Actions

### CLI Validation

`tests ingest` validates before reading, signing, or sending:

- base URL, owner, repository, platform, branch, and secret are non-empty;
- base URL satisfies the shared white-label URL contract;
- exactly one of `--payload` and `--payload-file` is supplied;
- a supplied payload file exists.

`secrets seed --dry-run` validates mapping content without requiring a DynamoDB table
name. Non-dry-run execution still requires a table name before constructing AWS
clients or mutating resources.

`badge update` requires its HMAC secret through `BADGESMITH_HMAC_SECRET`.
`--hmac-secret` is removed rather than retained as a compatibility alias. The reusable
action and direct CLI users use the environment contract so the secret is not placed in
generated shell text or the process argument list.

### Reusable `update-test-badge`

`update-test-badge` is the public integration surface for repositories that post test
results to any BadgeSmith deployment.

- Support remote action syntax such as
  `localstack-dotnet/badge-smith/.github/workflows/update-test-badge@v1`.
- Resolve the action repository through `github.action_path`, including its
  `tools/badgesmith.cs` and `global.json` files.
- Install the action repository's pinned .NET SDK with `actions/setup-dotnet@v4`.
- Use `dotnet run --file` on Windows and the executable file path on Unix-like runners.
- Pass action inputs and GitHub context values through step-level environment
  variables, then quote shell variables.
- Require `api_base_url` explicitly.
- Remove duplicated shell code that prints badge URLs; the CLI step summary is the
  single source.
- Document a white-label example and the LocalStack.NET deployment as one explicit
  example, not a default.

### Repository-Local `run-dotnet-tests`

`run-dotnet-tests` remains an internal BadgeSmith workflow helper because test runner,
framework, TFM, results, and integration-test requirements vary across repositories.

- Continue using the caller workspace's `tools/badgesmith.cs`.
- Move all expression values into step-level environment variables before shell use.
- Remove root documentation that tells consumers to copy this action directory.
- Do not publish or promise a general-purpose test action contract.

### ARM Artifact Job

The ARM64 `continuous-deployment` job installs the SDK declared by `global.json` before
running the file-based CLI. The workflow does not depend on the hosted runner image
retaining a particular .NET feature band.

## Workstream 4: Rider/Mend And Agent Documentation

### `bootstrap 1.0.0` Identity Collision

`BadgeSmith.Api.csproj` intentionally sets the assembly name to `bootstrap`, which AWS
Lambda custom runtimes require. Rider/Mend associates the output name and default
assembly version with the unrelated Bootstrap front-end package.

Evidence required for rejection:

- Rider lists the finding in the Security subsystem but project dependencies do not
  contain a package named `bootstrap`.
- `dotnet package list --vulnerable --include-transitive` reports no vulnerable NuGet
  packages.
- The cited CVEs describe browser-side XSS in `twbs/bootstrap` JavaScript components,
  not a Linux .NET Native AOT executable.

Reject only the `bootstrap@1.0.0` component finding in Rider/Mend as an identity
collision. Do not add an MSBuild `NoWarn`, rename the executable, or add a repository-
wide Bootstrap exclusion. If Rider MCP cannot perform the UI action, provide the exact
rejection rationale for manual entry.

### Aspire Source Navigation

The source-navigation skill and its documented copy must describe BadgeSmith as a
consumer, not a maintainer, of Aspire integrations.

- Map `Aspire.Hosting` packages to `dotnet/aspire`.
- Map `Aspire.Hosting.AWS` to `aws/integrations-on-dotnet-aspire-for-aws`.
- Map `LocalStack.Aspire.Hosting` to
  `localstack-dotnet/dotnet-aspire-for-localstack`.
- Keep `LocalStack.Client` mapped to `localstack-dotnet/localstack-dotnet-client` only
  for SDK/client configuration investigations.
- Update the canonical local skill adapter and the repository documentation together.

## Error Handling

- Client-controlled HMAC text cannot escape as an exception.
- Invalid CLI input returns the existing validation exit code before network or AWS
  operations.
- Base URL errors identify the rejected property without printing secrets.
- HTTP failures retain the existing opt-in `--fail-on-error` behavior for direct CLI
  use; the reusable action keeps its documented CI behavior.
- AOT warnings remain build failures.

## Verification Strategy

Each workstream receives focused verification before the next begins. Final
verification includes:

1. Focused HMAC and functional ingestion tests.
2. Focused CLI tests for base URLs, encoded branches, required values, payload-source
   exclusivity, missing files, environment secrets, and secret dry-run behavior.
3. Static validation of Windows and Unix action command paths.
4. Release solution build and the full test project.
5. File-based CLI build.
6. `dotnet package list --vulnerable --include-transitive` with no findings.
7. `actionlint`, `git diff --check`, and Slopwatch with no new findings.
8. Native AOT analysis with `TrimmerSingleWarn=false` and no trim/AOT warnings.
9. PR workflow rerun with successful `build-and-test` and ARM64 artifact jobs.
10. Final full-PR review with no Critical or Important findings.

PR #5 remains draft until all gates pass. Moving it to ready-for-review requires
explicit user approval after the evidence is reported.

## Commit Boundaries

The remediation should remain reviewable through focused commits:

1. `build: restore Native AOT serializer compatibility`
2. `fix: harden HMAC and white-label URL handling`
3. `fix: validate tooling and secure workflow inputs`
4. `docs: correct Aspire source navigation`

The Rider/Mend false-positive rejection is external security-tool state and does not
receive a repository commit unless the implementation reveals a safe, narrowly scoped,
portable policy mechanism. No such mechanism is assumed by this design.
