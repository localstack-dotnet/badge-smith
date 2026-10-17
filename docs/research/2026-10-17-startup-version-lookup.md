# Startup allocation — defer unused application-version lookup

**Implemented: unused application-version discovery no longer runs during production-shaped startup.**
The focused Native AOT diagnostic observed **78,272 B → 0 B** for that operation. The full API
project passed **579/579**, and an uninstrumented local native A/B passed **2,320 invocations**.
This is not proof of production INIT/RSS compliance or a general latency improvement.

Base remains `1044b6d6e7160fc8a3dedcd3154337a4dfb4bd9f`; changes are uncommitted. This increment
follows the [response-allocation optimization](2026-10-17-response-allocation-optimization.md) and
its [test-fixture repair](2026-10-17-functional-telemetry-fixture.md).

## Screened candidates

First-use managed probes screened two tempting changes before touching production code:

- Constructing `MemoryCache` allocated about **3.8 KB**. Making it lazy could move work from
  registry initialization to the first cache-using request; no net cold-request benefit was proven.
- Changing both HTTP handler lazies from preconstructed values to factories saved **104 B** on
  the first NuGet-only use but added **192 B** over the first use of both providers. Warm client
  construction stayed at **1,360 B**. This prototype was rejected, not applied.

Those are JIT first-use allocation observations, not Native AOT or AWS measurements. The probe's
initial resource-ownership errors and later recursive snapshot-glob build error were corrected in
the ignored measurement project; no product analyzer or build policy was weakened.

## Attribution and change

A snapshot-only startup probe was published through the existing Lambda CLI, using the production
telemetry/LocalStack exclusion flags. It measured nested cumulative managed-allocation windows:
router initialization about **2.23 MB**, including about **2.21 MB** during logger construction.
These are allocation windows, not live heap, retained objects or CloudWatch memory.

Further attribution found that reading `Settings.DotNetEnvironment` also initialized this field:

```csharp
private static readonly string DefaultAppVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
```

`ApplicationVersion` is used by `Program.Telemetry.cs`, which is absent from the production-shaped
publish. The eager field still performed its reflection/version work when unrelated settings were
read. The only new production change makes that private default an expression-bodied property.
The existing `ApplicationVersion` getter still applies `APP_VERSION` first and caches its first
value. Metadata is computed only when that fallback is actually requested.

This avoids unnecessary work rather than moving it to the first production NuGet request. It does
not rewrite the logger, change logging output/filtering/scopes, add a lazy wrapper, change cache
semantics or alter compilation flags. Remaining logger startup cost is not claimed eliminated.

## Verification

- Five original instrumented native launches performed the version lookup; a no-lookup assertion
  failed against those observations. Five deferred-version launches recorded **zero** lookup
  allocation. This is a task-scoped diagnostic regression, not a new permanent AOT test tier.
- Four fresh-process version contracts passed before and after: unset/default, explicit override,
  empty string, and a non-semver label. Each also verified caching after the first read. Expected
  default version came from the generated assembly-version attribute, not the getter under test.
- The full Release API project passed **579 / 0 failed / 0 skipped**. Slopwatch reported **0 issues**.
- A production-flags x64 candidate ZIP was published through `tools/badgesmith.cs lambda build`.
  No compiler/analyzer/AOT diagnostics were observed. Production source differed from the prior
  native candidate only in `Settings.cs`; shared source and build/package configuration matched.
- The uninstrumented before/after binaries ran eight times each, alternating AB/BA order, through
  the same local Runtime API and HTTPS NuGet fixture. All **2,320** response/revalidation checks
  passed; **2,048** warmed observations remain after first-call/warmup exclusions. Fixture keys
  were removed, and machine certificate trust was not modified.

## Uninstrumented local observations

| Metric | Before median | After median |
| --- | ---: | ---: |
| Process launch → first Runtime API GET, n=8 each | 14.602 ms | 14.196 ms |
| First invocation round trip, n=8 each | 39.842 ms | 34.712 ms |
| Process RSS high-water, n=8 each | 34,840 KiB | 34,570 KiB |
| Warm no-validator round trip, n=256 each | 0.587 ms | 0.602 ms |
| x64 ZIP byte length | 6,320,515 | 6,320,306 |
| ELF byte length | 14,055,608 | 14,055,608 |

These are descriptive results from a small, correlated local sample. Warm timing's point estimate
increased. Scheduling, TLS, fixture overhead and OS caches prevent declaring a general speedup or
significant resident-memory reduction. Readiness is not CloudWatch INIT; RSS in KiB is not Lambda
`Max Memory Used`. The diagnostic binaries are distinct from this uninstrumented A/B.

The [evidence packet](baselines/2026-10-17-startup-version-lookup.json) retains screening rows,
diagnostic rows, version contracts, artifact/source hashes and native launch statistics. The
[invocation CSV](baselines/2026-10-17-startup-version-native-invocations.csv) supports recomputation.
Task-scoped source/probes/logs remain in ignored `artifacts/startup-lifetime-20261017/` and
`artifacts/startup-version-ab-20261017/`, not RAM-backed `/tmp`.

No commit, push, hosted run, deployment, production traffic, package change, warning suppression or
timeout increase was made. Production source/interval attribution, ARM64 execution and the
[production performance goals](../architecture/platform-and-delivery.md#performance-goals) remain
separate and unverified. The remaining logger allocation window requires further attribution;
replacing its implementation is not an outcome of this increment.
