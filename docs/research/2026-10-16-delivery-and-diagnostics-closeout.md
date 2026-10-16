# Delivery and diagnostics closeout

**Evidence snapshot: 2026-10-16.** This records accepted merge, local validation,
CI/artifact-verification and operator reports at that date. It is not a fresh hosted/AWS
inspection, deployment record, production-performance result or authorization for further
operations. Historical approval restrictions below describe their own stages; later events
do not retroactively broaden them.

## Scope and approval chronology

The accepted October 16 handoff records [PR #7](https://github.com/localstack-dotnet/badge-smith/pull/7),
[PR #8](https://github.com/localstack-dotnet/badge-smith/pull/8) and
[PR #9](https://github.com/localstack-dotnet/badge-smith/pull/9) merged. The source snapshot is
[`df953f247aed5d03d89b4d1af6bb78e10214effb`](https://github.com/localstack-dotnet/badge-smith/commit/df953f247aed5d03d89b4d1af6bb78e10214effb),
parent `45681dda442b53fb88752b75fd0fb11114bdb7ac`, tree
`2a2ed3f176214502ad3e038d757e3fd47e3872bc`, reported equal to the reviewed PR #7 head
`96889d3` tree. The merged package includes delivery/provenance/ancestry and GET smoke,
the narrow logging slices, benchmark repair and fake-provider harness, zlib ZIP packaging,
the PR CI-trigger correction, and the Host-project MessagePack security override/badge guard.

The two consumed scope records remain immutable primary provenance:
[first increments](https://github.com/localstack-dotnet/badge-smith/blob/df953f247aed5d03d89b4d1af6bb78e10214effb/docs/plans/2026-10-16-roadmap-first-increments.md)
and [second wave](https://github.com/localstack-dotnet/badge-smith/blob/df953f247aed5d03d89b4d1af6bb78e10214effb/docs/plans/2026-10-16-roadmap-second-wave.md).
Their unique scope, review dispositions and validation limits are preserved here; their
then-unmerged wording is not current status.

### First package

Deniz separately approved the first package on October 16 against
`d778400bd16cee44994f1c9bb68121e6a54d66cf`: artifact/source binding, no deploy-time
rebuild, real diff failure propagation and bounded GET smoke; BDN instrument repair;
and only the CORS logging slice. Standards and Spec were separate acceptance axes.
The user-directed historical execution arrangement gave PM priority/scope/shared-docs and
approval ownership, domain leads implementation/tests/independent review/fixes, and
integration/quality combined evidence and final review. PM was not a mandatory per-file
reviewer or artifact packager. This is a record of that execution, not a standing staffing rule.

The original permission allowed existing declared dependency restore and focused local
fixture/build/test validation, not new packages, analyzer relaxation, test weakening,
Docker/Aspire startup, precision benchmarks, commits, push/PR/merge/release, AOT publishing,
CDK synth/deploy, AWS or live production smoke. Shared documentation and domain files had
separate owners; another stream's frozen work was not disposable. Dry runs proved
executability, not performance. Untrusted inputs/evidence had to fail closed; real JSON
contracts, not HTTP 200 alone, governed smoke. No production seeding or mutation was allowed.

Delivery passed R1→R2→R3 as described below; BDN R1 and logging R1 accepted their scoped
packages on both axes. At domain acceptance, the delivery eight-file, BDN four-file and
logging three-file diffs were frozen and uncommitted; hosted/smoke checks were still unrun.
Domain acceptance was followed by a separate 21-file combined gate,
not a merge or native-runtime claim. Documentation originating on the older `f3ab12b`
checkout was refreshed as docs-only deltas onto the validated `d778400` candidate; the dirty
root and its pre-existing Wave 2 handover were not the commit source or package contents.

Later, a separate approval authorized four local commits only: `316b316` (ci), `1358fcd`
(test), `fd6d2c6` (refactor), `eb13185` (docs). At that local closeout they were unmerged,
unpushed and undeployed; hosted Actions, AWS trust, AOT/candidate ARM64 size, INIT and memory
were still unverified. Those statements precede the later merge/hosted reports below.

### Second wave

A separate October 16 approval covered explicit master-history binding, processor-only
logging and a measurement-first provider harness. Isolated layers used the accepted
first-package prerequisite; uncommitted parent changes were not assumed to appear in new
copies, and original candidates/evidence remained frozen. Existing dependency restore,
focused local builds/tests/fixtures and one scheduled quiet, exclusive, bounded Short measurement
were allowed. New package/analyzer/build policy, infrastructure startup, staging/commits,
external publication, AOT/deployment/AWS/settings and production HTTP were excluded.
Routine choices within scope needed no repeat approval; material scope/external/security
changes still escalated. A free validation slot was not permission for extra runs.

Before integration, all three domains independently reported Standards/Spec READY. Delivery
already documented validation-time ancestry and oversized-comparison availability limits;
logging reported no native-performance proof; performance froze its isolated eight-case
Short result with wide intervals and no cache justification. At that point combined
validation/final review, candidate ARM64 size and trust/runtime remained separate unknowns.
The subsequent combined local acceptance is recorded in the next section.

A later separate approval authorized `3bde3fc` (ci), `e8b5c34` (refactor), `0802a59`
(test) and `33b256a` (docs), layered on the committed first package. It superseded only
the reviewed package's staging/local-commit restriction, not push, PR, merge, release,
AOT, AWS, settings or production HTTP restrictions. Later accepted merge, hosted CI and
operator reports are distinct events, not authority inherited from either plan.

## Local validation and review

These are results reported in the accepted local scope records, not tests rerun for this note.
Focused counts are subsets, not additive totals; local first/second and hosted suites are
different evidence identities.

| Stage | Recorded result and limit |
| --- | --- |
| First package: 21 files on `d778400` | All package fingerprints matched; Unit 508/508, focused CORS 29/29, workflow contracts 5/5, delivery Node 33/33, two selected BDN Dry cases. Affected Release and telemetry/LocalStack-disabled builds, generated CORS metadata, Slopwatch and diff checks were clean; no source fixes were needed. Final independent combined review: Standards READY / Spec READY, round 1, no Critical/Important findings. |
| Second wave on committed first package | Unit 522/522 (focused processor 18, CORS 29, workflow contracts 5), delivery Node 104/104; eight provider Dry cases with one job each. Release/feature-disabled builds had no warnings; generated events 1000–1002, strict Slopwatch and diff checks passed. Final independent combined review: Standards READY / Spec READY, round 1, no blockers. Test-double readability nits remained deferred. |
| Isolated provider Short | Two providers × 200/304 × 3/32 names; fake upstreams only, literal-result preflight and warm priming outside measurement. One explicit Short, not a default/Long/full-suite precision campaign. Four flat methods expand to eight cases. Wide three-sample intervals, no retained-heap/component A/B/cache-ROI/production proof. |

The [provider baseline](baselines/2026-10-16-provider-benchmark-short.json) owns exact results,
host/job identity and export hashes. Its measured identity is isolated `d778400` plus
first-package prerequisite v1 (`57dcfd99bff66f6d13f9bd8178f1191a11047522b9880f182f72278640f2d0f8`)
plus performance delta (`20f759bca2bba28685d2527a7a8dd08eabce60f0ff13a1aeb04d7d8a033e8439`),
not the combined second-wave candidate, `eb13185`, master, a native build or deployment.
Raw exports are identified by the baseline's full-JSON and CSV SHA-256 values and retained
outside the repository; no personal checkout path is needed to name the experiment.

The unchanged [pre-increment ARM64 baseline](baselines/2026-10-16-d778400-arm64-artifact.json)
records run 35340178681 / attempt 1, artifact 10544353895 at full `d778400`, an archive
digest matching metadata and an inner ZIP of 6,000,572 bytes: **−572 bytes headroom**
against the unchanged 6,000,000-byte target. Its observed timestamp is October 15 UTC,
grouped under the October 16 investigation. It proved size/ELF inspection only, not deployed
identity or INIT/memory. It is not overwritten by the later master result below.

## Master CI and artifact

The accepted master CI/artifact verification report dated October 16 records
[run 38903269306, attempt 1](https://github.com/localstack-dotnet/badge-smith/actions/runs/38903269306/attempts/1)
for same-repository `ci-cd.yml` push/master at full `df953f2` above:

| Evidence | Recorded observation |
| --- | --- |
| Hosted validation | `build-and-test` succeeded: .NET 559/559, CDK 4/4, delivery Node 104/104; no restore NU errors/warnings, zero IL warnings. These suite counts remain distinct. |
| Guarded automatic badge publication | Passed/failed/skipped 559/0/0, HTTP 201, no manual POST. This CI publication corrected the earlier zero-count record; it was not stack/Lambda deployment or production smoke. |
| Hosted artifact job | `continuous-deployment` succeeded in ARM64 AOT/package/provenance/upload work. Its job name is not evidence of deployment. |
| Selected master artifact | ID `12047446938`, name `lambda-zip-38903269306-1`, run 38903269306 / attempt 1; reported not expired at inspection, expiry `2026-11-15T13:01:51Z`. Repository and head-repository identities matched. The PR artifact was not reused. |
| GitHub download archive | 5,968,184 bytes; SHA-256 `f402a45755032bde833a09fd6d4198312a24d5d23476ebe71613cf34b6230951`, reported equal to API digest and CI `ARCHIVE_DIGEST`. Downloaded once. |
| Inner `badge-lambda-linux-arm64.zip` | 5,992,808 bytes; SHA-256 `0cda2b1f84e54ab54cbeeb100b78b3b3382815f4e6685ff936b82e4625d0e04f`; **7,192 bytes below** the exact 6,000,000-byte target. This is not the download archive's digest or byte length. |
| ZIP/payload inspection | One `bootstrap`, Deflate, Unix `0100755`, flags 0, no extra fields; CRC `67bccaf9`, integrity check passed; 14,453,032 uncompressed / 5,992,692 compressed bytes; ELF64 little-endian AArch64 (`e_machine=183`). Reported byte-identical to PR #7 candidate payloads, **not executed**. |
| Manifest | Repository/source/run/attempt and inner ZIP digest/byte count matched the selected master artifact. |
| Offline eligibility | Master's `validateProvenance` and `verifyArchive` accepted read-only fetched data offline. Run creation timestamp `2026-10-16T12:49:38Z`; explicit resolved master `df953f2`; source-to-master comparison identical, ahead/behind/total 0, matching base/merge-base. The history rule and all recorded eligibility gates passed at that check time only. |

No `deploy.yml` execution, artifact promotion or Lambda/stack deployment occurred during
this closeout. Ancestry, availability and expiry are time-bound, not perpetual eligibility.
AL2023 execution, current deployed identity, production INIT/memory and live IAM/CDK
compatibility were not established. The small ZIP headroom was accepted; it is not
permission for another build, a target change or a new CI run merely to log zlib's runtime version.

The accepted handoff also distinguishes the successful hosted badge-publication path from
offline missing/empty-report negative fixtures. Those fixtures are not wired into hosted CI;
partial valid TRX and reporter-exception limits were not promoted into newly approved work.

## Trust and IAM timeline

The unchanged [early trust baseline](baselines/2026-10-16-github-delivery-trust.json)
records GitHub-only GET observations at `2026-10-15T21:37:54.269267+00:00`, grouped under
October 16: H1/H4 absent, default repository OIDC subject configuration, AWS H2, actual
minted token claims and tag-run hypothesis unverified. No settings/AWS mutation accompanied
that discovery; next-wave preparation then remained read-only. This is an earlier snapshot,
not the later control state and not proof AWS could actually be assumed.

The later accepted October 16 handoff records:

| Control | Dated reported state and limit |
| --- | --- |
| H1 | Production deployment branch policy only Branch `master`; no tag/wildcard policy. |
| H3 | Repository default OIDC subject configuration retained, without a subject-template change. This is not inspection of an actual minted token. |
| H4a/H4b | Master ruleset `25233817`: PR required, zero approvals, deletion/force-push blocked, no bypass actors; required `build-and-test` tied to GitHub Actions integration `15368`, no strict/up-to-date requirement. Zero approvals gives traceability, not independent human review. |
| H2 | The authorized Windows operator's redacted completion report, “H2 and entry-role permissions — reported applied, 2026-10-16,” reports real authorized mutations/readback and scoped permission simulations. The coordinating session did not independently query AWS. Live OIDC/CDK deployment compatibility remained pending. |
| Canceled environment R1 | Required-reviewer/admin-bypass changes were rejected/canceled and not applied. The last reported environment had no reviewer/timer and `can_admins_bypass=true`. This differs from ruleset bypass actors; R1 is not a deployment prerequisite or a proposal to reopen. |

For `badge-smith-github-actions-role`, the operator reported exact audience
`sts.amazonaws.com` and only subject
`repo:localstack-dotnet/badge-smith:environment:production`; the extra master-ref subject
was removed. Direct grants were limited to AssumeRole/TagSession for the selected
eu-central-1 CDK deploy/file-publishing/lookup roles. An existing explicit deny was retained,
PowerUserAccess detached, no managed policies remained attached and no permissions boundary
was reported. The reported mutation order was inline permissions, narrowed trust, then
PowerUserAccess removal. Provider/bootstrap/other-project roles, stacks and GitHub settings
were reported unchanged by that AWS operation.

Operator readback, drift checks and allow/deny simulations passed for their recorded matrix:
the three intended roles were allowed; direct cfn-exec/image-publishing, other-region and
other-project role cases and representative direct service operations were denied. Access
Analyzer permission checks were reported clean; its audience suggestion and repository/branch
trust warning were retained, with no invented `:ref` condition. A scoped audit reported
51 principals checked, zero exposed, ten service-linked roles skipped; that does not prove
all indirect role chains isolated. Fingerprints were abbreviated operator-supplied hashes,
not complete hashes independently recomputed by the coordinator; full snapshots were not
embedded in the accepted report. Role IaC ownership was operator-reported, not mechanically
settled merely by absent tags or limited checkout references.

These are dated/operator-attributed findings, not fresh hosted enforcement, live AssumeRole
or deployment proof. The shared CDK deploy-role → cfn-exec-role path can still reach reported
AdministratorAccess: end-to-end least privilege and account/project isolation were not
established. No automatic widening rollback was approved; PowerUserAccess recovery would
require Deniz's decision while preserving environment-only trust. Shared bootstrap and
other projects were outside scope. Action SHA pins, token least privilege, Actions PR-approval
settings and dedicated/scoped bootstrap remain passive separate decisions, not new prerequisites.
Token ability to dispatch workflows depends on its actual authorization; no blanket exploit
claim follows. Old recommendations to add R1 and old unmerged wording are superseded.

## Delivery review dispositions

The first package accepted same-repository successful completed push/master selection;
explicit run/attempt/source/artifact IDs; distinct archive/inner-ZIP digests and bytes;
verified-source CDK checkout with no Lambda rebuild; and genuine diff failures blocking deploy.
Missing, expired, mismatched or ambiguous evidence fails before deployment. The
[production guide](../../build/BadgeSmith.CDK/README.md#ci-artifact-promotion) owns current
selection, archive layout and source/control checkout mechanics; the
[dated pinned-CLI evidence](2026-10-16-delivery-cdk-diff-semantics.md) preserves the inspected
package/hash/source reasoning. It was not reverified or redated by this closeout.
At the first local closeout, that note still labeled production branch restrictions
unverified while the early trust JSON had observed them absent. This was retained as a
dated-document nit, not rewritten into a current dependency or deployment claim; the
timeline above distinguishes the later reported control state.

The accepted smoke scope prevalidates real target inputs, checks health/test/NuGet JSON via
GET at both direct Gateway and public origins (six checks), with a shared 180-second budget,
10 seconds per request/body and at most five attempts. Only transient failures retry.
Malformed JSON, HTML 200, redirects, permanent failures, timeout/cancellation/deadline or
retry exhaustion fail. There is no data seed, HMAC write or automatic rollback; local fixtures
never call AWS or prove a live smoke outcome. A failed post-deploy smoke may follow an actual
production change and does not itself authorize recovery.

### R1–R3 resolution ledger

- **Delivery R1:** Spec was ready; Standards required scoped diagnostic/documentation fixes.
  R2 approved both axes after those corrections. These review-round labels are unrelated to
  the later canceled environment-R1 proposal.
- **D1:** useful safe diagnostics, not blanket redaction and not raw exception logging.
  Only finite owned categories and validated origin/check/numeric status/attempt context
  are allowed. No arbitrary exception text, bodies, URLs, tokens or credentials; network
  and unknown failures remain generic. Both usefulness and redaction were tested.
- **D2/D3:** move pinned CLI version/hash/source locations to dated research; retain current
  workflow-owned pins and operating rules. Only the obsolete tools build-use sentence was
  corrected in that delivery scope, not unrelated tooling ownership.
- **D4:** retain the correct nested numeric-PR formatting and existing contract tests.
  Flattening/semantic contract checks are deferred minor maintenance, not grounds to weaken tests.
- **D5:** repository code alone does not enforce hosted environment/OIDC trust. Then-unverified
  control requirements were documented without authorizing settings/IAM changes; the ordered
  timeline above owns later reports.
- **D6:** retain non-master dispatch skipping as a no-deployment outcome, not successful
  deployment. No new failure-policy change was required for that low-severity UX observation.
- **D7:** older successful master runs remain permitted if all provenance gates pass.
  Master-tip-only was not the accepted policy; old artifacts without the new manifest fail closed.
- **S5/S6:** CI provenance summary gained code fences. Existing checkout layout was retained;
  no current configuration-inheritance defect was demonstrated. A future inheritance change
  requires its own focused decision.
- **R2 N1:** only the forged known-category/context regression test needed correction.
  Removing `instanceof` checks in a scratch mutation had left older tests green; production
  guards were already correct. The sole Node-test-file change exercised the actual outer-CLI
  guard, with independent fingerprint/scope checks and an offline mutation check, not a source fix.
- **N2–N4:** safe-category default-label nits, generic unknown diagnostics for malformed external
  responses/output files, and one long tools-guide line were deferred. No gate/redaction defect
  justified expanding a test-only fix. D7 remained unchanged.
- **R3:** N1 ADDRESSED, Standards ready / Spec ready unchanged. **I1/I2** were informational:
  the fetch fixture is sanitized before the guard, exercised by the outer-CLI test; existing
  positive/negative context assertions suffice without optional exact-output equality. No
  further fix was required. Node 33/33 and later 21-file combined acceptance are separate from
  then-unrun hosted/smoke/trust checks.

### Second-wave ancestry acceptance

The selected source must equal or precede the explicitly resolved full `refs/heads/master`
SHA. Source is compare BASE, resolved master is HEAD; coherent identical/ahead evidence
passes, and missing/malformed/inaccessible/divergent or wrong-direction evidence fails
closed before download. Finite observed identity/comparison/run-time evidence is retained,
not signed URLs. Ancestry membership is validation-time only. The 1 MiB metadata budget can
reject a valid large ancestor comparison as unavailable evidence; this availability cost is
not proof of a live tag-named-master exploit, hosted trust or permanent eligibility.

## Benchmark review dispositions

The original bounded repair replaced custom `--type` with standard BDN selection and argument
forwarding, explicit noninteractive filtering, truthful measurements of the same resolver,
construction outside route-resolution measurement, and the actual ingestion template
`/tests/results/{platform}/{owner}/{repo}/{branch}`. Genuine buffer-allocation comparisons
were retained. Synthetic mechanisms could remain synthetic, but not claim obsolete route
semantics. Focused failing checks avoided accidentally launching the old full suite.

Only class-level job declarations were removed from Buffer/Routing/Provider suites; diagnostics,
categories and then-existing provider bodies/setup/corpus were retained. Removing Provider's
`[SimpleJob]` was specifically approved so CLI Dry would not coexist with an extra default job.
Each selected case had one Dry job; old/new timing comparisons require explicit matching job
and case settings, not assumed matching defaults. The repair itself did not approve local-core
CLI, corpus expansion, runtime/cache changes or precision/production measurements. The later
eight-case harness/one Short approval was a separate exception, not a rewrite of that history.

The [test guide](../../tests/BadgeSmith.Api.Tests/README.md#performance-harnesses) owns current
discovery/Dry invocation and interpretation. Preserved BDN R1 rulings:

- **D1/S1:** retain the currently equivalent mirrored route table for the narrow repair.
  Live `RouteTable.Routes` use and mirror-comment clarification were deferred; future route
  drift remains a real instrument risk, not a newly blocking defect.
- **D2/P2 and D3/P1:** retain standard runner exit behavior. Inspect selected counts, validation
  errors and reports; exit zero alone is not a fail-closed correctness gate. Explicit list/Dry
  commands were added, not a speculative runner wrapper, argument policy or new CI gate.
- **D4/S3:** benchmark-name convention remained unresolved; applicability of unit-test `When`
  naming to measurement labels was not decided. No second case-identity rewrite was required.
- **D5/D6:** allowed-method allocation includes the caller's list copy; void `RouteValues`
  measurements still need result-consumption/dead-code-elimination validation. Allocation
  columns must not be treated as only internal resolver cost without instrument validation.
- **D7/D8:** focused negative method/bare-path correctness was checked, but broader failure-path
  timing and a permanent instrument-regression suite were not added. Scratch red/green evidence
  is retained as repair evidence; automatic future regression detection remains weaker.
- **S2:** four-route resolution and five-route allowed-method fixtures are distinct requested
  workloads, not equivalent measurements. A clarifying comment was deferred.

### Measurement-first provider harness

The separate second-wave scope retained the deterministic three-name fixture and added 32
names, two providers × 200/304 × two sizes = eight fake-upstream cases. Literal expected-result
preflight and warm priming stay outside measurement; no network, credentials, new package or
range/prerelease Cartesian expansion was approved. One explicit Short/net10.0 job with raw
exports was allowed; an inconclusive/disappointing result was valid, not permission to add caches.

The validator briefly expected eight flat-list lines. BDN lists four method identities before
parameter expansion; Dry/CSV show eight cases. That count criterion was a checking error, not
a source defect. The isolated Short identity is recorded above and in the unchanged baseline,
not relabeled combined/master evidence. NuGet's three-name 304 allocated 3,304 B/op versus
3,288 for 200; three samples yield wide intervals, especially GitHub 32-name 200.

MemoryDiagnoser measures allocation per operation, not retained cache heap. Component A/B,
cache ROI, precision significance, Native AOT/ARM64, production latency/INIT/memory remain
unmeasured. Both provider services still revalidate on every request; 304 reuses raw JSON but
still deserializes and parses/filters versions. JSON-name snapshots do not remove semantic
version parsing or network revalidation. Extra lazy state, raw-plus-name retention, exception
memoization, selected-winner/semantic-version caches and freshness shortcuts require evidence
and explicit approval; no production cache feature follows from these timings.

## Logging review dispositions

The first slice was confined to `CorsHandler.cs`, `TestBase.cs` and
`HandlePreflightTests.cs`. Only metadata **1000 / CorsPreflightRequest** changed observably:
Debug, `CORS preflight request: Origin={Origin}, Method={Method}, Headers={Headers}`.
Field order/values, call order, null exception and HTTP behavior remained; tests covered
enabled null/empty/whitespace values and disabled Debug with populated headers. Assertions
did not depend on generated internal state names. Only the CORS CA1873 pragma pair was removed;
TestBase's Moq expression-tree pragma and unused helpers were retained.

Logging R1 accepted both axes, independently inspected generated metadata/frozen hashes, and
kept the valid static partial method without imposing a global static/instance style. The
ToString-rendered-message helper and all-level enabled toggle were retained as equivalent
for this slice, with level asserted separately. The disabled-Debug plus null-header-dictionary
combination was deferred: enabled-null and populated-disabled are covered, and no current
defect was found, but that combined future-refactor regression is not directly pinned.

The first review identified non-generic `SetupILogger`'s unset `IsEnabled` as a broader
generated-logging hazard, outside its three files. **The second wave resolved that support
hazard**; it is not an active unaddressed task. Broader helper/readability work and repository-wide
EventId allocation/collision rules remain deferred; only 1000–1002 were approved.

The separate processor-only slice accepted **1001 / LambdaRequestHandling**, Information,
`Handling {Method} {Path}`, and **1002 / LambdaUnhandledError**, Error, `Unhandled error`
with the exact caught exception. Request-ID string scope/lifetime/disposal, log call order,
raw Method/Path and fallback values, original request/token handoff, CTS/timeout construction
and try boundaries remained unchanged. Cancellation still maps to the existing safe 500,
not a new cancellation/exception policy. Success/error/cancellation/scope isolation and
per-level enabled/disabled combinations were tested, retaining CORS contracts without a
generic logging framework. ApiRouter/HMAC/nonce/provider changes and global EventId policy
were excluded. The [processor source](../../src/BadgeSmith.Api/Core/LambdaRequestProcessor.cs)
owns exact runtime details; remaining migration/readability work belongs to the roadmap.

## Remaining limits and current owners

- No fresh PR/settings/artifact/AWS/runtime verification is implied. Actual minted claims,
  live IAM/CDK compatibility, deployed revision/ZIP, AL2023 execution and production INIT/memory
  remain unverified; no live tag exploit is established. Check-time eligibility is not future
  artifact availability. Historical code acceptance, local fixtures, hosted packaging and
  actual artifact execution are different proofs.
- [ROADMAP](../ROADMAP.md#backlog) owns delivery/logging/performance/instrument/local-core
  deferrals, including PITR/retention, abuse controls, badge-publication failure semantics,
  runtime smoke and immutable action-tag promotion. The canceled R1 and passive security
  choices above are not added prerequisites or new authorized work.
- [Production operations](../../build/BadgeSmith.CDK/README.md) owns current promotion,
  diff, finite diagnostics and GET smoke. Older successful master ancestors remain eligible
  when all gates pass; no master-tip-only restriction is introduced.
- [Runtime cache contract](../architecture/runtime-and-routing.md#cache-layers) owns validator
  revalidation; [test guidance](../../tests/BadgeSmith.Api.Tests/README.md#performance-harnesses)
  owns instrument invocation. [Platform goals](../architecture/platform-and-delivery.md#performance-goals)
  and [measurement policy](../architecture/platform-and-delivery.md#measurement-policy) own
  deployed-revision production proof. Existing research and all baseline JSONs remain distinct
  historical evidence, not rewritten current observations.
