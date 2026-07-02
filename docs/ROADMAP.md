# BadgeSmith Roadmap

Date: 2026-07-01

Backlog and progress source of truth for BadgeSmith. Keep this current: the Status &
Plan Mapping table is the permanent index, detailed plans live in `docs/plans/`, and
new ideas land in Inbox / Untriaged until they are scoped.

## Status & Plan Mapping

| Workstream | Status | Plan | Notes |
| --- | --- | --- | --- |
| Agent contract adoption | done | — (landed as a single `docs:` commit) | Re-authored `AGENTS.md`, harness relays, and `docs/agents/` for BadgeSmith after they were copied from another repo |
| Iteration 0 — AOT contract tier, baseline harness, multi-arch build | plan approved, execution pending | [plans/2026-07-02-iteration0-aot-contract-tier-plan.md](plans/2026-07-02-iteration0-aot-contract-tier-plan.md) | Spec: [plans/2026-07-02-iteration0-aot-contract-tier-design.md](plans/2026-07-02-iteration0-aot-contract-tier-design.md); pickup: [agents/handover-prompts/s1-deep-dive-and-iteration0.prompt.md](agents/handover-prompts/s1-deep-dive-and-iteration0.prompt.md); execution mode (subagent vs inline) undecided |

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

- Testing-strategy decisions (2026-07-02, agreed, to be spec'd as iteration 0):
  Testcontainers-based contract tier running the REAL published AOT artifact in
  `public.ecr.aws/lambda/provided:al2023` via RIE (Aspire dev loop stays but never
  exercises the trimmed binary); point the prod binary at LocalStack via
  `AWS_ENDPOINT_URL*` env vars (verify SDK v4 support — first assumption to validate);
  local baseline harness recording latency + RSS + binary size + mstat before any
  change. Architecture split: arm64 artifacts built by QEMU-free cross-compilation
  (`--platform=$BUILDPLATFORM` build stage + clang cross toolchain — .NET under
  qemu-user is unsupported and was the cause of local arm64 build failures); for local
  contract-test containers, first refresh binfmt/QEMU and attempt arm64 once
  (best-effort, timeboxed), fall back to amd64 without insisting (trim-failure class is
  arch-independent); benchmarks always run amd64 locally (emulated numbers are
  meaningless); native arm64 verification happens in CI on `ubuntu-24.04-arm` as a
  deploy gate.
  Fold in the build-script RID default fix (findings doc §1.3).

- Performance pass (cold start + memory footprint) — measured and decided, ready to
  implement:
  [research/2026-07-02-performance-opportunities.md](research/2026-07-02-performance-opportunities.md).
  Agreed levers: edge-side 404 caching + badge TTL increase, eager INIT warm-up,
  `TrimMode=full` + ILC knobs, result caching in package services. Rejected:
  provisioned concurrency, keep-warm pings, SnapStart (N/A on provided.al2023).
  Folds in GitHub issue #1 (RouteValues buffer guard).
