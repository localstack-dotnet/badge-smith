# Agent Known Notes

Date: 2026-07-01

These notes are hints for agents during triage and review. They are not permission to
refactor unrelated code.

- **AOT/trim warnings are blocking.** Trim or AOT warnings emitted during `PublishAot`
  (via `tools/badgesmith.cs lambda build`) can turn into runtime failures in the deployed Lambda.
  Do not suppress them to get a green build.
- **JSON must be registered for source generation.** Every serialized type must be part
  of `LambdaFunctionJsonSerializerContext`. A missing registration compiles fine but
  fails only at runtime under Native AOT.
- **Conditional compilation hides code from production.** `ENABLE_TELEMETRY` and
  `ENABLE_LOCALSTACK` are defined for local development but disabled in production Docker
  builds. Code guarded by these constants (OpenTelemetry exporters, LocalStack client
  wiring) is absent from the shipped Lambda — do not assume it runs in production.
- **Tests use xUnit v3 on VSTest.** Run with plain `dotnet test`; standard `--filter`
  works. This is not TUnit and not Microsoft.Testing.Platform.
- **Internals are exposed to tests.** `BadgeSmith.Api` grants `InternalsVisibleTo` to
  `BadgeSmith.Api.Tests`, `BadgeSmith.Api.Performance.Tests`, and
  `DynamicProxyGenAssembly2` (Moq). Moved/renamed internals can break test compilation.
- **Local dev needs Docker.** LocalStack integration requires Docker. The Aspire AppHost
  (`src/BadgeSmith.Host`) starts LocalStack and provisions the CDK stack. In `Live` mode
  it runs `badgesmith secrets seed` and starts the Lambda only after that seeder is ready;
  `Mock` contract tests instead seed deterministic tables and secrets from their fixture
  after the distributed application starts.
- **Time is UTC-only.** `DateTime.Now` / `DateTimeOffset.Now` are banned via
  `BannedSymbols.txt`; HMAC timestamp and nonce logic depends on UTC. Use `DateTime.UtcNow`.
- **Env-var configuration is load-bearing.** Services read table names directly from
  `AWS_RESOURCE_ORG_SECRETS_TABLE`, `AWS_RESOURCE_NONCE_TABLE`, and
  `AWS_RESOURCE_TEST_RESULTS_TABLE`; missing values throw at service construction time.
