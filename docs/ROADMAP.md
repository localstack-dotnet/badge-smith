# BadgeSmith Roadmap

Backlog and progress source of truth for BadgeSmith. Keep completed entries concise and
link detailed closeout evidence instead of turning this table into a changelog. Active
workstreams may link a temporary plan; new ideas land in Inbox / Untriaged until scoped.

## Status & Plan Mapping

| Workstream | Status | Plan | Notes |
| --- | --- | --- | --- |
| Agent contract adoption | done | — (landed as a single `docs:` commit) | Re-authored `AGENTS.md`, harness relays, and `docs/agents/` for BadgeSmith after they were copied from another repo |
| Iteration 0 — RIE-free contract coverage and local benchmark harness | done | — | Completed on 2026-07-05 as `991769e`. Current contract and benchmark topology lives in the [test guide](../tests/BadgeSmith.Api.Tests/README.md) and [local development](engineering/local-development.md#apphost-and-contract-tests). Baselines: [local](research/baselines/2026-07-04-final-localstack-smoke.json), [direct Gateway](research/baselines/2026-07-04-live-gateway-smoke.json), and [CloudFront](research/baselines/2026-07-04-live-cloudfront-smoke.json). |
| Wave 1 — correctness and hygiene fixes | done | — | Closed in W1.7 on `feature/iteration0-aot-contract-tier`. HMAC `repoIdentifier` (`845440f`); GSI1PK case normalization; nonce-after-signature; client error-message hygiene; PAT rotation docs; naming hygiene (`5cbf87b`). |
| W1.5 — file-based tooling migration | done | — | Foundation `52d038a`; finished in W1.7: workflows call `tools/badgesmith.cs`, tracked `.sh`/`.ps1` retired, script-facing docs moved to `tools/README.md`, `perf baseline` C# command deferred (see Inbox). |
| W1.7 — closeout and platform refresh | done | — | Completed the stable central-package refresh, removed MessagePack, and finished the tooling and Wave 1 correctness work. See the [dated closeout evidence](research/2026-07-09-iteration0-wave1-closeout.md); `Directory.Packages.props` owns current versions. |
| PR #5 merge-readiness remediation | superseded by second pass | — | Implemented in `34fe5f7`: restored Native AOT serializer compatibility, hardened malformed HMAC handling and white-label URLs, secured reusable workflow inputs, and corrected Aspire source ownership. Hosted checks passed, but the subsequent whole-PR review found merge-blocking HMAC and CDK issues tracked by the second-pass workstream below. |
| PR #5 second-pass review remediation | done | — | Hard-cut HMAC, secure transport, LocalStack client alignment, separate CDK apps, and review hardening merged as `2b147de`; see the [closeout evidence](research/2026-08-08-pr5-second-pass-closeout.md) for commits, CI runs, deployment observations, tags, and smoke checks. |
| Wave 2 — test safety net | done | — | Direct coverage now protects `NuGetVersionService` range/prerelease selection and failures; `ResponseHelper` ETag, conditional request, and cache-header behavior; and the production `RouteTable.Routes` inventory and resolver parameter matrix. HMAC, nonce, `ApiRouter`, `TestResultsService`, and ingestion coverage had already landed; the original [gap inventory](research/2026-07-02-code-review-findings.md#4-test-suite-gaps) is historical evidence. |
| Wave 3 — hygiene | done | `plans/2026-08-20-wave3-hygiene.md` (removed at closeout; durable facts moved to canon) | Request core, named TestResults extraction, provider consistency, typed cache/redirect API, immutable route inventory and `BadgeSmith.CDK.Tests` safety net landed. The [archived specification snapshot and outcome](research/2026-08-20-wave3-spec-ready.md) retain the revision ledger, accepted contracts and recorded allocation/synth evidence. |
| Production delivery hardening — first increment | deployed in `fdf4c4a` | [dated closeout](research/2026-10-16-delivery-and-diagnostics-closeout.md#delivery-review-dispositions) | Artifact/source binding, deploy-time Lambda rebuild removal, diff error propagation and bounded GET smoke are deployed. The subsequent [production packet](research/2026-10-18-post-deploy-production.md) records the promoted package readback; successful deploy/smoke remains distinct from performance qualification and full least-privilege certification. |
| Performance instruments — BDN repair | merged in `df953f2` | [dated closeout](research/2026-10-16-delivery-and-diagnostics-closeout.md#benchmark-review-dispositions) | Standard runner arguments, truthful measurement/setup boundaries, current ingestion routes and class-job removal are merged. Local-core CLI, runtime optimization and precision/production measurements were outside this repair. |
| Logging hygiene — first CORS slice | merged in `df953f2` | [dated closeout](research/2026-10-16-delivery-and-diagnostics-closeout.md#logging-review-dispositions) | CORS event 1000/`CorsPreflightRequest` and supporting tests/helpers are merged. The [6,000,572-byte ARM64 baseline](research/baselines/2026-10-16-d778400-arm64-artifact.json) is pre-increment evidence, not the later master artifact result. |
| Roadmap second wave — ancestry binding, processor logging, provider benchmark harness | merged in `df953f2` | [dated closeout](research/2026-10-16-delivery-and-diagnostics-closeout.md) | Master-history binding preserves eligible older ancestors. Processor events 1001/`LambdaRequestHandling` and 1002/`LambdaUnhandledError` are source-generated. The eight-case fake-provider harness is merged; its [Short baseline](research/baselines/2026-10-16-provider-benchmark-short.json) measured an isolated performance copy, not the combined candidate or master. |
| Production evidence — observation-first increment | Authorized wider read executed; page-capped partial population | [collection procedure](plans/2026-10-16-production-evidence-collection.md) | The [frozen last-24-hour read](research/2026-10-17-production-evidence-window.md) collected **413 rows / 102 reported INIT** in 20 pages, then stopped with continuation; 29 AWS attempts including authentication, matching before/after configuration, all 14 pilot rows reconciled. Prefix p95 **135.15 ms INIT / 54 MB memory** numerically exceeds goals; both count minimums hold, but complete-window/source/clean-interval/executed-package/consumed-ZIP proof and formal compliance remain **UNVERIFIED**. Safe template/history/API checks did not identify deployed source. The [accepted diagnostic](research/2026-10-17-production-evidence-diagnostic.md) and [Phase A packet](research/2026-10-17-production-evidence-phase-a.md) remain unchanged; no further read or mutation is implied. |
| Response allocation — first optimization | deployed in `fdf4c4a`; performance unqualified | [local A/B evidence](research/2026-10-17-response-allocation-optimization.md) | Span validator parsing and direct header composition reduce measured response allocations **704→584 B** without validators, **1,696→584 B** for a weak-tag list, and **352→264 B** for redirects. **2,320 actual local x64 Native AOT invocations** passed. A [test-owned OTLP receiver](research/2026-10-17-functional-telemetry-fixture.md) resolved the validation stall without disabling telemetry or raising timeouts; the full API project passed **579/579 twice**, no skips. The deployed ARM64 ZIP is **5,993,723 bytes** with matching current package readback; native startup/resident-memory benefit and formal production goals remain unproven. |
| Startup allocation — application-version lookup | deployed in `fdf4c4a`; performance unqualified | [startup evidence](research/2026-10-17-startup-version-lookup.md) | Default version metadata is evaluated only when requested, not when unrelated settings initialize. Focused native diagnostic: **78,272→0 B** for the unused lookup; four fresh-process value/caching contracts preserved, **579 API tests** and a further **2,320 local native invocations** passed. Cache deferral and blanket HTTP-handler lazy changes were not applied. Hosted ARM64 package and current byte binding are verified; no proven production INIT/RSS gain or logger rewrite. |
| Post-deployment production evidence | approved read closed; partial population | [post-deploy observations](research/2026-10-18-post-deploy-production.md) | Package readback matches `fdf4c4a`'s promoted ZIP. **264 REPORTs / 86 reported INIT**, 13 continued pages, 22/24 permitted HTTP dispatches with matching closing function/routing snapshots. Collected-prefix p95 **135.06 ms INIT / 51 MB memory / 465.34 ms paired cold timing** is descriptive only: terminal pagination, 100 cold samples and formal qualification remain unmet. No further read, traffic or mutation follows from this closed pass. |
| Complete post-deploy available-window retrieval | terminal pagination complete; cold count insufficient | [complete available window](research/2026-10-18-post-deploy-complete-window.md) | New bounded autonomous follow-up completed **272 REPORTs / 90 INIT**, 15 pages (last empty/no token), 24/32 HTTP dispatches and matching package/routing closing checks. Window ends 15:51:05Z; all eight rows beyond the earlier packet are after its 14:48 cutoff. Available-window p95 **135.06 ms INIT / 52 MB memory / 465.34 ms paired cold timing** is not a causal old/new comparison. Ten natural cold observations are still missing for INIT qualification; no synthetic traffic, new deployment or periodic monitor was created. |
| Template construction — retained allocation candidate | current-package local gates passed; separate hosted PR/delivery pending | [current validation](research/2026-10-19-template-construction-release.md) / [historical study](research/2026-10-18-template-construction-optimization.md) | Fresh constructor RED **752>751 B** → GREEN **392 B**, **629 API +4 CDK**, Node104+14/Slopwatch0; new Native build0diagnostics and AL2023 **162/162** compatibility responses/upstream. Old **4,152→2,520 B** Native counter remains historical; no new speed/RSS/SLO claim. |
| Cold-request attribution — provider, TLS and official AL2023 | local investigation recorded; certificate-directory product change rejected as unqualified | [dated investigation](research/2026-10-19-native-first-request-attribution.md) / [offline evidence replay](research/reproducers/2026-10-19-native-first-request/README.md) | Existing BDN completed **8 out-of-process cases / 24 Actual samples**. Framing was not the dominant local first-request cost. Official AL2023 default-only Native cohort: **162 responses**, first feature **35.1–38.6 ms**, post81 RSS **29,100–29,124 KiB**; not Lambda INIT/memory or a production gain. All 21 observed processes, including one invalid cohort, remain separated in the public numeric packet. Next: application-startup attribution and one code candidate; no new LocalStack deployment or production collection performed. |
| Latest-stable dependency refresh | deployed in `2846586` through PR #11 | [delivery evidence](research/2026-10-19-package-refresh.md#remaining-boundaries-and-continuation) / [Native smoke](research/baselines/2026-10-19-package-native-smoke.json) | Latest stable packages; **579 API +4 CDK**, Native/AL2023 compatibility and hosted CI passed. New master artifact promoted by deploy39305427499/1; Code.S3Key-only live diff and **6 GET smoke checks passed**. ARM64 ZIP **6,027,393 B**, user accepted27,393B design-target deviation; not a target PASS/AWS limit. H2 excluded; INIT/memory remain unqualified. |

The accepted 2026-10-16 closeout records successful master CI and check-time artifact
eligibility, but no Lambda/stack deployment during that closeout. It does not establish
current deployed identity, AL2023 execution, or production INIT/memory compliance.

## Process Notes

The current performance goals and measurement protocol live in
[platform architecture](architecture/platform-and-delivery.md#performance-goals). Direct API Gateway measurements
represent Lambda/API behavior; CloudFront and k6 results are comparison or smoke evidence,
not substitutes for CloudWatch Lambda `REPORT` data because edge caching can reduce Lambda
invocations and hide API behavior.

## Backlog

Scoped work waiting to start. Promote an item into Status & Plan Mapping and link its
detailed plan when it becomes active.

- **Logging hygiene — remaining source-generated logging migration** (2026-07-02): The first
  CORS slice (1000/`CorsPreflightRequest`) and the second-wave `LambdaRequestProcessor` slice
  (1001/`LambdaRequestHandling`, 1002/`LambdaUnhandledError`) are merged above. For the
  remaining files, replace
  temporary `CA1873` pragmas with `LoggerMessageAttribute` source-generated logging,
  then remove the suppressions and keep the zero-warning build contract. Retain the existing test
  logger helpers until this work decides whether structured logging is a verified test contract and
  standardizes the corresponding assertion support; they are not a Wave 3 dead-code cleanup target.
  Before broader migration, decide repository-wide EventId allocation/collision rules (only
  1000–1002 are approved). The non-generic `SetupILogger` now supports `IsEnabled`, so negative
  log assertions through it are meaningful. Test readability (owner: logging domain):
  `LambdaRequestProcessorTests` has two hand-rolled test loggers, the newer making the older mostly
  redundant, and one theory uses two logger doubles; consolidate when those tests are next touched. The disabled-Debug plus null-header
  combination remains a deferred test gap, not a current defect. Measure ARM64 ZIP size per slice.
  The [dated master closeout](research/2026-10-16-delivery-and-diagnostics-closeout.md#master-ci-and-artifact)
  records a 5,992,808-byte hosted ARM64 ZIP, 7,192 below the unchanged 6,000,000-byte target;
  it does not establish deployed identity, AL2023 execution, or production INIT/memory.
- **Production delivery hardening — remaining work** (captured 2026-08-08): Artifact binding,
  diff error handling, post-deploy smoke and master-history ancestry binding are merged above.
  The [accepted 2026-10-16 closeout](research/2026-10-16-delivery-and-diagnostics-closeout.md#trust-and-iam-timeline)
  records master-only production branch policy and master protection requiring PRs and
  `build-and-test` with zero approvals, plus operator-reported narrowed OIDC trust and scoped
  direct role permissions. Those dated reports are not fresh hosted-state or live OIDC/CDK
  compatibility proof. Promotion resolves the master SHA and accepts that commit or an
  older successful ancestor, failing closed before download; comparisons larger than 1 MiB fail
  closed. Live deployment/smoke and OIDC/CDK compatibility remain unverified by the closeout,
  and no tag exploit is established.
  Hosted/AWS changes require separate approval.
  Retain nested numeric-PR formatting for now; consider a simpler
  expression and semantic contract checks later, along with low safe-category label nits.
  Also decide DynamoDB PITR/retention policies; define explicit
  abuse controls; choose retry/alert/failure semantics for badge publication; execute a
  hosted ARM64 Native AOT runtime smoke; and document promotion of immutable `v1.x.y`
  action tags plus the moving `v1` alias.
- **Performance pass — Native AOT INIT, memory, and artifact size**: Meet the measurable
  [architecture performance goals](architecture/platform-and-delivery.md#performance-goals). The
  [pre-increment d778400 baseline](research/baselines/2026-10-16-d778400-arm64-artifact.json)
  was 6,000,572 bytes, 572 over the unchanged 6,000,000-byte target. The
  [accepted master closeout](research/2026-10-16-delivery-and-diagnostics-closeout.md#master-ci-and-artifact)
  records a 5,992,808-byte hosted ARM64 ZIP, 7,192 below that target. The subsequent
  [post-deploy packet](research/2026-10-18-post-deploy-production.md) records the promoted
  5,993,723-byte ZIP and matching current package readback. Operational deployment/smoke
  is established; complete revision-qualified INIT/memory goals remain unverified.
  The [2026-10-19 local attribution study](research/2026-10-19-native-first-request-attribution.md)
  records provider/first-feature evidence and the official AL2023 fixture boundary. Its
  certificate-directory branch is unqualified for a product change; return to application startup
  and select one measured code candidate rather than treating more local baseline tables as goal completion.
  The dated implementation/validation sequence remains in
  [research/2026-07-02-performance-opportunities.md](research/2026-07-02-performance-opportunities.md).
  Candidate levers: explicit edge error-cache policy and badge TTL tuning (CloudFront already
  has default error caching, including 404s), `TrimMode=full` plus individually measured ILC knobs,
  request-path cleanup, and measurement-led parsed-result reuse only after upstream confirms 304.
  The 2026-10-16 provider Short baseline ([evidence](research/baselines/2026-10-16-provider-benchmark-short.json))
  covers fake-upstream provider paths only: the NuGet 3-name 304 path allocates 3,304 B/op versus
  3,288 B/op for 200, and three-sample intervals are wide (especially GitHub 32-name 200). Retained
  heap, component A/B benefit and cache ROI are unmeasured, so no production cache change follows.
  Reuse must never skip per-request upstream revalidation or add a freshness cache; see the
  [cache layers](architecture/runtime-and-routing.md#cache-layers). JSON-name snapshots, semantic-version and
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
