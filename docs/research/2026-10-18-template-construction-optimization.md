# Template construction — local allocation candidate

**Evidence date: 2026-10-18.** The selected constructor-only H2 change reduced actual once-only
route-table construction from **4,152 to 2,520 allocated bytes: 1,632 B saved (~39.3% of that
component only)** in every one of ten formal Native AOT pairs across two blocks. A separate fixed,
warmed managed constructor fixture measured **752→392 B (360 B saved)**. These are different
workloads/proof domains, not additive savings or MB-level Lambda memory reductions.

> **No frozen median-gate regression detected; no demonstrated latency or RSS improvement.**

This is a validated **local allocation-only candidate**, not shipped code or a production
release. Fresh reproduction and contract skeptics independently confirmed the material result;
the subsequent [final review](baselines/2026-10-18-template-construction-review.json) passed the repaired
public-package, completeness and whole-candidate Standards/Spec gates. **Local candidate validated;
publication pending.** The active [plan](../plans/2026-10-18-performance-candidate.md)
and [specification](../plans/2026-10-18-performance-candidate-spec.md) remain present.

**2026-10-19 follow-up:** [current-package release validation](2026-10-19-template-construction-release.md)
records fresh RED/GREEN, managed tests and a new Native compatibility artifact. It does not rewrite
this dated study's source, counters, pending-at-authoring labels or lack of latency/RSS benefit.

## Sources and working-byte identity

Repository: `localstack-dotnet/badge-smith`; authoring HEAD
`40cb349af214a642741faecf757676fa69eefbf8`, tree
`8126d09f4fb71ce4de0d8eececbf477e7bc0424d`. That HEAD tree equals deployed `fdf4c4a`'s tree,
but **B is an uncommitted working-byte change**, not a new deployed revision. A was the original
316-file working freeze, including eight preexisting dirty documentation paths, not a HEAD archive.
The measured B freeze had 318 files: the three reviewed source/test deltas below and two approved
goal plans. This later packet/status update is not relabeled the source that built those binaries.

| Reviewed shipping file | SHA-256 |
| --- | --- |
| `src/BadgeSmith.Api/Core/Routing/Patterns/TemplatePattern.cs` | `06d544a8a20ea0465b382aaaaa038bfb0c99b456710f5d71cbed00860f771ee5` |
| `tests/BadgeSmith.Api.Tests/Routing/Patterns/TemplatePatternTests.cs` | `62975ef7fdabbf6061138e938c18edf636508f4cd557269749969f4f05e82b7c` |
| `tests/BadgeSmith.Api.Tests/Routing/AllocationBaselineTests.cs` | `f12e97b081ba7a76e76d22a2332f2e271295103f75a8dfe4c92858c988b97ba8` |

Only constructor mechanics changed: count separators and enumerate span ranges, allocating the
same retained arrays/literal strings/unbraced keys without the temporary split strings/array.
Signature, nullability, leading-slash trimming, empty/trailing segments, brace classification,
duplicate-key behavior and later decoding remain. The entire matcher/suffix is byte-identical
(`2a64d361e2e9a6a44f270b4194ed332bad5c418f049aa50ac60518badfc20fa6`), including the actual
**OrdinalIgnoreCase** literal comparison. The extra scan is not free CPU by assumption; no CPU or
speed win follows from an allocation counter.

No logger/configurator, Settings, handlers, cache, AWS/security, serializer context, build/package,
analyzer, CI/CDK, Dockerfile or CLI behavior changed. Native AOT/trim safety and production
telemetry/LocalStack exclusions remain load-bearing. Existing response/ETag/cache/CORS/redirect,
every-request upstream revalidation, HMAC/nonce/replay/UTC and logging/version contracts remain the
validation subject, not changes licensed by this measurement.

## Native attribution and frozen acceptance

DA/DB are **source-only diagnostics**, separate from uninstrumented A/B. Both have identical probe,
Program and explicit RouteTable static-constructor bytes. The explicit constructor prevents hidden
beforefieldinit prewarming. The exact original collection expression still constructs all six
descriptors/five TemplatePatterns. Fixed numeric/matching buffers initialize outside the primary
interval. Start/finish current-thread counters enclose the synchronous construction only, before
marker emission; all descriptors/order/methods and representative named matches, including POST,
are consumed afterward without HandlerResolver/AWS-client construction. First/reuse matching is
measured separately. No thread-local counter is carried across an await.

Before any B/DB application execution, eight uninstrumented A/A main pairs, four separate A/A health
pairs and five DA/DA pairs calibrated their own domains. The original frozen acceptance receipt SHA
is `6b1021600ca9245bcc95e441b4381abf09666444cf63165fe6cc81ae0e37fb2f`;
freeze UTCns `1792349487858963388`, monotonicns `1036893968295875`. Public projection SHA is
separate. All baseline DA route observations were 4,152 B; all DA/DA differences and empty/idle
controls were zero. Meaningful floor `max(256, 2×0)=256 B`; native budget `4,152−256=3,896 B`.

| Formal diagnostic result | Block 1 (five pairs) | Block 2 (five pairs) |
| --- | --- | --- |
| DA route bytes, every process | 4,152 | 4,152 |
| DB route bytes, every process | 2,520 | 2,520 |
| A−B saving, min/median/max B | 1,632 / 1,632 / 1,632 | 1,632 / 1,632 / 1,632 |
| Startup B−A bytes, median [range] | −1,456 [−12,136, −424] | −3,240 [−20,712, **+3,000**] |
| First/reuse match bytes, both | 0 / 0 | 0 / 0 |
| Cumulative global A−B bytes, range | −48,792..91,160 | −57,264..60,632 |

The predeclared synchronous no-increase criterion is **per-block median(B−A)≤0**, not every pair.
The +3,000 B startup pair remains visible. Assertions over the artifact/PID-bound native observations
produced ten old-budget assertion failures and ten candidate passes, zero test errors. This is
post-collection native consumer assertion replay, not another shipping test or pre-edit native TDD.

Global windows mix background console/runtime work and are noncausal corroboration. Their final
marker ends **before** sidecar flush and final RuntimeSupport response serialization. They are not
complete lifetime allocations, retained object sizes, live heap or a proven global net saving.
The explicit constructor and extra probe roots also mean complete diagnostic startup is not
unmodified production startup. No profiler window alone is used as causal attribution.

## Uninstrumented timing and resident memory

Two balanced blocks each contain eight AB/BA process pairs, with the starting order reversed in
block2: **32 fresh main processes**, each first NuGet +16 labeled warmups +64 retained warm calls.
Warm statistic is the median of the retained 64 **within each process**, not 2,048 independent
process samples. Four additional balanced pairs run health→first NuGet. Diagnostic timing is not
pooled into these uninstrumented observations.

`ready=tReady−t0`; `first=completeBody−send`; `paired=ready+first`;
`gap=send−tReady`; `total=paired+gap`. t0 is immediately before native Popen inside the already
running fixture container. Both clocks are sampled after the final response body byte, before
parsing/assertions. PID/hash/RSS inspection occurs after the sequence, while the owned child lives;
there is no measurement barrier between readiness and first event. Same-row identities are checked.

All deltas below are **B−A**, median [full paired range], ms except RSS/HWM KiB:

| Main metric | Block 1 | Block 2 | Frozen margin |
| --- | --- | --- | --- |
| Ready | −0.539 [−4.560, 2.555] | −0.576 [−2.493, 4.319] | 9.308 |
| First NuGet | −0.144 [−3.011, 5.964] | +1.718 [−6.496, 4.136] | 7.992 |
| Same-process ready+first | −1.313 [−3.765, 6.120] | +1.328 [−8.989, 7.267] | 14.785 |
| Total first completion | −1.316 [−3.781, 6.117] | +1.330 [−9.030, 7.231] | **14.789** |
| Retained warm median | −0.033 [−0.104, 0.057] | +0.017 [−0.135, 0.078] | 0.204 |
| Complete 81-event sequence | −4.063 [−12.410, 12.011] | +2.266 [−13.605, 9.474] | 25.314 |
| VmRSS / VmHWM KiB | −98 [−324, 204] | −120 [−320, 228] | 608 |

Margins were frozen from A-only data: `max(5% A median, 2×maximum absolute A/A paired difference)`;
RSS/HWM use `max(512 KiB, 2×difference)`. All main fields pass in each block; all separate health
fields pass their independently calibrated median bounds. The health warm margin is 0.977289 ms.
The main-total latency-claim floor is 14.788944 ms. These broad noise-calibrated median detectors are
not tail/equivalence bounds, universal per-process guarantees, SLOs or proof the extra scan costs
nothing. Timing/RSS ranges cross zero, and block2 timing medians are slower. Host load1 was
**1.061..2.173**, ordinary host load rather than perfect idleness. Finite host aggregate CPU deltas
are retained, not mislabeled native-process CPU. No demonstrated latency or RSS improvement.

## Accounting, contracts and correctness

The [JSON](baselines/2026-10-18-template-construction.json) retains all 100 run-relative process
identities, roles/pairs/order, source/build/ZIP/ELF/live-PID hashes, clocks, marker rows, memory and
warm statistics, original frozen formulas, full paired values/ranges, cleanup and numeric outcomes.
The [CSV](baselines/2026-10-18-template-construction-invocations.csv) contains **all 4,200 invocation
rows**, including warmups/calibration/health/diagnostic/Settings/ABI—not just favorable formal rows.

| Role | Processes | Responses | Upstream attempts |
| --- | ---: | ---: | ---: |
| A/A main calibration | 16 | 1,296 | 1,296 |
| A/A health calibration | 8 | 48 | 40 |
| DA/DA calibration | 10 | 60 | 50 |
| Formal main A/B | 32 | 2,592 | 2,592 |
| Formal health A/B | 8 | 48 | 40 |
| Formal diagnostic DA/DB | 20 | 120 | 100 |
| Isolated Settings correctness | 5 | 30 | 25 |
| A ABI correctness | 1 | 6 | 5 |
| **Total** | **100** | **4,200** | **4,148** |

Main formal upstream accounting is exactly **32×200 +2,560×304**; across all roles it is
**100×200 +4,048×304**. Downstream conditional statuses are separate from upstream revalidation.
No failed/replaced/retried Native launch or omitted row; all owned children/containers/threads were
reaped and all ephemeral keys removed. Every response passed exact status/body/header/schema,
cache/ETag/Last-Modified/content-type/CORS/encoding checks. Health alone permits a dynamic UTC
timestamp with same-launch bounds. Wrong IDs/error endpoints reject before body reads; delayed
final-byte and mutation controls exercise the actual handler boundary. No raw headers/bodies/logs,
request payloads, fixture keys, credential inputs or secret-derived hashes are published.

Five separate fresh Native Settings cases preserve default/explicit/empty/arbitrary version labels,
DOTNET-over-ASPNET/fallback/literal environment behavior and cached first reads after environment
mutation. The independent same-SDK/source/publish-property **GetAssemblyVersion** oracle evaluates
1.0.0.0; it is not Settings itself or a recovered generated AssemblyInfo file. These processes are
correctness controls, never timing samples. Cross-property first-touch order was not mutated.

Managed TDD first characterized 49 new edge rows on old code. The fixed constructor fact genuinely
failed **752>751 B twice**, then passed at 392 B with reused-object control0, unchanged test DLL and
different executed API DLLs. Original tests/assertions and 584/264/264/64 B ceilings remain intact.
The independent post-write ordinary gate freshly ran solution restore/build, **629 API**
(592 Unit +37 overlapping Integration/Functional), **4 CDK**, **104+14 Node**, performance-project
build and **Slopwatch0**; zero failures/skips/compiler/analyzer diagnostics. Final normal Native
builds had zero compiler/analyzer/IL/trim/AOT diagnostics. Packaging did not rerun those suites.

Fresh reproduction independently executed **14,872 checks**, **3,522 producer-verifier replay
checks**, 15 existing +8 new fake-boundary controls and relevant mutation catches. Fresh contract
review executed **47 static checks**. Both verdicts are **CONFIRMED**, with no scoped blocker.
Fresh-session independence is established; model-lineage independence is unverified.

## Builds, retained failures and proof limits

Actual selected container SDK was **10.0.402** under frozen global.json at `/src`, matched to the
normal BuildKit SDK material, not inferred from a host/image label. The unchanged CLI/Dockerfile
were used with explicit new frozen context/output and no extra publish flags. All source→normal
build-record/material→ZIP/ELF→live owned PID/start-tick/executable chains were checked. Full hashes,
including SDK/frontend/ZIP-base materials, source manifests and policy hashes, are in the packet.
Cached stages are recorded; this is not a hermetic/signed build attestation.

Uninstrumented A x64 ZIP: **6,320,306 B**, SHA
`618fba64d6c7fd712b2e171907362271e54fd21e5eeff2390253f3683a03dce7`;
B: **6,320,534 B**, SHA `f03cce50e59815ae279c36fb5831fa1c68f63b9c7988571ab1fce98ae6de45c7`.
That is **+228 B, not a packaging-size win**. Both ELF lengths are 14,055,608 B; ZIP/entry/extracted
ELF hashes, CRC, Deflate, Unix0100755 and ELF64 little-endian machine62 are separate checked facts.
The current deployed ARM64 ZIP's **6,277 B headroom** belongs to
[different production evidence](2026-10-18-post-deploy-production.md); x64 predicts neither a B ARM64
size PASS nor FAIL.

All seven build attempts remain recorded: six successes, one owned diagnostic compile failure
(IDE0055/RCS1037/RCS1181), fixed only with whitespace/XML documentation. Settings-01 built but was
not executed; Settings-02 adds held-live bootstrap for PID attestation. An evaluation-only version
query was not promoted into the successful GetAssemblyVersion oracle. Earlier helper versions
reconstructed by reversing narrow edits are labeled **reconstructed**, not contemporaneous copies.
Failed fake-control/checker-wrapper attempts remain private original evidence; final controls did
not repair observations, source, frozen criteria or old failures.

Runtime was immutable Debian linux-x64/Python3.14.8/OpenSSL3.5.7/glibc2.41, **not AL2023**.
Each launch used pull-never/network-none/non-root/cap-drop/no-new-privileges/read-only/private PID+IPC,
minimal owned mounts and a literal environment, fake loopback TLS/upstreams only. Native GitHub,
AWS credential/client/storage/HMAC first-use, hosted candidate builds, ARM64 candidate size/execution
and AWS production qualification are **NOTRUN**. Ordinary managed HMAC/logging/telemetry tests are
not native/provider-exhaustive proofs. No production INIT≤100ms, memory≤50MB or Lambda RSS PASS is
claimed; no old hosted/passive production records or historical timings are pooled into this study.

## H1 disposition — real benefit, not retained

H1's explicit logging constructor-factory diagnostic measured a real **83,592 B median first-factory
saving** above its own frozen 44,304 B floor, with 78,784 B median startup-current saving.
It nevertheless failed **its own** frozen no-shift criteria: first-health global median
**104,248>93,144 B** ceiling and complete-global savings included **−36,360 B** despite a positive
51,200 B median. Its disposition stays **INCONCLUSIVE / DO NOT RETAIN**. Background-inclusive,
pre-flush globals do not prove a causal health-object/production regression; rejection does not mean
“no benefit.” No two-MB logger-object claim, H1 gate rewrite or logging change ships. Full pair
savings/rejection numbers are preserved in the packet, separately from H2's new calibration.

## Reproduction and handback status

The [historical appendix](reproducers/2026-10-18-template-construction/README.md) contains the
standalone offline checker, actual adapted Runtime API fixture with dependency closure, fake negative
controls and exact source-only probes. No default automatic build/network/native launch. New Native
reproduction is an explicit separate manual closed-fixture workflow with required source/build/
artifact inputs; missing inputs fail closed. Public adapted code is not an as-executed timing witness.

| Goal | Authoring snapshot |
| --- | --- |
| P1 attribution/provenance | Observed; fresh reproduce/contract confirmed |
| P2 meaningful local benefit | Observed allocation-only; frozen floor/secondary median gates pass |
| P3 contracts | Observed within ordinary suite and stated native fixture scope |
| P4 TDD/validation | Observed source-bound independent ordinary gate and final Native diagnostic checks |
| P5 review/evidence/handback | Material skeptics confirmed; **final public-package, completeness and whole-candidate gates pending** |

This packaging item checks its own offline arithmetic, controls, syntax, links and preservation.
Its public adaptation passed seven actual fake-handler tests (including two assertion-sensitive
mutations), fourteen packet/input negative controls and positive snapshot/binder/prepare dependency-
closure controls. These are new **offline package-control results**, not new Native measurements or
the original study's as-executed controls.
The parent's later independent package checks have not run here. All old evidence/source/tests and
seven other preexisting dirty documents remain immutable. ROADMAP receives one inserted row; the
two owned goal plans receive status-only updates. No plan deletion, staging, commit, push, PR,
deployment or publication is performed or implied.

## Package-input guard repair — 2026-10-18

The subsequent independent package gate **failed two public new-input guards**: `prepare` checked
only listed source files and accepted an extra input; `snapshot` resolved a linked root before
checking its identity. Fresh reproduce and contract skeptics confirmed those bounded findings.
That FAILED gate remains immutable. It did **not** invalidate the historical 1,632 B result or the
fresh 629 API /4 CDK /104+14 Node validation; no extra input was shown in an original measured build.

The original writer then observed actual public-seam RED: valid controls passed, while the extra
file and a linked **pristine exact target** were accepted. Two new public tests failed with zero
unrelated errors before the repair. One small shared validator now checks the original absolute
lexical root/all ancestors without resolving away links, validates manifest paths, and requires an
exact regular-file inventory plus only manifest-prefix directories. Name/no-follow-type rejection
precedes source-content reads/copy/output creation; expected hash/size/mode and artifact checks remain.
It assumes stable owned inputs, not hostile concurrent mutation/TOCTOU protection. `bind` is still
only the explicit declared-record/artifact binder.

Writer repair controls passed **15 public tests**: all seven original actual-handler tests and their
two sensitive mutations unchanged, plus eight source-root tests covering 68 actual caller cases
(six positives/62 specific-phase rejections). Rejected cases read zero source/unlisted bytes and
created no output. The same unchanged independent minimal regression script changed from exit1 to
exit0, with both valid positives retained and both invalid-input acceptance flags false. These are
offline synthetic guard controls, not new Native samples, source-to-compiler attestations or
production-exploit evidence. Original helpers, all100 process/4,200 invocation observations,
acceptance formulas/source/build IDs, probe texts and numeric CSV remain unchanged; the JSON updates
only current portable-adaptation file hashes. The original pre-status B318 source and later330-file
documentation snapshot remain distinct.

**Writer repair is not the final P5 verdict.** A fresh combined post-repair package/completeness and
whole-candidate Standards/Spec gate is still **NOTRUN/pending** here. Publication remains separate;
the pending goal plans and sole ROADMAP candidate row stay present. No new .NET/Native/container,
upstream, AWS, ARM64/AL2023 execution, production-goal qualification or publication occurred in repair.

## Observed final local review — 2026-10-18

The fresh independent post-repair gate passed all required local checks: unchanged independent
extra-input/pristine-root-link regression, 15 public controls/68 caller cases (six positives,
62 rejections with zero source reads/output), 18 prior input negatives, 20 broader caller cases,
24 newly authored caller cases, eight additional protocol controls and 14 packet negatives.
Historical arithmetic/provenance was checked afresh against all 100 original process receipts and
4,200 rows, actual source snapshots/ZIP/ELF bytes and recorded PID/clocks; no Native recollection.

Fresh ordinary execution passed **629 API, 4 CDK and 104+14 Node tests**, zero failures/skips;
solution restore/Release build, performance-project build and Slopwatch0 passed with zero diagnostics.
The whole-candidate **Standards**, **Spec** and **completeness** verdicts separately pass; **P1–P5
pass within the approved local goal**. The [observed review record](baselines/2026-10-18-template-construction-review.json)
pins post-status source/document bytes separately from the immutable as-measured B318 snapshot.

Earlier pending snapshots above describe their authoring time. The original package gate remains
historically **FAILED**, superseded only for the repaired public-input surface by this fresh gate;
the original safety PARTIAL likewise retains its separately resolved metadata history. Unexecuted
H1 checklists stay unchecked. No demonstrated latency/RSS improvement, no ARM64/AL2023/hosted/native
AWS-GitHub-storage-HMAC first-use or production INIT/memory/size qualification, and no publication
approval follows. **Ready for local publication-decision review; publication remains pending.**
