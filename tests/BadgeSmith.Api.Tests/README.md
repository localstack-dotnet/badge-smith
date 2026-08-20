# BadgeSmith.Api.Tests

The test project uses xUnit v3 on VSTest.

## Categories

- `Category=Unit`: in-process unit tests.
- `Category=Integration`: tests requiring Aspire, LocalStack, WireMock, or other infrastructure.
- `Category=Functional`: HTTP contract tests that exercise BadgeSmith routes.

`Category=AotContract` is reserved for a future RIE-free AOT artifact smoke tier. The Aspire-backed contract tests do not use this category.

## Contract Tests

Contract tests start `src/BadgeSmith.Host` through Aspire Testing and call `APIGatewayEmulator` over HTTP. They do not use Lambda RIE or the Lambda invocation endpoint.

### Emulator Culture

`APIGatewayEmulator` is started with invariant/C culture in `src/BadgeSmith.Host` so header normalization matches API Gateway HTTP API v2. This prevents Turkish-culture lowercasing from converting `If-None-Match` to `ıf-none-match` and keeps the 304 contract tests executable locally.

## Performance Harnesses

BenchmarkDotNet microbenchmarks live in `tests/BadgeSmith.Api.Performance.Tests`. The k6
scenario under `scripts/` is an HTTP load harness, not a test category or contract test.
Local k6 runs target LocalStack and seed DynamoDB plus Secrets Manager before invoking
package routes.

LocalStack Community 4.6 does not deploy API Gateway v2 resources through CloudFormation in this workflow, so the local CDK performance stack exposes a Lambda Function URL fallback. The production stack still uses API Gateway HTTP v2.
