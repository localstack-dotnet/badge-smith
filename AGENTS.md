# Agent Instructions

Harness-neutral entry point for LLM/code agents working in BadgeSmith. It keeps always-on safety and
approval rules; task-specific guidance is loaded through the routing table. Documentation ownership
and lifecycle are defined in [`docs/engineering/documentation.md`](docs/engineering/documentation.md).

## Purpose

BadgeSmith is a high-performance, Shields.io-compatible badge service shipped as a .NET 10 Native
AOT AWS Lambda behind API Gateway and CloudFront, with DynamoDB and Secrets Manager. Cold-start
performance, AOT/trim safety, secure request handling, and predictable HTTP contracts are core
behavior. Aspire and LocalStack integrations are consumed for local development, not built here.

## Approval Gate

### Require Explicit Approval

- Start a feature, refactor production code, or apply a production-code bug fix not already
  requested with an action such as `go`, `apply`, `proceed`, `başla`, or `yap`.
- Change build or package behavior (`Directory.Build.props`, `Directory.Packages.props`, MSBuild,
  `.editorconfig` analyzer policy, `Dockerfile`, or `tools/badgesmith.cs`).
- Change CI/CD under `.github/workflows/**`.
- Change repository policy in `AGENTS.md` or `docs/engineering/` (including documentation,
  coding/testing style, quality, workflow, and deviation policy), or policy in any other location;
  or change approval gates, repository routing, discovery relays, or agent-integration behavior.
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
- After LLM-authored code, project, analyzer, or test changes, run Slopwatch when available;
  [quality gates](docs/engineering/quality-gates.md#slopwatch) owns the exact command and exclusions.
  `external/**` contains ignored upstream source checkouts, not BadgeSmith-owned code.

## Load Before Acting

Read the relevant canon before proposing or changing design. Source and tests define runtime
behavior; dated research and handovers may intentionally describe superseded states.

| Task | Read first |
| --- | --- |
| Product use, endpoints, or public examples | [`README.md`](README.md) and the [consumer guide](docs/guide/README.md) |
| Request handling, routing, HTTP results, or caching | [Runtime and routing](docs/architecture/runtime-and-routing.md) |
| Data/storage, HMAC, nonce, secrets, replay protection, or transport security | [Security and storage](docs/architecture/security-and-storage.md); review the exact protocol, threat model, and relevant tests before mutation |
| Topology, Native AOT, delivery, or performance policy | [Platform and delivery](docs/architecture/platform-and-delivery.md) |
| Hand-written C#, including code comments | [Coding style](docs/engineering/coding-style.md) |
| Test design, test code, or test naming | [Coding style](docs/engineering/coding-style.md) and [testing style](docs/engineering/testing-style.md) |
| SDK, analyzer, language, package, build, or validation policy | [Quality gates](docs/engineering/quality-gates.md), [`global.json`](global.json), [`.editorconfig`](.editorconfig), [`Directory.Build.props`](Directory.Build.props), [`Directory.Packages.props`](Directory.Packages.props), and affected project files |
| CLI commands, secret seeding, or Lambda build tooling | [`tools/README.md`](tools/README.md) |
| Production or local-performance CDK | The matching [production](build/BadgeSmith.CDK/README.md) or [local-performance](build/BadgeSmith.CDK.LocalPerformance/README.md) guide |
| Aspire/LocalStack contract tests or AppHost setup | [Local development](docs/engineering/local-development.md#apphost-and-contract-tests) and [`tests/BadgeSmith.Api.Tests/README.md`](tests/BadgeSmith.Api.Tests/README.md) |
| Failing tests or internals visibility | [Test triage](docs/engineering/local-development.md#test-triage) |
| Required table environment variables | [Required environment](docs/engineering/local-development.md#required-environment) |
| Collaboration, review, commits, or session handoffs | [Workflow](docs/engineering/workflow.md); [slop review](docs/engineering/slop-review.md) for review and narrow arbitration |
| Agent relays or discovery shims | [Agent-integration boundaries](docs/engineering/workflow.md#agent-integration-boundaries) |
| Runtime-tool visibility or version limits | [Runtime-tool boundaries](docs/engineering/local-development.md#runtime-tool-boundaries) |
| Aspire/AWS/LocalStack source compatibility | [Aspire source navigation](docs/engineering/aspire-source-navigation.md) |
| Documentation placement, refactoring, research, or ADRs | [Documentation](docs/engineering/documentation.md); [ADR convention](docs/adr/README.md) when a real decision needs a record |
| Domain terminology | [`GLOSSARY.md`](GLOSSARY.md) |
| Backlog, current work, or deferred decisions | [`docs/ROADMAP.md`](docs/ROADMAP.md); use dated research only as evidence |
| Canonical sources disagree | [Deviation protocol](docs/engineering/deviation-protocol.md) |

## Operating Style

- Be direct, practical, and clear. Challenge decisions instead of agreeing into bad architecture.
- Prefer the smallest correct change. Do not add compatibility code without a concrete consumer or
  persisted/shipped contract.
- Deniz communicates in Turkish and English interchangeably; respond in the language that best
  matches the current message.
- If implementation and current canon disagree, follow the deviation protocol instead of choosing a
  convenient winner.

## Documentation And Tooling

- Documentation refactors follow `docs/engineering/documentation.md`: relocate useful unique information before
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
