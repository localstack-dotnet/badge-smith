# Iteration 0 Design — AOT Contract-Test Tier, Baseline Harness, Multi-Arch Build

> Superseded note (2026-07-04): RIE-dependent contract and benchmark paths are superseded by `2026-07-04-rie-free-aspire-localstack-redesign.md`. Keep this document for historical context only.

Date: 2026-07-02
Status: Superseded/historical; do not implement this RIE-based design.

## Context

The Aspire dev loop runs the Lambda as a JIT/CoreCLR build with `ENABLE_LOCALSTACK`
defined — the trimmed Native AOT binary that actually ships is never exercised by any
test today. Before the planned `TrimMode=full`/ILC iterations (see
[../research/2026-07-02-performance-opportunities.md](../research/2026-07-02-performance-opportunities.md)),
we need a safety net that runs the **real published artifact** and a **recorded
performance baseline** to diff every iteration against. Strategy decisions were agreed
in `docs/ROADMAP.md` (Inbox, 2026-07-02).

Trim failures historically manifest as silent hangs, not exceptions — the tier must
exercise every route and every response shape, not just happy paths.

## Goals

1. A contract-test suite that runs the production AOT artifact in the real Lambda base
   image (`public.ecr.aws/lambda/provided:al2023`) via the Runtime Interface Emulator.
2. A repeatable performance/memory baseline harness with dated, committed results.
3. A Dockerfile that cross-compiles arm64 artifacts on an x64 host **without QEMU**.
4. CI wiring: contract suite as a deploy gate + nightly run on native arm64 runners.

Non-goals (out of scope): any endpoint behavior change (two env-var overrides
excepted), TrimMode/ILC changes, Wave 1 bug fixes, unit-test expansion, Aspire loop
changes.

## Architecture

Orchestration choice: **Testcontainers-native** (decided). The xUnit fixture owns the
container lifecycle; the shell harness assembles the same stack with plain `docker`
CLI. The shared contract between the two is (a) the image tag and (b) the env-var
names below — documented in one place (the test project README) to bound drift.

```
xUnit collection fixture (Testcontainers, shared Docker network)
├── localstack/localstack        DynamoDB + Secrets Manager
├── wiremock/wiremock:3.x        NuGet + GitHub API stubs (checked-in mappings)
└── badge-smith image            prod artifact + RIE, env-pointed at the two above
        ▲
        │ POST /2015-03-31/functions/function/invocations  (APIGW v2 event JSON)
   test code (LambdaClient helper) — asserts APIGW v2 response JSON
```

- The suite **does not build** the image. It consumes a prebuilt tag from
  `BADGESMITH_TEST_IMAGE` (default `badge-smith:local`) and fails fast with a clear
  message if missing. Building (5–10 min AOT publish) belongs to the build script and
  CI steps.
- The Lambda container runs the true production build: `ENABLE_TELEMETRY` and
  `ENABLE_LOCALSTACK` off. RIE listens on container port 8080; the host port is
  dynamically mapped by Testcontainers.
- Fixture creates the three DynamoDB tables (same names/keys/GSI as
  `DynamoDbTablesConstruct`), writes the org-secret mapping row, and puts the HMAC
  secret into LocalStack Secrets Manager. Test data is seeded per test class.

### Environment plumbing (the two production touches)

1. **AWS endpoints.** Spike first: verify AWS SDK for .NET v4 honors
   `AWS_ENDPOINT_URL_DYNAMODB` / `AWS_ENDPOINT_URL_SECRETS_MANAGER` (expected: yes —
   zero code change). Fallback if not: add an env-based `ServiceURL` override to the
   production path of `AwsClientBuilder` (small, approval-gated edit).
2. **Upstream base URLs.** `HttpClientFactory` hardcodes `https://api.nuget.org/` and
   `https://api.github.com/`. Add optional env overrides `HTTP_NUGET_BASE_URL` and
   `HTTP_GITHUB_BASE_URL` (fall back to today's constants). This implements the
   mock/real switch: contract tests always point at WireMock; the perf harness selects
   via `BADGESMITH_UPSTREAM=mock|real` (decided: both modes supported — perf numbers
   are wanted against both).

### WireMock stubs

Mappings live in `tests/BadgeSmith.Api.ContractTests/wiremock/` and are mounted into
the container. They are **recorded** from the real NuGet/GitHub APIs once (WireMock
recording mode), then sanitized and checked in — not hand-invented. Minimum set:
NuGet flat-container index (large multi-version package, prerelease-only package,
404), GitHub package versions (happy, 401, 403, 404, empty).

## Coverage matrix

All six routes through the real binary, asserting status, headers, and full body
shape (any missing `JsonSerializable` registration must surface):

| Area | Cases |
| --- | --- |
| Health | 200 + no-cache headers |
| NuGet badge | ok, `?version=` range, `?prerelease=`, 404 package, 400 invalid range, ETag→304 |
| GitHub badge | ok, secret missing→401, upstream 401/403 mapping, ETag→304 |
| Test results badge | ok, 404 no data, ETag/304, Last-Modified |
| Ingestion (HMAC) | full signed round-trip (store→badge read-back), bad signature→401, stale/future timestamp→400, nonce replay→400, malformed hex signature, missing headers |
| Redirect | 302 + Location + cache headers |
| Routing/CORS | unknown route 404, OPTIONS preflight, HEAD→GET |

Contract tests pin **current** behavior, including known bugs — e.g., a malformed hex
`X-Signature` returns 500 today and the test asserts 500 with a comment referencing
findings §2. Wave 1 fixes update the pinned assertions in the same change as the fix.
The suite is a trim-regression net, not a bug-fix vehicle.

**"Test the tester" acceptance criterion:** on a scratch branch, remove one
`JsonSerializable` registration, rebuild the image, and prove the suite goes red.
Iteration 0 is not done until this is demonstrated.

## Multi-arch build fix

- Build stage becomes `FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:…`
  with clang cross toolchain + arm64 sysroot packages; `TARGETARCH` maps to the RID.
  ILC cross-compiles natively on the host — no emulation in the build path.
- `scripts/build-lambda.*` default RID: **linux-arm64 for release artifacts** (closes
  findings §1.3 — aligns with the CDK's hardcoded arm64 expectation); local test image
  defaults to host arch (amd64).
- Timeboxed experiment (once, ~30 min): refresh binfmt (`tonistiigi/binfmt`) and try
  running the arm64 image locally. If it works, local contract tests may run arm64;
  if not, amd64 stands (trim-failure class is arch-independent) and the outcome is
  documented. No insistence — decided.

## Baseline harness

`scripts/perf-baseline.sh` is the single source of truth; `perf-baseline.ps1` is a
thin wrapper that delegates (avoids the known ps1/sh drift). Prerequisite fix folded
in: `k6-perf-test.js` reads `__ENV.K6_API_URL` (and `K6_DURATION`/`K6_VUS`) as its
README already claims.

Stages: build image → record binary/zip sizes + `IlcGenerateMstatFile` output → boot
the stack (upstream toggle) → k6 scenario against the RIE/emulated endpoint →
`docker stats` sampling (idle + under load) → write
`docs/research/baselines/YYYY-MM-DD-<label>.json`:

```json
{
  "date": "…", "label": "…", "gitSha": "…", "arch": "amd64|arm64",
  "upstream": "mock|real",
  "image": { "binaryBytes": 0, "zipBytes": 0, "mstat": "path-or-summary" },
  "boot": { "startToReadyMs": 0 },
  "k6": { "p50Ms": 0, "p95Ms": 0, "p99Ms": 0, "rps": 0, "errorRate": 0 },
  "memory": { "rssIdleMb": 0, "rssPeakMb": 0 }
}
```

A second baseline rides along: the Slopwatch baseline (`.slopwatch/baseline.json`,
created 2026-07-02 via `slopwatch init`) captures the 22 pre-existing findings — 17×
SW002 pragma/SuppressMessage, 3× SW005 csproj NoWarn, 2× SW003 which are the
intentional retry-loop catches in `ResilienceRetryHandler.cs:33,37` — so that
`slopwatch analyze --fail-on warning` (AGENTS.md quality gate) flags only NEW slop
introduced by iteration work.

Ordering: iteration 0's own infrastructure changes (Dockerfile build stage, scripts,
the two dormant env overrides) land first — they are the prerequisites for producing
the image and running the harness. The baseline is then recorded from that state,
**before any behavior or performance iteration (Wave 1+, TrimMode/ILC) lands**, and
committed as the reference entry. Local numbers are relative (regression detection);
absolute cold-start truth remains prod CloudWatch REPORT data (captured 2026-07-02 in
the perf research doc).

## CI wiring (approval-gated step in the plan)

- `deploy.yml`: new job on `ubuntu-24.04-arm` — build arm64 image → run contract
  suite against it → required before the CDK deploy step.
- New nightly workflow (schedule) running the same suite, so runtime/base-image drift
  surfaces without a deploy.

## Risks and fallbacks

| Risk | Mitigation |
| --- | --- |
| SDK v4 ignores `AWS_ENDPOINT_URL_*` | Spike #1 validates before anything else; fallback is a small env override in `AwsClientBuilder` prod path |
| RIE not present in `provided:al2023` base image | Verify in spike; if absent, add RIE binary to a **test-only** image layer (prod artifact unchanged) |
| Cross-compile toolchain friction in SDK image | Known-good recipe: `clang llvm binutils-aarch64-linux-gnu` + arm64 sysroot; validate in spike on the build stage |
| WireMock stubs drift from real APIs | Stubs recorded (not hand-written); `BADGESMITH_UPSTREAM=real` harness mode doubles as a live-contract check |
| CI arm64 job cost/time | AOT build ~5–10 min; acceptable as deploy gate + nightly, not per-push |

## Required capabilities (per AGENTS.md routing)

Executors (main loop or subagents) must load explicitly before working:
`dotnet-skills:testcontainers`, `dotnet-skills:project-structure`,
`dotnet-skills:package-management` (CPM), `dotnet-test:run-tests`,
`dotnet-diag:microbenchmarking` (harness design), `dotnet-skills:serialization` (when
touching JSON shapes), `dotnet-skills:slopwatch` after code changes; process:
`superpowers:test-driven-development`, `superpowers:verification-before-completion`.

## Success criteria

1. Contract suite green locally (amd64) against the prod artifact with mock upstream.
2. Injected serializer breakage turns the suite red (test-the-tester demonstrated).
3. arm64 image builds on x64 host with no QEMU in the build stage; local arm64 run
   attempted once and outcome documented; CI contract job green on `ubuntu-24.04-arm`.
4. First baseline JSON committed for unchanged code.
5. Zero AOT/trim warnings; no public HTTP contract change; Aspire loop untouched.
