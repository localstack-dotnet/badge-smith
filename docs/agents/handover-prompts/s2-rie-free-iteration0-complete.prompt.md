---
name: "S2 RIE-free iteration 0 complete"
description: "Priming prompt for the next agent entering BadgeSmith after RIE-free Iteration 0 landed on feature/iteration0-aot-contract-tier on 2026-07-05. Contract tests, local LocalStack benchmark harness, and live Gateway/CloudFront smoke baselines are recorded. Recommended next: choose and execute the first Wave 1 correctness fix."
argument-hint: "Optional focus area, such as HMAC, GSI case normalization, build-script RID alignment, or perf follow-up"
agent: "agent"
model: "gpt-5.5"
---

You are an engineer entering BadgeSmith after the RIE-free Iteration 0 work closed on
`feature/iteration0-aot-contract-tier`. The most important state observation: active
contract and benchmark paths no longer use Lambda RIE; local contract coverage runs
through Aspire Testing and `APIGatewayEmulator`, while the local performance harness uses
LocalStack ZIP Lambda execution with a Function URL fallback.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-07-05 - S2)** and verify
> against the live repo, git log, `Directory.Packages.props`, `ARCHITECTURE.md`, and
> canonical docs before acting.

## What Just Happened

### RIE-free Iteration 0 landed

| Area | Current state |
| --- | --- |
| Contract tests | `tests/BadgeSmith.Api.Tests` starts `src/BadgeSmith.Host` through Aspire Testing and calls `APIGatewayEmulator` over HTTP. |
| RIE removal | `LambdaRieClient` and RIE-based stack fixture paths were removed. RIE references remain only in superseded docs or historical research. |
| Emulator culture fix | `src/BadgeSmith.Host/Program.cs` starts `APIGatewayEmulator` with invariant/C culture so `If-None-Match` does not become Turkish dotless-`i` under culture-sensitive lowercasing. |
| Local benchmark | `scripts/perf-baseline.sh` deploys the LocalStack local performance CDK stack, seeds test data, and runs k6 against a Lambda Function URL fallback when API Gateway v2 CloudFormation is unavailable in LocalStack Community. |
| CDK shape | Shared constructs preserve production API Gateway/CloudFront behavior while adding a LocalStack-only performance stack. |

Primary commits to inspect:

- `2827531` - `test: replace RIE harness with Aspire and LocalStack`
- `9ff91ac` - `docs: record live gateway baseline and Aspire agent notes`

### Verification evidence

Commands verified during the closing session:

- `dotnet build --configuration Release` - passed, 0 warnings/errors.
- `dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category=Unit"` - 292/292 passed.
- `dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category=Functional"` - 28/28 passed.
- `dotnet test "tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj" --filter "Category!=AotContract"` - 320/320 passed.
- `node --check scripts/k6-perf-test.js` - passed.
- `bash -n scripts/perf-baseline.sh` - passed.
- `slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"` - 0 issues.

### Baselines now recorded

| Baseline | File | Key result |
| --- | --- | --- |
| LocalStack final smoke | `docs/research/baselines/2026-07-04-final-localstack-smoke.json` | k6 p95 235.46 ms, peak Lambda worker RSS 20.279 MB. |
| Live direct API Gateway smoke | `docs/research/baselines/2026-07-04-live-gateway-smoke.json` | 64 client requests, 64 Lambda REPORT lines, k6 p95 309.14 ms, Lambda p95 229.96 ms, max memory 49 MB, 1 cold start. |
| Live CloudFront comparison smoke | `docs/research/baselines/2026-07-04-live-cloudfront-smoke.json` | 53 client requests, 25 Lambda REPORT lines, k6 p95 253.06 ms, Lambda p95 194.07 ms, max memory 51 MB, 1 cold start. |

Use the direct API Gateway baseline for Lambda/API measurements. Use the CloudFront
baseline only as an edge-cache comparison because CloudFront can serve requests without
invoking Lambda. CloudWatch Lambda `REPORT` lines are the source of truth for memory,
duration, and cold starts.

## Current State You Should Assume Until Verified

- **Branch:** `feature/iteration0-aot-contract-tier`.
- **HEAD before this handover edit:** `9ff91ac` - `docs: record live gateway baseline and Aspire agent notes`.
- **SDK:** `global.json` pins .NET SDK `10.0.100` with `latestFeature` roll-forward.
- **Pinned packages:** `Aspire.Hosting.AppHost` / `LocalStack.Aspire.Hosting` / `Aspire.Hosting.Testing` `13.1.0`; AWS SDK v4 packages; `xunit.v3` `3.2.1` on VSTest.
- **Tests:** last full non-AOT verification was green as listed above; rerun the relevant slice before changing behavior.
- **Local-only artifacts:** k6 summaries live under ignored `artifacts/`; the durable baseline JSON files are under `docs/research/baselines/`.
- **AWS profile for live checks:** `aws --profile personal --region eu-central-1`.

## Recommended Next Step

1. **Wave 1 correctness fix - HMAC repo identifier** (well-scoped, high impact). Pre-flight: load the relevant debugging/TDD/.NET skills, read `docs/research/2026-07-02-code-review-findings.md` §1, inspect `src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs`, and add/update tests that pin the corrected identifier shape. Acceptance: tests fail before the fix, pass after, and contract expectations are updated only for the intended bug fix.
2. **Wave 1 correctness fix - test-result GSI case normalization** (well-scoped, high impact). Pre-flight: inspect `TestResultsService` read/write key construction and existing functional tests. Acceptance: mixed-case public routes resolve the same stored data as lowercase routes, without weakening current HMAC or route tests.
3. **Performance pass follow-up** (multi-session arc). Pre-flight: read `docs/research/2026-07-02-performance-opportunities.md` and the three baseline JSON files. Acceptance: each change has before/after measured data and does not use CloudFront comparison numbers as the Lambda/API baseline.

Talk to Deniz before committing to which one. Default to the current branch unless there
is a concrete reason to isolate work. No commit without explicit "go / apply / proceed /
başla / yap" and a proposed Conventional Commit message.

## Mandatory Grounding

1. `AGENTS.md` - canonical repository contract: approval gate, AOT/Lambda constraints,
   Aspire MCP/context7 guidance, and capability routing.
2. `docs/ROADMAP.md` - current backlog and Status & Plan Mapping table.
3. `docs/plans/2026-07-04-rie-free-aspire-localstack-implementation-plan.md` - executed Iteration 0 plan.
4. `tests/BadgeSmith.Api.Tests/README.md` - current test categories and RIE-free contract-test shape.
5. `docs/research/baselines/2026-07-04-live-gateway-smoke.json` and
   `docs/research/baselines/2026-07-04-live-cloudfront-smoke.json` - live measurement context.
6. `docs/research/2026-07-02-code-review-findings.md` - Wave 1/2/3 backlog source.
7. `ARCHITECTURE.md` and `README.md` as needed for endpoints and runtime shape.

## Locked Policy Recap

- `AGENTS.md` is canonical; harness relays are adapters.
- No production bug fix, feature, refactor, build/CI/CDK mutation, deploy, push, or PR without approval.
- Package versions live in `Directory.Packages.props`; use `dotnet add/remove/list`, not manual package-version edits.
- Native AOT discipline stays active: source-generated JSON, no reflection-heavy shortcuts, no DI container, UTC-only time APIs, warnings as errors.
- Tests are xUnit v3 on VSTest. Use standard `dotnet test --filter`; do not use TUnit or MTP-only filter syntax.
- Run Slopwatch after LLM-authored code/test changes when available.

## Final Steering Note

Iteration 0 is now a safety-net foundation, not a feature endpoint. The next useful move
is to take one Wave 1 correctness fix at a time, prove the current behavior with a failing
test, fix the bug minimally, then rerun the direct slices. Keep performance changes
measured and keep CloudFront comparison data separate from Lambda/API baseline data.
