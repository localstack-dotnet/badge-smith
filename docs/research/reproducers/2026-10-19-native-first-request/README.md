# Offline evidence replay and Native rerun boundary

**Evidence snapshot: 2026-10-19.** Companion to the
[dated investigation](../../2026-10-19-native-first-request-attribution.md).

## What a clean checkout can reproduce

Run with Python's standard library only:

```bash
python3 -I -B docs/research/reproducers/2026-10-19-native-first-request/verify.py
```

This command reads the [JSON](../../baselines/2026-10-19-native-first-request.json) and
[CSV](../../baselines/2026-10-19-native-first-request-invocations.csv), verifies their schema/hash,
recomputes all process clocks, workload accounting, warm medians, phase partitions, CPU identity,
ordinary-input unions and managed sample statistics, and rejects deliberate in-memory corruptions.
It starts **no process, listener, container, benchmark, build, network call or deployment** and
writes no files. Contract flags are recorded runtime-oracle outputs; absent response bytes cannot
be independently replayed as HTTP/body/header assertions by this offline checker.

The public CSV contains every collected response, including the failed V2 cohort. Qualification is
per study/process, not inferred from the fixture's internal PASS. No accepted statistic pools the
invalid cohort or combines different instruments/platforms. Mono clocks/PID/startTicks are run
observations, not reusable identities or new machine requirements.

## Managed BDN rerun: existing repository instrument

In a source boundary containing just one copy of the benchmark project, use the existing runner:

```bash
dotnet run --project tests/BadgeSmith.Api.Performance.Tests -c Release -- --filter '*ProviderBenchmarks*' --job Short --noOverwrite --exporters fulljson --keepFiles --artifacts artifacts/provider-short
```

This is a **new managed component run**, not an exact replay of the historical numbers or a Native
cold-start experiment. Follow the [performance guide](../../../../tests/BadgeSmith.Api.Tests/README.md#performance-harnesses)
for runner limits. The historical run used an allowlisted 331-file materialization because ignored
copies caused duplicate-project discovery; changing cwd alone was insufficient. Restore/build must
obey current repository configuration. The historical cache-only restore and retained generated
project/restore evidence are not configuration changes requested by this example. Inspect actual
case count, generated build/child exits, samples and reports; exit0 alone is insufficient.

## Native rerun: protocol recipe, not a shipped launch tool

The JSON publishes artifact/report identities and complete numeric reductions, **not** the old ELF,
complete frozen source/build archive, raw private command receipts, test keys or as-executed
diagnostic controllers. `verify.py` is not a Native runner. The earlier
[public Runtime API appendix](../2026-10-18-template-construction/README.md) explains the underlying
closed fixture, but does not contain the 19 October observer, common-bundle and two-container
extensions. An exact fresh Native rerun is therefore **not available from Git alone**.

For an explicitly authorized new reproduction, retain these separate stages rather than silently
substituting a managed test, different binary or newly generated fixture:

1. Supply the original source/build receipts and ELF matching the published hashes, or build a
   new declared source snapshot through the repository's approved Native build path and label the
   result a **new build/study**, never the historical B318 artifact. Follow the
   [build guide](../../../../tools/README.md#lambda-build); no build is run by this appendix.
2. Use the published image identities with explicit architecture. Keep image index, architecture
   manifest and config identities distinct; record the daemon's actual resolution rather than
   assuming which identity its `.Id` exposes. Missing tools/images are prerequisites, not permission
   to install/pull or relax isolation.
3. Prepare a MAIN81 sequence: first NoHeader, then 20 repetitions of NoHeader/Quoted/WeakList/Miss;
   first16 subsequent requests are warmups, last64 retained warm. The separate six-event health
   sequence must stay separate. Expected main counts:81 responses/upstream81, upstream1×200+80×304,
   downstream41×200+40×304. Every request revalidates upstream.
4. Create only new owned fake TLS material; never use production credentials or change host trust.
   Preserve default certificate validation. For the common-file Debian study hold F∪C byte/input
   identities constant across D–E–E–D. For AL2023 use default-directory only; its TRUSTED auxiliary
   input policy was not qualified for a directory-removal experiment.
5. Preserve complete-body timestamps before decoding, first-event E/A/S/R/U/B observations and
   Native PID/startTicks/CPU/RSS identity. Keep observer brackets, signed cross-thread returns and
   100Hz uncertainty explicit. Debian pre-Popen Ready and official Docker-transport-inclusive Ready
   are different domains; neither is CloudWatch INIT.
6. Keep nonroot/network isolation/cap-drop/no-new-privileges/read-only root/minimal mounts. The
   official runtime has no added Python/dependencies; the fixture is separate with only owned
   network/PID sharing, no host socket/network/ports/home/repository/credential mounts.
7. Hold the terminal Runtime `/next` request while publishing measurements. Validate unfiltered
   logs; stop and confirm the exact owned Native target before signaling fixture shutdown. Every
   post-measurement error must set FAIL. Record final thread/CID/key closure and the lack of
   post-stop log evidence if only pre-stop logs are captured.
8. Freeze process count/order and invalidation rules before launch; stop on invalidity, preserve
   failures, do not replace or discard slower observations. The accepted official study had two
   default-only processes; the earlier one-process V2 cohort stays INVALID, never pooled.

This recipe preserves the methodology and failure lessons without pretending an unshipped
controller can be executed from this checkout. No new permission, production claim or permanent
performance framework is introduced.
