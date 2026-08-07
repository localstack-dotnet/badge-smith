# RIE-Free Aspire Testing And LocalStack Benchmark Redesign

Date: 2026-07-04
Status: Implemented/historical in `991769e`. Current behavior and follow-up status live
in `ARCHITECTURE.md`, `build/BadgeSmith.CDK/README.md`, and `docs/ROADMAP.md`.

## Context

Iteration 0 originally targeted a Runtime Interface Emulator (RIE) based contract tier
that ran the published Native AOT Lambda image through
`/2015-03-31/functions/function/invocations`. Task 10 exposed the core problem with
that direction: RIE is useful as a local Lambda container shim, but it is not the right
foundation for BadgeSmith's contract tests or local benchmark tooling.

This document supersedes the RIE-dependent portions of
`2026-07-02-iteration0-aot-contract-tier-design.md` and
`2026-07-02-iteration0-aot-contract-tier-plan.md`. The previous documents remain useful
for audit history and for the contract cases already discovered, but implementation must
not continue to the old Task 11 until a new RIE-free plan exists.

## Decisions

1. RIE is removed from BadgeSmith's active design.
2. BadgeSmith must not invoke, configure, document, or expose RIE as a supported local
   path. If an upstream AWS Lambda base image carries an emulator binary transitively,
   BadgeSmith treats it as an ignored implementation detail.
3. No RIE-specific production, benchmark, or contract-test code remains after the
   redesign, except reusable test infrastructure that is not tied to RIE semantics.
4. Contract and integration tests move to Aspire Testing as the primary test harness.
5. Local benchmark runs use LocalStack as the Lambda/API target for both mock-upstream
   and real-upstream modes.
6. GitHub Packages access always goes through the existing DynamoDB and Secrets Manager
   mapping path. Benchmark scripts may supply credentials to the seeder, but the Lambda
   reads them from LocalStack Secrets Manager.
7. k6 benchmark infrastructure remains separate from contract testing.
8. xUnit v3 remains the test framework. TUnit is not adopted in this iteration.

## Goals

1. Replace RIE contract execution with Aspire Testing backed by the existing AppHost.
2. Replace RIE benchmark execution with a LocalStack Lambda/API endpoint.
3. Keep mock and real upstream benchmark modes while using the same LocalStack seed path
   for DynamoDB tables, HMAC secrets, and GitHub package credentials.
4. Preserve useful test assets that were created during Iteration 0, including route
   cases, HMAC helpers, seed data, and WireMock mappings.
5. Remove dead RIE-specific code, docs, script modes, and claims.
6. Keep production AppHost and CDK paths clean; test-only upstream stubs must not leak
   into deployed infrastructure.

## Non-Goals

1. No public HTTP behavior changes.
2. No production bug fixes; contract tests pin current behavior until a later wave fixes
   those bugs intentionally.
3. No CI workflow changes without a fresh approval gate.
4. No TUnit migration.
5. No attempt to treat local k6 numbers as real AWS Lambda performance truth.

## Architecture Overview

The redesigned local validation stack has three separate purposes:

| Purpose | Harness | Target | What it proves |
| --- | --- | --- | --- |
| Unit tests | xUnit v3 | In-process code | Fast logic coverage |
| Contract/integration tests | Aspire Testing | `BadgeSmith.Host` with `APIGatewayEmulator` | Routing, headers, response shapes, LocalStack-backed data paths |
| Local benchmark | shell + k6 | LocalStack Lambda/API endpoint | Repeatable local end-to-end latency and artifact regression data |

Native AOT artifact verification becomes a separate LocalStack Lambda container-image
spike. If LocalStack can run the published image through API Gateway v2 or Function URL,
that target becomes the RIE-free AOT smoke path. If it cannot, local contract tests still
stay on Aspire Testing and AOT artifact verification is handled by deployed AWS or a
future purpose-built mode, not by reintroducing RIE.

## Contract And Integration Tests

Aspire Testing becomes the primary contract-test harness:

```text
xUnit v3 contract tests
└── DistributedApplicationTestingBuilder.CreateAsync<Projects.BadgeSmith_Host>()
    ├── LocalStack resource
    ├── BadgeSmithStackResource CDK stack
    ├── BadgeSmithDynamoDbSeeders project
    ├── BadgeSmithApi Lambda TestTool resource
    └── APIGatewayEmulator HTTP API v2 resource
```

Tests call `APIGatewayEmulator` as ordinary HTTP, using Aspire endpoint discovery rather
than hardcoded ports. This removes the need for an API Gateway v2 event wrapper client in
the normal contract path.

The Aspire tier covers the contract matrix that is independent of Native AOT mechanics:

| Area | Cases |
| --- | --- |
| Health | status, body, no-cache headers |
| NuGet badges | success, valid version range, prerelease, missing package, invalid range, ETag to 304 |
| GitHub badges | success, missing secret, upstream auth/error mappings, empty response, ETag to 304 |
| Test result badges | success, no data, ETag to 304, Last-Modified |
| Ingestion | signed round trip, bad signature, stale timestamp, future timestamp, nonce replay, malformed signature, missing headers |
| Redirect | Location and cache headers |
| Routing/CORS | unknown route, OPTIONS preflight, HEAD behavior |

### WireMock

WireMock remains test-owned at first. The default implementation should not add test-only
WireMock mappings to `src/BadgeSmith.Host/Program.cs` unless the implementation proves a
clean Aspire resource boundary that avoids AppHost pollution.

Acceptable options, in preference order:

| Option | Use when | Trade-off |
| --- | --- | --- |
| Test-owned WireMock container | Contract tests need deterministic upstreams | No AppHost pollution; tests manage one extra resource |
| Test-only Aspire resource wrapper | A clean helper can add WireMock without AppHost depending on `tests/` paths | Better endpoint discovery; more integration code |
| Conditional AppHost resource | Only if the resource is useful for local development too | Risk of test config leaking into AppHost |

WireMock mappings stay in the test tree. Production CDK constructs and production Lambda
configuration must not reference mock upstreams.

## LocalStack Benchmark Harness

The local benchmark tool uses LocalStack as the Lambda/API target in all modes:

```text
scripts/perf-baseline.sh
├── build BadgeSmith Lambda image and artifacts
├── start LocalStack with Docker access
├── create or deploy DynamoDB, Secrets Manager, Lambda, and HTTP entrypoint
├── seed DynamoDB and Secrets Manager
├── optionally start WireMock for mock-upstream mode
└── run k6 in HTTP mode against the LocalStack HTTP endpoint
```

The benchmark must not use `K6_TARGET_MODE=rie` or post Lambda invocation envelopes.
k6 sends normal HTTP requests. LocalStack is responsible for turning those requests into
Lambda events.

### Upstream Modes

| Mode | NuGet upstream | GitHub upstream | Credential source |
| --- | --- | --- | --- |
| `mock` | WireMock | WireMock | LocalStack Secrets Manager seeded with dummy or provided token |
| `real` | `https://api.nuget.org/` | `https://api.github.com/` | LocalStack Secrets Manager seeded from local credential file or explicit benchmark input |

The Lambda must never receive a direct `GITHUB_TOKEN` application environment variable.
The seeder owns credential import and writes the existing org-secret mapping to DynamoDB
plus the secret value to Secrets Manager. This keeps benchmark behavior aligned with the
production GitHub Packages path.

### LocalStack Lambda Target

The preferred local benchmark endpoint is LocalStack API Gateway v2 plus a Lambda
container image, because BadgeSmith's handler type is
`APIGatewayHttpApiV2ProxyRequest`. Function URL is an acceptable fallback if API Gateway
v2 proves significantly harder to automate and the event shape is validated against the
router and all benchmarked routes.

Validation order:

1. Build the existing Lambda image without relying on RIE-specific client behavior.
2. Push or register the image with LocalStack's Lambda container-image support.
3. Expose it through LocalStack API Gateway v2 HTTP API.
4. Verify `/health`, a NuGet badge, a GitHub badge, a test-result badge, and ingestion.
5. Run k6 through normal HTTP mode against the LocalStack endpoint.
6. Verify whether the Lambda worker container RSS can be attributed reliably. If it
   cannot, local benchmark JSON records artifact size and HTTP metrics only, while real
   Lambda memory remains sourced from AWS REPORT data.

## AOT Artifact Verification

Aspire Testing does not prove Native AOT artifact behavior. It runs the local project via
the AWS Lambda TestTool path, not the published Lambda container image. Therefore AOT
verification is split from the primary contract tier.

RIE-free AOT verification follows this decision tree:

| Result of LocalStack image spike | Design outcome |
| --- | --- |
| LocalStack API Gateway v2 + image works | Add a narrow AOT smoke tier against that endpoint |
| Function URL works but API Gateway v2 does not | Use Function URL only after event-shape validation is documented |
| LocalStack image execution is not reliable | Do not reintroduce RIE; use deployed AWS verification for AOT artifact behavior |

The AOT smoke tier, if available, is intentionally narrow: it checks boot, JSON source
generation coverage, core route response shapes, and conditional production build
behavior. Broad HTTP contract coverage stays in Aspire Testing.

## Existing Iteration 0 Work

The current branch contains valuable work, but RIE-specific pieces are superseded.

Keep or adapt:

1. Contract test cases and assertions that describe current HTTP behavior.
2. HMAC signing helpers and seed-data builders.
3. WireMock mappings that remain valid upstream fixtures.
4. DynamoDB and Secrets Manager seed logic.
5. HTTP upstream base URL overrides, if still needed for WireMock and benchmarks.

Remove or replace:

1. RIE invocation client and APIGateway event envelope projection code.
2. k6 `rie` target mode and Lambda response-envelope projection.
3. `perf-baseline.sh` logic that runs the Lambda container directly through RIE.
4. Baseline files generated from the RIE path.
5. Documentation that presents RIE as the contract or benchmark foundation.

The two Task 10 commits are not a reliable final baseline because they were produced on
the old RIE path. The implementation plan must explicitly reconcile them before moving
forward.

## Test Framework Decision

xUnit v3 remains the project test framework for this iteration. TUnit's source-generated
and Native AOT-capable test runner is useful technology, but it does not validate the
BadgeSmith Lambda artifact. Migrating would force changes to filters, CI commands,
agent instructions, traits, fixtures, and assertions without solving the current problem.

## Success Criteria

1. RIE-specific code and scripts are removed from the active implementation path.
2. Aspire Testing runs the contract/integration suite through `APIGatewayEmulator`.
3. Mock and real local benchmarks run through LocalStack, not RIE.
4. GitHub Packages benchmark access uses DynamoDB plus Secrets Manager seeding.
5. LocalStack Lambda image spike decides the RIE-free AOT smoke path.
6. Existing contract coverage gaps from the handover are closed or explicitly moved to a
   later wave with approval.
7. Documentation and baseline files no longer claim RIE-backed numbers are the current
   reference.

## Risks

| Risk | Mitigation |
| --- | --- |
| Aspire Testing hides AOT/trim failures | Keep AOT verification as a separate LocalStack or deployed AWS path |
| WireMock pollutes AppHost | Keep WireMock test-owned unless a clean Aspire wrapper is proven |
| LocalStack API Gateway v2 automation is complex | Validate Function URL fallback, but only after event-shape checks |
| LocalStack benchmark memory is hard to attribute | Record local HTTP/artifact metrics and source memory truth from AWS REPORT data |
| Real GitHub benchmark credentials leak into app env | Put credentials only through seeder into LocalStack Secrets Manager |

## Implementation Planning Notes

The implementation plan should be ordered as follows:

1. Reconcile old RIE Task 10 artifacts and baseline files.
2. Add Aspire Testing fixture and migrate the first health/routing contract tests.
3. Move the rest of the contract matrix to the Aspire tier.
4. Remove RIE-specific clients and k6 modes after replacement coverage exists.
5. Build the LocalStack benchmark target and seeder flow.
6. Run the LocalStack Lambda image spike for RIE-free AOT smoke testing.
7. Update README, ROADMAP, and plan status only after verification matches the claims.

Task 12 from the old plan remains CI-gated. No workflow edits are included without fresh
approval.
