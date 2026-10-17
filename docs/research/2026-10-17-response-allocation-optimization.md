# Response allocation optimization — local A/B

**Implemented, with a confirmed managed-allocation reduction; the later full-suite blocker is resolved.**
The change removes temporary validator strings/arrays and capturing header delegates from
`ResponseHelper`. It does **not** establish lower production INIT, lower resident memory, or
world-leading Lambda performance. No deployment or production traffic occurred.

**Follow-up:** a [test-owned OTLP receiver](2026-10-17-functional-telemetry-fixture.md) resolved the
functional-suite blocker; the full API project passed **579/579 twice**, with no skips or timeout
increase. The original allocation/native measurements and historical timeout receipts below remain
unchanged. The fixture fix adds no production-code change.

Base: `1044b6d6e7160fc8a3dedcd3154337a4dfb4bd9f`; candidate is uncommitted.
The [evidence packet](baselines/2026-10-17-response-allocation.json) contains both helper sources,
their hashes, benchmark samples, native artifact identities, launch measurements and task-scoped
reproducer sources. The [native invocation CSV](baselines/2026-10-17-response-allocation-native-invocations.csv)
retains all 2,048 measured warmed invocations.

## Change and preserved contracts

- Parse `If-None-Match` with spans instead of `Trim`/`Split`/substring/interpolation allocations.
- Pass already-created headers directly into response assembly rather than capturing them in a
  new delegate. Header dictionaries remain per response, not globally shared.
- Preserve JSON bytes, quoted uppercase SHA-256 ETags, case-insensitive matching, weak/unquoted
  tag compatibility, whitespace/list handling, whole-field wildcard behavior, 200/304 bodies,
  cache policies, last-modified formatting, redirect validation and default content type.
- Do not change routes, HMAC, upstream revalidation, freshness, dependencies, compilation policy,
  or serialization registrations.

The seam is the existing response-helper contract. Release allocation tests first failed on the
old behavior, then passed with the changes: weak-list **1,696 > 704 B**, cached response
**704 > 584 B**, and both redirects **352 > 264 B**. Existing ceilings were lowered, never raised.
Eighteen additional literal validator cases protect response compatibility.

## Managed allocation result

Same-process BenchmarkDotNet 0.15.8, Short job, one launch, three warmups and three actual
iterations per case; .NET SDK 10.0.402 / runtime 10.0.13, Ubuntu 26.04.1 x64.
The baseline is the original helper, with only class/namespace renaming and two additional
namespace imports so both implementations coexist. Setup compares status, body and all headers.

| Response scenario | Before B/op | After B/op | Reduction |
| --- | ---: | ---: | ---: |
| Badge, no validator | 704 | 584 | 17.0% |
| Badge, quoted match | 736 | 584 | 20.7% |
| Badge, weak match in list | 1,696 | 584 | 65.6% |
| Badge, nonmatching list | 1,056 | 584 | 44.7% |
| Cached redirect | 352 | 264 | 25.0% |

These counts agree with an independent warmed per-thread allocation probe and regression tests.
They measure **managed bytes allocated per response**, not Lambda `Max Memory Used`.

Timing is weaker evidence: no-validator mean was **1,252.7 → 1,282.3 ns**, quoted-match
**1,314.1 → 1,314.0 ns**, and weak-list **1,732.7 → 1,324.4 ns**. Three samples and wide
99.9% intervals do not establish a general speedup; the no-validator point estimate increased.

Two generated-project Dry attempts executed zero cases because the temporary snapshot/harness
and repository benchmark assemblies had duplicate project identities. The explicitly selected
in-process Dry and Short runs each executed all ten cases. No build/analyzer policy was weakened.
This is JIT microbenchmark evidence, not Native AOT timing or process-isolated precision evidence.

## Published Native AOT execution

Both ZIPs were built through `tools/badgesmith.cs lambda build --target zip --rid linux-x64`,
using separate frozen tracked-source contexts differing only in `ResponseHelper.cs`. Both used
the same SDK image digest and production telemetry/LocalStack exclusion flags. No AOT/trim
warnings were observed.

Each actual ELF executable ran eight times on the local x64 host, alternating AB/BA order.
A loopback Runtime API fixture drove the real NuGet route against a local HTTPS upstream;
production `Live` mode and certificate validation remained enabled. A fixture-only certificate
was trusted through `SSL_CERT_FILE` for those child processes, without changing machine trust.
No AWS credentials or external service were needed.

All **2,320 invocations** passed status/body/ETag/cache/last-modified/content-type assertions.
Each invocation revalidated upstream: every launch had one upstream 200 and 144 upstream 304s.
After first invocation and warmup exclusions, 256 observations remain per scenario per revision.

| Local native metric | Before median | After median |
| --- | ---: | ---: |
| Process launch → first Runtime API GET, n=8 each | 15.814 ms | 14.992 ms |
| First invocation Runtime API round trip, n=8 each | 35.456 ms | 34.457 ms |
| Warm no-validator round trip, n=256 each | 0.593 ms | 0.592 ms |
| Warm weak-list round trip, n=256 each | 0.598 ms | 0.596 ms |
| Native process RSS high-water, n=8 each | 34,580 KiB | 34,814 KiB |

**No demonstrated end-to-end speed or resident-memory benefit.** Memory's median rose 234 KiB;
warm timing was effectively flat at this instrument's scale. Page cache, scheduling, transport,
upstream fixture, logging and within-process correlation limit causal interpretation. The small
launch sample does not support a production tail claim. Native allocation bytes were not measured.

The x64 ZIP grew **6,319,260 → 6,320,515 bytes** (+1,255); executable size remained
**14,055,608 bytes**. These are not production ARM64 size results. Local process readiness is not
CloudWatch INIT, and Linux `VmHWM` in KiB is not CloudWatch memory in MB. Neither is an AWS goal pass.

## Original verification and blocker (before fixture follow-up)

- Before: **523/523 unit tests**, zero skipped. After: **542/542**, zero skipped; Release builds
  retain the analyzer wall. Slopwatch: **zero issues**.
- Isolated `RoutingContractTests.Responses_Should_Carry_Cors_Header`: passed against both the
  frozen original source and candidate.
- **Unfiltered suite did not complete** within 240 s, then 360 s. The runs reported 60-second
  HTTP timeouts in `RoutingContractTests.Responses_Should_Carry_Cors_Header` and
  `PackageBadgeContractTests.NuGet_Badge_Should_Return_Matching_Version_When_Version_Range_Is_Valid`,
  respectively. A separate functional-only run also reached its 180-second bound without a
  completed result. None is counted as passing; no timeout, skip or assertion was weakened.
- The timeout cause remains unresolved. Isolated CORS success is not proof that the combined-suite
  failure is pre-existing or infrastructure-only. No production fix was guessed from that failure.
- Earlier evidence files are preserved. No commit, push, PR, deployment, AWS API call, package change,
  policy change, or production traffic was made for this optimization.

That validation blocker is now resolved by the linked fixture follow-up. Keep this as an allocation
optimization, not a claimed solution to the [production INIT/memory goals](../architecture/platform-and-delivery.md#performance-goals).
The earlier [413-row production observation](2026-10-17-production-evidence-window.md) concerns an
unattributed deployed package and is not an A/B baseline for this source change.

## Reproduction

The task-scoped workspace is `artifacts/response-optimization-20261017/` (ignored, not a permanent
CLI feature). Its source contexts, build logs, probes and raw BDN/native reports remain local.
The durable JSON embeds the helper and harness sources so their content is not dependent on those
ignored files surviving. No certificate private key, personal path or raw application log is embedded.

Use a fresh ignored workspace with the same layout, restore the embedded files, and derive
`BaselineResponseHelper.cs` from `source.beforeUtf8` using the recorded name/import-only changes.
Build both source contexts through the existing CLI with distinct `--context` and `--out` paths;
never add `--push` or `--clean` against an existing output. From the harness directory:

```bash
dotnet run -c Release -- --probe
dotnet run -c Release --no-build -- --filter '*ResponseComparison*' --job Dry --inProcess --noOverwrite
dotnet run -c Release --no-build -- --filter '*ResponseComparison*' --job Short --inProcess --noOverwrite --exporters json
```

For native reproduction, generate a one-day test-only self-signed RSA certificate with
`subjectAltName=IP:127.0.0.1` as `fixture-cert.pem` / `fixture-key.pem` in that workspace, then run
the embedded `native_compare.py`. Set `TMPDIR` to a writable task-owned location if `/tmp` quota is
exhausted. The script extracts the two ZIPs and runs only local fixture traffic. Compare raw launch
rows and invocation CSV using exact integer nanoseconds; do not pool them with production records.
