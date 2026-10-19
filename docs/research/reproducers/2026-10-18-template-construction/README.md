# Historical template-construction appendix — 2026-10-18

This is a small, standard-library historical adapter, not a supported performance CLI or a new
repository/CI policy. [Dated evidence](../../2026-10-18-template-construction-optimization.md) owns the
conclusions; the [numeric packet](../../baselines/2026-10-18-template-construction.json) and
[4,200 invocation rows](../../baselines/2026-10-18-template-construction-invocations.csv) own the data.

## Safe default: reproduce the historical arithmetic

From the repository root, with Python 3.11 or newer:

```bash
python3 -B docs/research/reproducers/2026-10-18-template-construction/check.py
python3 -B docs/research/reproducers/2026-10-18-template-construction/controls.py
```

Neither command builds, starts a listener, executes Native code, contacts a service, reads credentials
or needs Docker/.NET. Controls install a process/socket audit denial and exercise the **actual adapted
Runtime GET/POST/upstream handlers** through fake streams, including all 81 main events, revalidation,
wrong IDs/error endpoints before body reads, final-byte clocks, full headers/schema and poisoned/early
clock mutations. These newly executed public-adaptation controls are **not** extra study samples or
the as-executed original control witness. `check.py` recomputes calibration margins, both formal
blocks, all 100 launches/4,200 rows, cleanup, clocks, warm statistics and artifact/source identifiers.
It verifies bundled adapter/probe file hashes, not unavailable original artifact bytes or live PIDs.

`runtime_adapter.py` is a self-contained flattening of the original `h2_measure.py` Runtime methods,
`native_measure.py` protocol/fixture/counter primitives, and four archive/hash helpers from
`build_attest.py`. The original `inside` measurement logic, literal environments, caps and full
response oracles are retained. Only dynamic private imports are replaced by local definitions; public
`prepare` and explicit CLI opt-in are new. No hidden imports, personal roots or dependency download.
The packet keeps **original helper/acceptance/receipt SHAs separately from public adaptation and
projection SHAs**; adapted bytes are not retroactively called the code that ran in the study.

The repaired `snapshot` and direct `prepare` share one exact-source guard. It checks the submitted
absolute lexical root and every ancestor for directory/non-link identity before any resolution;
rejects `..`, malformed relative manifest paths, symlinks/broken links and nonregular entries; and
compares the complete regular-file inventory with the manifest. Only manifest-prefix directories
are allowed, so even an unlisted empty/hidden directory or file is rejected. Names/no-follow types
are checked before opening source contents or creating output; unexpected bytes are never read.
This assumes stable, control-owned inputs, **not hostile concurrent writers or a TOCTOU security
guarantee**. `bind` remains a declared-record/artifact binder, not source closure or compiler proof.
Guard tests use synthetic, never-executed ELF/ZIP fixtures in owned `TemporaryDirectory` roots;
set process-local `TMPDIR` to an owned directory when an explicit scratch location is required.

## Explicit new Native reproduction (not run by packaging)

This section requires a separately authorized local experiment and existing dependencies. It never
authorizes AWS, real upstreams, registry pulls, new packages, image publication or production traffic.
Missing supplied inputs fail closed; do not fetch live data or silently use today's edited tree as A.

Required inputs for **each** A/B or diagnostic variant:

1. An explicit source-input directory with only the public bytes listed in that variant's packet
   `sources[variant].files` map (SHA-256, size and mode). A is the historical **working-byte** freeze,
   not merely HEAD. B also contains the two **pre-status-update** goal plans. Their original hashes
   deliberately differ from the current status documents. To replay those exact historical inputs,
   supply those exact bytes; no hidden private file is automatically discovered. If unavailable,
   construct and retain a **new, explicitly labeled** source manifest for a fresh same-code study;
   never claim its whole-source hash or ZIP is the original. The unchanged code/policy hashes remain
   independently checkable. The historical packet's offline replay needs none of these original files.
2. A new empty absolute source/output/input/work/executable directory for every build/launch.
3. An actual normal BuildKit build-record JSON and selected-SDK receipt captured for the new build,
   not the original study record copied as new. `inputs.py bind` requires record fields `Ref`,
   `Status`, `Target`, `Platform`, `Context`, `BuildArgs`, `Materials`; SDK receipt fields are
   `selectedSdk` and `materialDigest`. Retain actual command, UTC/monotonic source-before/after and
   combined stdout/stderr diagnostic-count receipts separately. The binder verifies the supplied
   record/artifact binding, not the historical truth of an operator-supplied record.
4. Existing linux/amd64 runtime image at the exact packet digest, existing SDK/build dependencies,
   compatible non-root Docker and host `openssl`. A permission/dependency failure is a stop, not
   permission to pull/install or use a host fallback.

Set all variables explicitly to **new owned absolute paths**. `R` is the actual Git checkout;
`G` is a new experiment root outside checkout compilation globs. `A_INPUT`, `B_INPUT`, `DA_INPUT`,
`DB_INPUT` are supplied exact source trees; never default them to `R`.

```bash
H="$R/docs/research/reproducers/2026-10-18-template-construction"
P="$R/docs/research/baselines/2026-10-18-template-construction.json"
python3 -B "$H/inputs.py" snapshot --packet "$P" --variant A --input-root "$A_INPUT" --out "$G/sources/A" --receipt "$G/A-source.json"
```

Repeat with B/H2DA/H2DB, their explicit input roots and fresh destinations. Diagnostic input trees
contain the exact [probe](source-probe.cs.txt), [Program](program-source-probe.cs.txt) and
[explicit RouteTable initializer](route-table-source-probe.cs.txt). These `.cs.txt` files never
enter shipping compilation. DA/DB share the exact diagnostic sources and differ only in the approved
constructor/test bytes. The explicit static constructor prevents beforefieldinit prewarming; the
complete original route collection and all six descriptors are consumed after the primary counter.

Run the existing unchanged CLI **from the real checkout**, once per explicit frozen source S and new
output O (A, then B, DA, DB), recording its actual inputs/results. Do not add publish flags:

```bash
cd "$R"
dotnet run --file "$S/tools/badgesmith.cs" -- lambda build --target zip --rid linux-x64 --dockerfile "$S/src/BadgeSmith.Api/Dockerfile" --context "$S" --out "$O" --verbose
```

Capture the matching normal BuildKit record by its exact new Ref. Prove actual SDK selection with a
same-platform-material, pull-never/network-none query at `/src` with the frozen `global.json` mounted
read-only; the historical selection was 10.0.402. An image label or host version is not a substitute.
`inputs.py bind` reads the supplied record/SDK receipt and actual ZIP (single bootstrap, CRC,
Unix0100755, ELF64 little-endian machine62); it does not invoke build/container commands:

```bash
python3 -B "$H/inputs.py" bind --source-receipt "$SOURCE_RECEIPT" --build-record "$NEW_BUILD_RECORD" --sdk-receipt "$NEW_SDK_RECEIPT" --zip "$O/badge-lambda-linux-x64.zip" --receipt "$G/new-build.json"
```

Extract only `bootstrap` into a **new** executable directory, preserving executable0755, and verify
its SHA equals the ZIP-entry ELF SHA. Then prepare an exclusively new input directory:

```bash
python3 -B "$H/runtime_adapter.py" prepare --source-receipt "$SOURCE_RECEIPT" --build-receipt "$G/new-build.json" --zip "$O/badge-lambda-linux-x64.zip" --executable "$EXE/bootstrap" --out "$INPUT" --cohort main --role fresh-main-A
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$INPUT/fixture.key" -out "$INPUT/fixture.crt" -days 1 -subj /CN=badgesmith-native-fixture.invalid -addext subjectAltName=IP:127.0.0.1 -addext basicConstraints=critical,CA:TRUE -addext keyUsage=critical,digitalSignature,keyEncipherment,keyCertSign -addext extendedKeyUsage=serverAuth
chmod 600 "$INPUT/fixture.key" "$INPUT/fixture.crt"
```

Use `--cohort health` for separate health/diagnostic runs. A main process has first NuGet +16 labeled
warmups +64 retained warm calls; health has six events/five upstream calls. Inputs define no remote
endpoint. The application environment is literal, metadata off, AWS files `/dev/null`, no inherited
credential/proxy/OTEL variables. All fixture listeners/URLs are loopback; the native runtime rejects
non-loopback URI/configuration, extra events, missing revalidation and wrong response IDs.

Create a new mode0700 `WORK` and launch only in the closed runtime, with the explicit opt-in:

```bash
docker run --rm --pull=never --platform linux/amd64 --network none --cap-drop ALL --security-opt no-new-privileges=true --read-only --user "$(id -u):$(id -g)" --ipc none --pids-limit 32 --workdir /work --mount "type=bind,source=$H,target=/harness,readonly" --mount "type=bind,source=$INPUT,target=/input,readonly" --mount "type=bind,source=$EXE,target=/executable,readonly" --mount "type=bind,source=$WORK,target=/work" --entrypoint /usr/bin/env sha256:446efb4225200a0a196707aa7953898b37962fd3b8859a636035de3af657081e -i PATH=/usr/local/bin:/usr/bin:/bin HOME=/work/home TMPDIR=/work/tmp XDG_CONFIG_HOME=/work/xdg XDG_CACHE_HOME=/work/cache LANG=C.UTF-8 SSL_CERT_FILE=/input/fixture.crt PYTHONDONTWRITEBYTECODE=1 /usr/local/bin/python3 -I -B /harness/runtime_adapter.py inside --execute-native-fixture
```

The adapter rejects root/capabilities/writable-root/non-loopback interfaces/foreign environment and
attests the owned live native PID/executable **after** timing. Inspect the finite numeric `result.json`,
require PASS/full accounting/reaped child+threads, and remove **only that launch's** ephemeral key
after closure. Retain every failed launch; never replace it with a favorable retry. No launcher runs
automatically from the public checker. New samples/ZIP timestamps are not expected to reproduce old
timings or ZIP bytes identically. Runtime is Debian/x64, **not AL2023/ARM64/AWS**.

For isolated Settings correctness, the packet's `H2Settings` source changes only Program to the
exact [held-live Settings probe](settings-program-source-probe.cs.txt). Use its separately supplied
source/build/artifact inputs and `--cohort health --settings-json "$FAKE_SETTINGS_CONDITION"` during
`prepare`. That file is one explicit `settingsControls.controls[i].condition` object from the packet
(keys `initialVersion`, `dotnet`, `aspnet`, `expectedVersion`, `expectedEnvironment`). Derive the default
again with the matching selected-SDK/source/publish-property GetAssemblyVersion query, not Settings
itself. Keep all five processes labeled correctness, never timing/benefit samples. `prepare` without
`--settings-json` retains the ordinary literal fake APP_VERSION. No credential or endpoint option exists.

For a new comparison, first run 8 A/A main pairs, 4 A/A health pairs and 5 DA/DA pairs; freeze the
documented formulas before any B/DB execution. Then use two 8-pair main blocks, two 5-pair diagnostic
blocks and four health pairs, alternating AB/BA and reversing the start in block2. Keep correctness/
ABI/Settings roles outside performance statistics. The packet's existing frozen numeric margins are
historical observations, not permission to reuse them for a different host/new experiment.
