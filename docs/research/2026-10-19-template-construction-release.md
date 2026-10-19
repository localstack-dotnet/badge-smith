# Template construction: current-package release validation

**Evidence snapshot: 2026-10-19.** This is the separate publication follow-up for the retained H2
constructor change, after the package refresh reached production. It is not a new algorithm,
serializer, cache policy or performance target. The earlier
[2026-10-18 study](2026-10-18-template-construction-optimization.md) remains historical evidence;
its source/build/counter observations are not relabeled as the new package baseline.

## Source scope and preserved behavior

Base: merged package master `284658607433b260588b2fbc9f062b0451106363`. Exactly three retained
code/test files changed, copied without redesign from the main checkout:

| File | SHA-256 |
|---|---|
| `TemplatePattern.cs` | `06d544a8a20ea0465b382aaaaa038bfb0c99b456710f5d71cbed00860f771ee5` |
| `TemplatePatternTests.cs` | `e3f16427a7aabc02ba577c89d4fd48db97d1334116f8885ee7de2b3f45bbe59a` |
| `AllocationBaselineTests.cs` | `f12e97b081ba7a76e76d22a2332f2e271295103f75a8dfe4c92858c988b97ba8` |

The constructor counts separators and enumerates span ranges instead of allocating a temporary
split array/strings. The same retained key/literal arrays and strings remain. Null/empty/slash-only,
leading/trailing/empty segments, outer-brace classification, duplicate-key behavior, literal case,
value case and later decoding are unchanged. The matcher suffix is byte-identical to the base,
SHA-256 `2a64d361e2e9a6a44f270b4194ed332bad5c418f049aa50ac60518badfc20fa6`.
This is exact-text equality, not a claimed Roslyn AST equivalence proof.

No public signature/name/nullability/serialization/configuration change, package update, compiler
profile, route change, logger/cache/security modification or analyzer suppression is included.
Main-checkout content was preserved while a separate publication checkout was validated.

## Fresh test-first allocation proof

The named seam is the actual warmed `TemplatePattern` constructor used by callers, with a
reused-object control and `GC.GetAllocatedBytesForCurrentThread()` around synchronous construction.
The two test files were copied first, before the production constructor:

| Stage | Actual result on .NET 10.0.13 x64 |
|---|---|
| RED, old constructor | Exact fact fails `752 > 751 B`; control 0 B; 1 failed, no test errors/skips |
| GREEN, retained H2 constructor | Same test source and test DLL: five measurements **392 B**, control 0 B; fact passes |
| Saving in this fixture | **360 B per construction**, not per request or per Lambda lifetime |
| Full current-package tests | **629 API + 4 CDK**, 0 failed / 0 skipped; all 49 new characterization rows pass |

The new ceiling remains the previously selected 751 B, one byte below the recorded 752 B baseline;
it was not raised. Existing 584/264/264/64 B ceilings remain unchanged. Fresh RED ran the allocation
fact only; the 49 characterization rows' pre-edit execution belongs to the earlier study, while the
current independent old/new source analysis and post-edit suite corroborate their behavior.

Release solution/performance-project build, file-based CLI build, Node delivery/badge fixtures
**104 + 14**, Slopwatch **0**, and whitespace checks pass. No test, assertion, case or timeout was
removed, disabled or relaxed. VSTest and sequential CDK execution remain the current package
baseline. Independent source review reported no Critical/Important/Minor Standards or Spec findings.

## New Native build and official AL2023 compatibility smoke

Frozen source: **341 files**, raw source-manifest SHA-256
`13467b09b916fef63f86f47ce4a003a7c41d4e2b5f827e13e50f6c28e4e3e9db`.
The canonical fixture source-receipt hash is a different object/serialization; neither digest is
substituted for the other. Normal CLI publish retained the existing flags and Dockerfile:

| Identity/gate | Observed result |
|---|---|
| Native publish | One fresh x64 publish, exit0, actual native code generation; **0 compiler/IL/AOT/trim diagnostics** |
| BuildKit Ref | `default/default/h8479v1iq1ut1qg6a75qhv1m7` |
| SDK/runtime material | SDK 10.0.402 / runtime 10.0.13, exact immutable SDK material and frozen global.json |
| x64 ZIP | **6,356,135 B**, SHA256 `50f87b9204034733dd0bd24db0ae62d9c7d0627551359aacca1a23d831dc9451` |
| ELF | **14,127,048 B**, SHA256 `0b238db9856901b8d8192ecc9587d67915ad5bf6d981432052a46717ed99407c`; ELF64/LE/machine62/0755 |
| Official AL2023 smoke | Two fixed default-only MAIN81 processes; **162/162 responses and upstream calls** |
| Accounting | Downstream82×200+80×304, upstream2×200+160×304; every request revalidates |
| Identity/security/closure | Actual new ELF/PID/startTicks/FD/peer; all four exact CIDs/names and three new key paths absent |

The existing isolated fixture was cloned and rebound to the new source/build/artifact; **24 +18
shared +8 source-guard controls** and independent literal rebind/raw audits pass. Runtime/body
clocks, 15 phases/5 coarse CPU records per process, target-before-fixture shutdown, fail-closed
post-measurement errors, nonroot/cap-drop/NNP/read-only isolation and default TLS validation remain.
SSL_CERT_DIR is absent; the common file contains official143 ordinary inputs plus one fake cohort
CA. Ordinary input coverage is not Native-loaded policy equivalence. No keys or real credentials
are published; raw logs and host paths stay out of the repository.

The ancillary SDK-info probe initially printed versions but exited1 when its read-only container
could not create a temporary file. That failed receipt was retained. A separate corrected source-only
query used an owned writable work directory, the same immutable SDK/global.json, nonroot/read-only
root/no capabilities/no network; it exited0 and its exact CID was absent. Native publish was not
retried. The producer's narrower main guard remains labeled; the parent additionally compared all
340 public main files against the earlier complete snapshot, including template/untracked docs.

## Claims and publication boundary

The fresh managed saving above is **per construction**. The old **1,632 B once-only Native route-table
saving** remains the earlier study's result; no fresh Native allocation counter ran here. Local smoke
timings/RSS do not establish startup, latency, retained-memory or CloudWatch SLO improvement.
The new Native smoke is a compatibility check, not a causal A/B experiment. Logs were sampled
pre-stop only. Normal source/request/material/artifact binding is not signed/hermetic per-file
compiler-input attestation. Later documentation status updates are not retroactively build inputs.

This separate H2 change still requires normal hosted PR/ARM64 CI, merge, a new successful master
artifact and the existing production workflow/smoke. The package rollout completed as described in
[package refresh evidence](2026-10-19-package-refresh.md); that success does not pre-approve an
untested H2 artifact. Architecture performance numbers remain design targets, not newly invented
release blockers; actual size and any user-accepted deviation must be reported honestly.
