# Post-deployment production observations — incomplete population

**Evidence snapshot: 2026-10-18.** Repository: `localstack-dotnet/badge-smith`.
This is a bounded, passive read of the production deployment promoted from
`fdf4c4a13df477bae6d4607ecfd922469faec2cc`, not a load test or a complete-window performance verdict.

## Outcome

- Current Lambda package readback matches the verified promoted ARM64 ZIP:
  **5,993,723 bytes**, SHA-256
  `16eb591d0a8ef8e395fdb4e2ff283165d1487cd1ebd31238c0a558626d87e512`.
  This is **6,277 bytes below** the unchanged 6,000,000-byte goal.
- **264 unique REPORT-shaped records**, including **86 with reported INIT**, were retained from
  **13 continued pages**. Zero returned candidates were rejected or deduplicated.
- The pass stopped at its conservative HTTP reservation for closing checks: **22 of 24** permitted
  HTTP dispatches, not exhaustion of all 14 permitted pages. Function and routing closing reads passed.
  The remaining two units were not used to resume collection after closing snapshots.
- **Pagination is incomplete.** The continuation does not prove how many additional matching rows
  exist, including whether the final unvisited page would be empty. The last retained event is not
  evidence of coverage through the requested end.
- INIT and memory goals remain **UNVERIFIED**. INIT has fewer than 100 cold records; memory has
  enough retained rows numerically, but neither has an established complete eligible population.

The [JSON manifest](baselines/2026-10-18-post-deploy-production.json) owns identities, safe call/page
receipts, exact configuration/routing snapshots, statistics, parser and limits. The
[reduced CSV](baselines/2026-10-18-post-deploy-production-reports.csv) owns the numeric observations.

## Descriptive measurements

These are distributions of the **collected prefix**, not qualified production p95 values.
Times are milliseconds; memory is the original `Max Memory Used` MB unit.

| Metric | n | Minimum | Median | p95 rank | Collected-prefix p95 | Maximum |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Reported INIT | 86 | 88.01 | 111.595 | 82 | **135.06** | 143.63 |
| Reported Duration | 264 | 0.88 | 2.50 | 251 | **263.45** | 997.63 |
| Same-row INIT + Duration | 86 | 113.22 | 292.22 | 82 | **465.34** | 1131.15 |
| Max Memory Used | 264 | 35 | 48 | 251 | **51** | 56 |
| Billed Duration | 264 | 1 | 3 | 251 | **369** | 1132 |

Of the retained observations, 80/86 reported INIT values exceed 100 ms and 23/264 memory values
exceed 50 MB. These are descriptive exceedances, **not a qualified goal FAIL**. The existing
[measurement protocol](../architecture/platform-and-delivery.md#measurement-policy) is unchanged.
The 178 records without INIT retain missing values, not warm zeros. All 264 text records lack an
explicit status field and remain `not_reported`, not asserted successes.

Exact `Decimal` values are sorted; p95 selects one-based rank `ceil(0.95 × n)`. The cold companion
adds INIT and Duration **within the same record before sorting**, not marginal percentiles. It is
Lambda-side timing, not client, Gateway or edge latency. Route/workload correlation is unknown.
Memory is an invocation-weighted execution-environment high-water observation, not per-request
allocation or independent retained-heap measurements; reused environments correlate samples.

## Subject and interval

Verified promotion records identify CI **39048346736 / attempt 1**, immutable artifact
**12106742202**, and [deployment 39049154134 / attempt 1](https://github.com/localstack-dotnet/badge-smith/actions/runs/39049154134).
Current source binding comes from that promotion plus API package-hash/byte equality, not inference
from `RevisionId`, `LastModified`, a CDK asset key or a ZIP's bytes alone.

The requested interval is **2026-10-17 16:40:20Z–2026-10-18 14:48:00Z**, inclusive API milliseconds
**1792255220000–1792334880000**. This is 22h07m40s, starting over 15 minutes after the later hosted
stack-completion observation and ending over five minutes before the earlier metadata preflight.
Those margins exclude the immediate transition/smoke; they do not guarantee complete late ingestion.

Actual collection ran **2026-10-18 15:10:16.118241Z–15:10:44.257395Z** (28.139 seconds).
Retained event times span **2026-10-17 16:43:13.627Z–2026-10-18 13:45:49.837Z**; maximum observed
ingestion lag is 9,016 ms. The requested end remains unproven, not shortened to the last event.

The exact stack-linked function in account `377140207735`, region `eu-central-1`, is
`badge-smith-function`, Zip/`$LATEST`, `provided.al2023`, ARM64, configured 512 MiB,
Active/Successful, revision `4b3899ff-659d-4238-88a7-a83b33545706`. Entire retained function snapshots
and both terminal stage/integration/route triplets match the earlier preflight and each other.
The default AutoDeploy stage/default AWS_PROXY route targets this unqualified function.
All retained records report 512 MB configuration; their 86 opaque stream keys indicate `$LATEST`.

This is operational interval/content evidence, not an exhaustive audit of out-of-band changes or
cryptographic per-invocation package attestation. A log-stream version marker is not a source SHA.
Current snapshots do not reconstruct every historical value of mutable `$LATEST`. Prior successful
Gateway/public smoke is separate execution evidence, not a representative cold cohort.

## Approved limits and observed acquisition

The user explicitly approved this concrete read-only pass after the metadata/budget checkpoint:

- At most **24 new HTTP dispatches**, including normal authentication; at most **14 event pages**;
  **10,000 returned candidates**, including duplicates/rejections; **2 MiB combined reduced outputs**.
- One request at a time, zero retries, 3/5-second connect/read timeouts and a 120-second request horizon.
- Exact function/group/window only; fresh caller, function and routing checks before/after; reserve
  closing reads and two-send per-call headroom, reducing feasible pages rather than exceeding the cap.
- No Insights query, package/S3 download, production request, workload/cold generation, settings,
  IAM, deployment, credential inspection or alternate-identity fallback.

Observed dispatches are caller 1 + function 2 + routing 6 + event pages 13 = **22**. No additional
authentication send or retry was observed in transient CLI traces. The first page returned 9 rows;
the final retained page returned 4; every page had a new continuation token. Individual page counts
are recorded in the manifest. No raw token, debug log or message was retained.

The official AWS CLI used the existing authorized read-only profile. Raw responses/debug remained
process-memory-only; CLI metadata projections excluded environment/reasons/headers, and event
messages were reduced by the previously reviewed anchored Text REPORT grammar before output.
Only numeric fields, finite classifications and scoped opaque identifiers were retained. Raw
request/stream identifiers, arbitrary application paths/bodies, credentials and signed URLs are absent.

The 22-send/no-retry/caller observations are **collector-attributed**. Independent offline checks can
recompute content, counters, control source and statistics; they do not independently authenticate
the discarded AWS responses or prove absence of hidden process activity. Page/row/time caps are
**not a hard scan-byte or currency ceiling**. Whole-group metadata is not this interval's scan cost.

## Verification and next boundary

Before acquisition, nine task-scoped offline tests passed: the actual reused grammar, failed-report
preservation, missing INIT, exact rank, paired sums, schema/version/boundary rejection, empty continued
pages, deduplication/byte accounting, closing-read reservations and a synthetic secret sentinel.
These are collector controls, not application tests, live provenance attestation or a new permanent CLI.

Reproduction: read the CSV with `csv.DictReader`, parse numeric strings using `Decimal`, exclude
empty INIT from INIT/cold-companion statistics, and calculate rank `(95*n+99)//100`. Recompute each
paired sum from that row's INIT and Duration. CSV SHA-256:
`10cdd0468e84f300604926dfe1cc3002afb3cc9d5b3216122ae3608b33ae5253` (52,660 bytes).

This approved acquisition is closed. No spare-call transfer, unannounced extra page, restarted scan,
smaller favorable window, pooling of the old 413-row prefix, traffic generation or automatic
optimization follows. More complete future evidence must preserve one attributable deployment
interval and the existing minimum counts; this packet cannot certify INIT or memory compliance.
