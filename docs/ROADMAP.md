# BadgeSmith Roadmap

Date: 2026-07-04

Backlog and progress source of truth for BadgeSmith. Keep this current: the Status &
Plan Mapping table is the permanent index, detailed plans live in `docs/plans/`, and
new ideas land in Inbox / Untriaged until they are scoped.

## Status & Plan Mapping

| Workstream | Status | Plan | Notes |
| --- | --- | --- | --- |
| Agent contract adoption | done | — (landed as a single `docs:` commit) | Re-authored `AGENTS.md`, harness relays, and `docs/agents/` for BadgeSmith after they were copied from another repo |
| Iteration 0 — RIE-free contract coverage and local benchmark harness | implementation in progress | [plans/2026-07-04-rie-free-aspire-localstack-implementation-plan.md](plans/2026-07-04-rie-free-aspire-localstack-implementation-plan.md) | Redirected on 2026-07-04 from a RIE-backed AOT contract tier to a RIE-free design: Aspire Testing for contract/integration coverage and LocalStack for local benchmark execution. LocalStack Community 4.6 blocks API Gateway v2 CloudFormation resources, so the verified local benchmark currently uses a CDK-created Lambda Function URL fallback; old RIE plan kept only as historical context: [plans/2026-07-02-iteration0-aot-contract-tier-plan.md](plans/2026-07-02-iteration0-aot-contract-tier-plan.md). |

## Backlog

Scoped work waiting to start. Promote an item into Status & Plan Mapping (and write a
plan under `docs/plans/`) when it becomes active.

- **Wave 1 — correctness fixes** from the 2026-07-02 deep-dive: HMAC `repoIdentifier`
  bug, GSI1PK case bug, build-script RID default vs CDK arm64, seeder `.dist` JSON fix,
  nonce-ordering + error-message hygiene, PAT rotation. Details:
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
