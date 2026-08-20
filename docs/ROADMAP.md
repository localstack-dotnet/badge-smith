# BadgeSmith Roadmap

Backlog and progress source of truth for BadgeSmith. Keep completed entries concise and
link detailed closeout evidence instead of turning this table into a changelog. Active
workstreams may link a temporary plan; new ideas land in Inbox / Untriaged until scoped.

## Status & Plan Mapping

| Workstream | Status | Plan | Notes |
| --- | --- | --- | --- |
| Agent contract adoption | done | — (landed as a single `docs:` commit) | Re-authored `AGENTS.md`, harness relays, and `docs/agents/` for BadgeSmith after they were copied from another repo |
| Iteration 0 — RIE-free contract coverage and local benchmark harness | done | — | Completed on 2026-07-05 as `991769e`. Current contract and benchmark topology lives in the [test guide](../tests/BadgeSmith.Api.Tests/README.md) and [architecture](../ARCHITECTURE.md#local-development). Baselines: [local](research/baselines/2026-07-04-final-localstack-smoke.json), [direct Gateway](research/baselines/2026-07-04-live-gateway-smoke.json), and [CloudFront](research/baselines/2026-07-04-live-cloudfront-smoke.json). |
| Wave 1 — correctness and hygiene fixes | done | — | Closed in W1.7 on `feature/iteration0-aot-contract-tier`. HMAC `repoIdentifier` (`845440f`); GSI1PK case normalization; nonce-after-signature; client error-message hygiene; PAT rotation docs; naming hygiene (`5cbf87b`). |
| W1.5 — file-based tooling migration | done | — | Foundation `52d038a`; finished in W1.7: workflows call `tools/badgesmith.cs`, tracked `.sh`/`.ps1` retired, script-facing docs moved to `tools/README.md`, `perf baseline` C# command deferred (see Inbox). |
| W1.7 — closeout and platform refresh | done | — | Completed the stable central-package refresh, removed MessagePack, and finished the tooling and Wave 1 correctness work. See the [dated closeout evidence](research/2026-07-09-iteration0-wave1-closeout.md); `Directory.Packages.props` owns current versions. |
| PR #5 merge-readiness remediation | superseded by second pass | — | Implemented in `34fe5f7`: restored Native AOT serializer compatibility, hardened malformed HMAC handling and white-label URLs, secured reusable workflow inputs, and corrected Aspire source ownership. Hosted checks passed, but the subsequent whole-PR review found merge-blocking HMAC and CDK issues tracked by the second-pass workstream below. |
| PR #5 second-pass review remediation | done | — | Hard-cut HMAC, secure transport, LocalStack client alignment, separate CDK apps, and review hardening merged as `2b147de`; see the [closeout evidence](research/2026-08-08-pr5-second-pass-closeout.md) for commits, CI runs, deployment observations, tags, and smoke checks. |

## Process Notes

The current performance goals and measurement protocol live in
[`ARCHITECTURE.md`](../ARCHITECTURE.md#performance-goals). Direct API Gateway measurements
represent Lambda/API behavior; CloudFront and k6 results are comparison or smoke evidence,
not substitutes for CloudWatch Lambda `REPORT` data because edge caching can reduce Lambda
invocations and hide API behavior.

## Backlog

Scoped work waiting to start. Promote an item into Status & Plan Mapping and link its
detailed plan when it becomes active.

- **Wave 2 — test safety net** (next planned engineering wave): Add coverage for
  `ResponseHelper`, the real `RouteTable`, and `NuGetVersionService`; align resolver tests
  with production routes. HMAC, nonce, `ApiRouter`, `TestResultsService`, and ingestion
  coverage has already landed. The original gap inventory remains
  [historical evidence](research/2026-07-02-code-review-findings.md#4-test-suite-gaps),
  not the current work list.
- **Wave 3 — hygiene**: Revalidate candidate DRY refactors (bootstrap, route-param
  extraction, and package services) and dead-code cleanup against the current tree before
  promotion. The original opportunities are
  [historical evidence](research/2026-07-02-code-review-findings.md#3-refactoring-opportunities-duplication--design)
  rather than the current work list.
- **Logging hygiene — source-generated logging migration** (2026-07-02): Replace
  temporary `CA1873` pragmas with `LoggerMessageAttribute` source-generated logging,
  then remove the suppressions and keep the zero-warning build contract.
- **Production delivery hardening** (captured 2026-08-08): Pin deploy artifacts to a
  successful workflow run, head SHA, and verified digest; stop masking genuine CDK diff
  errors; add automated post-deploy health and badge smoke checks; configure production
  environment protection; decide DynamoDB PITR/retention policies; define explicit
  abuse controls; choose retry/alert/failure semantics for badge publication; execute a
  hosted ARM64 Native AOT runtime smoke; and document promotion of immutable `v1.x.y`
  action tags plus the moving `v1` alias.
- **Performance pass — Native AOT INIT, memory, and artifact size**: Meet the measurable
  [architecture performance goals](../ARCHITECTURE.md#performance-goals) using the
  measured baseline and agreed implementation/validation sequence in
  [research/2026-07-02-performance-opportunities.md](research/2026-07-02-performance-opportunities.md).
  Agreed levers: edge-side 404 caching + badge TTL increase, `TrimMode=full` +
  individually measured ILC knobs, result caching in package services, and request-path
  cleanup. Evaluate eager INIT warm-up as a separate trade-off: it may reduce effective
  cold-request duration by increasing CloudWatch `Init Duration`, so report both metrics
  and retain it only if the candidate still satisfies the ≤100 ms INIT p95 goal;
  otherwise reject the change or explicitly revise the goal before proceeding. Rejected:
  provisioned concurrency, keep-warm pings, and SnapStart (not available on
  `provided.al2023`). This work also folds in GitHub issue #1 (RouteValues buffer guard).

## Inbox / Untriaged

Raw capture spot for ideas and requests before they are scoped into the backlog.

- **GitHub Packages prerelease-channel filtering:** support selecting the latest `ci`
  prerelease without pinning the base version; tracked in
  [GitHub issue #4](https://github.com/localstack-dotnet/badge-smith/issues/4).
- **Deferred from W1.7:** `perf baseline` C# command — the previous
  `scripts/perf-baseline.{sh,ps1}` + `perf-baseline-seed.sh` (~16KB of orchestration)
  was retired in the W1.7 closeout rather than half-ported. Keep the k6 scenario at
  `scripts/k6-perf-test.js`; re-home the LocalStack seed + k6 invocation orchestration
  under `tools/Commands/PerfBaselineCommand.cs` (registered as `perf baseline` in
  `BadgeSmithTool.CreateCommandApp`) after Wave 2 or as part of the performance pass.
  Consume the dedicated local-performance CDK app from the PR #5 second-pass
  remediation; do not restore stack-selection context in the production app.
