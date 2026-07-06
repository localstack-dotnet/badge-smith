# BadgeSmith Roadmap

Date: 2026-07-06

Backlog and progress source of truth for BadgeSmith. Keep this current: the Status &
Plan Mapping table is the permanent index, detailed plans live in `docs/plans/`, and
new ideas land in Inbox / Untriaged until they are scoped.

## Status & Plan Mapping

| Workstream | Status | Plan | Notes |
| --- | --- | --- | --- |
| Agent contract adoption | done | — (landed as a single `docs:` commit) | Re-authored `AGENTS.md`, harness relays, and `docs/agents/` for BadgeSmith after they were copied from another repo |
| Iteration 0 — RIE-free contract coverage and local benchmark harness | done | [plans/2026-07-04-rie-free-aspire-localstack-implementation-plan.md](plans/2026-07-04-rie-free-aspire-localstack-implementation-plan.md) | Completed on 2026-07-05 on `feature/iteration0-aot-contract-tier` and squashed into `991769e`: RIE was removed from active contract and benchmark paths; Aspire Testing + `APIGatewayEmulator` covers HTTP contracts; LocalStack ZIP Lambda benchmark execution uses a CDK-created Lambda Function URL fallback because LocalStack Community 4.6 blocks API Gateway v2 CloudFormation resources. Baselines: [final local smoke](research/baselines/2026-07-04-final-localstack-smoke.json), [live direct Gateway smoke](research/baselines/2026-07-04-live-gateway-smoke.json), and [live CloudFront comparison smoke](research/baselines/2026-07-04-live-cloudfront-smoke.json). Old RIE plan kept only as historical context: [plans/2026-07-02-iteration0-aot-contract-tier-plan.md](plans/2026-07-02-iteration0-aot-contract-tier-plan.md). |
| Wave 1 — correctness and hygiene fixes | in progress | — | Started on `feature/iteration0-aot-contract-tier` after Iteration 0. Landed so far: HMAC `repoIdentifier` correction in `845440f`, and test/benchmark naming convention cleanup in `5cbf87b`. Remaining correctness backlog is listed below. |
| W1.5 — file-based tooling migration | in progress | [superpowers/plans/2026-07-06-w1-5-file-based-tools-implementation-plan.md](superpowers/plans/2026-07-06-w1-5-file-based-tools-implementation-plan.md) | Checkpoint landed on 2026-07-06 in `52d038a`: `global.json` now pins SDK `10.0.301`; `tools/badgesmith.cs` is a hosted file-based CLI with Spectre/CliWrap/LocalStack.Client-backed commands; the old standalone seeder project was removed; `tools/organization-pat-mapping.json.dist` is valid JSON; linked-source tool tests pass. Remaining W1.5 work: migrate GitHub workflows, retire tracked `.sh` / `.ps1` scripts, add or defer the perf baseline command, and update script-facing docs. Pickup: [agents/handover-prompts/s3-w1-5-file-based-tooling-checkpoint.prompt.md](agents/handover-prompts/s3-w1-5-file-based-tooling-checkpoint.prompt.md). |

## Process Notes

- For live Lambda/API performance, use the direct API Gateway baseline; CloudFront runs are comparison data because edge caching can reduce Lambda invocations and hide API behavior.
- For Lambda duration, memory, and cold starts, use CloudWatch Lambda `REPORT` lines as the source of truth. k6 client-side cold-start heuristics are only smoke-test hints.

## Backlog

Scoped work waiting to start. Promote an item into Status & Plan Mapping (and write a
plan under `docs/plans/`) when it becomes active.

- **Wave 1 — remaining correctness fixes** from the 2026-07-02 deep-dive: GSI1PK case
  bug, nonce-ordering + error-message hygiene, PAT rotation. HMAC `repoIdentifier` is
  fixed in `845440f`; the new W1.5 tool defaults Lambda builds to `linux-arm64` and
  moves/fixes the seeder `.dist` JSON in `52d038a`, but legacy tracked scripts still
  remain until W1.5 workflow migration finishes.
  Details:
  [research/2026-07-02-code-review-findings.md](research/2026-07-02-code-review-findings.md) §1–2.
- **Wave 2 — test safety net**: HMAC / ResponseHelper / real RouteTable /
  NuGetVersionService tests; align resolver tests with production routes. Details:
  findings doc §4.
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
