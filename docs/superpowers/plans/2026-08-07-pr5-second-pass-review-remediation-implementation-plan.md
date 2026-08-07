# PR #5 Second-Pass Review Remediation Implementation Plan

Status: Implemented locally in `4d9c699`, `9e1344c`, `eae8df3`, and `621f2ce`.
Hosted ARM64 checks and the separate production synth gate remain pending; current
workstream status is tracked in `docs/ROADMAP.md`.

**Goal:** Replace body-only HMAC authentication with one canonical request contract,
enforce secure badge-update transport, update LocalStack client compatibility, and
separate production and local-performance CDK app entrypoints before PR #5 receives its
final review.

**Architecture:** Link one trim-safe canonical-request helper into the Lambda and
file-based CLI so client and server cannot drift. Tests reference the API-linked copy
through `InternalsVisibleTo` while compiling the existing linked tool sources. Keep the
existing `sha256=` digest envelope but change its signed input atomically with no legacy
path. Split the CDK entrypoints because native stack selection occurs after the app
constructs and synthesizes its stacks.

**Tech Stack:** .NET 10, C# 14, Native AOT, `System.Security.Cryptography`, AWS Lambda,
DynamoDB, Spectre.Console.Cli, xUnit v3, Moq, AWS CDK, LocalStack, GitHub Actions.

## Global Constraints

- Follow
  `docs/superpowers/specs/2026-08-07-pr5-second-pass-review-remediation-design.md`.
- Do not add protocol-version markers, legacy HMAC validation, fallback verification, or
  compatibility aliases.
- Keep `X-Signature` formatted as `sha256=` plus 64 lowercase hexadecimal characters.
- Sign the canonical method, logical path, timestamp, nonce, and exact body hash.
- Preserve constant-time comparison and signature-before-nonce ordering.
- Keep source-generated/AOT-safe runtime behavior; the shared helper must not use
  reflection or runtime JSON metadata.
- Keep `LocalStack.Client` at 2.0.0.
- Do not implement `perf baseline` in this workstream.
- Preserve production CloudFormation stack identity.
- Do not run production CDK deploy or any AWS mutation.
- Use LocalStack for optional infrastructure smoke checks only after explicit approval.
- Preserve unrelated user-owned worktree changes.

---

## File Structure

- Create `src/shared/Security/HmacCanonicalRequest.cs`: deterministic canonical message
  construction and exact body hashing.
- Modify `src/BadgeSmith.Api/BadgeSmith.Api.csproj`: link the shared helper into the
  Lambda project.
- Modify `tools/badgesmith.cs`: include the shared helper in the file-based app.
- Keep the test project on its existing API project reference; linked tool sources use
  the API-linked internal helper through the existing `InternalsVisibleTo` contract.
- Modify `tools/Infrastructure/HmacSigner.cs`: sign canonical input.
- Modify `src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs`: verify canonical
  input.
- Modify `tools/Commands/BadgeUpdateCommand.cs`: generate headers before signing and stop
  printing signatures.
- Modify `tools/Commands/TestIngestCommand.cs`: generate headers before signing and stop
  printing signatures.
- Modify HMAC unit and functional tests: fixed vectors, mutation rejection, replay, and
  nonce ordering.
- Modify URL and command tests: HTTPS policy and dry-run disclosure behavior.
- Modify `Directory.Packages.props`: update LocalStack Extensions and align AWS Setup/Core
  entries through the package-management workflow.
- Modify `ToolAwsClientFactory` tests: cover LocalStack and real-AWS construction.
- Create `build/BadgeSmith.CDK.LocalPerformance/`: isolated local-performance CDK app.
- Modify `build/BadgeSmith.CDK/Program.cs`: restore a production-only entrypoint.
- Modify `BadgeSmith.sln`: include the local-performance app project.
- Modify `.github/workflows/deploy.yml`: target only `BadgeSmithStack`.
- Update active architecture, tooling, action, and CDK documentation.

### Task 1: Add The Canonical HMAC Contract With Tests

**Files:**
- Create: `src/shared/Security/HmacCanonicalRequest.cs`
- Modify: `src/BadgeSmith.Api/BadgeSmith.Api.csproj`
- Modify: `tools/badgesmith.cs`
- Modify: `tools/Infrastructure/HmacSigner.cs`
- Modify: `src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/HmacTestSigner.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Security/HmacAuthenticationServiceTests.cs`

- [ ] **Step 1: Add a fixed known-answer test for canonical construction and signing**

Use fixed values for owner, repository, platform, timestamp, nonce, body, and secret.
Cover branches containing `/`, a literal `%2F`, `+`, and space. Assert the complete
canonical text and precomputed signatures. The expected values must be literals so
client and server cannot agree on the same accidental drift.

- [ ] **Step 2: Add per-field mutation tests before implementation**

Create one valid signature and verify authentication fails independently when owner,
repository, platform, branch, timestamp, nonce, or body changes. Configure the changed
owner to resolve the same secret so the test proves route binding rather than relying on
secret mismatch. Use a strict nonce mock with no setup for invalid signatures so any
nonce marking fails the test.

- [ ] **Step 3: Run focused tests and observe the body-only contract failures**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~HmacAuthenticationServiceTests|FullyQualifiedName~HmacCanonicalRequest"
```

Expected before implementation: route/header mutation tests authenticate because the
current signer and verifier use only the body.

- [ ] **Step 4: Implement the shared canonical helper**

Implement the exact message defined in the design:

```text
BADGESMITH-HMAC
POST
/tests/results/{platform}/{owner}/{repo}/{branch}
{timestamp}
{nonce}
{sha256-body}
```

Normalize platform, owner, and repository with `ToLowerInvariant`; preserve branch case;
escape route segments independently; trim timestamp and nonce; hash the exact UTF-8 body;
and omit a trailing newline. The helper accepts logical decoded values. The server passes
values after the existing single `RouteValues` `HttpUtility.UrlDecode` call; clients pass
their logical fields before `BadgeSmithUrlBuilder` escapes them.

- [ ] **Step 5: Link one helper implementation into the two production boundaries**

Add the source link/include entries for the API and file-based CLI. Do not compile the
same fully-qualified helper into the test assembly: it already references the API and
has internal access. Verify linked tool sources resolve the API copy without `CS0436`,
and build the file-based CLI to verify its own included copy.

- [ ] **Step 6: Change client signing to require canonical fields**

Replace `HmacSigner.CreateSignature(payload, secret)` with an API that requires route
fields, timestamp, nonce, payload, and secret. Keep only the `sha256=` envelope and
lowercase hexadecimal digest.

- [ ] **Step 7: Change server verification to reconstruct canonical input**

Build the canonical request from `HmacAuthContext` and verify it with the existing
exception-free digest parser and fixed-time comparison. Do not add a body-only branch.

- [ ] **Step 8: Run focused canonical and HMAC tests**

Run the Step 3 command again.

Expected: fixed-vector, valid request, all mutation failures, malformed digest behavior,
signature-before-nonce ordering, and repository identifier assertions pass.

### Task 2: Update Commands And Functional Authentication Coverage

**Files:**
- Modify: `tools/Commands/BadgeUpdateCommand.cs`
- Modify: `tools/Commands/TestIngestCommand.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Functional/TestResultsContractTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/ContractHttpClient.cs` if
  request helpers need explicit signed values.

- [ ] **Step 1: Generate timestamp and nonce before signing in both commands**

Pass exactly the route fields, timestamp, nonce, and serialized body sent on the HTTP
request to the canonical signer.

- [ ] **Step 2: Remove signature output from dry runs**

Retain URL, payload, timestamp, and nonce diagnostics. Assert neither command output nor
GitHub step output contains `X-Signature` or the computed digest.

- [ ] **Step 3: Add functional route/header tampering tests**

Derive a signature that is valid for one original route and header set, then send only a
tampered request to a unique empty target with:

- a fresh timestamp;
- a fresh nonce;
- another owner mapped to the same test secret;
- another repository under the same owner;
- another platform;
- another branch.

Each tampered request must return 401. For changed-route cases, query the changed target;
for timestamp, nonce, and body cases, query the otherwise-empty original target. Verify
no result was stored. Keep a separate valid request case rather than seeding the same
route before the no-write assertion.

- [ ] **Step 4: Preserve valid and same-nonce behavior**

The valid request returns the existing created response. Reusing the exact signed
request and nonce remains rejected through nonce replay protection.

- [ ] **Step 5: Run command and HMAC unit tests**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~Hmac|FullyQualifiedName~BadgeUpdate|FullyQualifiedName~TestIngest"
```

- [ ] **Step 6: Run the focused functional ingestion tests with Docker**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~TestResultsContractTests"
```

Expected: valid ingestion, malformed signature, exact replay, and every tampering case
pass against Aspire + LocalStack.

### Task 3: Enforce Badge Update Transport Safety

**Files:**
- Modify: `tools/Infrastructure/BadgeSmithUrlBuilder.cs`
- Modify: `tools/Commands/BadgeUpdateCommand.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithUrlBuilderTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolCommandTests.cs`
- Modify: `tests/BadgeSmith.Api.Tests/Tooling/BadgeSmithToolInProcessTests.cs`
- Modify: `.github/workflows/update-test-badge/README.md`

- [ ] **Step 1: Add transport-policy tests**

Cover public HTTP rejection, public HTTPS acceptance, IPv4/IPv6/hostname loopback HTTP
acceptance, and unchanged local HTTP acceptance for `tests ingest`.

- [ ] **Step 2: Keep parsing separate from command policy**

Retain shared absolute HTTP/HTTPS parsing in `BadgeSmithUrlBuilder`. Add a focused policy
check used by `BadgeUpdateSettings.Validate` so local ingestion does not lose HTTP
support.

- [ ] **Step 3: Verify action behavior through command contract tests**

The reusable action continues passing `api_base_url`; no duplicate Bash or PowerShell
URL validation is added.

- [ ] **Step 4: Run focused URL/action tests**

Run:

```bash
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "FullyQualifiedName~BadgeSmithUrlBuilderTests|FullyQualifiedName~GitHubActionContractTests|FullyQualifiedName~BadgeUpdate"
```

### Task 4: Update LocalStack Client Compatibility

**Files:**
- Modify: `Directory.Packages.props`
- Modify: package references only if package graph inspection proves they are needed
- Modify/Create: focused `ToolAwsClientFactory` tests under
  `tests/BadgeSmith.Api.Tests/Tooling/`

- [ ] **Step 1: Invoke the package-management capability and update Extensions**

Update `LocalStack.Client.Extensions` from 2.0.0 to 2.0.1 through the repository's
Central Package Management workflow. Keep `LocalStack.Client` at 2.0.0.

- [ ] **Step 2: Align stale AWS Setup/Core central entries**

Ensure central package metadata does not claim versions below the new Extensions floor:
`AWSSDK.Extensions.NETCore.Setup` must be at least 4.0.100.5 and the resolved
`AWSSDK.Core` must be at least 4.0.100.6. Do not add unnecessary direct references merely
to influence transitive resolution.

- [ ] **Step 3: Inspect actual package graphs**

Run:

```bash
dotnet package list --file tools/badgesmith.cs --include-transitive --format json
dotnet package list --project tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --include-transitive --format json
dotnet package list --project src/BadgeSmith.Host/BadgeSmith.Host.csproj --include-transitive --format json
```

Expected: Extensions 2.0.1 and compatible AWS Setup/Core versions resolve in both
tooling graphs, `AWSSDK.Core` is compatible in the AppHost graph, and no downgrade
warnings are emitted.

- [ ] **Step 4: Add client-construction tests**

Construct DynamoDB and Secrets Manager clients for `UseLocalStack=true` and
`UseLocalStack=false` without issuing service calls. The real-AWS path must exercise the
constructor-resolution code fixed upstream rather than testing only
`AwsOptionsResolver`.

- [ ] **Step 5: Run package and tooling tests**

Run the focused factory tests, restore, and build the file-based CLI.

### Task 5: Split Production And Local-Performance CDK Apps

**Files:**
- Modify: `build/BadgeSmith.CDK/Program.cs`
- Create: `build/BadgeSmith.CDK.LocalPerformance/BadgeSmith.CDK.LocalPerformance.csproj`
- Create: `build/BadgeSmith.CDK.LocalPerformance/Program.cs`
- Create: `build/BadgeSmith.CDK.LocalPerformance/cdk.json`
- Modify: `BadgeSmith.sln`
- Modify: `.github/workflows/deploy.yml`
- Modify: `build/BadgeSmith.CDK/README.md`
- Create or modify local-performance CDK documentation in its natural existing home

- [ ] **Step 1: Restore a production-only entrypoint**

Remove the `stack` context branch and all local-performance settings from
`BadgeSmith.CDK/Program.cs`. Construct only `ProductionStack` with the existing
`ProductionStackId` and environment resolution.

- [ ] **Step 2: Add the local-performance app project**

Reference `BadgeSmith.CDK.Shared`, construct only `LocalPerformanceStack`, and move the
existing local context/default helpers into this entrypoint. Preserve
`LocalPerformanceStackId`. Because its CDK working directory is
`build/BadgeSmith.CDK.LocalPerformance`, set the default Lambda ZIP path to
`../../artifacts/badge-lambda-linux-x64.zip`.

- [ ] **Step 3: Give the local app its own CDK execution boundary**

Add a local `cdk.json` whose app command runs the local-performance project from
`build/BadgeSmith.CDK.LocalPerformance`. Do not add a topology-selection context value.

- [ ] **Step 4: Keep physical stack identity stable**

Do not set or change production `StackProps.StackName`. Confirm `cdk ls` reports
`BadgeSmithStack` for production and `BadgeSmithPerformanceStack` for local performance.

- [ ] **Step 5: Replace production `--all` selectors**

Change synth, diff, and deploy workflow commands to target `BadgeSmithStack` explicitly.
Do not run the deploy job during local verification.

- [ ] **Step 6: Build both app projects**

Run:

```bash
dotnet build build/BadgeSmith.CDK/BadgeSmith.CDK.csproj -c Release
dotnet build build/BadgeSmith.CDK.LocalPerformance/BadgeSmith.CDK.LocalPerformance.csproj -c Release
```

- [ ] **Step 7: Build required Lambda assets and synth each app separately**

Use the file-based Lambda builder to create the architecture-specific ZIP required by
each app. Run production `cdk synth BadgeSmithStack` from `build` and local-performance
`cdklocal synth BadgeSmithPerformanceStack` from
`build/BadgeSmith.CDK.LocalPerformance`.

Expected: each command requires only its own asset and lists only its own stack.

- [ ] **Step 8: Do not deploy**

Stop after synth. A LocalStack deploy smoke and every real AWS action require separate
explicit approval.

### Task 6: Update Active Documentation

**Files:**
- Modify: `ARCHITECTURE.md`
- Modify: `README.md`
- Modify: `tools/README.md`
- Modify: `.github/workflows/update-test-badge/README.md`
- Modify: `build/BadgeSmith.CDK/README.md`
- Modify: `docs/ROADMAP.md`
- Modify: relevant known notes only when they describe changed active behavior

- [ ] **Step 1: Document the canonical signature contract**

List the exact field order, normalization, escaping, body hash, signature envelope,
timestamp window, and nonce behavior. Do not document a legacy format or migration
path.

- [ ] **Step 2: Document HTTPS behavior**

State that public badge updates require HTTPS and that HTTP is reserved for loopback or
explicit local ingestion.

- [ ] **Step 3: Document CDK app and stack boundaries**

Provide production and local-performance build/synth commands with their native stack
IDs. Keep production deploy approval-gated.

- [ ] **Step 4: Keep perf baseline separate**

Update its roadmap note to consume the local-performance app when implemented; do not
add the command in this workstream.

### Task 7: Final Verification And Review

- [ ] **Step 1: Reformat and inspect changed C# files**

Use Rider formatting and file analysis on every changed C# file. Require zero warnings
and errors.

- [ ] **Step 2: Run Release build and full tests**

Run:

```bash
dotnet build BadgeSmith.sln -c Release
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj -c Release
dotnet build tools/badgesmith.cs
```

- [ ] **Step 3: Run static repository checks**

Run `actionlint`, `git diff --check`, and:

```bash
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"
```

- [ ] **Step 4: Run Native AOT ARM64 verification**

Use `tools/badgesmith.cs lambda build` with the approved non-deploy artifact target.
Require zero trim/AOT warnings and the expected ZIP.

- [ ] **Step 5: Rerun two independent security reviews**

Ask reviewers to attempt timestamp, nonce, owner, repository, platform, branch, and body
tampering from a captured valid request. Require no High or Critical findings.

- [ ] **Step 6: Rerun the hosted PR pipeline**

Require successful `build-and-test` and ARM64 artifact jobs. Do not deploy.

- [ ] **Step 7: Update roadmap status with evidence**

Record implementation commit(s), local verification, hosted checks, and final review
state. Keep PR #5 draft until the user explicitly approves ready-for-review.

## Commit Plan

After each boundary is implemented and verified, present the change summary and proposed
message and request commit approval. Proposed boundaries:

1. `fix: bind HMAC authentication to canonical requests`
2. `fix: enforce secure badge update transport`
3. `build: update LocalStack client compatibility`
4. `refactor: separate production and performance CDK apps`
5. `docs: document second-pass PR remediation`

Do not commit, amend, push, or update PR state without explicit approval.
