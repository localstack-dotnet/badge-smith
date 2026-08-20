# BadgeSmith.Api.Tests

The test project uses xUnit v3 on VSTest.

## Categories

- `Category=Unit`: in-process unit tests.
- `Category=Integration`: tests requiring Aspire, LocalStack, WireMock, or other infrastructure.
- `Category=Functional`: HTTP contract tests that exercise BadgeSmith routes.

`Category=AotContract` is reserved for a future RIE-free AOT artifact smoke tier. The Aspire-backed contract tests do not use this category.

## Contract Tests

Contract tests start `src/BadgeSmith.Host` through Aspire Testing and call `APIGatewayEmulator` over HTTP. They do not use Lambda RIE or the Lambda invocation endpoint.
LocalStack-backed tests require Docker; see the current [agent triage notes](../../docs/agents/KNOWN_ISSUES.md)
for seeding and startup details.

### Emulator Culture

`APIGatewayEmulator` is started with invariant/C culture in `src/BadgeSmith.Host` so header normalization matches API Gateway HTTP API v2. This prevents Turkish-culture lowercasing from converting `If-None-Match` to `ıf-none-match` and keeps the 304 contract tests executable locally.

## Performance Harnesses

BenchmarkDotNet microbenchmarks live in `tests/BadgeSmith.Api.Performance.Tests`. The k6
scenario under `scripts/` is an HTTP load harness, not a test category or contract test.
It sends requests directly to `K6_API_URL` (or the script's default endpoint); it does not
provision infrastructure or seed DynamoDB and Secrets Manager.

The local-performance CDK stack defines API Gateway resources and a Lambda Function URL. The
Function URL is the compatibility fallback for the LocalStack benchmark workflow; production uses
API Gateway HTTP v2. See the
[local-performance guide](../../build/BadgeSmith.CDK.LocalPerformance/README.md) for infrastructure
commands and the [tooling guide](../../tools/README.md#performance-testing-k6) for direct k6 usage.
