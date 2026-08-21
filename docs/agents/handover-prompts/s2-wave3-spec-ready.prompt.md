---
name: "S2 Wave 3 hygiene specification ready"
description: "Priming prompt for the next agent entering BadgeSmith after the Wave 3 hygiene specification was revised and committed at b090992 on 2026-08-20. The spec is implementation-ready and approval-gated; no production code has changed. Recommended next: Increment 1a (Api test safety net + allocation baselines), then Increment 1b (CDK safety net)."
argument-hint: "Optional focus area, constraints, or reason to start with a different increment"
agent: "agent"
model: "inherit"
---

You are the engineer entering BadgeSmith after the Wave 3 hygiene specification was challenged,
revised, and committed on `master` as `b090992` (`docs: clarify wave 3 allocation and consistency
contracts`) on 2026-08-20. The key state: `docs/plans/2026-08-20-wave3-hygiene.md` is the
implementation contract, every design decision in it was made explicitly by Deniz, and **no
production, test, solution, CDK, or CI code has been touched**. Your job is to implement it,
starting with Increment 1a, under the `AGENTS.md` approval gate.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-08-20 — Wave 3 spec ready)** and
> verify against the live repo, git log, `Directory.Packages.props`, `ARCHITECTURE.md`, and
> canonical docs before acting. The plan itself is transient canon: if it disagrees with source or
> tests, follow `docs/agents/deviation-protocol.md`.

## What Just Happened

### Change Ledger

| Commit | Files | Outcome |
| --- | --- | --- |
| `879e909` | `docs/ROADMAP.md`, `docs/plans/2026-08-20-wave3-hygiene.md` | Promoted Wave 3 to Status & Plan Mapping (`ready; implementation approval-gated`) and rewrote the plan after Deniz's challenge review: minimum-allocation rules, OneOf route result with `ErrorResponse` 400s, provider consistency instead of a fetch collaborator, typed redirect/cache API with a named preset, `ImmutableArray` route inventory, corrected CDK safety net. |
| `440c9a2` | plan | Tightened after an external review: exact 304-without-cache contract, GitHub-label exception in the unchanged-response criterion, `PublicCachePolicy` as validated sealed class, bounded single-allocation ETag path, CA1873 pragma allowance, `CustomErrorResponses` absence assertion, benchmark invocation fix, recording cache double for the provider matrix. |
| `b090992` | plan | Clarified allocation facts for escaping/weak ETags, allowed provider cache keys to differ, consume-every-count in the ETag rewrite, allocation gates against Increment 1a baselines, `MissingRouteParameter` placement in the TestResults feature, `ProductionStack` tidy extended to the certificate import. |

No verification commands were run in the authoring session beyond `git diff --check` (clean on
every commit); nothing else needed them. The last known test result is the Wave 2 closeout at
`152dfaa`: `Category=Unit` 420 passed, 0 skipped.

### Decisions Deniz Made (do not relitigate)

These came from the challenge session and are written into the plan; treat them as settled:

- **Copying over dependency for two providers.** `NuGetPackageService` and `GitHubPackageService`
  keep their conditional-GET/cache mechanics inline and textually identical (permitted
  differences: provider headers, URL construction, provider-specific cache key, status switch),
  guarded by one scenario matrix run against both. No `ConditionalHttpFetcher`. A third provider
  is the extraction trigger.
- **OneOf-driven results are a project hallmark.** New results are `[GenerateOneOf] sealed partial
  class : OneOfBase<...>` exposing `IsSuccess` / typed accessor / `Failure`; consumers never call
  `TryPick*`, `IsT*`, or `AsT*`. Do not propose struct or `Try*` replacements.
- **Minimum allocation is the other hallmark.** Every work item states its allocation delta; the
  OneOf result instance is the one accepted allocation; sync hot paths are gated with
  `GC.GetAllocatedBytesForCurrentThread()` against Increment 1a baselines, async HTTP paths are
  benchmarked with `[MemoryDiagnoser]` and recorded.
- **No backward-compatibility workarounds and no characterize-then-replace ceremony.** Tests are
  written against the intended contract. Intentional wire changes are listed in the plan
  (`ErrorResponse` JSON for missing route parameters, exact `Cache-Control: no-store`,
  `GitHub API error` label, upstream escaping / verbatim weak ETags).
- **Error bodies use `ErrorResponse`/`ErrorDetail`**, never raw strings.
- **No enums.** `RedirectStatus` is a `readonly record struct` with static members;
  `PublicCachePolicy` is a validating `sealed class` with a precomputed `Cache-Control` value,
  consumed through `BadgeResponsePolicy.PublicCache`.
- **CDK factory is public**, scoped to the caller stack, no `InternalsVisibleTo`; logical-ID drift
  is proven offline (tracked `build/cdk.context.json` + placeholder ARM64 zip); PR CI gets
  `setup-node` only.

### Canon Corrections Captured in the Plan

- CloudFront `DefaultTTL=0` governs 2xx/3xx only. The Error Caching Minimum TTL (10 s, AWS
  default, not in the template) caches 404/414/500/501/502/503/504 even without origin
  `Cache-Control`; 400/403/405/412/415 only with `max-age`/`s-maxage`; 401 is in neither list.
  The plan documents and asserts the absence of `CustomErrorResponses`; it does not change it.
- The Lambda memory cache is a validator store, not a freshness cache: every badge request
  revalidates upstream. A freshness window is a separate roadmap decision.

## Onboarding Delta

- The plan now contains an **Engineering Rules** block (allocation discipline, allocation
  verification tiers, no shared fetch collaborator) that is stricter than `coding-style.md`. While
  Wave 3 is in flight, the plan wins for its scope; relocate durable rules to canon at closeout.
- `ResponseHelper` currently depends on `Features.HealthCheck` (`OkHealthWithNoCache`); Wave 3
  moves that into `HealthCheckHandler`. Do not add new `Core → Features` references.
- `LoggerFactory.CreateLogger<T>()` delegates to Microsoft's factory: the category logger is cached
  but the `Logger<T>` wrapper allocates per call. The plan's Work Item 1 claim is scoped to that one
  object; measure, do not inflate.
- `tests/BadgeSmith.Api.Performance.Tests/Program.cs` parses only `--type=`; `--mode`/`--category`
  in its usage text are not implemented. The providers benchmark suite must register `providers`
  in `GetBenchmarkType`.
- Avoid a `BadgeSmith.Api.Tests.Core.*` namespace (shadows `BadgeSmith.Api.Core` for the CORS
  tests). Provider tests go under `...Tests.Features.NuGet` / `...Tests.Features.GitHub`.

## Current State You Should Assume Until Verified

- **HEAD (`master`)**: `b0909926e1978094a888dd7934e7f09f480ff71c` —
  `docs: clarify wave 3 allocation and consistency contracts`.
- **Remote state**: local `master` is 4 commits ahead of `origin/master`; nothing was pushed.
- **Worktree expectation**: clean except the untracked Wave 2 pickup
  `docs/agents/handover-prompts/s1-wave2-safety-net.prompt.md` (superseded by this file; decide
  with Deniz whether to delete or commit it).
- **Pinned versions** (`Directory.Packages.props`): `Amazon.CDK.Lib 2.263.0`, `xunit.v3 3.2.2`,
  `Microsoft.NET.Test.Sdk 18.7.0`, `Amazon.Lambda.TestUtilities 4.1.0` (pinned, not yet referenced
  by the test project), `Microsoft.Extensions.Caching.Memory 10.0.9`.
- **Tests**: not run this session; last known 420/420 unit at `152dfaa`.
- **Active workstream**: Wave 3 hygiene — `docs/plans/2026-08-20-wave3-hygiene.md`, status
  `ready; implementation approval-gated`, nothing implemented.
- **Local-only state**: Node.js v24 and `npx cdk` 2.1138 are present on the authoring machine;
  `build/cdk.context.json` is tracked, `artifacts/` has no production zip.
- **Background work**: none.

## Recommended Next Step

### 1. Increment 1a — Api Test Safety Net (recommended, well-scoped, test-only)

Pre-flight: read the plan's Engineering Rules, Work Item 4 (tests), and the Validation Matrix;
run `dotnet restore BadgeSmith.sln && dotnet build BadgeSmith.sln -c Release --no-restore` and the
`Category=Unit` suite to confirm the 420 baseline before touching anything.

Touch: `tests/BadgeSmith.Api.Tests/Routing/ResponseHelperTests.cs` (redirect status/cache and
no-store decisions against the **current** API), a new allocation-baseline fixture that measures
`GC.GetAllocatedBytesForCurrentThread()` (Release, warmed up) for the cached 200 badge path, cached
and no-store redirects, and test-result route-parameter extraction, and the two Lambda compile-mode
build commands from the Validation Matrix (remember the `EnableTelemetry=false` restore rewrites
`obj/project.assets.json`; re-restore afterwards).

Acceptance: baselines recorded as named constants with measured values; no production change;
Release build, unit suite, Slopwatch, and `git diff --check` clean. Test additions follow the
approval rule in `AGENTS.md` (test code follows the behavior under change; these lock current
behavior).

### 2. Increment 1b — CDK Safety Net (next, approval-gated)

Pre-flight: read the plan's CDK Safety Net section end to end (Project, Test Seam, Logical-ID Drift
Proof, Optional Tidy, Assertions) and `build/BadgeSmith.CDK/README.md`.

Touch: `build/BadgeSmith.CDK.Shared/ProductionStack.cs` (extract public static
`BadgeSmithCloudFrontFactory`), new `tests/BadgeSmith.CDK.Tests` project + solution folder,
`.editorconfig` scoped section, `.github/workflows/ci-cd.yml` (`setup-node` + CDK test step with
its own TRX), `tests/BadgeSmith.CDK.Tests/README.md`, `docs/README.md` role row.

Acceptance: before/after `BadgeSmithStack.template.json` diff shows no logical-ID or property
drift; CDK tests pass locally without AWS credentials or the CDK CLI; optional `ProductionStack`
tidy lands as a separate commit with a second drift proof.

### 3. Increments 2–6 — in plan order

Route hygiene → provider consistency → typed response/cache → Lambda request core → canon and
closeout. Each is independently reviewable; do not batch them.

Talk to Deniz before committing to which one. Work on the current branch unless a concrete reason
requires another, and follow the approval gate in `AGENTS.md` (production, CDK, CI, solution, and
build-policy changes need an explicit `go`/`apply`/`proceed`/`başla`/`yap`).

## Mandatory Grounding

1. `AGENTS.md` — canonical always-on contract and approval gates.
2. `docs/README.md` — documentation authority and temporary plan lifecycle.
3. `docs/plans/2026-08-20-wave3-hygiene.md` — the active implementation contract; read it whole
   once before any increment.
4. `docs/ROADMAP.md` — Wave 3 row and current Backlog.
5. `README.md` and `ARCHITECTURE.md` (cache strategy, routing, security) — product and design.
6. `docs/engineering/coding-style.md` — hand-written C# and test naming.
7. `docs/agents/README.md` and `docs/agents/KNOWN_ISSUES.md` — agent boundaries and triage hints.
8. The files named by the increment you pick under `src/BadgeSmith.Api`,
   `tests/BadgeSmith.Api.Tests`, `build/BadgeSmith.CDK.Shared`, and `.github/workflows`.

## Scope-Specific Guardrails

- The plan's Engineering Rules are binding for this wave: state the allocation delta, keep the
  OneOf result shape, no shared fetch collaborator, no compatibility shims, no
  characterize-then-replace tests.
- `CA1873` stays suppressed with the existing narrow file-scoped pragma in any new logging file;
  do not start the LoggerMessage migration here.
- In `build/`, logical-ID neutrality is a blocker criterion, not a nice-to-have: run the offline
  drift proof before asking for approval to commit the factory extraction.

## Final Steering Note

Start by building and running the unit suite to confirm the 420 baseline, then do Increment 1a as
pure test work and present it for approval; that gives Deniz a reviewable, zero-risk first diff and
gives you the allocation baselines every later gate depends on. Increment 1b is the first
production mutation and the one with the sharpest blocker (logical-ID drift), so run the synth diff
before you show the change. The plan is detailed on purpose; when it is silent, prefer the smaller,
typed, allocation-neutral option and say so in the diff summary.
