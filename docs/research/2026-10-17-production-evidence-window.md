# Production evidence — bounded last-24-hour REPORT read

**Outcome: useful measurements, incomplete population.** The authorized wider read collected
**413 unique REPORT-shaped records, 102 with reported Init Duration**, then stopped at the hard
**20-page cap with continuation still present**. Collected-prefix p95 was **135.15 ms INIT**,
**253.39 ms Duration**, **408.82 ms same-row INIT + Duration**, and **54 MB Max Memory Used**.
These exceed the numerical INIT/memory goals; they are **not a qualified production FAIL or a
complete-window p95**. Both minimum counts are met, but completeness and source/interval proof are not.

Investigation label: **2026-10-17**. Actual collection: **2026-10-16T23:00:23.210313Z–23:00:33.543456Z**,
local UTC clock, not externally attested. No new traffic, deployment or code change was made.
Acquisition and pre-live controls are collector-attributed; retained arithmetic can be reproduced
offline. This packet does not certify its own independent acceptance.

## Practical performance answer

| Collected population | n | Minimum | Median | Prefix p95 (rank) | Maximum |
| --- | ---: | ---: | ---: | ---: | ---: |
| Duration, ms | 413 | 0.68 | 3.02 | 253.39 (393) | 374.94 |
| Max Memory Used, MB | 413 | 35 | 46 | 54 (393) | 56 |
| Reported Init Duration, ms | 102 | 87.98 | 110.645 | 135.15 (97) | 146.14 |
| Same-row Init Duration + Duration, ms | 102 | 90.46 | 302.25 | 408.82 (97) | 507.03 |
| Billed Duration, ms | 413 | 1 | 4 | 367 (393) | 508 |

- **INIT:** 88/102 reported values exceed 100 ms; prefix p95 is **35.15 ms above** that goal.
- **Memory:** 99/413 values exceed 50 MB; prefix p95 is **4 MB above** that goal. All rows report
  **512 MB configured memory**, not 512 MB used.
- **First-invocation companion:** among INIT-bearing rows, Duration median/p95 is **197.12/288.52 ms**;
  paired INIT + Duration median/p95 is **302.25/408.82 ms**. Add within each row before sorting.
- **No reported INIT:** 311 rows have Duration median/p95 **1.11/148.59 ms**. Their absent INIT is
  **null, not zero and not proof of a warm invocation**. Suppressed initialization is not enumerated.

This is evidence of a fast typical reported invocation with a materially slower tail and additional
first-invocation work—not a route-specific explanation or an optimization result. Workloads/routes
are unknown. Duration, paired timing and Billed Duration are **not client/network/CloudFront latency**.
Max Memory Used is an invocation-weighted execution-environment high-water observation, not a
per-request allocation. The 413 rows cover 102 opaque streams; they are not 413 independent environments.

The [CSV](baselines/2026-10-17-production-window-reports.csv) owns original decimal tokens and reduced
observations. The [JSON manifest](baselines/2026-10-17-production-window.json) owns exact counts,
partitions, ranks, hashes, page/request receipts, projection controls and qualification limits.
The immutable [14-row pilot](2026-10-17-production-evidence-diagnostic.md) remains separate:
**all 14 eligible rows matched every retained immutable field**, using the same opaque-key namespace.
Page numbers and collection snapshot references intentionally differ; the pilot was not rerun or edited.

## Frozen window, retrieval and honest coverage

The successful before-configuration observation froze the requested last-24-hour window **before
any event read**:

- Exact UTC: **2026-10-15T23:00:23.833443Z–2026-10-16T23:00:23.833443Z**, 86,400 seconds.
- Inclusive API epoch milliseconds: **startTime=1792105223834**, **endTime=1792191623833**;
  ceil the exact start and floor the exact end, without shifting the window after seeing counts.
- Exact function: `arn:aws:lambda:eu-central-1:377140207735:function:badge-smith-function`;
  exact group: `/aws/lambda/badge-smith-function`, account **377140207735**, region **eu-central-1**.
- Sequential `FilterLogEvents`, `filterPattern='"REPORT RequestId:"'`, `unmask=false`, default
  ascending ordering. Each `limit=min(10000,5000-all_previous_returned_candidates)`; rejected and
  duplicate candidates would also consume that bound. Only returned transient continuation tokens
  were followed; no Insights, stream discovery, sampling by success or window expansion.

| Pages | Returned candidates per page | Cumulative |
| --- | --- | ---: |
| 1–5 | 8, 6, 6, 16, 206 | 242 |
| 6–10 | 26, 19, 3, 9, 12 | 311 |
| 11–15 | 15, 11, 3, 29, 14 | 383 |
| 16–20 | 4, 10, 6, 8, 2 | 413 |

Every page had continuation; none was empty or repeated a token. Every page contributed unique rows.
The final request limit was **4,589**, not a stop-at-100 rule. All **413 candidates parsed**:
zero rejections, event/request duplicates, storage exclusions or reported-memory mismatches.
All rows have the observed stream version **`$LATEST`**, all outcomes are **`not_reported`**, and
none has a reported Error Type. Absent status is **not asserted success**. No version or memory
partition was silently discarded; the manifest includes per-version, INIT-presence, status and
reported-memory partitions. A mutable version label does not identify a historical configuration.

Retained event times span **2026-10-15T23:04:40.018000Z–2026-10-16T22:36:16.440000Z**. That span
does not prove there were no matching events afterward or gaps elsewhere. The final page was observed
at **2026-10-16T23:00:32.354410Z**. This is an earliest matching, nonrandom **incomplete prefix**, not
the complete 24-hour population. Total available-window REPORT/INIT counts remain unknown, not 413/102.

`paginationComplete=false`; `parsedCoverage=true` for returned candidates;
`filteredAvailableREPORTPopulationComplete=false`; `sourceAttributionEligibility=false`.
Even terminal pagination would establish only matches available during export, not an atomic
snapshot, future late arrivals, dropped/unlogged records, all schemas or all cold starts.
[FilterLogEvents](https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_FilterLogEvents.html)
can return partial/empty pages with continuation. Page/candidate limits do **not** impose a hard
internal scan-byte/currency ceiling; neither scan bytes nor cost was observed or invented.

## Configuration and bounded source check

The expected exact caller/account was verified privately **before resources**, at
**2026-10-16T23:00:23.564730Z**. Ordinary sanctioned authentication was used, without credential-file
inspection, another profile or access setup. Identity success does not audit effective permissions.

Before **23:00:23.833443Z** and after **23:00:33.543407Z**, all **16 projected fields** matched the
[accepted Phase A manifest](baselines/2026-10-17-phase-a-preflight.json): Zip / mutable `$LATEST`,
revision `105c54c1-24ba-4fea-bbf7-af9224362815`, package SHA-256
`5cca93d09b1f40eb88b9f8ea544c96fa3309a739af6a3b7ad0f571b9d7f004bb`, API CodeSize **5,986,396 bytes**,
`provided.al2023` / `arm64` / **512 MB**. Base64/hex digest and UTC normalization reproduced the
accepted fields digest `6e52a1836ec9aff9c81199766c54318a770ca682a2c948c686397cb2e4742935`.
The API size is numerically 13,604 bytes below 6,000,000, **not an exact consumed-ZIP measurement**.
Snapshots alone establish neither execution of that package nor continuous historical stability.

Five optional, exact-resource metadata reads ran after the event pages:

| Read / UTC, 2026-10-16 | Retained finding and limit |
| --- | --- |
| `DescribeStacks`, 23:00:32.615131Z | Exact known `BadgeSmithStack` ID, `UPDATE_COMPLETE`; last update 2026-08-08T12:28:16.247000Z. Not a complete change history. |
| `GetTemplate`, 23:00:32.790761Z | Only the known Lambda logical resource's safe Code locator/configuration projection retained. Runtime, architecture and memory match; handler is `bootstrap`. No Git source binding. |
| `DescribeStackEvents`, 23:00:32.936967Z | One reverse-chronological page, 100 rows all older than the frozen window, discarded. Older boundary reached with continuation; zero relevant in-window events retained. This bounds available CloudFormation history, not out-of-band changes. |
| `GetStages`, 23:00:33.251512Z | One `$default` AutoDeploy stage, deployment `lccyot`, no continuation; same retained stage identity as Phase A. Not historical/public routing proof. |
| `GetIntegrations`, 23:00:33.362553Z | One `nkcccpl` AWS_PROXY/POST/payload-2.0 integration, same unqualified function target, no continuation. Route mapping itself was not reread. |

The recognized template locator is bucket `cdk-hnb659fds-assets-377140207735-eu-central-1`, key
`20e2f278cbbbb7d5322fa7799454f6d0b1657271e3a32a4b34798164db5693dc.zip`. This is **CDK asset metadata**,
not a Git SHA, not the Lambda package digest, and not proof of S3 content or its deployed consumption.
No S3 object/version read or download ran. Whole template, environment, parameters, outputs, tags,
status reasons, arbitrary metadata values and a raw-template hash were not retained.

The local [PR #5 closeout](2026-08-08-pr5-second-pass-closeout.md#merge-and-deployment-evidence)
attributes merge `2b147de`, CI `31255516767` attempt 2 and deploy run `31257227036`, but contains no
current-package digest/provenance binding. Its date agrees with update metadata; that is a **clue,
not source attribution**. The current package still differs from the dated `df953f2` hosted artifact
identified in Phase A. Neither dated note, local HEAD, LastModified nor the asset key establishes
`deployedSourceCommit`: **unknown**. The source hunt stopped at these authorized sources, with no
GitHub refresh, arbitrary local artifact search or broader AWS access.

## Controls, accounting and reproducibility

User authorization covered this proposed bounded wider read: at most **30 AWS SDK entries/HTTP
attempts including normal authentication**, **20 log pages**, **5,000 candidates**, **2 MiB combined
new report/JSON/CSV**, concurrency one, no retries, 3/5-second connect/read timeouts and a 120-second
live horizon. Unused metadata/request allocation cannot become extra log pages.

Actual: **29 SDK entries / 29 HTTP dispatches**, **one normal authentication request**, all HTTP 200,
zero recorded retries; **10.333146 seconds**. Breakdown: authentication 1 + caller 1 + function
before/after 2 + log pages 20 + exact metadata 5. No thirtieth request or twenty-first page followed.
SDK entry order starts caller then its nested authentication; HTTP dispatch order is authentication
then caller, not concurrent sends. The manifest retains fixed-operation/time/status receipts only.

Installed Boto3/Botocore **1.40.72** was used in memory; no helper/tool was installed or committed.
Session defaults and all explicit/nested clients enforced `total_max_attempts=1`, regional official
HTTPS endpoints/TLS, no proxy, pool one and the timeouts above. SDK-entry and transport hooks counted
nested authentication, rejected retries/concurrent sends/unapproved operations/resources/endpoints,
and reserved the final configuration read. Nonfinal reads had a 110-second cutoff; a 120-second
alarm bounded live acquisition. These process assertions are **collector-reported, not forensic
independent attestation**.

Pre-live synthetic checks exercised wrong caller/configuration drift, safe Environment exclusion,
full-line parsing, units/numeric/status rejection, missing INIT null, metadata/version validation,
remaining-candidate scope, nested authentication/request guards, exact time bounds and decimal
rank/pairing arithmetic. The accepted diagnostic's exact anchored Text REPORT grammar was reused,
without a live-schema extension. Unsupported layouts would be counted without echo; copied
application text with the exact grammar can still pass. **REPORT-shaped is not cryptographic
platform-origin proof.** Opaque identifier keys use the pilot's scoped SHA-256-prefix rule, never
message hashes; raw messages, request IDs, tokens, bodies, headers and arbitrary errors were not retained.

Offline reproduction requires only Python's standard library; this reads retained data, not AWS:

```python
import csv, hashlib, json
from decimal import Decimal
from pathlib import Path

p = Path("docs/research/baselines")
m = json.loads((p / "2026-10-17-production-window.json").read_text())
b = (p / m["sample"]["csv"]).read_bytes()
assert hashlib.sha256(b).hexdigest() == m["sample"]["sha256"]
rows = list(csv.DictReader(b.decode().splitlines()))
assert len(rows) == m["counts"]["acceptedUniqueRows"]
assert len({r["eventKey"] for r in rows}) == len(rows)
assert len({r["requestKey"] for r in rows}) == len(rows)

def stats(values):
    x = sorted(map(Decimal, values))
    n = len(x)
    if not n:
        return dict(n=0, min=None, median=None, p95Rank=None, p95=None, max=None)
    rank = (95*n + 99)//100
    middle = x[n//2] if n % 2 else (x[n//2-1] + x[n//2])/2
    return dict(n=n, min=str(x[0]), median=str(middle), p95Rank=rank,
                p95=str(x[rank-1]), max=str(x[-1]))

cold = [r for r in rows if r["reportedInitPresent"] == "true"]
observed = {k: stats(r[k] for r in rows)
            for k in ("durationMs", "maxMemoryUsedMb", "billedDurationMs")}
observed["reportedInitDurationMs"] = stats(r["initDurationMs"] for r in cold)
observed["pairedInitPlusDurationMs"] = stats(
    Decimal(r["initDurationMs"]) + Decimal(r["durationMs"]) for r in cold)
assert observed == m["statistics"]
assert stats(range(1, 101))["p95"] == "95"
assert stats([Decimal(10)+90, Decimal(90)+10])["p95"] == "100"
assert stats([])["p95"] is None
print(json.dumps(observed, indent=2))
```

The collector also preserved an aggregate inventory of **295 prior regular files outside the five
allowed repository paths**, plus HEAD/index fingerprints. Earlier evidence is immutable; no staged
or committed change was made. Build/test/benchmark/Slopwatch were not run: this is a documentation
and reduced-data collection task, not an authored product/test/build change.

## Qualification and next logical action

Both numerical minimums now hold (**413 ≥100 total; 102 ≥100 reported INIT**), but page-capped
coverage is still incomplete. Source commit, a clean attributable deployment interval, executed
package/platform correlation, exact consumed ZIP size and all formal performance compliance remain
**UNVERIFIED** under the unchanged [measurement policy](../architecture/platform-and-delivery.md#measurement-policy).
The actual slower-than-goal prefix observations stand despite those proof gaps.

The next useful step is **offline review of this fixed packet**, not replaying the pilot, silently
continuing pagination, generating cold starts or optimizing an unidentified revision. Any later
collection should address terminal coverage for an explicitly fixed interval and source/package
provenance; it needs a separately bounded authorization, not leftover quota from this run. The
[active procedure](../plans/2026-10-16-production-evidence-collection.md) is not retired or complete.
