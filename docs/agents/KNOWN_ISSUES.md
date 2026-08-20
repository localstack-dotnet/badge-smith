# Agent Known Notes

These notes are hints for agents during triage and review. They are not permission to
refactor unrelated code. Universal AOT, test-runner, UTC, and quality constraints live
in `AGENTS.md`; this file keeps only details that do not belong in the always-on contract.

- **Internals are exposed to tests.** `BadgeSmith.Api` grants `InternalsVisibleTo` to
  `BadgeSmith.Api.Tests`, `BadgeSmith.Api.Performance.Tests`, and
  `DynamicProxyGenAssembly2` (Moq). Moved/renamed internals can break test compilation.
- **Local dev needs Docker.** LocalStack integration requires Docker. The Aspire AppHost
  (`src/BadgeSmith.Host`) starts LocalStack and provisions the CDK stack. In `Live` mode
  it runs `badgesmith secrets seed` and starts the Lambda only after that seeder is ready;
  `Mock` contract tests instead seed deterministic tables and secrets from their fixture
  after the distributed application starts.
- **Env-var configuration is load-bearing.** Services read table names directly from
  `AWS_RESOURCE_ORG_SECRETS_TABLE`, `AWS_RESOURCE_NONCE_TABLE`, and
  `AWS_RESOURCE_TEST_RESULTS_TABLE`; missing values throw at service construction time.
