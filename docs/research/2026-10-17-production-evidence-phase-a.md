# Production evidence — Phase A metadata preflight

Investigation label: **2026-10-17**. Actual observations occurred on **2026-10-16 UTC**:
collector `21:54:41–21:54:43`; independent caller/function check `21:58:31–21:58:32`.
These are local UTC clock readings, not externally attested times or retimed observations.

**Outcome:** existing sanctioned access now works; bounded metadata is recorded. Source linkage,
clean deployment interval, REPORT schema/counts, runtime execution and all performance verdicts
remain **UNVERIFIED**. **Phase B is unapproved and NOT RUN.** This is not increment closeout.
The [active procedure](../plans/2026-10-16-production-evidence-collection.md) remains in flight.
The [metadata manifest](baselines/2026-10-17-phase-a-preflight.json) contains observed data, not a
numeric performance baseline; no REPORT CSV or empty sample dataset was created.

## Subject and observation provenance

- Repository: `github.com/localstack-dotnet/badge-smith`; local master
  `1044b6d6e7160fc8a3dedcd3154337a4dfb4bd9f`, observed `2026-10-16T21:50:54.297104Z`.
  This is **local source**, not a deployed commit or a source chronology for the package.
- Account `377140207735`, region `eu-central-1`, exact stack `BadgeSmithStack`, stack ID
  `arn:aws:cloudformation:eu-central-1:377140207735:stack/BadgeSmithStack/1b26f040-8467-11f0-97ea-06c36a337e67`.
- Resolved function `arn:aws:lambda:eu-central-1:377140207735:function:badge-smith-function`;
  actual function log group `/aws/lambda/badge-smith-function`.
- Collector verified the expected account and exact sanctioned caller before resource reads at
  `2026-10-16T21:54:41.481545Z`. A fresh independent check matched the expected caller at
  `2026-10-16T21:58:31.899302Z`, then reread only this exact function.
- The access/setup handoff was operator-reported. Caller verification is direct, but neither STS
  success nor a role name audits effective read-only permissions, IAM trust or least privilege.
  No such audit or access/configuration change was part of this packet.

| Source | Actual observation UTC, 2026-10-16 | Verification boundary |
| --- | --- | --- |
| Stack / resource mapping | `21:54:41.667996Z` / `21:54:41.895203Z` | Collector-observed; offline consistency checked, not independently reread live. |
| Function configuration | `21:54:42.145094Z` | All 16 retained fields independently matched at `21:58:32.206173Z`. |
| API / stages / routes / integrations | `21:54:42.547453Z` / `21:54:42.674943Z` / `21:54:42.786026Z` / `21:54:42.900077Z` | Collector-observed; reference consistency checked offline. |
| Log group / ten latest streams | `21:54:43.058365Z` / `21:54:43.111843Z` | Collector-observed; units/conversions checked offline. |
| Bounded stack history | `21:54:43.339724Z` | Collector-observed; discarded older timestamps not independently recoverable. |

## Current function configuration, not execution

| Field | Matching current snapshots |
| --- | --- |
| Version / revision | Mutable `$LATEST` / `105c54c1-24ba-4fea-bbf7-af9224362815` |
| Package / lifecycle | `Zip`; `Active`; last update `Successful` |
| Runtime / architecture / configured memory | `provided.al2023` / `arm64` / **512 MB** |
| API CodeSize | **5,986,396 bytes**; not an independently consumed-file measurement |
| Package SHA-256, normalized from base64 | `5cca93d09b1f40eb88b9f8ea544c96fa3309a739af6a3b7ad0f571b9d7f004bb` |
| LastModified | `2026-08-08T12:28:26.000+0000` (`2026-08-08T12:28:26.000000Z`) |
| LoggingConfig | `Text`, exact function group above; system/application level fields not returned |

[GetFunctionConfiguration](https://docs.aws.amazon.com/lambda/latest/api/API_GetFunctionConfiguration.html)
defines package bytes/hash and update metadata. LastModified is not a unique Git commit, a clean
deployment cutover or evidence this package executed. Configured memory is not Max Memory Used.
Matching snapshots do not establish continuous stability; unqualified invocation selects mutable
[`$LATEST`](https://docs.aws.amazon.com/lambda/latest/dg/configuration-versions.html).

The [dated hosted master record](2026-10-16-delivery-and-diagnostics-closeout.md#master-ci-and-artifact)
reports source `df953f247aed5d03d89b4d1af6bb78e10214effb`, run `38903269306`/1, artifact
`12047446938`, inner ZIP **5,992,808 bytes**, SHA-256
`0cda2b1f84e54ab54cbeeb100b78b3b3382815f4e6685ff936b82e4625d0e04f`.
Its wrapper archive was 5,968,184 bytes. Current package metadata differs in both hash and size
(API bytes minus dated ZIP bytes = **−6,412**). This is an offline comparison to attributed historical
evidence, not a fresh GitHub/artifact read, failed-deployment finding or bad-artifact finding.
Neither package's source identity can be assigned to the current deployment by this comparison.
API arithmetic alone does **not** establish the canonical exact-consumed-ZIP size PASS.

## Routing, storage and history limits

The collector's `UPDATE_COMPLETE` stack snapshot reports creation `2025-08-28T23:31:22.289000Z`
and last update `2026-08-08T12:28:16.247000Z`. One resource-list page returned 17 rows; seven
relevant rows were retained, with no continuation. This is a type projection, not a seven-resource stack.

HTTP API `g4yecfi5hl` (`badge-smith-api`) has `$default` stage, deployment `lccyot`,
`AutoDeploy=true`; route `wkuklad` (`$default`) targets `integrations/nkcccpl`.
That `AWS_PROXY`/POST/payload-2.0 integration targets the exact **unqualified** function ARN.
Stage/route/integration listings each returned one row on one page without continuation.
[AutoDeploy](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-stages.html)
does not prove transition-free historical routing. No stage export, alias inventory, active request,
CloudFront read or public-domain/edge-path verification occurred. No referenced alias was observed;
that is not an account-wide assertion that aliases do not exist.

The exact function group reports **731-day retention**, **43,145,039 stored bytes**, `STANDARD`,
and creation epoch milliseconds `1756423945202` (`2025-08-28T23:32:25.202000Z`).
One descending LastEventTime stream page retained ten rows and had continuation **present**.
The manifest preserves their 40 original epoch-millisecond timestamps and UTC conversions.
[LogStream](https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_LogStream.html)
event/ingestion timestamps are eventually consistent; deprecated stream `storedBytes=0` does not
mean empty streams. Group stored bytes are storage metadata, not scanned bytes or a cost estimate.
Today's retention/Text settings do not prove historical retention, schema or successful REPORT coverage.
There were **no log-event reads**; complete REPORT/cold/paired counts are unknown, not zero.

The CloudFormation history lookback was `2026-09-16T21:54:43.111853Z` through
`2026-10-16T21:54:43.111853Z`. One page returned 100 rows transiently; continuation was present,
the collector reported reaching the older boundary and retained **zero relevant events**.
[DescribeStackEvents](https://docs.aws.amazon.com/AWSCloudFormation/latest/APIReference/API_DescribeStackEvents.html)
is reverse chronological with no request time filter. The retained result is not proof of no
out-of-band updates, complete code/configuration/routing history or a clean attributable interval.

## Review identity, controls and exact accounting

Two fresh skeptics found **no material positive claim refuted**. Both reviewed the same collector
report SHA-256 `97179539d9204fd60568389bc2ce8fc0d65ee6c6ff6af6c45bffa965e3381e0a`
(1,242 lines, 52,310 bytes). This identifies reviewed evidence, **not the Lambda package**.
Their separate report fingerprints are in the manifest. The live lens corroborated caller and all
16 function fields only; both lenses checked retained-content interpretation and offline consistency,
including 41 epoch conversions, digest normalization, resource links and ledger arithmetic.
Collector process/control assertions remain collector-reported. Neither skeptic forensically audited
all collector commands or every pre-output projection; the live verifier separately checked its own
controls. Optional adversarial counterclaims in reviews are not real producer findings.

| Accounting category | Consumed / boundary |
| --- | --- |
| Collector current actual HTTP attempts | 12, including one normal authentication request |
| Zero-network preparation charges | 2 conservative units; collector total **14/15** |
| Independent verifier actual HTTP attempts | 3, including one normal authentication request |
| Current resumed HTTP attempts | **12 + 3 = 15** |
| Historical no-response SDK probe | 1 conservative unit; actual HTTP dispatch count not independently established |
| Combined conservative consumption | **14 + 3 + 1 = 18 of original 20**; frozen maximum allocation **19** |

No retries were reported among recorded calls. Do not label 16 an independently observed total HTTP
count. No unused allocation transfers. An incorrect local authentication precondition was corrected
before network dispatch, with no identity switch or AWS denial bypass; its two charges remain.
A separate pre-execution syntax failure reported zero SDK/HTTP/writes. Full preparation detail stays
in the private collector record, not nested into the current manifest. The
[historical failed-auth attempt](2026-10-16-202846Z-production-evidence.md) and
[twice-reviewed scoping note](2026-10-16-production-evidence-scoping.md) remain unchanged.
This recording step made zero AWS/authenticated/live-production calls; it used local evidence and
public official documentation only. No build/test/benchmark, download, invocation, mutation or commit ran.

## Checkpoint proposal — first diagnostic only, NOT APPROVED

**Request explicit approval for this new diagnostic budget, or stop here.** It does not reuse or
expand the Phase A cap. The author recommends the following narrowly scoped first Phase B step:

1. Existing exact function/group/account/region above; no other source or resource. Fixed recent
   one-hour window **`2026-10-16T20:58:32.206173Z`–`2026-10-16T21:58:32.206173Z`**, ending at
   the last verified function observation, not execution time. This is not an attributable cohort.
   [FilterLogEvents](https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_FilterLogEvents.html)
   uses inclusive epoch-millisecond boundaries. To stay inside the exact microsecond window, use
   **startTime `1792184312207`** (ceil) and **endTime `1792187912206`** (floor).
2. Sequential `FilterLogEvents`, exact `logGroupName`, `filterPattern='"REPORT RequestId:"'`,
   `unmask=false`, default ascending order; no Insights/StartQuery, stream discovery or new traffic.
   At most **20 returned candidates total**, including rejected/nonplatform rows, across **two pages**.
   Set each page's `limit` to the remaining count; follow only its transient nextToken, stop at either
   cap or no token. Empty/partial pages can still continue; reaching a cap never means completeness.
3. At most **six AWS attempts total**: normal authentication 1 + GetCallerIdentity 1 + exact function
   configuration before 1 + event pages up to 2 + exact function configuration after 1. No retries;
   concurrency 1, 3/5-second connection/read timeouts, 120-second request horizon. Count normal auth;
   fail closed before any extra auth/send. Stop on denial, unexpected identity, changed package/revision
   or any compared configuration, exhausted bound, or unsafe projection. No fallback resource/identity.
4. Raw events remain transient. Require anchored full-line Text platform REPORT grammar, UUID request
   ID, finite nonnegative numeric fields and recognized fixed optional status fields; reject unsupported
   layouts/app text without echo. Retain only scoped opaque event/request/stream keys, event/ingestion
   milliseconds plus UTC, Duration/Billed Duration/optional Init Duration in ms, configured/maximum
   memory in MB, finite outcome/missing/rejection counters and snapshot references. Missing INIT stays
   null. Maximum reduced export **64 KiB**; no bodies, headers, arbitrary error text, paths or messages.
   REPORT-shaped text alone is not cryptographic proof of platform origin or package attribution.
5. Service documentation bounds each page to **1 MB of log events** (also at most 10,000 events;
   our limit is smaller). Two pages bound documented event payload, not exact HTTP/JSON overhead,
   internally scanned bytes or currency. The returned-event limit is not a hard scan/spend cap;
   storedBytes cannot price this step. Accept this uncertainty explicitly or decline the diagnostic.

This diagnostic can establish format/availability only. It cannot prove the complete 30-day cohort,
source commit, clean history, executed package or compliant p95. Canonical
[measurement requirements](../architecture/platform-and-delivery.md#measurement-policy) remain unmet;
INIT, memory, size and paired Lambda-side cold timing verdicts stay **UNVERIFIED**. A full-cohort and
attribution step requires its own justified approval/budget after diagnostics; no CloudTrail or other
source expansion is approved or executable by this proposal. **Stop at this approval checkpoint.**
