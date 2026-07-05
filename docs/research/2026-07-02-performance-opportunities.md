# Performance Opportunities — Cold Start & Memory Footprint

Date: 2026-07-02

Goal: squeeze bootstrap latency and memory footprint of the Native AOT Lambda
(`src/BadgeSmith.Api`). Every claim below is either a production measurement or a
direct source-code observation (file:line as of `671e40e`). Estimates are marked as
estimates; per AGENTS.md, no change ships without before/after measurement.

## Measured baseline (production, 2026-07-02)

`badge-smith-function`, `provided.al2023`, arm64, 512 MB. Sampled ~40 REPORT lines from
the last 30 days of CloudWatch logs:

| Metric | Value |
| --- | --- |
| Init Duration | ~105–140 ms |
| Warm, cache-hit invoke | 1–3 ms |
| Cold invoke Duration (after init) | 165–680 ms typical; outliers 1.3–1.7 s |
| Max Memory Used | 32–49 MB (of 512 MB) |
| Artifact | `bootstrap` 13.9 MB uncompressed / 6.3 MB zip (local x64 build) |

Notes: the INIT phase is billed (AWS change, Aug 2025) — init ms are billed ms. Cold
starts are frequent at this traffic level, so the cold path dominates user-visible
latency for cache-missing badges.

## Traffic, concurrency, and edge profile (measured 2026-07-02)

Lambda, 30-day window (CloudWatch metrics + Logs Insights over all REPORT lines):

| Metric | Value |
| --- | --- |
| Total invocations | 3,213 (≈107/day ≈ **0.07 RPM**) |
| Typical day / spike days | 10–40 / 330–455 invocations |
| Max concurrency | **3 on most days**; 12–33 on spike days |
| Cold-start ratio | 517 / 3,213 = **16%** |
| Avg init / duration p50 / p95 / max | 116 ms / 1.2 ms / 291 ms / 1,655 ms |

The recurring max-concurrency of exactly 3 confirms the burst shape: a README render
makes shields.io fetch ~3 badge URLs in parallel; on a CloudFront miss all 3 hit Lambda
simultaneously, and since one execution environment serves one request at a time, that
is 3 environments = **3 parallel cold starts**. They do not queue behind each other —
each pays its own full cold penalty independently.

CloudFront distribution `E2I09H2SLUEGLF` (api.localstackfor.net), same window:

| Metric | Value |
| --- | --- |
| CloudFront requests | 5,683 |
| Forwarded to Lambda | 3,213 → edge absorbs only **~43%** |
| 4xx rate | **52%** (bot probes + nonexistent repo/branch badges) |
| CacheHitRate metric | unavailable (additional metrics not enabled) |

Key insight: `ResponseHelper.NotFound`/`BadRequest` send **no Cache-Control**, and the
cache policy is origin-controlled — so 404s are never cached at the edge. Half the
traffic is 4xx, and every bot probe / broken badge URL wakes the Lambda. The low p50
(1.2 ms) is largely these cheap 404s.

## Ranked opportunities

### 0. Serve misses at the edge (HIGH — removes cold starts instead of speeding them up)

Infrastructure-side, cheaper than any code change, driven by the edge measurements
above:

- **Cache negative responses**: add a short `Cache-Control` (`s-maxage=60–300`) to
  404/400 badge responses in `ResponseHelper`. With a 52% 4xx rate, this alone removes
  a large share of Lambda invocations.
- **Raise badge TTL**: `s-maxage=600` is conservative for badge data; 1800–3600 (with
  the existing `stale-while-revalidate`) pushes edge absorption up from ~43% and makes
  the "3 parallel cold starts per README render" scenario rare.
- Optional: enable CloudFront additional metrics to get a real `CacheHitRate` series
  for before/after validation.

### 1. Move first-request work into the INIT phase (HIGH — cold-start latency)

Init only builds `ApiRouter` (`Program.cs:15`). Everything else is `Lazy<T>` and runs
inside the **first billed invoke**: AWS SDK client construction + credential chain +
endpoint resolution, `HttpClient`/`SocketsHttpHandler` creation, `MemoryCache`,
`LoggerFactory`. That is exactly the measured 165–680 ms cold-invoke gap (warm is
1–3 ms).

The INIT phase runs with a full-vCPU burst regardless of the memory setting, while a
512 MB invoke gets ~0.29 vCPU — the same work runs several times faster in init. Add an
explicit warm-up in `Main` before `LambdaBootstrap.RunAsync`: touch the
`ApplicationRegistry` graph, force AWS credential resolution (e.g., a cheap signed call
or `ResolveIdentityAsync`), optionally pre-open the DynamoDB TLS connection.

Validate: REPORT init/cold-duration distribution before vs after.

### 2. ILC / publish settings (HIGH — binary size, MED — init time)

No ILC or GC knobs are set anywhere (verified). Current explicit `TrimMode=partial`
(`BadgeSmith.Api.csproj:17`) roots all unannotated assemblies — including the AWS SDK —
into the AOT image. Candidates, each measured individually via publish-size diff:

- `TrimMode=full` — likely the single biggest size lever. Trim/AOT warnings are
  blocking (KNOWN_ISSUES); AWS SDK v4 claims trim-compat, verify at publish.
- `IlcGenerateMstatFile=true` + sizoscope — measure what fills the 13.9 MB before
  guessing further.
- `StackTraceSupport=false` — real size win, real diagnostics cost; decide consciously.
- `UseSystemResourceKeys=true` — strips framework exception-message resources.
- `OptimizationPreference=Size` — try; the service is I/O-bound.
- Feature switches for prod (telemetry-off) builds: `MetricsSupport=false`,
  `HttpActivityPropagationSupport=false`.

Smaller image → less to load/relocate → faster INIT; measure both size and init.

### 3. Cache outcomes, not just payloads (MED — warm latency and allocations)

Package services cache the raw upstream JSON but recompute everything per badge hit:
deserialize + `NuGetVersion.TryParse` over **every** version (500+ for popular
packages) + range filtering, on every request including cache hits
(`NuGetPackageService.cs:93-108`, `GitHubPackageService.cs:106-122`,
`NuGetVersionService.ParseAndFilterVersions`). Cache the final result keyed by
`(packageId, versionRange, includePrerelease)` with the same TTL; the cache-hit path
becomes near-allocation-free. Validate: BenchmarkDotNet with a 500-version corpus,
`Allocated` column.

### 4. Lambda memory rightsizing (MED — cost/latency tradeoff, measure first)

Peak memory is 49 MB of 512 MB. Two directions: 256 MB halves GB-s cost but halves CPU
(cold path slows); 1024 MB doubles CPU (cold TLS/init-heavy work speeds up) at double
rate. Run AWS Lambda Power Tuning before touching `MemorySize`
(`BadgeSmithFunctionConstruct.cs:39`); latency-first → likely 1024 MB, cost-first →
256 MB. Traffic is small enough that this is a latency decision, not a cost one.

### 5. Request-path allocation cleanup (LOW-MED each; mostly free wins)

- `ApiRouter.cs:54` — `ToImmutableDictionary()` per request (builder + AVL nodes,
  slower lookups). A `Dictionary(capacity, OrdinalIgnoreCase)` or a fixed-slot struct
  (≤5 params today) is cheaper. The elaborate span-based `RouteValues` is currently
  nullified by this materialization.
- `RouteResolver.cs:18` — param buffer heap-allocated per request. An
  `[InlineArray]` buffer sized from the route table at startup removes the allocation
  **and** resolves GitHub issue #1 (buffer-overflow guard) in one change.
- `RouteResolver.cs:23` — `Normalize(d.Method)` per descriptor per request; precompute
  normalized methods in the resolver constructor.
- `ResponseHelper` — `Func<Dictionary>` closures invoked immediately (pure indirection
  + closure allocs); pass dictionaries directly. Do NOT share static header instances:
  `CorsHandler.ApplyResponseHeaders` mutates response headers.
- `Program.cs:36` — `CreateLogger<Program>()` per request; hoist to a static field.

### 6. HMAC path: drop the double hex round-trip (LOW perf, includes a correctness fix)

`HmacAuthenticationService.cs:127-143`: computed HMAC → hex string → lowercased →
`Convert.FromHexString` again; provided signature also hex-decoded. Replace with static
`HMACSHA256.HashData(key, payload, stackalloc 32B)` + `FixedTimeEquals` on raw bytes.
While there: `Convert.FromHexString(providedHash)` **throws `FormatException` on
malformed input today** (no catch — verified), turning a garbage `X-Signature` header
into a 500 instead of a 401. Use the `OperationStatus`/Try overload.

### 7. Robustness items found during this pass (not perf, tracked here)

- No `IsBase64Encoded` handling anywhere (verified). If API Gateway ever
  base64-encodes a POST body (content-type/encoding dependent), HMAC validation and
  JSON parsing silently fail. Cheap guard in the ingestion handler.
- Request CTS uses a compile-time constant timeout (`Settings.LambdaTimeout`) rather
  than `ILambdaContext.RemainingTime`.

## Explicitly not worth it

- **Hand-rolled SIMD**: the hot primitives are already vectorized/HW-accelerated in the
  BCL — `IndexOf('/')` and ordinal-ignore-case compares (SpanHelpers), hex conversion,
  SHA-256/HMAC (OS crypto, ARMv8 crypto extensions). Payloads are ~200 B JSON; there is
  no loop in this codebase long enough for custom vectorization to beat the BCL.
- Swapping SHA-256 ETags for XxHash (~1 µs on 200 B is already noise).
- Replacing NuGet.Versioning with hand-rolled semver (range/prerelease correctness risk).
- Pooling ~200 B response bodies; `IlcInstructionSet` tuning on Graviton.
- Dropping `Microsoft.Extensions.Caching.Memory` for a hand-rolled TTL cache — revisit
  only if the item-2 mstat data shows it paying meaningful size; it has no background
  timer (expiration piggybacks on access), so there is no freeze/thaw concern.

## Honest assessment (2026-07-02)

Question asked: "is this the fastest, lowest-memory .NET Lambda that can be built?"

- **Architecture: A.** AOT + RuntimeSupport (no ASP.NET host), arm64, source-gen-only
  JSON, no DI/config framework, conditional compilation stripping telemetry from prod.
  Reference points: a typical managed `dotnet8` + ASP.NET-hosted Lambda inits at
  400–900 ms and idles at 90–150 MB; this one is 116 ms / 33–49 MB.
- **Memory: A-.** 33–49 MB is near the practical floor for AOT + AWS SDK v4 + two
  HttpClient stacks. Chasing sub-25 MB is vanity — billing is by configured memory.
- **Cold-start execution: B-.** Effective cold start is ~300–800 ms, not 116 ms,
  because lazy-init defers AWS/TLS/HttpClient setup into the first billed invoke at
  ~0.29 vCPU. The remaining gap to the floor is items 0–2 above, all cheap.
- Deliberately out of bounds: raw runtime-API loop + hand-parsed event JSON (last ~2%,
  not worth the maintainability cost).

## Decisions (2026-07-02)

Agreed direction, pending "go" for implementation:

1. **Do**: negative-response caching + badge TTL increase (item 0); INIT-phase warm-up
   (item 1); `TrimMode=full` + ILC/feature switches with per-knob size measurement
   (item 2). Result caching (item 3) and request-path cleanup (item 5) ride along.
2. **Rejected — provisioned concurrency**: eliminates colds but is always-on paid
   capacity; overkill at 0.07 RPM.
3. **Rejected — keep-warm ping (EventBridge)**: keeps only 1 environment warm; the
   3-parallel-fetch burst still colds the other 2. Limited value once item 0 lands.
4. **N/A — SnapStart**: not available for `provided.al2023` custom runtime.
5. **Not doing — hand-rolled SIMD / raw runtime loop / NuGet.Versioning replacement**:
   see "Explicitly not worth it".

## Measurement plan

1. `IlcGenerateMstatFile` + sizoscope snapshot before any csproj change.
2. Publish-size diff per ILC knob; zero-AOT-warning bar holds.
3. CloudWatch REPORT init + cold-duration distribution before/after (baseline above).
4. BenchmarkDotNet: `ParseAndFilterVersions` (500-version corpus) and the routing
   pipeline `Allocated` column. Fix the vestigial `_Current`/`_Optimized` benchmark
   pairs first (they currently execute identical code).
5. k6 end-to-end after wiring `K6_API_URL` support.

## Process note

A marketplace perf-scan agent was dispatched for a second opinion; its report cited
nonexistent files and fabricated code (zero tool calls recorded) and was discarded.
All findings above come from direct source reading and CloudWatch measurements.
