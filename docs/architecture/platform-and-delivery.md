# Platform And Delivery Architecture

Current rules for Lambda/AOT composition, infrastructure boundaries, build delivery, and performance
measurement. Read this document before changing deployment topology, production compilation flags,
artifact construction, or performance-goal claims. [Runtime and routing](runtime-and-routing.md)
owns cache and request contracts; [security and storage](security-and-storage.md) owns data,
authentication, and transport. Engineering [quality gates](../engineering/quality-gates.md) own
validation and evidence qualification, not a second set of performance targets.

## System Overview

BadgeSmith is a high-performance, secure badge service optimized for AWS Lambda cold starts and
separation of concerns. Its production request flow is:

```text
Client → CloudFront → API Gateway → Lambda → DynamoDB
                                         ↓
                                   Secrets Manager
```

- **CloudFront:** global edge caching with endpoint-specific TTLs, under the
  [origin-controlled cache contract](runtime-and-routing.md#cache-strategy).
- **API Gateway HTTP v2:** request routing and CORS handling.
- **Lambda:** .NET 10 Native AOT runtime.
- **DynamoDB:** NoSQL storage with optimized access patterns.
- **Secrets Manager:** secure credential storage.

## Native AOT And Composition

BadgeSmith prioritizes cold-start performance and deployment efficiency. Native AOT removes JIT
startup overhead and supports smaller self-contained artifacts, lower memory use, and a native
executable on Lambda's `provided.al2023` runtime. These are design mechanisms and motivations, not
guarantees that a deployed revision meets the [performance goals](#performance-goals).

- **Direct Lambda runtime integration:** the Lambda does not use an ASP.NET Core host.
- **Lambda-only composition:** `ApplicationRegistry`, with `Lazy<T>` service initialization,
  replaces a DI container in `src/BadgeSmith.Api`.
- **Lambda-only configuration:** required configuration is read directly from environment
  variables rather than through a configuration framework.
- **Source-generated JSON:** serialization uses source generators instead of reflection.

`PublishAot=true` is load-bearing. Avoid reflection, runtime code generation, and unannotated
trim-unsafe patterns; trim/AOT warnings are blocking runtime risks. Register every serialized type
in `LambdaFunctionJsonSerializerContext`: a successful compilation alone does not establish that
the Native AOT Lambda can serialize it.

The separate CLI intentionally uses `HostApplicationBuilder`, `IServiceCollection`,
`IConfiguration`, and constructor injection; the Aspire AppHost uses resource composition. Keep
these [composition boundaries](../engineering/coding-style.md#composition-boundaries) scoped to
their projects rather than normalizing them onto one model.

## Conditional Compilation

Ordinary builds default both properties to `true`:

```xml
<EnableTelemetry>true</EnableTelemetry>      <!-- Development: telemetry enabled -->
<EnableLocalStack>true</EnableLocalStack>    <!-- Development: LocalStack integration -->
```

The production Docker publish passes `-p:EnableTelemetry=false` and
`-p:EnableLocalStack=false`. Guarded `ENABLE_TELEMETRY` and `ENABLE_LOCALSTACK` code is absent from
the shipped Lambda: production removes telemetry dependencies and LocalStack client libraries to
reduce binary size and cold-start work.

The [`Dockerfile`](../../src/BadgeSmith.Api/Dockerfile) owns those publish properties.
`tools/badgesmith.cs lambda build` drives its Docker targets but does not independently redefine
the flags. Ordinary validation remains separate from this production publish path; see
[quality gates](../engineering/quality-gates.md).

## Development Tooling

`tools/badgesmith.cs` is the file-based .NET CLI for BadgeSmith-specific Lambda builds, test
execution and ingestion, badge updates, and secret seeding. The [tooling guide](../../tools/README.md)
owns exact commands, defaults, dry-run behavior, secret mapping, and cross-platform invocation.

The remaining `scripts/` fixtures are:

- [`k6-perf-test.js`](../../scripts/k6-perf-test.js): HTTP load testing with realistic traffic patterns.
- [`sample-test-payload.json`](../../scripts/sample-test-payload.json): example test-result payload
  for `tests ingest`.

Local setup belongs to [local development](../engineering/local-development.md); exact performance
harness invocation and limits belong to the
[test-project guide](../../tests/BadgeSmith.Api.Tests/README.md#performance-harnesses) and
[tooling guide](../../tools/README.md#performance-testing-k6).

## Infrastructure Boundaries

`build/` contains shared constructs and **two separate .NET CDK app entrypoints**:

- The production app constructs only `BadgeSmithStack`.
- The local-performance app constructs only `BadgeSmithPerformanceStack` and is **never deployed
  to AWS**.

Context configures each known app; it does not select topology. Do not reintroduce topology-selection
context. CDK constructs the whole app tree before CLI stack selectors apply. The
[separate-apps ADR](../adr/0002-separate-production-and-local-performance-cdk-apps.md) records the
accepted rationale and alternatives.

Production synth, diff, and deploy must target `BadgeSmithStack` explicitly; never use `--all` for
those operations. Deployment and AWS mutation remain separately approval-gated. The
[production](../../build/BadgeSmith.CDK/README.md) and
[local-performance](../../build/BadgeSmith.CDK.LocalPerformance/README.md) guides own project paths,
working directories, artifacts, context, and safe commands. Deferred performance-command
orchestration belongs to the [roadmap](../ROADMAP.md), not a current CLI guarantee.

## Local And Production Boundaries

Aspire provides local orchestration with LocalStack AWS-service emulation and local Lambda function
execution. Aspire-backed contract tests start `src/BadgeSmith.Host` and call `APIGatewayEmulator`
over HTTP; they do not use Lambda RIE. Their operational topology and startup/seeding rules belong
to [local development](../engineering/local-development.md#apphost-and-contract-tests) and the
[test-project guide](../../tests/BadgeSmith.Api.Tests/README.md#contract-tests).

Local benchmark infrastructure uses Docker, LocalStack, and CDK. Production keeps API Gateway HTTP
v2, while the local-performance stack also exposes a Lambda Function URL as the LocalStack workflow's
compatibility fallback. The observed limitation that introduced the fallback is preserved in the
dated [Iteration 0 and Wave 1 closeout](../research/2026-07-09-iteration0-wave1-closeout.md), not a
fresh compatibility guarantee.

HTTP harness behavior and published AOT artifact proof are separate boundaries. The
[HTTP/AOT separation ADR](../adr/0003-separate-http-contracts-from-aot-artifact-verification.md)
records the accepted decision; an HTTP contract-test result does not prove execution of the
published Native AOT artifact.

## Build And Delivery

The [`Dockerfile`](../../src/BadgeSmith.Api/Dockerfile) implements optimized multi-stage builds:

1. **Build stage:** .NET SDK with Native AOT compilation.
2. **Lambda image:** minimal runtime for container deployment.
3. **ZIP export:** artifact generation for ZIP deployment.

The ZIP is written with Python's standard library using standard zlib Deflate level 9.

`tools/badgesmith.cs lambda build` provides cross-platform automation for x64 and ARM64, ZIP artifacts
and container images, and Docker-backed publishing through the targets that own production
conditional-compilation flags. This publish path is not part of an ordinary `dotnet build` loop;
the [tooling guide](../../tools/README.md#lambda-build) owns its prerequisites and options.

Hosted action scopes and PR-versus-master validation/publication rules belong to
[quality gates](../engineering/quality-gates.md#hosted-validation). Exact triggers, runner images,
action versions, and deployment steps belong to the workflow files. The
[production CDK guide](../../build/BadgeSmith.CDK/README.md#ci-artifact-promotion) owns verified-artifact
promotion and separate approval-gated synth/deploy operations; the
[reusable action guide](../../.github/workflows/update-test-badge/README.md) owns consumer usage.

## Performance Goals

The following non-functional requirements are design targets, not statements that every
deployed revision already satisfies:

| Objective | Target | Measurement source |
| --- | --- | --- |
| Lambda initialization | CloudWatch `Init Duration` p95 ≤100 ms | Cold-start Lambda `REPORT` lines for the deployed production revision |
| Runtime memory | CloudWatch `Max Memory Used` p95 ≤50 MB | Lambda `REPORT` lines for the deployed production revision |
| Deployment size | Production ARM64 ZIP ≤6 MB (6,000,000 bytes) | Hosted CI artifact before deployment |

The initialization goal covers Lambda's CloudWatch `Init Duration`, not the total
latency of the first request. First-invoke AWS client, credential, TLS, and upstream work
is measured separately as effective cold-request duration. No numeric effective
cold-request target is set yet; report it alongside INIT so optimization does not merely
move latency between phases.

## Measurement Policy

Native AOT removes JIT compilation from Lambda startup, but cold-start latency and
memory usage must be measured for each deployed revision rather than treated as fixed
architecture guarantees. Dated measurements live under `docs/research/` and
`docs/research/baselines/`; CloudWatch Lambda `REPORT` lines are the source of truth for
production INIT duration, execution duration, and memory usage. The initial production
baseline and optimization plan are recorded in
[`2026-07-02-performance-opportunities.md`](../research/2026-07-02-performance-opportunities.md).

Goal verification uses this protocol:

- Measure the deployed production ARM64 revision on `provided.al2023`; record its commit,
  configured memory, deployment timestamp, and observation window. Do not mix samples
  from different deployment intervals.
- Use the most recent slice of up to 30 days wholly contained in one deployment interval,
  with at least 100 cold-start `REPORT` samples for `Init Duration`. If the interval has
  fewer samples, report the goal as unverified.
- Calculate each p95 with the nearest-rank method: sort the numeric samples ascending and
  select rank `ceil(0.95 × sample-count)`. Preserve the Logs Insights query or exported
  sample set with the result.
- Calculate `Max Memory Used` p95 over all `REPORT` samples in the same deployment slice,
  with at least 100 total samples.
- Measure the exact byte length of the ARM64 ZIP consumed by deployment; pre-deploy CI
  artifacts that are not deployed are validation inputs, not production results.

The 2026-07-02 baseline predates this percentile-based protocol. It remains useful
historical evidence but does not by itself establish pass/fail for these goals. Hosted ZIP build
and size validation do not establish executed AL2023 behavior, deployed identity, INIT, or memory.

For the public product story, see the [README](../../README.md); for supported endpoint use, see the
[consumer guide](../guide/README.md).
