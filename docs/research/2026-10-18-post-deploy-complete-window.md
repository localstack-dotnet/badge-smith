# Post-deployment complete available REPORT window

**Evidence snapshot: 2026-10-18**, repository `localstack-dotnet/badge-smith`, promoted source
`fdf4c4a13df477bae6d4607ecfd922469faec2cc`. This fresh bounded read reaches terminal pagination.
It completes retrieval of the available matching REPORT records, **not every historical invocation
or every performance-qualification obligation**.

## Outcome

- **272 unique REPORTs / 90 reported INIT / 182 missing INIT / 90 opaque streams**, zero rejected
  or duplicate candidates. Every text status is `not_reported`, not asserted success.
- **15 pages**: the first 14 have continuation; page 15 is empty with **no continuation token**.
  Available matching-window retrieval is complete at collection time.
- **24 HTTP dispatches** within the newly announced 32-call/20-page bound, including successful
  function and stage/integration/route closing reads. No retries, production requests or mutations.
- The exact current package remains **5,993,723 bytes**, SHA-256
  `16eb591d0a8ef8e395fdb4e2ff283165d1487cd1ebd31238c0a558626d87e512`, matching the promoted ZIP.
- INIT qualification remains **UNVERIFIED: 90 < 100 cold records**. No traffic, resets, memory
  changes or deployment was used to manufacture the missing ten observations.
- Observed memory p95 is **52 MB**, above the 50 MB target, with 272 records and terminal retrieval.
  The bounded historical/source-attribution limitations below remain separate from this numeric
  comparison; this packet does not certify exhaustive revision-qualified compliance.

The [JSON manifest](baselines/2026-10-18-post-deploy-complete-window.json) owns identities, exact
safe snapshots/call receipts, page counters, statistics and the prior-packet reconciliation. The
[CSV](baselines/2026-10-18-post-deploy-complete-window-reports.csv) owns the numeric observations.

## Exact statistics for the available retrieved window

Times are milliseconds and memory retains the original REPORT MB unit.

| Metric | n | Minimum | Median | p95 rank | p95 | Maximum |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Reported INIT | 90 | 88.01 | 111.595 | 86 | **135.06** | 143.63 |
| Reported Duration | 272 | 0.88 | 2.905 | 259 | **263.45** | 997.63 |
| Same-row INIT + Duration | 90 | 113.22 | 291.49 | 86 | **465.34** | 1131.15 |
| Max Memory Used | 272 | 35 | 48 | 259 | **52** | 56 |
| Billed Duration | 272 | 1 | 4 | 259 | **369** | 1132 |

Exact Decimal sorting and one-based rank `ceil(0.95*n)` reproduce these values. Paired cold timing
adds the two fields in each row before sorting, not marginal percentiles. Missing INIT stays empty,
not zero or proven warm. Memory is invocation-weighted environment high-water, not per-request
allocation, retained managed heap or independent environments. Workloads/routes, client latency
and suppressed initialization remain unmeasured. There are 84/90 INIT values above 100 ms and
26/272 memory values above 50 MB; do not label these as causal old/new optimization results.

## Window, acquisition and operational identity

Frozen interval: **2026-10-17T16:40:20Z–2026-10-18T15:51:05Z**, inclusive FilterLogEvents milliseconds
**1792255220000–1792338665000**, 23h10m45s. The start retains the conservative post-cutover buffer;
the end is five minutes before the round's setup. Neither buffer proves all late events have arrived.

Actual collection: **2026-10-18T15:57:56.847154Z–15:58:24.519010Z**, 27.672 seconds. Retained events
span **2026-10-17T16:43:13.627Z–2026-10-18T15:34:52.114Z**. Empty terminal pagination, not the last
event time, establishes completion of this matching query as observed then.

The existing source/artifact/deployment chain is CI **39048346736/1**, artifact **12106742202**,
[deployment 39049154134/1](https://github.com/localstack-dotnet/badge-smith/actions/runs/39049154134),
followed by live CodeSha256/CodeSize equality with the retained promoted ZIP. The function is the
same `badge-smith-function` in `BadgeSmithStack`, account `377140207735`, region `eu-central-1`:
Zip/`$LATEST`, `provided.al2023`, ARM64, 512 MiB, Active/Successful, revision
`4b3899ff-659d-4238-88a7-a83b33545706`, LastModified `2026-10-17T16:25:07Z`.

Entire retained function snapshots and both terminal API stage/integration/route triplets equal
the earlier preflight and each other. The default AutoDeploy stage routes through the same
unqualified AWS_PROXY Lambda integration. Prior promotion/CFN chronology plus package and routing
checks support operational interval attribution; they are not an exhaustive audit of mutable
`$LATEST`, out-of-band routing changes, discarded-response authenticity or per-event source hashes.
Code hashes, RevisionId, version markers and LastModified have distinct meanings.

## Reconciliation with the earlier partial read

The [earlier packet](2026-10-18-post-deploy-production.md) remains unchanged. All 264 rows were
freshly reobserved with identical reduced values, ignoring only page number. **Eight additional
rows, four with INIT, are all after its earlier 14:48:00Z end**; none adds a matching row inside
the old interval. The old packet lacked terminal evidence; this does not demonstrate that it
actually omitted a matching REPORT. New observations were not assembled by pooling old data.

The memory p95 change from 51 to 52 MB therefore concerns **different observation windows**, not
a proven performance regression or effect of the optimization. The older source-unattributed
413-row prefix is also not an A/B baseline and was never pooled into this population.

## Bounds, safety and completion limits

The user explicitly delegated safe autonomous progress on completing the same-revision baseline.
Before any new read, this round announced and froze **32 new HTTP calls / 20 event pages**, with
10,000 returned candidates, 2 MiB combined reduced output, 120 seconds, zero retries, concurrency
one and 3/5-second connect/read timeouts. Earlier budgets stayed closed. This is one bounded
follow-up, not indefinite querying or permission for configuration/code/deployment changes.

Actual sequence: five initial metadata reads + 15 event pages + four closing reads = **24 sends**.
No additional authentication send was observed. All page candidates were counted before parse/
deduplication; all 272 passed the same pinned anchored Text REPORT grammar. Raw message/debug/
authentication/API data stayed transient; only finite metadata, numeric fields and scoped opaque
identifiers were retained. No credentials, raw request/stream IDs, arbitrary application paths,
headers, signed URLs, continuation tokens, personal profile or machine paths enter the packet.

Ten offline collector tests passed before acquisition, including a control proving all 20 allowed
pages fit with ordinary authentication and closing reservations. Current CLI/caller/send facts
are collector-attributed; offline reproduction does not authenticate discarded raw AWS responses.
Page/row/time/call bounds do not guarantee a hard scan-byte or currency ceiling.

The [measurement policy](../architecture/platform-and-delivery.md#measurement-policy) is unchanged.
Terminal retrieval removes the earlier pagination gap, but does not supply the missing ten cold
samples, future delayed records, a full historical audit or a cryptographic per-invocation identity.
No additional live collection, periodic monitor, synthetic request, fix or target relaxation follows
automatically. The safe stopping point is a completed available window with an explicit cold-count
limit, rather than another truncated packet or manufactured observations.

CSV: **54,288 bytes**, SHA-256
`7d66280c0b326470fa32e35d0bce5dd09c2587d39de7a270ae6c1a91b759f661`.
Recompute all values from numeric strings with Decimal and rank `(95*n+99)//100`; exclude empty
INIT only from INIT/paired distributions, and recompute each paired sum from its own record.
