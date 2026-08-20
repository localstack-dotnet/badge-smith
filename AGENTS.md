# Agent Instructions

Harness-neutral entry point for LLM/code agents working in BadgeSmith. It keeps always-on safety and
approval rules; task-specific guidance is loaded through the routing table. Documentation ownership
and lifecycle are defined in [`docs/README.md`](docs/README.md).

## Purpose

BadgeSmith is a high-performance, Shields.io-compatible badge service shipped as a .NET 10 Native
AOT AWS Lambda behind API Gateway and CloudFront, with DynamoDB and Secrets Manager. Cold-start
performance, AOT/trim safety, secure request handling, and predictable HTTP contracts are core
behavior. Aspire and LocalStack integrations are consumed for local development, not built here.

## Load Before Acting

| Task | Read first |
| --- | --- |
| Product behavior, endpoints, data/storage, topology, HMAC, caching, or performance policy | [`README.md`](README.md), then the relevant part of [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| Hand-written C#, including code comments, tests, and test naming | [`docs/engineering/coding-style.md`](docs/engineering/coding-style.md) |
| SDK, analyzer, language, package, or build policy | [`global.json`](global.json), [`.editorconfig`](.editorconfig), [`Directory.Build.props`](Directory.Build.props), [`Directory.Packages.props`](Directory.Packages.props), and the affected project files |
| CLI commands, secret seeding, or Lambda build tooling | [`tools/README.md`](tools/README.md) |
| Production or local-performance CDK | The matching [production](build/BadgeSmith.CDK/README.md) or [local-performance](build/BadgeSmith.CDK.LocalPerformance/README.md) guide |
| Aspire/LocalStack contract tests | [`tests/BadgeSmith.Api.Tests/README.md`](tests/BadgeSmith.Api.Tests/README.md) |
| Failing tests, internals visibility, local seeding, or required environment variables | [`docs/agents/KNOWN_ISSUES.md`](docs/agents/KNOWN_ISSUES.md) |
| Backlog, current work, or deferred decisions | [`docs/ROADMAP.md`](docs/ROADMAP.md); use dated research only as evidence |
| Agent relays, discovery shims, or runtime-tool boundaries | [`docs/agents/README.md`](docs/agents/README.md) |
| Aspire/AWS/LocalStack source compatibility | [`docs/agents/skills/aspire-source-navigation.md`](docs/agents/skills/aspire-source-navigation.md) |
| Documentation refactoring or placement | [`docs/README.md`](docs/README.md) |
| Canonical sources disagree | [`docs/agents/deviation-protocol.md`](docs/agents/deviation-protocol.md) |

Read the relevant canon before proposing or changing design. Source and tests define runtime
behavior; dated research and handovers may intentionally describe superseded states.

## Operating Style

- Be direct, practical, and clear. Challenge decisions instead of agreeing into bad architecture.
- Prefer the smallest correct change. Do not add compatibility code without a concrete consumer or
  persisted/shipped contract.
- Deniz communicates in Turkish and English interchangeably; respond in the language that best
  matches the current message.
- If implementation and current canon disagree, follow the deviation protocol instead of choosing a
  convenient winner.

## Approval Gate

### Require Explicit Approval

- Start a feature, refactor production code, or apply a production-code bug fix not already
  requested with an action such as `go`, `apply`, `proceed`, `başla`, or `yap`.
- Change build or package behavior (`Directory.Build.props`, `Directory.Packages.props`, MSBuild,
  `.editorconfig` analyzer policy, `Dockerfile`, or `tools/badgesmith.cs`).
- Change CI/CD under `.github/workflows/**`.
- Change repository policy in `AGENTS.md`, `docs/README.md`,
  `docs/engineering/coding-style.md`, or `docs/agents/deviation-protocol.md`; or change approval
  gates, repository routing, discovery relays, or agent-integration behavior.
- Weaken, skip, delete, or substantially rewrite tests to change verified behavior.
- Change CDK/infrastructure under `build/`, public HTTP routes, or response schemas.
- Run CDK deploy, Lambda publish/release builds, or any AWS mutation.
- Commit, amend, push, create a PR, or publish a release.

Production CDK operations must target `BadgeSmithStack` explicitly; never use `--all` for
production synth, diff, or deploy. The local-performance app must never be deployed to AWS.

### Allowed Without Additional Approval

- Read-only discovery, dry-run checks, diagnosis, and test-failure investigation.
- Documentation-only edits that do not alter repository policy, routing, approval gates, or harness
  behavior.
- Broken internal-link fixes and behavior-neutral comment improvements.

Test-code approval follows the behavior under change. Removing or weakening coverage always
requires approval. Diagnose bugs before proposing or applying production changes.

### Before Any Commit

Present a concise change summary and proposed Conventional Commit message, then ask for approval.
Use `feat`, `fix`, `docs`, `test`, `refactor`, `build`, `ci`, or `chore`; do not add AI attribution
trailers.

## Always-On Safety

These constraints prevent runtime-invisible Lambda failures and repository-wide quality regressions:

- `PublishAot=true` is load-bearing. Avoid reflection, runtime code generation, and unannotated
  trim-unsafe patterns. Treat trim/AOT warnings as blocking runtime risks.
- Register every serialized type in `LambdaFunctionJsonSerializerContext`; missing registrations
  can compile successfully and fail only in the Native AOT Lambda.
- The Lambda uses `ApplicationRegistry` with `Lazy<T>`, not a DI container, and reads required
  configuration directly from environment variables.
- That composition rule is scoped: `tools/` intentionally uses `HostApplicationBuilder`,
  `IServiceCollection`, and `IConfiguration`; `src/BadgeSmith.Host` uses Aspire composition. Do not
  normalize these projects onto the Lambda pattern.
- UTC is mandatory. Follow `BannedSymbols.txt`; do not use `DateTime.Now`, `DateTimeOffset.Now`, or
  `DateTimeOffset.DateTime`.
- `ENABLE_TELEMETRY` and `ENABLE_LOCALSTACK` exist in ordinary builds but are disabled by the
  production Docker publish. Guarded code is absent from the shipped Lambda.
- Keep the analyzer wall and warnings-as-errors clean. Narrowly arbitrate valid exceptions; never
  weaken analyzer policy globally to make a change pass.
- Tests use xUnit v3 + Moq on VSTest. Use plain `dotnet test` and standard `--filter`; this is not
  TUnit or Microsoft.Testing.Platform, and `--treenode-filter` is wrong here.
- Ordinary validation is restore/build/test for the affected scope. Native AOT publishing goes
  through `tools/badgesmith.cs lambda build` and is not part of a normal `dotnet build` loop.
- Package versions belong in `Directory.Packages.props`; do not add versions to individual project
  files.
- Treat HMAC, nonce, secrets, and replay-protection changes as security-sensitive. Review the exact
  protocol, threat model, and tests before mutation.
- After LLM-authored code, project, analyzer, or test changes, run Slopwatch when available:
  `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"`.
  `external/**` contains ignored upstream source checkouts, not BadgeSmith-owned code.

## Documentation And Tooling

- Documentation refactors follow `docs/README.md`: relocate useful unique information before
  deleting its source, and keep one canonical owner per current fact.
- `AGENTS.md` is canonical. `CLAUDE.md`, `.github/copilot-instructions.md`, and project discovery shims
  are discovery relays and cannot override it.
- Never commit secrets, OAuth tokens, personal machine paths, local MCP configuration, or personal
  OpenCode model-routing configuration.
- Use runtime-discovered tools when useful; committed documentation does not pin native skill,
  agent, plugin, model, or local-tool identifiers.

## Reviews

Lead with findings ordered by severity, with file/line references. If there are no findings, say so
and identify residual risk or unrun verification. For interactive reviews, present a recommended
option with effort, risk, impact, and maintenance cost, plus reasonable alternatives; confirm before
large changes.
