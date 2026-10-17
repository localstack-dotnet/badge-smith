# Production evidence — bounded REPORT diagnostic

**Two fresh offline reviews independently reproduced retained calculations and confirmed contract
interpretation.** Acquisition/process remains collector-attributed; physical acceptance is a separate
gate receipt. Investigation label:
2026-10-17; actual collection **2026-10-16T22:25:05.079064Z–22:25:06.705519Z**, from the local UTC
clock, not externally attested. This is a descriptive sample, **not a production performance baseline**.

## What the sample says

The two permitted pages returned **14 unique, accepted REPORT-shaped rows**, including **8 with
reported Init Duration**. Reported Duration ranged from **3.00 to 256.03 ms** (median **164.27 ms**).
For the eight INIT-bearing rows, initialization was **90.05–144.64 ms**; paired INIT + Duration was
**212.06–369.43 ms**. Reported Max Memory Used was **46–53 MB**, median **51 MB**, against **512 MB**
configured memory. These observations show sub-257-ms reported execution in this small prefix and
roughly 0.21–0.37-second paired Lambda-side timing where INIT was reported—not a general speed promise.

Seven of eight reported INIT values exceed 100 ms; eight of fourteen memory values exceed 50 MB.
That suggests limited margin against the unchanged architecture goals, **not a qualified FAIL**:
neither population reaches 100, collection is incomplete, and historical package attribution is
unverified. It is not evidence for changing memory allocation, optimizing code or changing targets.

| Descriptive population | n | Minimum | Median | Sample p95 (rank) | Maximum |
| --- | ---: | ---: | ---: | ---: | ---: |
| Duration, ms | 14 | 3.00 | 164.27 | 256.03 (14) | 256.03 |
| Max Memory Used, MB | 14 | 46 | 51 | 53 (14) | 53 |
| Reported Init Duration, ms | 8 | 90.05 | 112.485 | 144.64 (8) | 144.64 |
| Same-row Init Duration + Duration, ms | 8 | 212.06 | 340.695 | 369.43 (8) | 369.43 |
| Billed Duration, ms | 14 | 3 | 238.5 | 370 (14) | 370 |

Each sample p95 is nearest rank `ceil(0.95*n)`, here the maximum; it is **not representative production
p95**. Medians average the two middle values for even n. Decimal originals remain in the
[CSV](baselines/2026-10-17-production-report-sample.csv); the [manifest](baselines/2026-10-17-production-diagnostic.json)
owns counts, units, ranks, request receipts, controls and the CSV SHA-256. Pair each row **before**
sorting; never add marginal percentiles. Missing INIT is null (empty CSV cell), not zero or proof of
a warm invocation. Billed Duration is a billing observation, not client latency. Max Memory Used is
an invocation-weighted execution-environment high-water observation, not per-request allocation;
fourteen rows cover nine opaque streams, not fourteen independent environments.

## Authorized subject and provenance

After presentation of the concrete diagnostic proposal, the user explicitly instructed autonomous
execution without further check-ins. That authorized this diagnostic only, including the disclosed
scan-cost uncertainty, not substantial/full-cohort collection or any mutation/new traffic.
The [accepted Phase A packet](2026-10-17-production-evidence-phase-a.md) remains byte-identical and
records its earlier, then-unapproved proposal; this report records the subsequent execution.

- Repository: `github.com/localstack-dotnet/badge-smith`; local HEAD
  `1044b6d6e7160fc8a3dedcd3154337a4dfb4bd9f`, not a deployed-source attribution.
- Account **377140207735**, region **eu-central-1**; expected exact caller verified privately at
  `2026-10-16T22:25:05.426711Z`, before resource reads. Ordinary sanctioned authentication was used;
  identity success does not audit effective read-only permissions or trust.
- Exact function `arn:aws:lambda:eu-central-1:377140207735:function:badge-smith-function`;
  exact group `/aws/lambda/badge-smith-function`. No resource or stream discovery.
- Before configuration `22:25:05.677957Z`, after `22:25:06.705446Z` on October 16: **all 16 retained
  fields matched** the accepted [metadata manifest](baselines/2026-10-17-phase-a-preflight.json).
  This includes Zip / mutable `$LATEST`, revision `105c54c1-24ba-4fea-bbf7-af9224362815`, package
  SHA-256 `5cca93d09b1f40eb88b9f8ea544c96fa3309a739af6a3b7ad0f571b9d7f004bb`, API CodeSize
  **5,986,396 bytes**, `provided.al2023` / `arm64` / 512 MB. Environment was excluded before output.
  Derived hash/time fields used the accepted normalization. Snapshot references do not assign a
  historical revision to individual rows or establish continuous stability.

## Exact retrieval and coverage

Fixed historical window: **2026-10-16T20:58:32.206173Z–2026-10-16T21:58:32.206173Z**.
Each `FilterLogEvents` request used the exact group, `filterPattern='"REPORT RequestId:"'`,
`unmask=false`, default ascending order, inclusive `startTime=1792184312207` (ceil start) and
`endTime=1792187912206` (floor end), in epoch **milliseconds**. Only the returned transient token
was followed; no stream filtering, Insights, window shift or fallback occurred.

| Page | Requested limit | Returned candidates | Cumulative | Continuation present |
| --- | ---: | ---: | ---: | --- |
| 1 | 20 | 9 | 9 | yes |
| 2 | 11 | 5 | 14 | yes |

**Stopped at the two-page cap with continuation present**, not because the population was complete.
The stop receipt also grouped repeated-token stopping with page-cap stopping; no separate token-
repeat fact was retained. All fourteen candidates were accepted; zero layout/numeric/metadata
rejections and zero event/request duplicates were observed. Every candidate, including any rejected
or duplicate one, consumed the 20-candidate bound. Accepted event times span
`21:00:20.392000Z–21:45:48.462000Z`, not the full selected hour. This is the capped earliest matching
prefix, not random sampling or proof of gaps/zero traffic elsewhere. Partial/empty pages do not
prove completion. The result limit does not bound internally scanned bytes or currency; those were
not measured. Documented page-event payload bounds do not describe exact HTTP/JSON overhead.

## Projection, parser and numerical controls

The in-memory collector used installed Boto3/Botocore **1.40.72**, no installation or permanent
tooling. Session defaults **and all explicit/nested clients** used one total attempt, standard retry
mode, 3/5-second connect/read timeouts, pool size one, TLS verification and exact regional official
HTTPS endpoints. Debug logging and metadata fallback were disabled. Process-wide SDK-entry and
transport-send hooks counted nested ordinary authentication, rejected extra operations/requests,
retries/concurrency/nonofficial endpoints, and enforced a 120-second live horizon. No auth-parameter
ARN/session-name guess was used as a precondition. These are collector controls/receipts, **not an
independent forensic attestation**.

Before live calls, synthetic controls rejected wrong caller, source configuration-field drift,
prefixed/application text, multiline/unsupported layouts, wrong units, negative/NaN/infinite numeric
forms and unknown status combinations. They checked safe configuration projection (including a
non-exported Environment sentinel), missing INIT null, exact ceil/floor time bounds, remaining-limit
accounting, nested auth/send caps, endpoint/retry rejection and decimal/rank arithmetic. Values 1–100
give p95 95; paired `(10,90)` and `(90,10)` give p95 100, not 180. Synthetic fixtures are not CSV rows.

The anchored full-line Text grammar requires `REPORT RequestId:` plus UUID, then Duration and Billed
Duration in ms, Memory Size and Max Memory Used in MB, optional Init Duration in ms, then optional
fixed Status (`success`, `error`, `timeout`) and Error Type (`Runtime.ExitError`, `Extension.Crash`,
only with error status). Only one normal terminal LF/CRLF is removed; horizontal separators and a
terminal horizontal separator are allowed by the grammar. Numeric tokens are bounded unsigned
decimal text, checked finite/nonnegative; required memory size is positive. The exact regex is in
the manifest. Unknown layouts are counted without echo, not parsed by substring extraction. All 14
observed statuses were **not_reported**, not asserted successful; no error type was present.

Only validated numeric/system fields, epoch/UTC times and 96-bit opaque event/request/stream keys
were retained. Keys are scoped SHA-256 prefixes of **source identifiers**, never raw-message hashes;
they permit within-packet deduplication, not independent reauthentication of source. No raw message,
continuation token, request path, body, header, arbitrary exception text or credential was retained.
A copied/spoofed application line with this exact grammar can pass: **REPORT-shaped is not
cryptographic platform-origin proof**. Row eligibility is not a package-execution certificate.

## Accounting and remaining proof gaps

Exactly **6 SDK entries and 6 HTTP dispatches**, including **1 ordinary authentication request**;
all returned HTTP 200, no retries recorded, concurrency one. Physical dispatch order: authentication,
caller identity, before configuration, page 1, page 2, after configuration. The identity SDK entry
started before its nested auth entry; those are not two concurrent HTTP sends. Live elapsed time
was approximately **1.626455 seconds**. No extra live check is authorized for the offline reviewers.

Source/promotion linkage, historical code/configuration/routing interval, complete REPORT coverage,
suppressed initialization, proven platform origin, executed package/runtime, exact consumed-ZIP size
and **all performance compliance remain UNVERIFIED**. Route/workload is unknown; no endpoint or
client/edge latency inference is justified. Eight INIT-bearing rows are not a complete cold census.
Matching current snapshots cannot fill those gaps and do not erase the descriptive numbers.
The [canonical measurement protocol](../architecture/platform-and-delivery.md#measurement-policy)
and goals remain unchanged. No diagnostic p95 was promoted into a production baseline.

No AWS mutation, invocation, production HTTP/new traffic, other AWS source, GitHub request, download,
build, test, benchmark, install, commit or push occurred. Only this report, its JSON/CSV and targeted
current-status pointers were written in the repository. Earlier evidence and numeric baselines are
preserved; the active [procedure](../plans/2026-10-16-production-evidence-collection.md) is not complete.
Two fresh offline reviews independently reproduced retained calculations and confirmed contract
interpretation; acquisition/process remains collector-attributed. Physical acceptance is a separate
gate receipt, not established by these reviews.
