# Production evidence collection procedure

**Latest status (2026-10-18):** the user-directed bounded autonomous
[complete-window follow-up](../research/2026-10-18-post-deploy-complete-window.md) reached terminal
pagination: **272 REPORTs / 90 INIT**, 15 pages with the last empty/no token, 24/32 permitted HTTP
dispatches, unchanged package and routing closing checks. Available matching-window retrieval is
complete; INIT still lacks ten cold records. Formal historical/per-event qualification remains
bounded, and no traffic was generated to fill the gap. This pass is closed; the plan remains active.

**Earlier same-day status:** the newly approved [post-deployment pass](../research/2026-10-18-post-deploy-production.md)
bound the current package to the promoted `fdf4c4a` ZIP and retained **264 REPORTs / 86 reported INIT**.
It stopped with continuation after **13 pages / 22 HTTP dispatches**, preserving closing reads within
the approved 24-call/14-page ceiling. The new budget is closed. Prefix p95 is **135.06 ms INIT /
51 MB memory**; full-window/100-cold-sample qualification remains **UNVERIFIED**. No more reads,
traffic or mutation are implied; this operational plan remains active and unretired.

**Earlier status:** the subsequently authorized bounded last-24-hour wider read executed on 2026-10-16 UTC
(October 17 investigation label). The [window packet](../research/2026-10-17-production-evidence-window.md)
contains **413 rows, 102 with reported INIT**; it stopped at **20 pages with continuation**. Both
numerical minimum counts are met; complete-window and revision-qualified compliance remain
**UNVERIFIED**. Prefix p95 is **135.15 ms INIT / 54 MB memory**, not qualified production p95.
The accepted [14-row diagnostic](../research/2026-10-17-production-evidence-diagnostic.md) and
[Phase A packet](../research/2026-10-17-production-evidence-phase-a.md) remain unchanged.
**This wider-read budget is consumed; Task 3 still gates later expansion.** The
[initial failed-auth attempt](../research/2026-10-16-202846Z-production-evidence.md) remains historical evidence.
This is an active operational plan, not a software-implementation task.

**Goal:** produce reproducible evidence about the existing production deployment, including honest
FAIL or UNVERIFIED outcomes, without changing the deployment to obtain a better result.

Read [AGENTS.md](../../AGENTS.md), the [measurement owner](../architecture/platform-and-delivery.md#measurement-policy),
the [production guide](../../build/BadgeSmith.CDK/README.md), and the
[source-grounded research](../research/2026-10-16-production-evidence-scoping.md) before execution.
The research records provider facts and historical limits; this plan records the subsequently
agreed scope and collection sequence. Current canon remains authoritative.

## Agreed scope and gates

| Decision | Collection contract for this increment |
| --- | --- |
| Subject | Investigate what is actually deployed first; neither local HEAD nor the historical hosted artifact is automatically the subject. |
| Outcome | Separate artifact/source linkage, deployed identity, execution, size, INIT and memory conclusions. Evidence completion does not require performance compliance. |
| Sequence | Observation preflight, mandatory budget checkpoint, then separately authorized substantial collection and offline analysis. |
| Timing companion | Pair Init Duration and Duration from the same invocation. Label the result Lambda-side cold timing, not end-user latency; keep INIT's own target separate. |
| Deliverable | A sanitized evidence bundle and reproducible collection procedure, using existing tools where sufficient. |
| Provenance | Existing authorized operators may supply exports. Attribute their observations separately from independently checked data and calculations. |
| Exclusions | No optimization, target changes, automatic deployment, active HTTP smoke, deliberate load/cold generation, configuration/IAM changes, or new permanent CLI/CI platform. |

There are two execution approvals below. Scope confirmation and the existence of this file satisfy
neither. A permission denial stops the affected read; it does not authorize access setup or IAM
changes. An unavailable fact stays unknown. Deployment, traffic generation and remediation need a
separately scoped decision, not an automatic fallback from this procedure.

## Task 1 — Authorize a bounded preflight

- [x] Record explicit approval for **Phase A only**, identifying the operator, intended production
  account/region, `BadgeSmithStack`, and the permitted metadata actions. Use existing authorized
  authentication; retain no credential values or personal profile paths.
- [x] Include bounds for metadata pagination/history lookback. If a diagnostic log sample is
  permitted, specify its exact group, time range, event/page/byte limits, and allowed fields first.
  An unspecified bound does not mean unlimited access. Schema sampling is not a performance cohort.
- [x] Record observation time in UTC and whether each input will be directly observed or supplied
  by an operator. An operator export must identify its collection time, source and limits.
- [x] Select a transport that projects allowed fields **before** writing stdout, tool transcripts,
  files or debug logs. If the available tool dumps complete sensitive responses, stop that path
  and request a sanitized operator export instead.

**Done when:** the Phase A authorization and bounds are explicit, or the preflight is recorded as
BLOCKED. This document supplies no credential-discovery or live-read permission by itself.

## Task 2 — Establish identity, interval and coverage

Use only the Phase A actions actually approved. The following are candidate sources, not a grant
to call every API or enumerate an account. Resolve physical resources from the exact production
stack; follow only references needed for that deployment.

| Evidence | Candidate read and retained fields | Required interpretation |
| --- | --- | --- |
| Account/stack | Existing authorized caller identity; exact-stack description and resource mapping. Retain account/region, stack ID, relevant logical/physical IDs, status and observation time. | An unexpected account/region or ambiguous stack is a stop, not permission to search other accounts. |
| Function | `GetFunctionConfiguration` for the resolved function and relevant qualifier: ARN, Version, RevisionId, PackageType, CodeSha256, CodeSize, LastModified, Runtime, Architectures, MemorySize, State, LastUpdateStatus and LoggingConfig. | Exclude Environment and unrelated configuration. Record API package bytes/hash as observations, not execution or Git provenance. |
| Effective route | Exact API/stage/route/integration references; referenced alias/version and weights only if present. | Current API configuration alone may differ from a manually deployed stage. Establish the deployed-stage mapping or leave it unverified; do not deploy it. |
| Stable interval | Relevant existing deployment records, stack events and selected template references, correlated with function/routing observations. | LastModified alone is not a clean code/configuration cutover or a complete history of mutable `$LATEST`. |
| Source/artifact linkage | Existing or explicitly authorized read-only CI/deployment records and provenance for the package actually identified. | Do not refresh CI, select a replacement candidate, or automatically download an artifact/package. Missing or ambiguous linkage remains unverified. |
| Logs | Metadata and any explicitly bounded diagnostic sample from the identified function's actual log group/streams. Retain retention/format/level observations, coverage limits and sanitized schema evidence. | No account-wide log search or initial full-window scan. Metadata/sample observations alone do not prove complete REPORT counts or interval history. |

- [ ] Complete the permitted identity reads and keep only allowlisted fields. Inspect selected
  template references privately; do not retain whole templates, parameters or arbitrary stack outputs.
- [ ] Resolve actual routing, including qualifiers or weighted versions if observed. Treat mixed
  versions/configurations as distinct subjects; do not pool them into one compliance population.
- [ ] Establish a defensible stable interval for the identified code, relevant configuration and
  routing. Mark uncertain boundaries explicitly; current snapshots cannot reconstruct all history.
- [ ] Inspect existing log format and coverage within the approved diagnostic bound. Successful
  REPORT availability, retention gaps and historical format/level changes matter. Do not change
  logging configuration to make collection easier.
- [x] Produce the sanitized preflight summary: identified resources, linkage confidence, interval
  evidence, log availability/schema, access limits, and which conclusions can potentially be tested.

**Recorded result:** permitted caller/function reads and exact-stack mapping are available in the
[metadata manifest](../research/baselines/2026-10-17-phase-a-preflight.json). Current API metadata
links the default AutoDeploy stage to the unqualified function; effective historical/public routing
is not established. The later [window read](../research/2026-10-17-production-evidence-window.md)
inspected only the known Lambda template resource's safe Code/configuration projection and reread
stages/integrations; source linkage and a clean historical interval remain unknown. The earlier
[bounded diagnostic](../research/2026-10-17-production-evidence-diagnostic.md) accepted 14 REPORT-shaped
rows across two pages, with continuation still present. The wider read adds 413 parsed rows across
20 pages, also with continuation. Neither establishes complete coverage. The compound checkboxes
above remain open where obligations or stable interval/coverage proof are unmet.

For resource mapping, `DescribeStackResources` is capped at 100 resources and is not pageable;
use the appropriate bounded resource lookup or `ListStackResources` when necessary. For a manually
deployed HTTP API stage, stage-specific export may be needed rather than latest configuration.
Advanced log-level filtering requires JSON configuration; today's format does not rewrite history.
These are collection details supported by the research reviews, not requests to change resources.

**Done when:** the preflight summary supports a specific proposed cohort and collection method, or
names the identity/access/coverage gaps that block one. An inability to bind the revision does not
prevent reporting the available observations; it prevents presenting them as revision-qualified proof.

## Task 3 — Stop for the collection-budget checkpoint

- [x] Present the Phase A summary before any substantial log query/export or package download.
- [ ] Propose the exact function/qualifier/configuration, log group, UTC observation window and
  cutover exclusions. Use the most recent slice of up to 30 days wholly inside one attributable
  deployment interval, as required by the measurement owner. Identify unavailable history rather
  than silently substituting an older deployment or a convenient shorter population.
- [x] Propose the retrieval method, exact time partitions, scan estimate and its uncertainty,
  export event/byte limits, request/page/query limits, elapsed-time limit, concurrency, and stop
  conditions. State how each bound is enforced or observed; no numeric budget is preapproved here.
- [x] Identify all requested API actions, including result retrieval and cancellation if choosing
  Logs Insights. `StartQuery` stores query results and incurs scanning; it is not an application
  invocation, but is not a cost-free metadata GET either. Cancellation is limited to this run's
  own query IDs, never another operator's query.
- [x] Obtain explicit approval for **Phase B** with those concrete bounds. Any later material
  expansion returns to this checkpoint. Lack of approval leaves Phase B NOT RUN.

**Historical wider-read checkpoint:** after the accepted diagnostic, the parent proposed a bounded last-24-hour
read and deployed-source check; the user explicitly instructed autonomous execution without more
questions. Approved bounds: 30 AWS attempts including ordinary authentication, 20 event pages,
5,000 returned candidates, 2 MiB combined new report/JSON/CSV, concurrency one, zero retries,
3/5-second timeouts, 120-second live horizon; exact existing function/group, no traffic or mutations.
The [result](../research/2026-10-17-production-evidence-window.md) froze
2026-10-15T23:00:23.833443Z–2026-10-16T23:00:23.833443Z before event reads, then collected
413 rows/102 reported INIT in 20 pages and stopped with continuation. It used 29 SDK/HTTP attempts
including authentication and five permitted exact-resource metadata calls, in 10.333146 seconds.
Before/after configuration matched all 16 accepted fields; all 14 pilot rows reconcile. No extra page
or request followed. This satisfies authorization of that read, not canonical clean-interval/full-cohort
proof; any later budget needs its own justified checkpoint.

**Latest approved checkpoint (2026-10-18):** after a fresh metadata preflight and explicit numeric
proposal, the user approved the fixed post-deploy read autonomously. Bounds: 24 new HTTP dispatches
including authentication; at most 14 event pages, fewer when closing/authentication reservations
require; 10,000 returned candidates; 2 MiB combined reduced output; concurrency one; zero retries;
3/5-second transport timeouts; 120-second request horizon. Exact same function/group and frozen
2026-10-17T16:40:20Z–2026-10-18T14:48:00Z interval, no traffic/downloads/queries/mutations. The
[result](../research/2026-10-18-post-deploy-production.md) is partial at 264 rows/86 INIT with
continuation, despite matching package/routing before/after. The conservative closing reservation
stopped acquisition at 13 pages/22 dispatches; the two unused units are not permission to resume.
All prior diagnostic/wider-read data remain unchanged, and no later budget is automatically granted.

**Subsequent autonomous follow-up (2026-10-18):** after the recommended next step was a complete
same-revision baseline, the user explicitly delegated safe autonomous progress without routine
questions. A new one-pass 32-HTTP/20-event-page ceiling was announced and frozen before acquisition;
the other 10,000-candidate/2-MiB/120-second/zero-retry/concurrency-one/3–5-second bounds remained.
The end advanced to 2026-10-18T15:51:05Z before any new event read, retaining the same start, function,
group, source and closing checks. This was a fresh bounded read, not reopening the old 24-call cap.
The [terminal result](../research/2026-10-18-post-deploy-complete-window.md) retains 272 rows/90 INIT,
including the earlier 264 rows freshly reobserved and eight rows after the old window's end.
The last empty page has no continuation. The missing ten cold samples were not manufactured;
there is no unannounced additional round, traffic, periodic monitor or configuration change.

The [StartQuery request](https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_StartQuery.html)
has time-range and result limits, not a hard byte/currency budget parameter. A result-row limit is
not a scan-cost cap. Describe estimates and cancellation controls honestly; monitoring
[`bytesScanned`](https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_GetQueryResults.html)
and attempting [`StopQuery`](https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_StopQuery.html)
does not guarantee an exact spend ceiling. An unacceptable overshoot risk is a reason not to start.
Record each API's time units explicitly: StartQuery uses epoch **seconds**, not milliseconds.

**Done when:** Phase B is explicitly authorized within recorded bounds, declined, or blocked. The
procedure can close with a truthful limited evidence packet rather than an unapproved collection.

## Task 4 — Collect the complete approved cohort safely

- [x] Freeze the approved interval and extraction method. Verify the extractor against the actual
  text/JSON platform schema using the diagnostic sample; preserve its version/query and checks.
  Keep platform REPORT records distinct from application text and standalone INIT_REPORT events.
- [ ] Collect all eligible REPORT records in the approved interval, including failure/slow outcomes,
  without selecting only successful routes or stopping as soon as the minimum count is reached.
  If the approved limits cannot cover that population, stop and report incompleteness.
- [ ] Follow applicable pagination to completion. Empty/partial pages can still have continuation
  tokens. For Insights require terminal `Complete`, all result pages and evidence that result limits
  did not truncate the cohort. Running, Failed, Cancelled, Timeout or Unknown is not complete proof.
- [x] Partition only within the approved interval when necessary. Record API boundary inclusivity
  and units, detect gaps, and deduplicate overlaps using stable source-record identity. A preview,
  capped result set or deliberately sampled subset cannot stand in for all REPORT records.
- [x] Keep raw responses transient or in approved private storage outside Git; project before any
  retained output. Retain only the reduced fields below, collection provenance and necessary counters.
  Exclude environment values, credentials, signed URLs, bodies, headers and raw application paths.
- [x] Record total/unique REPORT counts, parsed counts, cold/paired counts, failures, missing fields,
  malformed records, exclusions and their reasons. Use null for absent Init Duration, never warm zeros.
  Do not count INIT_REPORT as REPORT or claim REPORT-only data enumerates suppressed initialization.
- [ ] Recheck relevant code/configuration/routing identity at collection end within the approved
  actions. Record changes and transition uncertainty. Matching endpoint snapshots alone do not
  establish a complete historical interval; reconcile the independent interval evidence.

The reduced report dataset needs stable event/request/stream keys, event time UTC, version/cohort
identity, Duration milliseconds, Max Memory Used with original units, optional Init Duration
milliseconds, and platform outcome/missing-field/exclusion classifications. Retain configured memory
and collection/ingestion timing where needed to check attribution and completeness. Use scoped opaque
keys for identifiers that should not be published; preserve deduplication and pairing. Raw log-message
hashes are not a substitute for sanitization. Map application correlation only to known route names;
otherwise retain an unknown route, not a user-controlled path. Retain no arbitrary message text.

**Recorded wider-read result:** 413/413 returned candidates parsed, zero rejections/duplicates;
102 reported INIT, 311 null INIT, all `$LATEST` / 512 MB reported memory, absent statuses not
asserted success. Pagination remains incomplete at the page cap; total-window and complete-cold
counts remain unknown. Exact current snapshots and bounded CloudFormation history do not prove
historical stability or source identity. The complete-cohort and historical-attribution obligations
above therefore remain open; no checkbox asserts terminal pagination.

**Done when:** the approved population is demonstrably complete and attributed, or the packet states
which bound, denial, schema, missing records or identity uncertainty makes a claim UNVERIFIED. Stop
without IAM changes, traffic generation, configuration changes, unapproved retries or broader scans.

## Task 5 — Compute offline and qualify each conclusion

Apply the unchanged [performance goals](../architecture/platform-and-delivery.md#performance-goals)
and measurement protocol; this procedure does not create a new acceptance policy.

- [x] Validate sample provenance, units, numeric fields, deduplication and interval eligibility before
  calculation. Keep diagnostic samples separate. If omissions or attribution gaps prevent the required
  population from being established, report descriptive values only, not a qualified PASS/FAIL.
- [x] For each eligible population, sort ascending, calculate `rank = ceil(0.95 * n)`, and select
  the 1-based rank. Retain n, rank, value, units and input-file SHA-256 so another reviewer can reproduce
  it. A Logs Insights pct aggregate alone does not prove this exact rank calculation.
- [ ] INIT: use at least 100 cold REPORT samples with Init Duration; compare p95 with **100 ms**.
  Fewer samples leave INIT UNVERIFIED. Do not manufacture missing initialization values.
- [ ] Memory: use all REPORT samples in the same slice, with at least 100 total samples; compare
  reported Max Memory Used p95 with **50 MB**. Preserve/report units and any conversion. Interpret
  these as invocation-weighted environment high-water observations, not per-request allocations or
  independent environments; do not change the population to one value per environment.
- [ ] Cold companion: add Init Duration and Duration within each attributable cold invocation,
  then calculate/report that population's distribution and count. Never add marginal percentiles.
  Distinguish routes where reliable correlation exists, otherwise label mixed/unknown workloads.
  This has no numeric target and is neither end-user latency nor a complete suppressed-init census.
- [ ] Size/linkage: normalize the API's base64 CodeSha256 and provenance's hexadecimal digest before
  comparing. Keep AWS CodeSize distinct from measured exact consumed-ZIP bytes, the GitHub wrapper
  archive and uncompressed bootstrap size. Apply **6,000,000 bytes** only to the identified production
  ARM64 ZIP under the canonical proof requirements. No package download is an automatic fallback;
  if needed, request it explicitly with size/destination/redaction bounds and never execute it.
- [ ] Execution: require invocation/platform evidence correlated to the identified package and
  ARM64 provided.al2023 configuration. Configuration/hash equality alone is not execution. Existing
  logs do not automatically prove HTTP response contracts or dependency health; keep those claims
  separate. Active Gateway/public smoke, if later requested, remains separately scoped and approved.

**Recorded calculation scope:** exact descriptive prefix distributions and per-version/INIT/status/
memory partitions are in the window manifest. Both count minimums are met, but the population is
page-capped and historical attribution is unknown: INIT/memory goals are numerically exceeded
(135.15 ms / 54 MB prefix p95), while formal compliance stays UNVERIFIED. Paired median/p95 is
302.25/408.82 ms; no client-latency or exact consumed-ZIP claim follows.

Before accepting offline results, record checks that reject duplicate boundary rows, missing INIT
coercion, mixed-revision rows and incomplete populations. Check nearest rank against values 1–100
(p95 = 95). For paired samples `(10,90)` and `(90,10)`, the companion p95 is 100, not the sum of
marginal p95s, 180. Those tiny arithmetic controls are not compliant production sample populations.

Use separate PASS, FAIL or UNVERIFIED conclusions, with evidence and reasons. FAIL requires positive
evidence of a mismatch or an eligible threshold violation; unavailable data is not proof of failure.
An established size fact may stand while execution or INIT remains unverified. Record observed
incompatibilities without silently changing the subject or attempting remediation.

**Done when:** each conclusion is reproducible from the sanitized inputs, or explicitly states the
missing proof; calculations and attribution have a recorded check, not just a favorable number.

## Task 6 — Package and review the evidence

- [x] Allocate a unique UTC run ID, such as `YYYY-MM-DD-HHmmssZ`; check destination paths are unused.
  Keep raw logs, binaries and private source mappings outside Git. Create no evidence files merely
  because this procedure has been authored, and never overwrite an earlier run to revise its result.
- [ ] After collection, place only reviewed sanitized outputs in the existing evidence homes:
  `docs/research/<run-id>-production-evidence.md`,
  `docs/research/baselines/<run-id>-production-evidence.json`, and
  `docs/research/baselines/<run-id>-production-reports.csv` when numeric rows exist. The report owns
  interpretation; the JSON manifest owns identities, approvals/bounds, counts, units, ranks, values,
  file hashes, source attribution and completeness/limitations; CSV owns the reduced REPORT rows.
  Omit unavailable data files and explain why rather than creating empty success-shaped evidence.
- [x] Include the actual operator/source, collection times, exact resources/cohort/window, queries or
  extraction requests, pagination/terminal status, observed scan/export usage, parsing checks and each
  separate verdict. Preserve historical and operator-reported facts as such. Independent recomputation
  of an operator export does not make its underlying account observations directly observed here.
- [x] Recompute hashes/counts/ranks from the sanitized files; check file/fragment links and inspect
  every retained field for the allowlist. An outside reviewer must be able to reproduce the numeric
  result without secret-bearing raw logs. Record checks not run and any independence limitation.
- [ ] Update the ROADMAP with a concise outcome and evidence link. After its durable outcome is
  captured, retire this consumed plan under [documentation lifecycle](../engineering/documentation.md).
  Commit/push/publication still require the existing separate approvals; none is automatic cleanup.

**Done when:** the bundle records what actually ran, supports its conclusions, and exposes every
relevant unverified limit. A scoped packet ending with FAIL, UNVERIFIED or a blocked collection is
a valid evidence outcome, not a claim that production meets its goals.

## Authoring verification and execution state

The scoping research received two fresh skeptical checks and a separate physical/scope gate before
this procedure was written. Those checks cover the research note, not this later procedure or a
production run. Procedure authoring uses inline scope/semantic/link checks; it adds no agent budget,
new permanent tooling, domain glossary entry or speculative ADR. The later user execution request
authorized bounded Phase A. Its initial no-response authentication probe remains preserved, but fresh
sanctioned caller checks resolved that blocker. Bounded stack/function/API/log/history metadata is
now recorded in the [Phase A report](../research/2026-10-17-production-evidence-phase-a.md), with
two fresh reviews of the same collector fingerprint: one independently repeated caller/function
only; the other checked offline contract/source consistency. These are not a forensic process audit
or a claim those reviewers reviewed this new packet or the original procedure's authoring.
The accepted [diagnostic packet](../research/2026-10-17-production-evidence-diagnostic.md) remains
separate and unchanged. The later authorized [window packet](../research/2026-10-17-production-evidence-window.md)
adds 413 reduced observations and reproducible descriptive distributions, but stops honestly at the
20-page bound. Acquisition/control assertions remain collector-attributed; independent acceptance
receipts are separate from this packet. Neither packet proves historical source/clean interval,
complete counts, package execution or compliance. Both read budgets are consumed; no replay,
extra page, download or broader source hunt follows automatically. Runtime smoke, deployment, load,
builds/tests/benchmarks, installs and commits remain **NOT RUN** in the wider-read run.
The procedure stays active; canonical full-cohort/attribution and increment closeout are not complete.
