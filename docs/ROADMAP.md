# BadgeSmith Roadmap

Date: 2026-08-07

Backlog and progress source of truth for BadgeSmith. Keep this current: the status table
is the permanent history, active workstreams may link a temporary detailed plan, and new
ideas land in Inbox / Untriaged until they are scoped.

## Status & Plan Mapping

| Workstream | Status | Plan | Notes |
| --- | --- | --- | --- |
| Agent contract adoption | done | — (landed as a single `docs:` commit) | Re-authored `AGENTS.md`, harness relays, and `docs/agents/` for BadgeSmith after they were copied from another repo |
| Iteration 0 — RIE-free contract coverage and local benchmark harness | done | — | Completed on 2026-07-05 on `feature/iteration0-aot-contract-tier` and squashed into `991769e`: RIE was removed from active contract and benchmark paths; Aspire Testing + `APIGatewayEmulator` covers HTTP contracts; LocalStack ZIP Lambda benchmark execution uses a CDK-created Lambda Function URL fallback because LocalStack Community 4.6 blocks API Gateway v2 CloudFormation resources. Baselines: [final local smoke](research/baselines/2026-07-04-final-localstack-smoke.json), [live direct Gateway smoke](research/baselines/2026-07-04-live-gateway-smoke.json), and [live CloudFront comparison smoke](research/baselines/2026-07-04-live-cloudfront-smoke.json). |
| Wave 1 — correctness and hygiene fixes | done | — | Closed in W1.7 on `feature/iteration0-aot-contract-tier`. HMAC `repoIdentifier` (`845440f`); GSI1PK case normalization; nonce-after-signature; client error-message hygiene; PAT rotation docs; naming hygiene (`5cbf87b`). |
| W1.5 — file-based tooling migration | done | — | Foundation `52d038a`; finished in W1.7: workflows call `tools/badgesmith.cs`, tracked `.sh`/`.ps1` retired, script-facing docs moved to `tools/README.md`, `perf baseline` C# command deferred (see Inbox). |
| W1.7 — closeout and platform refresh | done | — | Packages: Aspire 13.4.6, LocalStack.Aspire.Hosting 13.4.0, explicit Aspire.Hosting.AWS 13.3.1, full CPM stable bump, MessagePack removed. Tooling finish + remaining Wave 1 correctness. |
| PR #5 merge-readiness remediation | superseded by second pass | — | Implemented in `34fe5f7`: restored Native AOT serializer compatibility, hardened malformed HMAC handling and white-label URLs, secured reusable workflow inputs, and corrected Aspire source ownership. Hosted checks passed, but the subsequent whole-PR review found merge-blocking HMAC and CDK issues tracked by the second-pass workstream below. |
| PR #5 second-pass review remediation | review follow-ups in progress; infrastructure gates passed | — | Implemented on 2026-08-07 in `4d9c699`, `9e1344c`, `eae8df3`, and `621f2ce`: hard-cut canonical HMAC authentication, secure badge transport, LocalStack.Client.Extensions 2.0.1, and separate production/local-performance CDK apps. Local evidence: zero-warning Release build, 403 tests, file-based CLI build, actionlint, Slopwatch, package graphs, local-performance CDK synth, and two independent security reviews with no Medium-or-higher findings. Hosted [CI run 31159120605](https://github.com/localstack-dotnet/badge-smith/actions/runs/31159120605) passed `build-and-test` and the ARM64 Native AOT ZIP build/upload on head `fba5c8d`. Production `cdk synth BadgeSmithStack` passed on 2026-08-07 using that hosted ARM64 artifact, pinned AWS CDK CLI `2.1135.1`, and Amazon.CDK.Lib `2.263.0`; no deployment was performed. PR #5 has review comments pending discussion and closure. |

## Process Notes

- For live Lambda/API performance, use the direct API Gateway baseline; CloudFront runs are comparison data because edge caching can reduce Lambda invocations and hide API behavior.
- For Lambda duration, memory, and cold starts, use CloudWatch Lambda `REPORT` lines as the source of truth. k6 client-side cold-start heuristics are only smoke-test hints.

## Backlog

Scoped work waiting to start. Promote an item into Status & Plan Mapping and link its
detailed plan when it becomes active.

- **Wave 2 — test safety net** (next after PR #5): ResponseHelper / real RouteTable /
  NuGetVersionService tests; align resolver tests with production routes. The initial
  HMAC suite landed during PR #5 remediation. Details:
  [research/2026-07-02-code-review-findings.md](research/2026-07-02-code-review-findings.md) §4.
- **Wave 3 — hygiene**: DRY refactors (bootstrap, route-param extraction, package
  services), dead-code removal, script/docs drift, DynamoDB PITR/removal policy.
  Details: findings doc §3, §5.
- **Logging hygiene — source-generated logging migration** (2026-07-02): Replace
  temporary `CA1873` pragmas with `LoggerMessageAttribute` source-generated logging,
  then remove the suppressions and keep the zero-warning build contract.

## Inbox / Untriaged

Raw capture spot for ideas and requests before they are scoped into the backlog.

- Performance pass (cold start + memory footprint) — measured and decided, ready to
  implement:
  [research/2026-07-02-performance-opportunities.md](research/2026-07-02-performance-opportunities.md).
  Agreed levers: edge-side 404 caching + badge TTL increase, eager INIT warm-up,
  `TrimMode=full` + ILC knobs, result caching in package services. Rejected:
  provisioned concurrency, keep-warm pings, SnapStart (N/A on provided.al2023).
  Folds in GitHub issue #1 (RouteValues buffer guard).
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
