# W1.7 Closeout And Platform Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close Wave 1 correctness and W1.5 tooling leftovers, refresh the full CPM stack onto LocalStack.Aspire.Hosting 13.4.0 / Aspire 13.4.6, remove MessagePack, and leave Wave 2 as the next backlog item.

**Architecture:** Single consolidated wave on `feature/iteration0-aot-contract-tier`. Packages first (stabilize Host + contract tests), then finish W1.5 workflow/script migration on the new pins, then remaining production correctness fixes with minimal pinning tests, then ROADMAP closeout.

**Tech Stack:** .NET 10, CPM (`Directory.Packages.props`), Aspire 13.4.6, LocalStack.Aspire.Hosting 13.4.0, Aspire.Hosting.AWS 13.3.1, AWS SDK v4, xUnit v3 on VSTest, Moq, Spectre.Console.Cli file-based tool (`tools/badgesmith.cs`), GitHub Actions composite workflows.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-09-w1-7-closeout-and-platform-refresh-design.md`
- Approval gate: no package, CI, production-code, or script-deletion mutation without Deniz `go` / `apply` / `proceed` / `başla` / `yap`
- Aspire pins (exact): AppHost / Testing / AppHost.Sdk **13.4.6**; LocalStack.Aspire.Hosting **13.4.0**; Aspire.Hosting.AWS **13.3.1** explicit
- MessagePack: remove all direct PackageReference and CPM PackageVersion
- CPM only: never put package versions in individual project files
- Full CPM bump = latest **stable** (non-prerelease) at implementation time; keep Microsoft.Extensions / System.Text.Json on **10.x** (not 11 preview); do not take xUnit / BenchmarkDotNet prereleases unless Deniz approves
- Native AOT constraints remain for `src/BadgeSmith.Api`; Hosting packages must not leak into the shipped Lambda
- Tests: xUnit v3 on VSTest; method names `Subject_Should_Expected_Behavior_When_Condition`
- Do not commit real PATs / `organization-pat-mapping.json` / secrets
- Historical docs under `docs/plans/`, `docs/research/`, `docs/agents/handover-prompts/` may keep old script paths

## File Map

| Area | Files |
| --- | --- |
| Packages | `Directory.Packages.props`; `src/BadgeSmith.Host/BadgeSmith.Host.csproj`; `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj` |
| Hosting | `src/BadgeSmith.Host/Program.cs` (only if Aspire 13.4 API requires changes) |
| Workflows | `.github/workflows/ci-cd.yml`; `deploy.yml`; `run-dotnet-tests/action.yml`; `update-test-badge/action.yml` |
| Delete scripts | `scripts/build-lambda.sh|.ps1`; `scripts/perf-baseline.sh|.ps1`; `scripts/perf-baseline-seed.sh`; `scripts/test-ingestion.sh|.ps1`; `.github/workflows/run-dotnet-tests/run-unix.sh`; `run-win.ps1` |
| Keep scripts assets | `scripts/k6-perf-test.js`; `scripts/sample-test-payload.json`; evaluate `scripts/localstack.yml` |
| Tooling | `tools/Services/BadgeSmithTool.cs`; optional new `tools/Commands/PerfBaselineCommand.cs` (+ settings/services); `tools/README.md` |
| Correctness | `src/BadgeSmith.Api/Features/TestResults/TestResultsService.cs`; `src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs`; `src/BadgeSmith.Api/Core/Security/NonceService.cs`; `src/BadgeSmith.Api/Core/Routing/ApiRouter.cs`; ingestion handler parse paths as needed |
| Tests | `tests/BadgeSmith.Api.Tests/Security/HmacAuthenticationServiceTests.cs`; new `tests/BadgeSmith.Api.Tests/Features/TestResults/TestResultsServiceTests.cs` (or under existing Features folder); optional ApiRouter unit test |
| Docs / roadmap | `docs/ROADMAP.md`; `AGENTS.md` (script refs only); `ARCHITECTURE.md`; `README.md`; retire `scripts/README-*.md` |

---

### Task 1: Aspire/LocalStack hosting cluster + MessagePack removal

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/BadgeSmith.Host/BadgeSmith.Host.csproj`
- Modify: `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj`

**Interfaces:**
- Consumes: LocalStack.Aspire.Hosting 13.4.0 dependency contract (Aspire.Hosting 13.4.6, Aspire.Hosting.AWS 13.3.1)
- Produces: Host restores without direct MessagePack; explicit Aspire.Hosting.AWS on Host

- [ ] **Step 1: Confirm approval for package/build mutation**

Do not edit until Deniz has approved this plan phase (or whole plan).

- [ ] **Step 2: Update CPM Aspire/LocalStack pins and add Aspire.Hosting.AWS**

In `Directory.Packages.props`:

```xml
<!-- aspire packages -->
<PackageVersion Include="Aspire.Hosting.AppHost" Version="13.4.6" />
<PackageVersion Include="Aspire.Hosting.AWS" Version="13.3.1" />
<PackageVersion Include="LocalStack.Aspire.Hosting" Version="13.4.0" />
...
<PackageVersion Include="Aspire.Hosting.Testing" Version="13.4.6" />
```

Remove the MessagePack line entirely:

```xml
<!-- DELETE this line -->
<PackageVersion Include="MessagePack" Version="3.1.7" />
```

- [ ] **Step 3: Update Host project**

In `src/BadgeSmith.Host/BadgeSmith.Host.csproj`:

```xml
<Sdk Name="Aspire.AppHost.Sdk" Version="13.4.6" />
...
<PackageReference Include="Aspire.Hosting.AppHost" />
<PackageReference Include="Aspire.Hosting.AWS" />
<PackageReference Include="AWSSDK.Core" />
<PackageReference Include="LocalStack.Aspire.Hosting" />
<!-- remove MessagePack PackageReference -->
```

- [ ] **Step 4: Update test project**

In `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj`, delete:

```xml
<PackageReference Include="MessagePack" />
```

- [ ] **Step 5: Restore and build Host + tests**

Run:

```powershell
dotnet restore "src/BadgeSmith.Host/BadgeSmith.Host.csproj"
dotnet restore "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj"
dotnet build "src/BadgeSmith.Host/BadgeSmith.Host.csproj" --configuration Release --no-restore
dotnet build "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --configuration Release --no-restore
```

Expected: success, 0 warnings. If Aspire 13.4 API breaks Host/tests, fix compile errors in the same task (minimal surface only).

- [ ] **Step 6: Grep for leftover MessagePack**

Run:

```powershell
rg -n "MessagePack" Directory.Packages.props src tests tools
```

Expected: no PackageReference/PackageVersion hits (comments in historical docs outside this grep scope are fine).

- [ ] **Step 7: Commit**

```powershell
git add Directory.Packages.props src/BadgeSmith.Host/BadgeSmith.Host.csproj tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj
git commit -m "build: align Aspire 13.4.6 and LocalStack.Aspire.Hosting 13.4.0"
```

---

### Task 2: Full CPM stable bump

**Files:**
- Modify: `Directory.Packages.props`
- Possibly: project files only if package IDs rename (prefer not)

**Interfaces:**
- Consumes: Task 1 pins (do not regress Aspire/LocalStack/AWS hosting pins)
- Produces: remaining PackageVersion entries at latest stable

- [ ] **Step 1: Inventory current CPM entries**

Run:

```powershell
Select-String -Path Directory.Packages.props -Pattern 'PackageVersion Include='
```

- [ ] **Step 2: Resolve latest stable versions**

For each PackageVersion **except** the pinned Aspire/LocalStack/AWS hosting cluster from Task 1, resolve latest **stable** from NuGet (exclude `*-preview*`, `*-rc*`, `*-alpha*`, `*-pre*`).

Keep:

- `Aspire.Hosting.AppHost` = 13.4.6
- `Aspire.Hosting.AWS` = 13.3.1
- `Aspire.Hosting.Testing` = 13.4.6
- `LocalStack.Aspire.Hosting` = 13.4.0
- Microsoft.Extensions.* / System.Text.Json on **10.x** stable (not 11 preview)

Prefer `dotnet` / NuGet APIs or `dotnet package` workflows over hand-guessing. Record the chosen version table in the commit body.

- [ ] **Step 3: Apply version bumps in Directory.Packages.props**

Update only version attributes. Do not reorder packages unnecessarily beyond readability.

- [ ] **Step 4: Build full solution**

Run:

```powershell
dotnet restore BadgeSmith.sln
dotnet build BadgeSmith.sln --configuration Release --no-restore
dotnet build tools/badgesmith.cs
```

Expected: 0 warnings / 0 errors. Fix analyzer or API breakages with minimal code changes in this task.

- [ ] **Step 5: Run unit tests (non-Docker)**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --configuration Release --filter "Category=Unit"
```

Expected: all unit tests pass.

- [ ] **Step 6: Run functional/contract tests (Docker required)**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --configuration Release --filter "Category=Functional"
```

Expected: pass. If Aspire Testing 13.4 changes fixture APIs, update `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/AspireContractFixture.cs` and related helpers only as needed.

- [ ] **Step 7: Commit**

```powershell
git add Directory.Packages.props
# include any minimal compile-fix files
git commit -m "build: bump remaining CPM packages to current stable"
```

---

### Task 3: Migrate workflows to tools/badgesmith.cs

**Files:**
- Modify: `.github/workflows/ci-cd.yml`
- Modify: `.github/workflows/deploy.yml`
- Modify: `.github/workflows/run-dotnet-tests/action.yml`
- Modify: `.github/workflows/update-test-badge/action.yml`

**Interfaces:**
- Consumes: existing CLI commands
  - `lambda build --target zip --rid linux-arm64 --clean --verbose`
  - `tests run --project-path ... --results-dir ... --configuration ...`
  - `badge update --platform ... --test-passed ...` (existing inputs preserved)
- Produces: workflows with no `scripts/*.sh|.ps1` or `run-unix`/`run-win` dependencies

- [ ] **Step 1: Update SDK setup to global.json (ci-cd + deploy)**

Replace hard-coded `dotnet-version: '10.0.x'` with global.json-driven setup where the plan/W1.5 acceptance requires it:

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    global-json-file: global.json
```

- [ ] **Step 2: Replace lambda build in ci-cd.yml**

Replace:

```yaml
./scripts/build-lambda.sh --target zip --rid linux-arm64 --clean --verbose
```

With Unix direct execution:

```yaml
"${{ github.workspace }}/tools/badgesmith.cs" lambda build --target zip --rid linux-arm64 --clean --verbose
```

Ensure the step runs on a runner with executable bit / shebang support (existing `ubuntu-24.04-arm` job).

- [ ] **Step 3: Replace lambda build in deploy.yml**

Same substitution as Step 2 for the deploy workflow build step.

- [ ] **Step 4: Replace run-dotnet-tests composite action body**

Replace OS-specific script invocations with:

```yaml
runs:
  using: "composite"
  steps:
    - if: runner.os == 'Windows'
      shell: pwsh
      run: |
        dotnet run --file "${{ github.workspace }}/tools/badgesmith.cs" -- tests run `
          --project-path "${{ inputs.project-path }}" `
          --results-dir "${{ inputs.results-dir }}" `
          --configuration "${{ inputs.configuration }}"

    - if: runner.os != 'Windows'
      shell: bash
      run: |
        "${{ github.workspace }}/tools/badgesmith.cs" tests run \
          --project-path "${{ inputs.project-path }}" \
          --results-dir "${{ inputs.results-dir }}" \
          --configuration "${{ inputs.configuration }}"
```

Preserve input names: `project-path`, `results-dir`, `configuration`.

- [ ] **Step 5: Replace update-test-badge composite action body**

Preserve existing inputs. Replace inline Bash HMAC/post logic with thin wrappers calling:

Unix:

```bash
"${{ github.workspace }}/tools/badgesmith.cs" badge update \
  --platform "${{ inputs.platform }}" \
  --test-passed "${{ inputs.test_passed }}" \
  --test-failed "${{ inputs.test_failed }}" \
  --test-skipped "${{ inputs.test_skipped }}" \
  --test-url-html "${{ inputs.test_url_html }}" \
  --commit-sha "${{ inputs.commit_sha }}" \
  --run-id "${{ inputs.run_id }}" \
  --repository "${{ inputs.repository }}" \
  --server-url "${{ inputs.server_url }}" \
  --api-domain "${{ inputs.api_domain }}" \
  --hmac-secret "${{ inputs.hmac_secret }}"
```

Windows (if the action must support Windows runners): `dotnet run --file ... -- badge update ...` with the same flags.

Map flag names to the actual `BadgeUpdateCommand` settings property names if they differ — inspect `tools/Commands/BadgeUpdateCommand.cs` and its settings type before editing; do not invent flags.

- [ ] **Step 6: Grep workflows for stale script paths**

Run:

```powershell
rg -n "scripts/build-lambda|scripts/test-ingestion|scripts/perf-baseline|run-unix|run-win|\.sh|\.ps1" .github
```

Expected: no hits that invoke deleted helpers (README under update-test-badge may need content update in Task 5).

- [ ] **Step 7: Commit**

```powershell
git add .github/workflows
git commit -m "ci: route workflows through file-based badgesmith tool"
```

---

### Task 4: Delete tracked shell/PowerShell helpers + optional perf baseline decision

**Files:**
- Delete: listed scripts and run-unix/run-win helpers
- Modify or create: perf baseline tool command **or** ROADMAP deferral note
- Keep: `scripts/k6-perf-test.js`, sample JSON

**Interfaces:**
- Consumes: Task 3 workflows no longer reference deleted files
- Produces: `git ls-files "*.sh" "*.ps1"` empty

- [ ] **Step 1: Decide perf baseline path**

Inspect remaining scripts:

- `scripts/perf-baseline.sh`
- `scripts/perf-baseline.ps1`
- `scripts/perf-baseline-seed.sh`

If implementation stays bounded (orchestrate existing LocalStack seed + k6 using CliWrap patterns already in tools), implement `perf baseline` under `tools/Commands/` and register in `BadgeSmithTool.CreateCommandApp`.

If not bounded, write an explicit deferral in `docs/ROADMAP.md` Inbox/Backlog:

```markdown
- **Deferred from W1.7:** `perf baseline` C# command — keep k6 scenario at
  `scripts/k6-perf-test.js`; re-home orchestration after Wave 2 or with the performance pass.
```

Do **not** leave silent dual state.

- [ ] **Step 2: If implementing perf baseline, add command skeleton**

Register:

```csharp
config.AddBranch("perf", perf =>
{
    perf.SetDescription("Performance baseline commands.");
    perf.AddCommand<PerfBaselineCommand>("baseline")
        .WithDescription("Run local LocalStack + k6 performance baseline orchestration.");
});
```

Port seed records and k6 invocation from the shell scripts; keep `scripts/k6-perf-test.js` as the scenario file. Add linked-source tests only if behavior is non-trivial and testable without Docker; otherwise smoke via dry-run flags.

- [ ] **Step 3: Delete tracked helpers**

Delete these files if they still exist:

- `scripts/build-lambda.sh`
- `scripts/build-lambda.ps1`
- `scripts/perf-baseline.sh`
- `scripts/perf-baseline.ps1`
- `scripts/perf-baseline-seed.sh`
- `scripts/test-ingestion.sh`
- `scripts/test-ingestion.ps1`
- `.github/workflows/run-dotnet-tests/run-unix.sh`
- `.github/workflows/run-dotnet-tests/run-win.ps1`

Evaluate `scripts/localstack.yml`: if unreferenced, delete; if referenced, document why it remains.

- [ ] **Step 4: Verify no tracked shell/PowerShell remains**

Run:

```powershell
git ls-files "*.sh" "*.ps1"
```

Expected: empty output.

- [ ] **Step 5: Commit**

```powershell
git add -A scripts .github/workflows tools docs/ROADMAP.md
git commit -m "build: retire shell helpers after badgesmith tool migration"
```

---

### Task 5: Tooling docs cleanup (W1.5 docs close)

**Files:**
- Create or modify: `tools/README.md`
- Modify: `AGENTS.md` (script path references only — not policy/approval gates)
- Modify: `ARCHITECTURE.md`, `README.md` as needed
- Delete or empty: `scripts/README-TEST-INGESTION.md`, `scripts/README-PERF-TESTING.md` after content move
- Modify: `.github/workflows/update-test-badge/README.md` if present

**Interfaces:**
- Consumes: final CLI command surface from Tasks 3–4
- Produces: current-facing docs point at `tools/badgesmith.cs`

- [ ] **Step 1: Write tools/README.md usage**

Include Unix vs Windows invocation, main commands (`lambda build`, `tests run`, `tests ingest`, `badge update`, `secrets seed`, optional `perf baseline`), secret mapping `.dist` copy instructions, org-scoped secret name format `badgesmith/github/{org}/{key}`, and PAT safety note (never commit real mapping file).

- [ ] **Step 2: Update AGENTS.md script references**

Replace `scripts/build-lambda.*` mentions with `tools/badgesmith.cs lambda build`. Do **not** change approval gates, capability routing, or harness policy.

- [ ] **Step 3: Update ARCHITECTURE.md / README.md tooling bullets**

Point tooling at the C# CLI. Keep public API badge URLs unchanged.

- [ ] **Step 4: Retire scripts README files**

Move any still-useful content into `tools/README.md`, then delete `scripts/README-TEST-INGESTION.md` and `scripts/README-PERF-TESTING.md`.

- [ ] **Step 5: Grep current-facing docs**

Run:

```powershell
rg -n "scripts/build-lambda|scripts/perf-baseline|scripts/test-ingestion|run-unix|run-win|BadgeSmith\.DynamoDb\.Seeders|tests/seeders" AGENTS.md ARCHITECTURE.md README.md .github tools docs/ROADMAP.md
```

Expected: no stale current-facing hits (historical plan/research paths excluded from this command).

- [ ] **Step 6: Commit**

```powershell
git add tools/README.md AGENTS.md ARCHITECTURE.md README.md scripts .github/workflows
git commit -m "docs: point tooling docs at file-based badgesmith CLI"
```

---

### Task 6: Fix GSI1PK case normalization (TDD)

**Files:**
- Test: `tests/BadgeSmith.Api.Tests/Features/TestResults/TestResultsServiceTests.cs` (create)
- Modify: `src/BadgeSmith.Api/Features/TestResults/TestResultsService.cs` (line ~95)

**Interfaces:**
- Consumes: `GetLatestTestResultAsync(string owner, string repo, string platform, string branch, CancellationToken ct)`
- Produces: GSI1PK `LATEST#{ownerNormalized}#{repoNormalized}#{platformNormalized}#{branchNormalized}`

- [ ] **Step 1: Write failing unit test**

Create a unit test that mocks `IAmazonDynamoDB` and asserts the `QueryRequest` ExpressionAttributeValues for `:gsi1pk` uses lowercase components when mixed-case inputs are supplied.

Example shape (adjust namespaces/usings to match project):

```csharp
[Trait("Category", TestCategories.Unit)]
public sealed class TestResultsServiceTests
{
    [Fact]
    public async Task GetLatestTestResultAsync_Should_Query_Lowercase_GSI1PK_When_Route_Values_Have_Mixed_Case()
    {
        QueryRequest? captured = null;
        var dynamo = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        dynamo
            .Setup(d => d.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<QueryRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new QueryResponse { Items = [] });

        var sut = new TestResultsService(
            dynamo.Object,
            tableName: "badge-smith-test-result",
            Mock.Of<ILogger<TestResultsService>>());

        _ = await sut.GetLatestTestResultAsync("LocalStack-DotNet", "Badge-Smith", "Linux", "Master", TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("LATEST#localstack-dotnet#badge-smith#linux#master", captured!.ExpressionAttributeValues[":gsi1pk"].S);
    }
}
```

If `TestResultsService` constructor differs, use Rider `get_symbol_info` / read the type and match the real constructor.

- [ ] **Step 2: Run test — expect FAIL**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~GetLatestTestResultAsync_Should_Query_Lowercase_GSI1PK"
```

Expected: FAIL because production builds `LATEST#{owner}#{repo}#{platform}#{branch}` with raw inputs.

- [ ] **Step 3: Fix production code**

In `TestResultsService.GetLatestTestResultAsync`, change:

```csharp
var gsi1Pk = $"LATEST#{owner}#{repo}#{platform}#{branch}";
```

to:

```csharp
var gsi1Pk = $"LATEST#{ownerNormalized}#{repoNormalized}#{platformNormalized}#{branchNormalized}";
```

- [ ] **Step 4: Run test — expect PASS**

Same filter as Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/BadgeSmith.Api/Features/TestResults/TestResultsService.cs tests/BadgeSmith.Api.Tests/Features/TestResults/TestResultsServiceTests.cs
git commit -m "fix: normalize GSI1PK case on latest test-result queries"
```

---

### Task 7: Reorder HMAC validation — nonce after signature (TDD)

**Files:**
- Modify: `tests/BadgeSmith.Api.Tests/Security/HmacAuthenticationServiceTests.cs`
- Modify: `src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs`

**Interfaces:**
- Consumes: `ValidateRequestAsync(HmacAuthContext, CancellationToken)`
- Produces: order timestamp → secret+signature → nonce mark last

- [ ] **Step 1: Write failing tests for call order**

Add tests that use Moq `CallBase = false` and sequence/callback counters:

1. **Invalid signature must not call nonce service**

```csharp
[Fact]
public async Task ValidateRequestAsync_Should_Not_Mark_Nonce_When_Signature_Is_Invalid()
{
    // secret returns known secret; signature is wrong; nonce service must never be called
}
```

2. **Valid signature marks nonce after secret fetch**

```csharp
[Fact]
public async Task ValidateRequestAsync_Should_Mark_Nonce_After_Signature_Succeeds()
{
    // verify GetGitHubTokenAsync invoked before ValidateAndMarkNonceAsync via callback order list
}
```

Use the existing `HmacTestSigner` helper for valid signatures.

- [ ] **Step 2: Run tests — expect FAIL**

Run:

```powershell
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "FullyQualifiedName~HmacAuthenticationServiceTests"
```

Expected: new tests fail because nonce is currently validated before signature (see `HmacAuthenticationService.cs` lines 45–72).

- [ ] **Step 3: Reorder ValidateRequestAsync**

Target control flow:

```csharp
// 1) timestamp (existing)
// 2) secret lookup
// 3) signature validation -> InvalidSignature on failure (no nonce call)
// 4) nonce ValidateAndMarkNonceAsync
// 5) AuthenticatedRequest success
```

Keep `repoIdentifier` shape: `$"{owner}/{repo}/{platform}/{branch}"` (already fixed).

Minimal sketch:

```csharp
var repoIdentifier = $"{authContext.Owner}/{authContext.Repo}/{authContext.Platform}/{authContext.Branch}";

var secretResult = await _gitHubOrgSecretsService.GetGitHubTokenAsync(authContext.Owner, TokenType, ct).ConfigureAwait(false);
if (secretResult is { IsSuccess: false, GithubSecret: null })
{
    return secretResult.Failure.Match<HmacAuthenticationResult>(
        notFound => new RepoSecretNotFound(notFound.Reason),
        error => error);
}

var secret = secretResult.GithubSecret!;
if (!ValidateHmacSignature(authContext.Signature, authContext.RequestBody, secret))
{
    _logger.LogWarning("Invalid HMAC signature for repository {RepoIdentifier}", repoIdentifier);
    return new InvalidSignature("HMAC signature verification failed");
}

var nonceResult = await _nonceService.ValidateAndMarkNonceAsync(authContext.Nonce, repoIdentifier, requestTimestamp, ct).ConfigureAwait(false);
if (!nonceResult.IsSuccess)
{
    return nonceResult.Failure.Match<HmacAuthenticationResult>(
        alreadyUsed => alreadyUsed,
        error => error);
}

_logger.LogInformation("Successfully authenticated request for repository {RepoIdentifier}", repoIdentifier);
return new AuthenticatedRequest(repoIdentifier, requestTimestamp);
```

- [ ] **Step 4: Run tests — expect PASS**

Same filter. Expected: all HMAC unit tests pass, including the existing platform-identifier test.

- [ ] **Step 5: Commit**

```powershell
git add src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs tests/BadgeSmith.Api.Tests/Security/HmacAuthenticationServiceTests.cs
git commit -m "fix: validate HMAC signature before burning nonce"
```

---

### Task 8: Client error-message hygiene (TDD)

**Files:**
- Modify: `src/BadgeSmith.Api/Core/Routing/ApiRouter.cs`
- Modify: `src/BadgeSmith.Api/Core/Security/NonceService.cs`
- Modify: `src/BadgeSmith.Api/Features/TestResults/TestResultsService.cs`
- Modify: `src/BadgeSmith.Api/Features/TestResults/Handlers/TestResultIngestionHandler.cs` (parse catch paths)
- Test: add focused unit tests under `tests/BadgeSmith.Api.Tests/` matching existing layout

**Interfaces:**
- Consumes: `Error(string Reason)`, `ResponseHelper.InternalServerError`
- Produces: client-facing reasons without raw `ex.Message`

- [ ] **Step 1: Write failing assertions for leak sites**

Cover at least:

1. `ApiRouter` catch returns body without exception message (generic `"An error occurred processing the request"` to match `Program.cs` / `Program.Telemetry.cs`).
2. `NonceService` exception path returns `Error` reason without `ex.Message`.
3. `TestResultsService` store exception path returns `Error` without `ex.Message`.

For services, force exceptions via mock throws. For ApiRouter, use a handler mock that throws.

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Fix leak sites**

Examples:

```csharp
// ApiRouter
return Helpers.ResponseHelper.InternalServerError("An error occurred processing the request");

// NonceService catch
return new Error("Failed to validate nonce");

// TestResultsService catch
return new Error("Failed to store test result");
```

Keep full exception details in `LogError` / `LogWarning` calls.

For ingestion JSON parse:

- Bad JSON → keep BadRequest with a **safe** validation message (no raw framework exception text if it can leak internals); prefer fixed `"Invalid JSON payload"`.
- Unexpected parse failures → InternalServerError generic message.

Scan sibling handlers in the same pass for `ex.Message` in response bodies; fix only client-facing leaks (do not expand into Wave 3 refactors).

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```powershell
git add src/BadgeSmith.Api tests/BadgeSmith.Api.Tests
git commit -m "fix: stop leaking exception messages to HTTP clients"
```

---

### Task 9: PAT rotation checklist + docs note

**Files:**
- Modify: `tools/README.md` (safety section)
- Local only (gitignored): `tools/organization-pat-mapping.json` if present

**Interfaces:**
- Consumes: `.dist` template placeholders
- Produces: no real token in git; operator steps documented

- [ ] **Step 1: Inspect local mapping without printing secrets**

Run a presence/redaction check only:

```powershell
if (Test-Path tools/organization-pat-mapping.json) {
  # DO NOT cat the file into logs
  Write-Host "Local mapping exists (gitignored). Operator must rotate any live ghp_ tokens in GitHub and update the local file."
} else {
  Write-Host "No local mapping file present."
}
```

- [ ] **Step 2: Document operator steps in tools/README.md**

Include:

1. Copy `tools/organization-pat-mapping.json.dist` → `tools/organization-pat-mapping.json`
2. Fill placeholders only on the developer machine
3. Never commit the real file (gitignore already covers `**/organization-pat-mapping.json`)
4. If a token may have leaked historically, rotate it in GitHub and update the local file
5. Prefer least-privilege fine-grained PAT / org secret types used by BadgeSmith

- [ ] **Step 3: Confirm git status never stages the real mapping**

```powershell
git status --short
git check-ignore -v tools/organization-pat-mapping.json
```

Expected: real mapping ignored if present.

- [ ] **Step 4: Commit docs only**

```powershell
git add tools/README.md
git commit -m "docs: document org PAT mapping safety and rotation"
```

---

### Task 10: ROADMAP closeout + final verification

**Files:**
- Modify: `docs/ROADMAP.md`
- Optional: `docs/agents/handover-prompts/` pickup for Wave 2

- [ ] **Step 1: Update Status & Plan Mapping**

Mark:

| Workstream | Status |
| --- | --- |
| Wave 1 — correctness and hygiene fixes | done |
| W1.5 — file-based tooling migration | done |
| W1.7 — closeout and platform refresh | done |

Link W1.7 to:

- design: `docs/superpowers/specs/2026-07-09-w1-7-closeout-and-platform-refresh-design.md`
- plan: this file

Remove completed residual Wave 1 backlog bullets (GSI1PK, nonce, error hygiene, PAT, RID default, seeder JSON) or mark them completed in notes.

State Wave 2 as next scoped backlog item.

- [ ] **Step 2: Final verification suite**

Run:

```powershell
dotnet --version
dotnet build tools/badgesmith.cs
dotnet build BadgeSmith.sln --configuration Release
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --configuration Release --filter "Category=Unit"
dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --configuration Release --filter "Category=Functional"
git ls-files "*.sh" "*.ps1"
rg -n "MessagePack" Directory.Packages.props src/BadgeSmith.Host tests/BadgeSmith.Api.Tests
rg -n "PackageVersion Include=\"Aspire" Directory.Packages.props
```

Expected:

- SDK ≥ 10.0.301 per `global.json`
- builds/tests green
- no tracked `.sh`/`.ps1`
- no MessagePack package refs
- Aspire pins show 13.4.6 / LocalStack 13.4.0 / AWS 13.3.1

Optional:

```powershell
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"
```

- [ ] **Step 3: Commit closeout**

```powershell
git add docs/ROADMAP.md
git commit -m "docs: close W1, W1.5, and W1.7 on roadmap"
```

---

## Spec Coverage Checklist

| Spec requirement | Task |
| --- | --- |
| LocalStack.Aspire.Hosting 13.4.0 | Task 1 |
| Aspire 13.4.6 cluster + AppHost.Sdk | Task 1 |
| Explicit Aspire.Hosting.AWS 13.3.1 | Task 1 |
| Remove MessagePack | Task 1 |
| Full CPM stable bump | Task 2 |
| Stabilize unit + contract tests after packages | Task 2 |
| Workflow migration to tools/badgesmith.cs | Task 3 |
| Delete tracked .sh/.ps1 helpers | Task 4 |
| perf baseline ship-or-defer | Task 4 |
| Tooling docs / AGENTS script refs | Task 5 |
| GSI1PK case fix + test | Task 6 |
| Nonce-after-signature + tests | Task 7 |
| Error-message hygiene + tests | Task 8 |
| PAT rotation checklist/docs | Task 9 |
| ROADMAP closeout Wave 2 next | Task 10 |
| No Wave 2/3/perf-pass scope creep | enforced via Non-Goals + task boundaries |

## Plan Self-Review Notes

- No TBD placeholders for required work; perf baseline has an explicit ship-or-defer exit.
- Package pins match LocalStack.Aspire.Hosting 13.4.0 nuspec (Aspire.Hosting 13.4.6, Aspire.Hosting.AWS 13.3.1).
- Correctness tasks use TDD with concrete files and current buggy line references.
- Workflow task requires reading actual Spectre option names before editing `update-test-badge` (do not invent flags).
