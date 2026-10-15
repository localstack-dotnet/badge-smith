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
| Wave 2 — test safety net | done | — | Direct coverage now protects `NuGetVersionService` range/prerelease selection and failures; `ResponseHelper` ETag, conditional request, and cache-header behavior; and the production `RouteTable.Routes` inventory and resolver parameter matrix. HMAC, nonce, `ApiRouter`, `TestResultsService`, and ingestion coverage had already landed; the original [gap inventory](research/2026-07-02-code-review-findings.md#4-test-suite-gaps) is historical evidence. |
| Wave 3 — hygiene | done | `plans/2026-08-20-wave3-hygiene.md` (removed at closeout; durable facts moved to canon) | Landed as six increments: shared `LambdaRequestProcessor`; named TestResults route extraction returning a OneOf result with standard `ErrorResponse` 400s (`MISSING_ROUTE_PARAMETER`); provider consistency without a fetch collaborator (`UpstreamCacheEntry`, verbatim weak-ETag replay, `Uri.EscapeDataString`, GitHub label fix, 11-scenario matrix over both providers); typed redirect/cache API (`RedirectStatus`, validated `PublicCachePolicy`, single `BadgeResponsePolicy.PublicCache` preset, exact `no-store` contract, single-allocation strong ETag — OkCached allocation baseline dropped 1208→704 bytes); `ImmutableArray` route inventory; cross-layer CDK safety net (`BadgeSmithCloudFrontTests` project with in-process assertions, public `BadgeSmithCloudFrontFactory` proven logical-ID-neutral by before/after synth diff, hosted CI via setup-node only). Provider allocation evidence: NuGet cold/warm 3.28/3.23 KB, GitHub 4.29/3.95 KB per op. |
| Production delivery hardening — first increment | committed locally; not merged | [approved implementation plan](plans/2026-10-16-roadmap-first-increments.md) | Artifact/source binding, rebuild removal, diff error propagation and bounded GET smoke implementation accepted after R3 (N1 addressed; Standards and Spec ready). All three scopes accepted and combined local validation complete; final whole-package review completed (Standards READY / Spec READY, round 1). Committed locally on `work/first-increments-validation`; not merged to master, pushed, released or deployed. Hosted execution and AWS OIDC trust (H2) remain unverified; GitHub environment branch restriction (H1) and master protection (H4) observed missing ([evidence](research/baselines/2026-10-16-github-delivery-trust.json)). No deployment or AWS operation is authorized. |
| Performance instruments — BDN repair | committed locally; not merged | [approved implementation plan](plans/2026-10-16-roadmap-first-increments.md) | Four-file implementation validated with bounded correctness checks and approved on both independent review axes; committed locally on `work/first-increments-validation`; not merged to master. Local-core CLI, provider corpus expansion, runtime optimization, and precision/production measurements remain outside this increment. |
| Logging hygiene — first CORS slice | committed locally; not merged | [approved implementation plan](plans/2026-10-16-roadmap-first-increments.md) | Three-file implementation validated and independently approved on both axes; committed locally on `work/first-increments-validation`; not merged to master. ARM64 CI baseline recorded ([6,000,572 bytes, 572 over the 6,000,000-byte goal](research/baselines/2026-10-16-d778400-arm64-artifact.json)); deployed identity and candidate size/runtime remain unverified. Retain remaining migration work below. |

## Process Notes

The current performance goals and measurement protocol live in
[`ARCHITECTURE.md`](../ARCHITECTURE.md#performance-goals). Direct API Gateway measurements
represent Lambda/API behavior; CloudFront and k6 results are comparison or smoke evidence,
not substitutes for CloudWatch Lambda `REPORT` data because edge caching can reduce Lambda
invocations and hide API behavior.

## Backlog

Scoped work waiting to start. Promote an item into Status & Plan Mapping and link its
detailed plan when it becomes active.

- **Logging hygiene — remaining source-generated logging migration** (2026-07-02): The first
  CORS slice is integration pending above. For the remaining files, replace
  temporary `CA1873` pragmas with `LoggerMessageAttribute` source-generated logging,
  then remove the suppressions and keep the zero-warning build contract. Retain the existing test
  logger helpers until this work decides whether structured logging is a verified test contract and
  standardizes the corresponding assertion support; they are not a Wave 3 dead-code cleanup target.
  Before broader migration, decide EventId allocation/collision rules (only CORS
  1000/`CorsPreflightRequest` is approved) and address non-generic `SetupILogger`'s unset
  `IsEnabled` before relying on negative log assertions. The disabled-Debug plus null-header
  combination remains a deferred test gap, not a current defect. Measure ARM64 ZIP size per slice;
  the CI baseline above is already over goal and proves neither deployed identity nor candidate compliance.
- **Production delivery hardening — remaining work** (captured 2026-08-08): Artifact binding,
  diff error handling, and post-deploy smoke implementation are integration pending above. Remaining
  work: restrict the `production` environment to `master` deployments and configure master protection
  (H1/H4 observed missing on 2026-10-16, [evidence](research/baselines/2026-10-16-github-delivery-trust.json));
  verify AWS OIDC trust audience/subject (H2 unverified); decide the **unapproved** master-ancestry
  guard proposal, preserving promotion of selected older successful master ancestors rather than
  requiring the latest tip. Historical promotion policy is unchanged; no tag exploit is established.
  Hosted/AWS changes require separate approval.
  The final candidate's production CDK guide records the dated H1/H4 observations and unverified H2.
  Retain nested numeric-PR formatting for now; consider a simpler
  expression and semantic contract checks later, along with low safe-category label nits.
  Also decide DynamoDB PITR/retention policies; define explicit
  abuse controls; choose retry/alert/failure semantics for badge publication; execute a
  hosted ARM64 Native AOT runtime smoke; and document promotion of immutable `v1.x.y`
  action tags plus the moving `v1` alias.
- **Performance pass — Native AOT INIT, memory, and artifact size**: Meet the measurable
  [architecture performance goals](../ARCHITECTURE.md#performance-goals) using the
  [d778400 ARM64 CI artifact baseline](research/baselines/2026-10-16-d778400-arm64-artifact.json)
  (6,000,572 bytes, 572 over the 6,000,000-byte goal; deployed identity, production INIT/memory
  and candidate compliance unverified) and the dated implementation/validation sequence in
  [research/2026-07-02-performance-opportunities.md](research/2026-07-02-performance-opportunities.md).
  Candidate levers: explicit edge error-cache policy and badge TTL tuning (CloudFront already
  has default error caching, including 404s), `TrimMode=full` plus individually measured ILC knobs,
  request-path cleanup, and measurement-led parsed-result reuse only after upstream confirms 304.
  Reuse must never skip per-request upstream revalidation or add a freshness cache; see the
  [cache layers](../ARCHITECTURE.md#cache-layers). JSON-name snapshots, semantic-version and
  selected-winner caching are not implemented or silently approved by this backlog entry.
  Evaluate eager INIT warm-up as a separate trade-off: it may reduce effective
  cold-request duration by increasing CloudWatch `Init Duration`, so report both metrics
  and retain it only if the candidate still satisfies the ≤100 ms INIT p95 goal;
  otherwise reject the change or explicitly revise the goal before proceeding. Rejected:
  provisioned concurrency, keep-warm pings, and SnapStart (not available on
  `provided.al2023`). This work also folds in GitHub issue #1 (RouteValues buffer guard).
- **Benchmark instrument limits** (2026-10-16, deferred from BDN repair): The mirrored route table
  can drift from `RouteTable.Routes`; four-route resolution and five-route allowed-method fixtures
  are different workloads; allowed-method allocation includes the caller's list copy; void
  `RouteValues` measurements need result-consumption/dead-code-elimination validation; and no
  permanent instrument-regression suite was added. Validate these before using allocation columns
  as optimization evidence. Runner exit code alone is not a gate: inspect case selection,
  validation errors and reports as described in the [test guide](../tests/BadgeSmith.Api.Tests/README.md#performance-harnesses).
- **Performance — `perf baseline` local-core CLI** (deferred from W1.7; deferred again 2026-10-16;
  owner: performance domain): The previous `scripts/perf-baseline.{sh,ps1}` +
  `perf-baseline-seed.sh` (~16KB of orchestration) was retired in the W1.7 closeout rather than
  half-ported. Keep the k6 scenario at `scripts/k6-perf-test.js`; re-home the LocalStack seed + k6
  invocation orchestration under `tools/Commands/PerfBaselineCommand.cs` (registered as
  `perf baseline` in `BadgeSmithTool.CreateCommandApp`) as part of the performance pass. Consume the
  dedicated local-performance CDK app from the PR #5 second-pass remediation; do not restore
  stack-selection context in the production app. The 2026-10-16 first increment repaired the BDN
  instruments instead; the proposed attach + local-core profile (explicit local-only target that
  fails closed without `K6_API_URL`, perf-owned fixture tuples with fixed seed timestamps, controlled
  404 thresholds, and an evidence manifest that labels unverified source/artifact linkage) remains
  unapproved. Local results are relative regression evidence only, not production goal evidence.

## Inbox / Untriaged

Raw capture spot for ideas and requests before they are scoped into the backlog.

- **GitHub Packages prerelease-channel filtering:** support selecting the latest `ci`
  prerelease without pinning the base version; tracked in
  [GitHub issue #4](https://github.com/localstack-dotnet/badge-smith/issues/4).
