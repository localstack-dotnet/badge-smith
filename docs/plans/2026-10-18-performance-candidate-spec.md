# Performance Candidate — Approved Goal Specification

## Approval and present state

On 2026-10-18 the user explicitly confirmed “evet olsun” to an autonomous, measured and reviewed
**local optimization candidate**, ready for a separate final publication decision. This is the
approved goal, not an approval of an unmeasured hypothesis or a production release. Creating this
specification does not require another routine approval. The [implementation plan](2026-10-18-performance-candidate.md)
incorporates the completed independent reproduction and contract critics. **Status on 2026-10-18:**
the original Stage 1 preparation freeze has advanced through actual safety, H2 TDD/ordinary validation
and controlled local Native AOT comparison. H2's 4,152→2,520 B once-only route-construction result
passed fresh material reproduce/contract skeptics. **P1–P5 now pass within the approved local goal**:
the [observed final review](../research/baselines/2026-10-18-template-construction-review.json) passed
fresh post-repair public-package, ordinary validation, whole-candidate Standards/Spec and completeness
checks. **Local candidate validated; publication pending.** See the [dated evidence](../research/2026-10-18-template-construction-optimization.md).
This is not a production release; both owned goal documents remain present pending the separate publication decision.

The source anchor is `localstack-dotnet/badge-smith`, HEAD
`40cb349af214a642741faecf757676fa69eefbf8`, tree `8126d09f4fb71ce4de0d8eececbf477e7bc0424d`.
The run's input manifest protects **316 working files: 310 tracked and six preexisting untracked**,
including eight dirty documentation paths. Preserve their working bytes, not merely HEAD's bytes.
Neither a previous binary nor a historical test receipt is fresh proof of this candidate.
The contract critic's literal exact-total-status replay failure is preserved: the original 316 bytes
and eight dirty paths were unchanged, while these two authorized plan files appeared concurrently.
That expected status growth is not a production failure or permission to rewrite the peer receipt.

## Outcome

Identify a specific production-shaped startup or first-use cost, remove useful work or allocation
without changing caller-visible contracts, and demonstrate a reproducible benefit in controlled
local linux-x64 Native AOT execution. Complete source-bound correctness, analyzer, AOT and
independent-review gates; record evidence and roadmap status. If no hypothesis survives, report a
truthful blocker/rejected experiment rather than an optimization candidate.

Allocation can be the qualifying benefit. It does not imply faster startup, lower resident memory,
smaller ARM64 packaging, or production-goal compliance. Readiness and first-invocation work must
both be observed so that shifting work between them cannot qualify as a saving.

## Candidate design choices

These are reversible choices for this milestone, not new repository-wide policy:

1. **Screen H1, then prefer H2 if H1 is noise or disproportionate.** H1 substitutes explicit DI
   constructor factories for the existing framework `LoggerFactory` and `ConsoleLoggerProvider`,
   together in the initial native D0/D12 screen; four cheap managed cells are optional orientation,
   and native single-constructor ablations require a material signal needing separation. Managed
   noise alone does not establish native absence. Retain the complete existing logging graph. H2 removes temporary
   strings/split arrays from `TemplatePattern(string template)` using two span passes while keeping
    its retained arrays and matcher unchanged. At the initial freeze both benefits were unmeasured;
    H2 is now the measured allocation-only candidate. H1 is **NOT SELECTED / DO NOT RETAIN** under
    its own frozen no-shift criteria despite a real 83,592 B factory saving; its conditional
    implementation/provider-parity work remains unexecuted. The [disposition](../research/2026-10-18-template-construction-optimization.md#h1-disposition--real-benefit-not-retained) preserves that distinction.
2. **One production hypothesis at a time.** H3's unused exact-route buffer and H4's null-query
   dictionaries are later alternatives, not an authorized blind bundle. Do not rewrite the logging
   framework, routing validation, AWS clients, providers, cache policy, serializer or HMAC protocol.
3. Use a small run-owned private adapter and source-only diagnostics, not a permanent harness
   product. Freeze the existing CLI, Dockerfile and flags. The first isolation choice is normal
   nonprivileged Docker with one official resolved linux/amd64 `python:3.14-trixie` image; one ordinary
   public image pull is approved fixture dependency acquisition, not a host tool install. Use existing
   host openssl for private fixture certificates and Python ssl inside the container. No apt/pip
   repairs, mounts of host libraries, the repository, home directories or daemon sockets, or an alternate path after permission denial.
    The required TLS/network/filesystem/proc/library/ABI-capability preflight subsequently passed for
    the recorded local runtime; this status does not claim a new preflight or AL2023/AWS qualification.
4. Native readiness, health and real NuGet fixture calls, narrow first-category/component probes,
   and full existing managed regressions are the initial coverage. Logger-construction or template-
   constructor changes alone do **not** require building an AWS/HMAC emulator. Native AWS-first-use
   remains explicitly NOTRUN unless separately implemented and verified. A finding that the chosen
   change affects AWS/client/auth first use blocks this bounded selection; do not silently broaden it.
5. Before candidate comparison, freeze the primary metric, benefit floor, secondary regression
   margins, finite cohorts and stop rules from baseline/control observations. Do not choose values
   after seeing candidate improvements. Exact allocation ceilings come from a measured receipt,
   not conventional object-layout estimates. H1's primary claim is synchronous first-factory native
   allocated bytes, with complete first-category/feature and global/warm no-shift checks; its practical
   floor is max(256 bytes, twice baseline DA/DA control spread), not the one-byte-lower test ceiling.

If H1 is retained, use field-free `LoggerConfiguration.Configure(ILoggingBuilder builder, string environment)`
inside the existing framework Create callback. Keep the Settings read inside that callback and the
eager holder unchanged. Managed telemetry parity uses public `BaseExporter<LogRecord>` and
`SimpleLogRecordExportProcessor` test snapshots plus loopback OTLP acknowledgment; no new package or
protobuf decoder is needed, and semantic wire-protobuf attestation is not claimed. Selected container
SDK proof must show the actual SDK selected under the frozen global.json at the build cwd; an image's
advertised SDK label, host version, tag or index digest alone is insufficient.

The historical broad logger window of 2,129,088 allocated bytes is neither the logger object's
size nor retained RSS nor a promise of reclaimable memory. Removing the unused application-version
lookup is already done; blanket handler laziness and cache deferral remain rejected. See the
[startup evidence](../research/2026-10-17-startup-version-lookup.md).

## Global constraints

- Preserve all 316 input-manifest working files and all eight preexisting dirty documentation deltas; only selected, reviewed candidate/test/evidence deltas may be added.
- No commit, stage, amend, push, merge, PR, deploy, image publication, AWS authentication or candidate-owned production traffic.
- No build/package/analyzer/CI/CDK/policy/Dockerfile/CLI behavior changes, new tools/system installs, privilege escalation, settings changes, force/reset/rebase or global cleanup.
- Keep PublishAot=true, existing trim-safe JSON registrations and unchanged production telemetry/LocalStack exclusion flags; no runtime code generation or unannotated reflection.
- Preserve HTTP/status/JSON/routes/ETag/cache/CORS/redirect contracts and per-request upstream revalidation; preserve HMAC/nonce/secrets/replay ordering, UTC and application-version value/caching contracts.
- Preserve logging templates, level/category filters, format, scopes, EventId, Activity correlation, provider order, queue behavior and disposal ownership, including enabled ordinary-build telemetry.
- Preserve OneOf result conventions and minimum allocation; no success-path allocation increase, weakened/skipped tests or global warning suppression.
- Runtime application calls use fake/local fixtures only; no real AWS/GitHub/NuGet or default k6 endpoint. Normal existing build dependency/image retrieval is distinct and allowed.
- Keep secret inputs, raw logs/headers/bodies/cookies/tokens/signed URLs and hashes of secret-derived material out of outputs; inspect no credentials. Persist only allowlisted projections and public source/artifact hashes.
- Use one parent-owned heavy lease for every build/test/native/profile/benchmark phase, at most four live children and 20 unique children overall; reserve one possible child for the separate passive baseline follow-up.

The existing production-build authorization for this local experiment is specific to the approved
goal; it does not alter [repository approval gates](../../AGENTS.md#approval-gate). The
[coding](../engineering/coding-style.md), [testing](../engineering/testing-style.md),
[quality](../engineering/quality-gates.md), [runtime](../architecture/runtime-and-routing.md),
[security](../architecture/security-and-storage.md) and [platform](../architecture/platform-and-delivery.md)
authorities remain controlling.

## Exit criteria — all five must pass

| Gate | Required local-candidate evidence |
| --- | --- |
| **P1 — attribution and provenance** | Separate the selected operation using fresh narrow diagnostics plus empty/idle/first/reuse controls. Identify source bytes → normal CLI/build materials and actual SDK → fresh ZIP/ELF → launched PID/start time and `/proc/PID/exe` hash. Diagnostic snapshots are distinct from unmodified A/B. A profiler window alone is not causal attribution. |
| **P2 — benefit** | At least one selected change beats the frozen meaningful floor reproducibly in Native AOT allocation, startup/first-invoke or RSS. Balanced independent AB/BA blocks agree; readiness, complete first response, same-process paired total, feature-first and warmed observations rule out deferral/regression. Allocation-only acceptance carries no latency/RSS claim. |
| **P3 — contracts** | Baseline characterizations and mutation-sensitive tests protect the unchanged contracts above. H1 exercises actual providers/console/telemetry, not logger doubles alone. H2 pins constructor edge semantics, key order/duplicates and decoding without new validation. All selected success paths retain their allocation bounds. |
| **P4 — validation** | A real performance/behavior assertion fails on old mechanics and passes on the candidate; missing-method compilation is not RED. Fresh affected Release restore/build/test, full API/CDK/Node scope as applicable, performance-project build, zero compiler/analyzer/IL/trim/AOT warnings/errors and Slopwatch when available. Match executed source, test names/counts and artifact identity; exit zero alone is insufficient. |
| **P5 — independent review and handback** | Fresh reproduction and contract skeptics adjudicate material acted-on findings; an independent post-write test gate and final whole-candidate Standards/Spec review pass. Sanitized dated evidence, executable reproducers and roadmap agree and preserve prior work. Provide a publication-decision brief without performing publication. |

## Evidence domains and limits

Keep managed/JIT, source-only diagnostics, uninstrumented linux-x64 Native AOT, hosted ARM64,
executed AL2023 and AWS measurements separate. Retain every planned first, warmup, warm, failed and
invalid observation with its role; process, not individual invocation, is the independent sampling
unit. No sample-count inflation, selective fast rows, old-source/new-binary labels or summed marginal
percentiles. A corrected body-complete first-response timestamp defines a new instrument version;
do not pool its timings with historical request-header timestamps.

The unchanged production targets remain INIT p95 ≤100 ms, memory p95 ≤50 MB and ARM64 ZIP
≤6,000,000 bytes under the [production measurement protocol](../architecture/platform-and-delivery.md#measurement-policy).
The existing passive production-baseline follow-up concerns deployed `fdf4c4a`, not this candidate.
It is parent-owned under its separate once-only plan, runs between heavy measurement phases, and
adds no candidate AWS permission. Local success cannot mark these production targets PASS.

## Stops and completion boundary

Stop the affected phase on permission denial with the exact safe error, missing required provenance,
unrepairable in-scope regression, required protected-file change, security/scope expansion or budget
exhaustion. No alternate privilege/network path after denial. Stop discovery after two rounds with
no new findings or three rounds total; at most two fix-review rereviews. Reserve resources for final
proof rather than spending the whole budget on discovery. No meaningful reproducible benefit means
P2 fails, even when correctness tests pass.

The final state is **local candidate ready for publication approval**, or an explicit partial/blocker
with failed/NOTRUN gates. It is never an automatic commit, release, deployment or production-target
qualification. This temporary spec/plan follows the existing [documentation lifecycle](../engineering/documentation.md).
