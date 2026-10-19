# Latest-stable package refresh

**Evidence snapshot: 2026-10-19.** The user explicitly approved updating all packages to their
latest versions before continuing startup work. This refresh selects stable releases, not previews.
It does not change the .NET 10/C#14 target, deployment runtime, analyzer policy, HTTP/cache/security
contracts, CI actions, or production resources. The initial managed refresh performed no Native
publish; the subsequent package-only Native validation is recorded separately below. This note
does not claim a production deployment or performance result.

## Scope and version selection

The initial inventory contained **57 central NuGet pins plus Aspire.AppHost.Sdk**. Public NuGet
version indexes selected the highest stable numeric version; all original pins/SDK were compared
again after updates. AppHost's explicit package reference/central entry is now replaced by the
matching implicit **13.6.1** SDK reference. A supported xUnit package variant was added to preserve
VSTest; current versions continue to belong to [central props](../../Directory.Packages.props),
not this dated note. No tracked Node package manifest or local .NET tool manifest was found.

Major groups updated include Lambda serializer/APIGatewayEvents **3.0.1**, RuntimeSupport **2.3.0**,
Lambda.Core **3.4.0**; AWS SDK v4; Microsoft.Extensions/System.Text.Json **10.0.13**;
OpenTelemetry **1.20.0**; Aspire SDK/Testing **13.6.1** and AWS integration **13.7.2**;
MessagePack **3.1.12**; Roslynator **5.0.1**; Spectre **0.57.2**; Testcontainers **4.16.0**;
and current stable test/analyzer packages. Packages already latest were not needlessly changed.

All `PackageVersion`/`PackageReference` edits were made through .NET CLI commands. The installed
Aspire updater performed its supported project-SDK migration. A temporary ignored CLI project
updated central pins not directly referenced by ordinary projects; it was never added to the
solution, restored as a dependency aggregate, built, or shipped. Its two owned files were removed
afterward. No packages were added to production merely to update a central entry.

The [version/validation snapshot](baselines/2026-10-19-package-refresh.json) retains all original
pin comparisons, the SDK-implicit AppHost mapping and the extra VSTest-preserving package variant.

The updater's incidental local Aspire config and NuGet source-mapping/encoding changes were removed;
the original NuGet configuration is retained. No machine CLI/tool installation or feed/auth change
was needed. The original [pre-refresh AWS inventory](2026-10-19-aws-native-aot-package-review.md)
remains dated evidence of the old pins, not the post-update version owner.

## Compatibility changes, not weakened checks

- **Aspire:** SDK is now `Aspire.AppHost.Sdk/13.6.1`; redundant AppHost PackageReference is removed.
  `AspireUseCliBundle=true` selects the supported bundle path instead of suppressing ASPIRE010.
  Existing hosting calls remain unchanged. Exact-source checks used Aspire tag `v13.6.1`, commit
  `3751e615ad660621f2a7aa4c390ef48e08a0eed8`; AWS release `release_2026-08-26`, commit
  `653c103df4e342a927f7ac0fd533b350ef772c34`; unchanged LocalStack hosting tag `13.4.0`, commit
  `6cdc91719e8637346b22d4d4c013a8c124e3d417`. Named APIs were present; source availability alone was
  not treated as runtime verification. No new AppHost lifecycle or deployment run was started.
- **Spectre:** five `ExecuteAsync` overrides become `public`, matching the new public base member.
  Command bodies, arguments and exit-code handling are unchanged; file-based CLI also compiles.
- **Sonar/nullability:** redundant `!` operators are removed after existing non-null flow facts;
  no new runtime guard or response behavior is added. Moq log-state matching models nullable input;
  boxed log levels use `Assert.IsType<LogLevel>` rather than unchecked nullable unboxing.
- **String assertions:** existing `Assert.Contains` checks receive explicit `StringComparer.Ordinal`.
  No expected value/assertion is removed or made case-insensitive. The two preexisting H2 test
  assertions receive only this overload adaptation; the H2 production constructor and allocation
  test are unchanged. Historical H2 results are not requalified for the new dependency set.
- **xUnit v3 4.0.2:** its default package now brings MTP v2, which rejects the .NET10 VSTest target.
  Both test projects instead use official **`xunit.v3.mtp-off` 4.0.2** with the current VSTest adapter.
  This turns off MTP integration, **not tests**; `global.json`, `dotnet test` and standard filters
  retain their previous platform. CDK assembly sequential execution is preserved with
  `[assembly: Parallelization(Mode = ParallelMode.None)]` in place of the now-obsolete property.
  See [official variants](https://xunit.net/releases/v3/3.2.0) and
  [4.0 breaking changes](https://xunit.net/releases/v3/4.0.0).

## Observed verification

| Gate | Observed result |
|---|---|
| `dotnet restore BadgeSmith.sln` | Succeeded |
| Release solution build | **0 warnings / 0 errors**, all eight projects |
| Main-checkout `dotnet test BadgeSmith.sln -c Release` | **629 API + 4 CDK; 0 failed / 0 skipped**, VSTest; includes the separate unpublished H2 tests |
| Release file-based `tools/badgesmith.cs` build | Succeeded |
| Ordinary API build with telemetry/LocalStack disabled | **0 warnings / 0 errors**; production compilation branch, not Native publish |
| Latest-version comparison | Every original central pin/SDK matched the captured stable feed target; AppHost handled as SDK-implicit; MTP-off 4.0.2 separately confirmed latest stable |
| Slopwatch | **0 findings** |

Failures preceding those results remain part of the record: new Sonar redundant-null operators,
Spectre override visibility, obsolete xUnit parallelism property, test nullability/comparer overloads,
Aspire bundle warning, and the first VSTest launch rejected by default MTP v2. They were fixed by
source/API adaptations, not disabled diagnostics, raised allocation ceilings, skipped tests or a
test-platform switch.

The restored serializer **3.0.1 `net10.0` DLL** was inspected directly: it contains
`AssemblyMetadata("IsTrimmable", "True")`. Its generic source-generated serializer still performs
annotated `GetConstructor`/`Invoke` context construction. This confirms the package metadata and
mechanism, not an application Native AOT publish, runtime correctness proof, or performance benefit.

## Package-only release validation

The subsequent standing delivery approval covers Native validation and the normal PR/CI/deploy
sequence without repeated routine questions. It does not include the previously excluded H2 code.
A clean publication source therefore starts from `58a5bf0` plus only package/compatibility changes:
the old `TemplatePattern` constructor and allocation test remain HEAD-exact; the existing matcher
tests receive only the two explicit Ordinal comparer additions. This is **not** the earlier
629-test H2 working source.

| Package-only gate | Actual result |
|---|---|
| Frozen source | **340 files**, manifest `6d3c57a4ccc66b3f2781dfce95af549482509456d73bd003599454615ee262c3` |
| Fresh Release VSTest | **579 API + 4 CDK**, 0 failed / 0 skipped |
| Fresh x64 Native publish through existing CLI | Exit0, fresh native code generation, **0 compiler/IL/AOT/trim diagnostics** |
| BuildKit binding | `xxvhm5qmgrev1defk7t4pquwx`; guarded explicit frozen context; SDK 10.0.402/runtime material 10.0.13 |
| x64 ZIP | **6,356,141 B**, SHA256 `496207de2ac1b57869c4e0c2cfc4ba3b4fd7d2dda9e758506c1c65cd68764b74` |
| x64 ELF | **14,127,048 B**, SHA256 `509be7f237d8a3b7d9b0d80af9b24d0a048c6187c218f7ba2d2c279034e4010a` |
| Official AL2023 fixed two-process smoke | **162/162** responses/upstream calls, downstream82×200+80×304, upstream2×200+160×304 |
| Recorded Native identity/security/closure | Same new ELF, actual PID/startTicks, isolated default-validation fixture; four exact CIDs/names and three new key paths absent |
| Offline pre/post production-template comparison | **16 full resource objects identical**, no differences; same non-executable placeholder asset and unresolved hosted-zone context, no live AWS diff proof |

The [Native smoke projection](baselines/2026-10-19-package-native-smoke.json) retains both processes'
162 invocation rows, 30 observations, identity pins and receipt limits. Local first-feature elapsed
was 39.873525/36.840022 ms; warm64 medians 0.630920/0.6276185 ms; post81 RSS/HWM 29,000/29,428 KiB.
These are compatibility-smoke observations, **not a causal old/new speedup or memory/SLO result**.
Ready still includes Docker orchestration and fixture attestation, not isolated application startup.
Default SSL_CERT_DIR remains absent; ordinary CA input coverage does not establish Native policy
equivalence. Logs were sampled pre-stop only. No source/helper/controller or prior failed record
was silently relabeled. Fresh independent rebind and raw-data reviews found no blocking findings;
the parent separately rehashed all 340 frozen files and new ZIP/ELF bytes.

An initial delegated build task stopped because its prompt contradicted full-test fixture setup.
The parent clarified that existing test-owned Mock/Aspire/LocalStack fixtures were authorized;
the original BLOCKED report was retained, and the full release-scope tests and single Native build
then ran. No test was filtered, skipped, weakened or replaced. Documentation status updates and this
projection postdate the actual frozen build source; no per-file compiler-input attestation is claimed.

## Remaining boundaries and continuation

The earlier [Native attribution packet](2026-10-19-native-first-request-attribution.md) remains
historical evidence for its exact old binary/dependencies. No old ELF or performance numbers are
relabeled as the upgraded application. Compilation and managed tests cannot establish Native
loader/trim/serialization behavior, deployed ARM64 ZIP size, CloudWatch INIT or MaxMemoryUsed.

Production ARM64 size/execution and delivery remain separate: x64 ZIP bytes do not answer the
6,000,000-byte ARM64 goal. Normal hosted CI must build the selected ARM64 artifact; deployment
promotes only a new successful master-push artifact through the existing gated workflow. Inspect the
live change-set diff before deployment; offline parity is not permission to accept infrastructure
drift. The current work has standing task-scoped delivery approval, not a repository-policy change.
No custom serializer, speculative reflection elimination or production performance win was introduced.

The independent working-diff review found no critical or important Standards/Spec findings. Its
minor final-newline issue was corrected. The unused central `xunit.v3` pin is intentionally retained
as an inert version entry, not consumed by either test project; the console-runner pin was already
unused before this refresh. No runtime dependency or alternative test runner is activated by those
entries. The review's conditional missing-closeout note is resolved by this file and link validation.
The reviewer did not rerun the author-recorded builds/tests; those are separate executed evidence.
Node action code was not modified and its tests were not rerun by this refresh.
