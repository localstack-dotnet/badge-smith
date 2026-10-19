# Native first-request attribution: Debian fixtures and official AL2023

**Evidence snapshot: 2026-10-19.** This investigation restored an out-of-process managed provider
measurement and narrowed local Native first-request cost. It did **not** produce a new production
optimization, certify Lambda INIT/memory targets, or justify changing production certificate inputs.
The certificate-directory optimization branch is **unqualified; do not retain as a product change**.

The earlier [template-construction candidate](2026-10-18-template-construction-optimization.md)
remains a separate, uncommitted allocation-only result. Its 1,632 B once-per-process saving is not
reclassified as a startup-latency or Lambda-memory win by these experiments.

## Evidence and source boundary

- [Machine-readable projection](baselines/2026-10-19-native-first-request.json): all 21 observed
  Native processes, including the rejected AL2023 process; study order, clocks, phases, coarse CPU,
  public certificate-input sets, source/artifact hashes, managed samples and review provenance.
- [Invocation CSV](baselines/2026-10-19-native-first-request-invocations.csv): all **1,551** response
  rows, including warmups, health controls and **81 explicitly invalid-cohort rows**. The accepted
  local cohorts contain **20 processes / 1,470 responses / 1,468 upstream calls**. These accounting
  totals are not a pooled statistical population or independent cold-sample count.
- [Offline verification and rerun boundaries](reproducers/2026-10-19-native-first-request/README.md):
  clean-checkout arithmetic reproduction, negative controls and the distinct Native-rerun inputs.

Repository identity is `localstack-dotnet/badge-smith`, authoring HEAD
`40cb349af214a642741faecf757676fa69eefbf8`, tree
`8126d09f4fb71ce4de0d8eececbf477e7bc0424d`. No new production/test/project/policy code was authored
during collection. Protected working inputs contained the preexisting H2 change and documentation.

Managed BDN used an exact **331-file working-source materialization**, not a committed checkout.
Native runs reused the historical **318-file B build**: 315 corresponding current files matched;
three later documentation-only differences and the later source inventory were checked separately.
This was not a fresh Native build of the 331-file snapshot, and this documentation export is not
retroactively a build input. Source manifests, build receipts and launched ELF identities were checked.

| Native artifact | Observed identity |
|---|---|
| ZIP SHA-256 | `f03cce50e59815ae279c36fb5831fa1c68f63b9c7988571ab1fce98ae6de45c7` |
| ZIP bytes | 6,320,534; linux-x64, **not** the deployed ARM64 package |
| ELF SHA-256 | `ed6fdf159b9cc977e0da3bbc172c829da8ef87f9258164e779c9bac353c6c3dd` |
| ELF bytes / machine | 14,055,608 / ELF64 little-endian x86-64 |
| Native runtime-pack version/commit | **Unknown**; managed BDN runtime version is not its identity |

## 1. Managed provider baseline, not Native startup

One out-of-process BDN Short run completed **8 cases / 24 Workload Actual samples / 8 successful
child processes** using the existing provider benchmarks. BDN 0.15.8, SDK 10.0.402 and .NET 10.0.13
x64 RyuJIT were observed. Setup warmed the process and seeded the 304 cache cases. Fake handlers
exclude real DNS/TLS/network/resilience and production cache/storage behavior.

| Provider / response | Corpus | Mean ns/op | 99.9% error half-width ns | Allocated B/op |
|---|---:|---:|---:|---:|
| NuGet / 200 | 3 | 2,600.648177 | 496.033081 | 3,288 |
| NuGet / 304 | 3 | 2,384.177195 | 245.786335 | 3,304 |
| GitHub / 200 | 3 | 3,547.839108 | 635.230622 | 4,304 |
| GitHub / 304 | 3 | 3,424.417146 | 1,193.086135 | 4,040 |
| NuGet / 200 | 32 | 5,484.396027 | 1,001.385285 | 5,928 |
| NuGet / 304 | 32 | 5,115.296501 | 1,875.957922 | 5,152 |
| GitHub / 200 | 32 | 13,897.923635 | 2,690.226156 | 11,672 |
| GitHub / 304 | 32 | 13,305.046341 | 2,708.740806 | 9,832 |

The small three-sample cells have wide intervals; no old/new or cache-ROI claim follows. In
particular, NuGet/3's 304 path allocates **16 B/op more**, not less. ColdCache means a reset fake
response cache after setup, not a cold process or Lambda INIT. Allocation is not retained heap/RSS.

Ignored historical project copies caused duplicate BDN project discovery; changing cwd alone did
not solve it. Manifest-only source materialization did. Cache-only restore used a new empty local
feed and the existing package cache, without changing repository policy. Four Git/SourceLink
warnings and eight optional high-priority denials remained visible. No privileges were changed.
The standard BDN-generated wrapper disables its own analyzers/warnings-as-errors; BadgeSmith
consumer settings remained unchanged. Offline audit configuration is not fresh vulnerability data.

## 2. Local Native baseline and framing screen

Each MAIN process delivered one first-feature request, 16 warmups and 64 retained warm requests.
The separate health sequence delivered health, first NuGet, then four warm controls. Every NuGet
request revalidated upstream: first upstream 200, subsequent 304s; downstream 200/304 is a separate
conditional-response contract. No freshness-cache shortcut was introduced.

| Study | Fixed order | Responses / upstream | Observation and bounded disposition |
|---|---|---:|---|
| Initial baseline | main, health, health, main | 174 / 172 | First NuGet 99.256756 / 49.582527 / 48.461922 / 49.039952 ms; the 99 ms observation remains unexplained and retained. |
| Framing screen | original, coalesced, coalesced, original | 324 / 324 | Event-send→upstream-handler entry occupies **96.785–97.316%** of first-feature elapsed in both arms. Response framing is not the dominant cause in this screen. |
| TLS boundary observer | original, original | 162 / 162 | Server-wrap return→HTTP entry **37.553514 / 37.784054 ms**; ownership of this interval remains unresolved. |

Original framing writes headers/body separately; the coalesced fixture branch combines the first
200 write. Fixed-Date source controls proved byte equality and the planned 2→1 write change.
Live write counts, TLS-record/packet counts and identical live Date/certificate bytes were not
captured. A small timing difference is not a certified framing optimization. No timings are pooled
across these different instruments.

TLS phases are E=event sent, A=accept observed, S/R=server-side wrap start/return, U=HTTP handler
entry, B=complete Runtime response body. The ordered partition E→A→S→R→U→B sums to first-feature
elapsed. A is not kernel-connect completion; R is **not client authentication/validation completion**.
Observer brackets are retained, not subtracted. Signed cross-thread write-return gaps are not causal
cuts. CPU uses actual Native PID/startTicks and 100 Hz process counters: 0/1 tick is not no CPU,
and no stack, constructor, PEM parsing, I/O or chain-search ownership was established.

## 3. Certificate-directory experiments: two different input contracts

The cached Debian inspection observed 150 ordinary DER inputs in both its default bundle and
directory. The directory had **301 entries / 151 usable file identities**, including bundle and
individual-certificate paths. Alias/inode accounting and certificate-input equality are observations
of that image, not actual Native store/file-open/parse-count traces.

### Fixture-only file: input-changing diagnostic

Both arms explicitly used the fixture CA file; only SSL_CERT_DIR default-absent versus an owned
valid empty directory varied. Removing the OS directory also changed the ordinary certificate
inputs. Consequently the apparent timing difference is not a security-preserving product proposal.

| D–E–E–D process | First feature ms | R→U ms | Post81 RSS KiB |
|---|---:|---:|---:|
| default1 | 100.503701 | 89.167433 | 29,988 |
| empty1 | 12.167772 | 0.812880 | 27,600 |
| empty2 | 11.657909 | 0.656728 | 27,852 |
| default2 | 48.825669 | 38.220479 | 29,968 |

### Common file: ordinary-input-union-preserving Debian diagnostic

Before timing, both arms constructed the same file containing **OS bundle F ∪ fixture CA C**.
The four processes used the same new fixture CA and exactly the same **151 ordinary inputs and
common-file bytes**. The pinned directory reference D equaled F, so `(F ∪ C) ∪ D = F ∪ C`.
Only SSL_CERT_DIR default-absent versus owned-empty varied between arms. Both arms' common file
was an intentional change from the earlier fixture-only study, not a matched old/new instrument.

| D–E–E–D process | First feature ms | R→U ms | Post81 RSS KiB |
|---|---:|---:|---:|
| default1 | 66.013434 | 54.348728 | 30,512 |
| empty1 | 26.136158 | 14.642543 | 29,096 |
| empty2 | 27.004562 | 15.079181 | 29,076 |
| default2 | 64.366614 | 52.083743 | 30,388 |

All 324 responses/upstream calls passed in each four-process study; no replacement or favorable
latency exclusion. The common-file reduction establishes **ordinary certificate input-set equality
only**, not Native-loaded Root/Intermediate collections, auxiliary policy, user-store effects or
production security equivalence. The directory was a pinned inventory reference, not re-enumerated
by Native. Synthetic certificate signature-byte mutations in controls were parse tests, not chain
validation. Observed nonlink/owned-file checks do not prove resistance to later hostile path swaps.

## 4. Official AL2023: target layout differs, default-only execution works

After explicit image-acquisition approval, the official `public.ecr.aws/lambda/provided:al2023`
linux/amd64 image resolved to
`sha256:f42e771ae36a9d34ab2e594ef5860fff8ace80aff5ad7c75fca28c06cbc7c6d5`.
Observed OS was Amazon Linux 2023.12.20260914, not a claim about every deployed Lambda filesystem.

- `/etc/pki/tls/cert.pem` and `certs/ca-bundle.crt` resolve to the **same ordinary bundle inode**.
  That bundle has 143 distinct ordinary DER inputs, SHA-256
  `9c927a9eefb9117fce0228bc5e4c859e9342ef9e6be8fd44246273c54ce69abb`.
- `/etc/pki/tls/certs` has **two entries**, not the Debian 301-entry layout. The other entry,
  `ca-bundle.trust.crt`, resolves to a distinct 539,353-byte bundle with **310 TRUSTED CERTIFICATE
  blocks**, SHA-256 `7222db1552b1487c2d995c7a187e5ba57c876e338c02cefad81179324929dc36`.
  Its auxiliary policy was not equated to the ordinary bundle or proven as Native-loaded policy.

No Python/OpenSSL CLI/package was installed in the minimal official runtime. A separate cached
Python fixture hosted mock Runtime API and loopback TLS; the Native target shared only that owned
fixture's network/PID namespaces. The target executed the same ELF directly against its official
image libraries. Containers were nonroot, network-isolated, cap-drop ALL, no-new-privileges,
read-only root, with minimal owned mounts and no host Docker socket/home/repository/credentials.

**Two default-directory processes** completed with the same fixture CA and a common file containing
official ordinary inputs plus that CA. SSL_CERT_DIR remained absent; no empty-directory AL2023
comparison was performed. This fixture-specific SSL_CERT_FILE is not unmodified production trust.

| Accepted V4 observation | default1 | default2 |
|---|---:|---:|
| First event→complete body ms | 35.138271 | 38.579648 |
| Warm64 median ms | 0.615090 | 0.5854145 |
| Post81 Native RSS/HWM KiB | 29,100 | 29,124 |
| Server-wrap return→HTTP entry ms | 25.171914 | 27.681326 |
| Transport-inclusive Ready ms (**not INIT**) | 226.415044 | 209.433616 |

Both MAIN81 sequences passed: **162 responses / 162 upstream calls**, upstream 2×200+160×304,
downstream 82×200+80×304. Native PID/executable/startTicks/namespace/security checks succeeded.
Four exact owned container IDs/names and all three new key paths were absent after cleanup.
Fresh raw-data review reproduced all arithmetic. Final official-cohort model-family independence
is unverified. Native logs were sampled **before target stop only**, not after stop.

The Ready clock starts before host Docker create/inspect/start and is explicitly transport-inclusive.
Debian Ready instead starts immediately before Native Popen in an already-running container.
Neither is CloudWatch INIT; they must not be subtracted as a cross-platform startup benefit.
The first-event clock is preserved, but OS/library/fixture differences prevent a causal Debian→AWS
speedup claim. Post81 RSS/HWM is not Lambda `Max Memory Used` or a memory-SLO result.

## Failures and corrections retained, not relabeled

| Stage | What failed / disposition |
|---|---|
| BDN discovery | Ignored duplicate projects; a cwd-only workaround was insufficient. Clean manifest materialization solved discovery; no InProcess surrogate is presented as the final Short run. |
| Baseline/framing preflight | A directory-iteration-order assumption and a Docker descriptor-template mismatch were corrected in checkers; raw measurements were not changed or rerun. The 99 ms baseline observation stays. |
| Trust helper V1 | A sampled final-link condition returned metadata instead of rejecting. Repaired fail-closed controls preceded V2 Native collection; no V1 Native study was run. |
| Common-bundle preparation | Behavioral RED/GREEN controls cover missing build/integration and corrupted output; parsing synthetic X509 inputs did not prove chain validity. |
| AL2023 source inspection | Image was initially absent; no unauthorized pull occurred. After approval it was downloaded once. `find` was missing; shell globs completed metadata inspection without package installation. |
| Docker environment control | Bare unset markers remain in inspect but are absent from exported process env; inspect order varies. A shell-variable test was insufficient. Exact unordered raw entries plus duplicate-key rejection replaced order-sensitive equality. Initial image-ID conflation suspicion was not supported by actual daemon receipts. |
| AL2023 V2 Native | One 81-response process was **INVALID**: fixture closed Runtime API while Native waited for `/next`, causing `HttpRequestException/ResponseEnded`. Second process was not attempted; all 81 invalid rows are exported separately. No exception filtering or favorable replacement. |
| AL2023 V3 review | Post-measurement close rejection could leave final `PASS`. Review blocked execution; **zero V3 Native processes**. |
| AL2023 V4 | Native is stopped before fixture closes; every caught close failure sets FAIL. Fixture-level timeout/invalid-close controls prove nonzero/FAIL and driver rejection. Fresh 20 adapter +18 shared +8 source-guard controls passed before the distinct two-process cohort. |

The original private receipts, helpers, failed controls and seals remain immutable. The public
projection hashes source reports/results but does not pretend those original files are in Git.
It omits raw logs, payloads, headers, private keys, credential inputs, developer paths and local
agent/model configuration. A hash verifies identity only when the original material is available.

## Source references and unresolved questions

Tagged [.NET v10.0.13 handshake source](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Net.Security/src/System/Net/Security/SslStream.IO.cs#L356-L404)
permits server-side protocol completion before client certificate validation finishes.
The [system-store provider](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/X509Certificates/OpenSslCachedSystemStoreProvider.cs#L87-L290)
and [root-path shim](https://github.com/dotnet/runtime/blob/v10.0.13/src/native/libs/System.Security.Cryptography.Native/pal_x509_root.c#L9-L42)
treat file/directory inputs separately; successful inode aliases can deduplicate before parsing,
different files' certificate objects after parsing. These are **reference-source facts**, not exact
Native B runtime-pack attestation or observations of file opens/chain work.

The existing [local-performance stack](../../build/BadgeSmith.CDK.LocalPerformance/README.md)
offers API Gateway and a Function URL fallback. A lone local runtime does not implement Lambda
service scheduling. Standard Lambda's one-invoke-per-environment rule does not make API Gateway
a prerequisite for scaling direct Invoke calls. No LocalStack deployment/concurrency measurement
was performed here; [managed Aspire contract tests](../../tests/BadgeSmith.Api.Tests/README.md#contract-tests)
are separate from published Native execution. No new deployment authority follows from these notes.

## Disposition and next work

1. **Close the certificate-directory product-optimization branch as unqualified.** Keep the
   diagnostic and rejected alternatives; do not ship empty-directory or trust-policy changes.
2. Retain the official AL2023 default-only result as a **local first-request baseline**, not a new
   implementation or a production improvement. No new build/deploy/AWS traffic was performed.
3. The next performance task is application-startup attribution and one falsifiable code candidate,
   using existing data before more harness work. It has not run as part of this documentation change.
4. Production qualification stays with the existing [available-window evidence](2026-10-18-post-deploy-complete-window.md)
   and [architecture goals](../architecture/platform-and-delivery.md#performance-goals). No expired
   collector/timer is rearmed, no synthetic cold traffic is authorized, and no SLO is declared met.

This packet was assembled offline from retained artifacts. Numeric consistency, privacy, links and
preexisting-file preservation are checked separately from the historical runtime gates. No new
application tests/Slopwatch/Native/BDN/LocalStack/AWS execution is implied by offline verification.
