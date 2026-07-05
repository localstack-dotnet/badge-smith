---
name: "S1 Deep-dive audit, perf research, iteration 0 spec+plan"
description: "Superseded priming prompt for the 2026-07-02 BadgeSmith handover after the full-repo deep-dive, measured perf research, and original iteration 0 contract-tier design+plan. Retained for historical context only; use docs/ROADMAP.md and the 2026-07-04 RIE-free plan for current routing."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "Claude Fable 5"
---

> Superseded note (2026-07-04): This pickup prompt records the 2026-07-02 state only. Do not use it to start Iteration 0. Use `docs/plans/2026-07-04-rie-free-aspire-localstack-implementation-plan.md` and `docs/ROADMAP.md` for current routing.

You are an engineer entering BadgeSmith right after a research-and-planning wave:
a full-codebase deep-dive audit, a measured performance investigation, and the
iteration 0 (AOT contract-test tier) spec + implementation plan all landed on
2026-07-02 (`6c6f609` + one uncommitted plan file). The single most important state
observation: **no production code has changed** — everything so far is findings, spec,
plan, and baselines. Execution of iteration 0 is approved in principle but has not
started; Deniz had not yet chosen subagent-driven vs inline execution when the session
ended.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-07-02 — S1)** and verify
> against the live repo, git log, `Directory.Packages.props`, `ARCHITECTURE.md`, and
> canonical docs before acting.

## What Just Happened

### 1. AWS Health "provided.al2 deprecation" email triage (context for the day)

The email that started the session does **not** concern this repo: git history proves
BadgeSmith never used `provided.al2` (CDK was born on `PROVIDED_AL2023`; deployed
`badge-smith-function` verified al2023/arm64 via AWS CLI). The flagged functions are
two abandoned 2023 demos in the personal account (`lambda-dotnet-function`,
`profile-service-demo`, eu-central-1, 0 invocations/30d). **Carry-forward, undecided:**
delete vs flip those two; also `couples-threapy-prod/dev` (nodejs8.10, public API
Gateway, ~45k scanner hits/month) awaits a teardown decision. AWS CLI: use
`--profile personal` (account 377140207735).

### 2. Full-repo deep-dive → `docs/research/2026-07-02-code-review-findings.md`

Severity-ordered findings with file:line refs. Headlines the next agent must know:

| Finding | Where |
| --- | --- |
| HMAC `repoIdentifier` = `Owner/Repo/Repo/Branch` (Repo doubled, Platform missing) | `HmacAuthenticationService.cs:42` |
| Badge query builds GSI1PK from non-normalized case (writes are lowercase) | `TestResultsService.cs:86-93` |
| Build scripts default `linux-x64`, CDK expects arm64 zip | `scripts/build-lambda.*:5` |
| Nonce burned before signature validation; 500s leak `ex.Message` | findings §2 |
| Only routing is unit-tested; HMAC/features/ResponseHelper/real RouteTable = zero tests | findings §4 |

Fix waves are defined in the doc (§6) and mirrored in `docs/ROADMAP.md` Backlog.
**Rule: contract tests (iteration 0) pin these bugs as current behavior; fixes are
Wave 1, after iteration 0.**

### 3. Performance research (measured) → `docs/research/2026-07-02-performance-opportunities.md`

Prod measurements (30d CloudWatch): init 105–140 ms, **cold invoke 165–680 ms** (the
`Lazy<T>` graph defers AWS/TLS setup into the first billed invoke at ~0.29 vCPU), warm
1–3 ms, memory 32–49 MB/512 MB, 0.07 RPM, max-concurrency 3 (= three README badges
fetched in parallel → 3 parallel cold starts), 16% cold ratio. CloudFront: 5,683
req/30d, only ~43% absorbed at edge, **52% are 4xx** and error responses carry no
Cache-Control. Decisions section is authoritative: do edge 404-caching + TTL split,
INIT warm-up, `TrimMode=full` + ILC knobs (each measured), result caching; rejected
provisioned concurrency, keep-warm pings, SnapStart (N/A), hand-rolled SIMD (BCL
already vectorized). Honest scorecard: architecture A, memory A-, cold-start execution
B-.

### 4. Iteration 0 spec + implementation plan (superseded workstream)

- Spec (approved): `docs/plans/2026-07-02-iteration0-aot-contract-tier-design.md`
- Plan (approved, **uncommitted at session end**): `docs/plans/2026-07-02-iteration0-aot-contract-tier-plan.md`

Core idea: the Aspire dev loop runs the Lambda as JIT with `ENABLE_LOCALSTACK` — the
shipped AOT binary is never tested. Iteration 0 builds a Testcontainers-native
contract tier running the real image (`provided:al2023` + RIE) against LocalStack +
WireMock (mock/real upstream switch via new `HTTP_NUGET_BASE_URL`/`HTTP_GITHUB_BASE_URL`
env overrides — the only two production touches), a `perf-baseline.sh` harness
(latency + RSS + zip/binary size + mstat → dated JSON in `docs/research/baselines/`),
QEMU-free arm64 cross-compilation, and CI gate + nightly on `ubuntu-24.04-arm`.
13 tasks; Task 12 (CI) requires a fresh explicit approval; "test the tester" drill
(inject a missing `JsonSerializable`, prove the suite goes red) is an acceptance
criterion. Baseline ordering rule: infra tasks land → baseline recorded → only then
Wave 1+/perf iterations.

### 5. Slopwatch baseline

`.slopwatch/baseline.json` committed (22 pre-existing findings: 17× SW002, 3× SW005,
2× SW003 — the SW003s are the intentional retry catches in
`ResilienceRetryHandler.cs:33,37`). Gate command:
`slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"`.

### Verification this session

Docs/planning only — no build or `dotnet test` run. AWS claims verified live via CLI
(`--profile personal`). Slopwatch analyze verified clean against the new baseline.

## Session Learnings (read these — they cost real time to earn)

- **`dotnet-diag:optimizing-dotnet-performance` agent hallucinated an entire report
  with 0 tool calls** (fabricated paths/code). Before trusting any subagent output,
  check its tool-use count and spot-verify one cited file:line. Prefer Explore agents
  for code-grounded work; treat specialist-agent output as hypotheses.
- **Subagents must be told explicitly which skills to load** (Deniz requirement). The
  spec's "Required capabilities" section is the per-task list; put it verbatim in
  every subagent prompt.
- **Git Bash on Windows mangles AWS CLI args starting with `/`** (log group names):
  prefix with `MSYS_NO_PATHCONV=1`.
- **.NET under qemu-user is unsupported** — that's why local arm64 builds failed
  historically. The fix is cross-compilation (`--platform=$BUILDPLATFORM` build stage),
  not newer QEMU. A one-time timeboxed binfmt retry is in plan Task 11.
- **README badge URLs being lowercase is the only thing masking the GSI case bug** —
  don't "clean up" test URLs into mixed case and conclude the service is broken.
- The AWS Health email lists affected resources per **account**, not per repo — check
  deployed reality (`aws lambda list-functions`) before assuming the repo is at fault.

## Current State You Should Assume Until Verified

- **HEAD (`master`)**: `6c6f609` — "docs: add deep-dive findings, perf research,
  iteration 0 design and slopwatch baseline"
- **Worktree**: one untracked file — `docs/plans/2026-07-02-iteration0-aot-contract-tier-plan.md`
  (plus this pickup, if not yet committed). Committing them needs Deniz approval.
- **Tests**: not run this session (docs-only). Last known: routing suite green.
- **Active workstream**: Iteration 0 — plan approved, **execution not started**;
  open question to Deniz: subagent-driven (recommended) vs inline execution.
- **Local-only state**: Docker Desktop available; `badge-smith:local` image NOT built
  yet; slopwatch 0.4.2 installed as a global dotnet tool; AWS `personal` profile has
  working static credentials.
- **Undecided AWS cleanup**: two `provided.al2` demo lambdas + couples-threapy stack
  (see §1) — surface when relevant, don't act without a decision.

## Historical Recommended Next Step

These were the next steps as of 2026-07-02. They are superseded by the 2026-07-04 RIE-free plan and are retained only for context.

1. **Execute iteration 0** (multi-session arc, the default). Pre-flight: read the spec
   + plan (grounding list below), confirm Docker is up, then ask Deniz the pending
   question — subagent-driven vs inline — and start at Task 1 (RIE spike). Follow the
   plan's Global Constraints verbatim (slopwatch per task, CPM via CLI only, pin
   current behavior, Task 12 approval stop). Acceptance: plan tasks checked off through
   Task 10 (baseline recorded) at minimum; Tasks 11–13 complete the wave.
2. **Wave 1 correctness fixes** (well-scoped, only AFTER iteration 0's suite + baseline
   exist) — findings doc §1–2; each fix updates the pinned contract assertions in the
   same change.
3. **AWS account cleanup decision** (lightweight, optional) — the §1 carry-forward;
   needs Deniz's pick, then two CLI calls.

Talk to Deniz before committing to which one. Default to working on `master`; do not
branch unless there is a concrete reason. No commit without explicit
"go / apply / proceed / başla / yap" (AGENTS.md approval gate).

## Historical Grounding Only

Do not use this archived prompt as current routing. If you need the historical context,
read the current sources first, then treat the 2026-07-02 RIE documents as superseded
background only:

1. `AGENTS.md` — canonical contract: approval gate, AOT/Lambda constraints, capability
   routing (`CLAUDE.md` is relay-only).
2. `docs/ROADMAP.md` — current backlog and active workstream routing.
3. `docs/plans/2026-07-04-rie-free-aspire-localstack-implementation-plan.md` — current
   RIE-free Iteration 0 implementation plan.
4. `docs/plans/2026-07-02-iteration0-aot-contract-tier-design.md` and
   `docs/plans/2026-07-02-iteration0-aot-contract-tier-plan.md` — superseded RIE-based
   historical design and task list.
5. `docs/research/2026-07-02-code-review-findings.md` +
   `docs/research/2026-07-02-performance-opportunities.md` — findings and perf
   decisions the original plan built on.
6. `docs/research/baselines/2026-07-04-localstack-smoke.json` — current LocalStack smoke
   baseline from the RIE-free benchmark harness.
7. `docs/agents/README.md` (capability mapping — resolve skill names here) +
   `docs/agents/KNOWN_ISSUES.md`.
8. `ARCHITECTURE.md` / `README.md` as needed for endpoint behavior.

## Historical Policy Recap

This section records the 2026-07-02 session state. Current policy comes from
`AGENTS.md` and the active RIE-free plan.

- No commit without explicit "go / apply / proceed / başla / yap". Conventional
  Commits; **no AI attribution trailers**.
- No feature/refactor/build/CI/CDK mutation without approval. Plan-internal commits
  are pre-approved by the plan, **except Task 12 (CI workflows) — fresh approval**.
- Package versions only via `dotnet add package` (CPM); never hand-edit versions.
- `slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"`
  after every code change (baseline is committed — only NEW slop fails).
- Native AOT discipline: no reflection, JSON types registered in
  `LambdaFunctionJsonSerializerContext`, trim/AOT warnings blocking, UTC only.
- Contract tests pin CURRENT behavior (bugs included) — production bug fixes belong to
  Wave 1, not iteration 0.
- Subagent prompts must explicitly list the skills to load (spec "Required
  capabilities" section).
- Tests are xUnit v3 on VSTest — plain `dotnet test` / `--filter`.

## Historical Steering Note

This session converted an inbox scare (a deprecation email that turned out not to be
ours) into the project's first complete map: verified bugs, measured performance
truth, and an approved plan for the safety net that must exist before anyone touches
`TrimMode`. This steering note is superseded by the 2026-07-04 RIE-free plan and is
retained only to explain the earlier handoff state.
