# Local Development

This guide owns AppHost operation boundaries and unique current triage notes. It is not permission
to refactor unrelated code or change setup, infrastructure, or production resources. Universal
approval, AOT, UTC, runner, and analyzer constraints remain in [AGENTS.md](../../AGENTS.md).

## AppHost And Contract Tests

LocalStack integration requires Docker. `src/BadgeSmith.Host` uses Aspire to start LocalStack,
provision the CDK resources, and expose the local Lambda through `APIGatewayEmulator`.
[README local development](../../README.md) provides the first-run path; exact mapping format,
seeding options, secret-name rules, and file-based CLI invocation (including Windows) live in the
[tooling guide](../../tools/README.md).

The mode contract is owned by
[security and storage](../architecture/security-and-storage.md#upstream-and-transport-modes):

- **Live** is the default. The AppHost requires local `tools/organization-pat-mapping.json` and
  fails before startup if it is missing. Copy the tracked `.dist` template and provide local
  `Package` and `TestData` secrets; keep the populated mapping uncommitted. Live configured upstreams
  require HTTPS.
- In Live mode the AppHost invokes `badgesmith secrets seed` and declares
  `badgeSmithApi.WaitFor(dynamoDbSeeder)`: seeding is an explicit Lambda startup dependency. This
  source declaration is not evidence of a successful startup or a guarantee that a new seed run
  completed; inspect the actual seeder/test logs when diagnosing readiness.
- **Mock** contract tests provide both `HTTP_NUGET_BASE_URL` and `HTTP_GITHUB_BASE_URL` for
  test-owned WireMock endpoints. Mock is accepted only in builds with `ENABLE_LOCALSTACK`.
  The fixture seeds deterministic tables and secrets **after** the distributed application starts;
  it does not use the Live mapping/seeder path. Production sets Live and rejects Mock.

Aspire Testing starts this AppHost in-process through `DistributedApplicationTestingBuilder`.
Contract tests call `APIGatewayEmulator` over ordinary HTTP, not Lambda RIE or the Lambda invocation
endpoint. This is HTTP contract evidence, not execution of the published Native AOT artifact.
`Category=AotContract` remains reserved for a future RIE-free artifact smoke tier. The
[test guide](../../tests/BadgeSmith.Api.Tests/README.md#contract-tests) owns categories and exact
project execution details. It also explains the emulator's invariant/C culture, which keeps header
normalization consistent with API Gateway HTTP API v2 under Turkish culture.

Local benchmark infrastructure uses Docker, LocalStack, and CDK. The local-performance stack
defines API Gateway resources and also exposes a Lambda Function URL as the LocalStack workflow's
compatibility fallback; production keeps API Gateway HTTP v2. The observed limitation behind that
fallback is dated [Iteration 0 / Wave 1 evidence](../research/2026-07-09-iteration0-wave1-closeout.md),
not a newly verified statement about current upstream capabilities. Exact app working directories,
artifacts, and safe commands live in the [local-performance guide](../../build/BadgeSmith.CDK.LocalPerformance/README.md).
That app must never be deployed to AWS; production operations use the
[production guide](../../build/BadgeSmith.CDK/README.md) and explicit `BadgeSmithStack` targeting.

## Runtime-Tool Boundaries

The documented official Aspire runtime-tool boundary is AppHosts launched through the Aspire CLI.
Those tools do not see the in-process `DistributedApplicationTestingBuilder` AppHosts used by
contract tests; use test logs and the debugger for those sessions. This documents the existing
operational limitation, not a fresh capability check of whichever tool is installed now.

Runtime-discovered tools may describe a newer Aspire version than BadgeSmith consumes. Installed
tool availability or advice does not establish compatibility: use the
[verified package-matching source workflow](aspire-source-navigation.md) for concrete
Aspire/AWS/LocalStack compatibility conclusions. Missing tools do not authorize setup, replacement
installs, or weaker checks; [workflow](workflow.md#agent-integration-boundaries) owns that boundary.

## Test Triage

These notes support diagnosis and review; they do not authorize unrelated refactoring.
`BadgeSmith.Api` grants `InternalsVisibleTo` to `BadgeSmith.Api.Tests`,
`BadgeSmith.Api.Performance.Tests`, and `DynamicProxyGenAssembly2` (Moq). Moving or renaming internals
can break test compilation; the API project file owns the exact declarations.
[Testing style](testing-style.md#contracts-and-seams) keeps tests on public/internal contracts,
rather than private reflection. For infrastructure failures, check Docker, mode-specific seed
ownership above, required table variables below, and the relevant test/seeder logs before proposing
a production-code change.

## Required Environment

Services read table names directly from:

- `AWS_RESOURCE_ORG_SECRETS_TABLE`
- `AWS_RESOURCE_NONCE_TABLE`
- `AWS_RESOURCE_TEST_RESULTS_TABLE`

A missing value throws when the corresponding service is constructed. Registry services are lazy;
this is not eager validation of all three variables at process startup. The AppHost wires table
outputs to those variables. Do not replace the Lambda's direct environment/registry composition
with the CLI's Host/DI/configuration model; [composition boundaries](coding-style.md#composition-boundaries)
owns the distinction.

## Local Tooling And Fixtures

`tools/badgesmith.cs` owns Lambda builds, test execution and ingestion, badge updates, and secret
seeding. `scripts/k6-perf-test.js` is the HTTP load fixture and `scripts/sample-test-payload.json`
is an example ingestion payload; neither is a new startup/provisioning mechanism. See the
[tooling guide](../../tools/README.md#performance-testing-k6) and
[performance harnesses](../../tests/BadgeSmith.Api.Tests/README.md#performance-harnesses) for exact
operations. Set `K6_API_URL` explicitly for local work: omitting it uses the production endpoint.
