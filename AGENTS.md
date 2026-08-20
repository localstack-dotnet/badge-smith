# Agent Instructions

Harness-neutral entry point for LLM/code agents working in BadgeSmith. Keep this file focused on
rules that must be known before task-specific context is loaded; detailed current guidance is
reached through the routing table below. Documentation ownership and lifecycle are defined in
[`docs/README.md`](docs/README.md).

## Purpose

BadgeSmith is a high-performance, Shields.io-compatible badge service shipped as a .NET 10 Native
AOT AWS Lambda behind API Gateway and CloudFront, with DynamoDB and Secrets Manager. It consumes
Aspire and LocalStack integrations for local development; it does not build or publish them.
Cold-start performance, AOT/trim safety, secure request handling, and predictable HTTP contracts
are core behavior.

## Load Before Acting

| Task | Read first |
| --- | --- |
| Product behavior, endpoints, topology, HMAC, caching, or performance policy | [`README.md`](README.md), then the relevant part of [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| Hand-written C# | [`docs/engineering/coding-style.md`](docs/engineering/coding-style.md) |
| Analyzer, language, package, or build policy | [`.editorconfig`](.editorconfig), [`Directory.Build.props`](Directory.Build.props), [`Directory.Packages.props`](Directory.Packages.props), and the affected project files |
| CLI commands, secret seeding, or Lambda build tooling | [`tools/README.md`](tools/README.md) |
| Production or local-performance CDK | The matching guide under [`build/`](build/) |
| Aspire/LocalStack contract tests | [`tests/BadgeSmith.Api.Tests/README.md`](tests/BadgeSmith.Api.Tests/README.md) |
| Failing tests, internals visibility, local seeding, or required environment variables | [`docs/agents/KNOWN_ISSUES.md`](docs/agents/KNOWN_ISSUES.md) |
| Backlog, current work, or deferred decisions | [`docs/ROADMAP.md`](docs/ROADMAP.md); use dated research only as evidence |
| Agent relays, discovery shims, or runtime-tool boundaries | [`docs/agents/README.md`](docs/agents/README.md) |
| Aspire/AWS/LocalStack source compatibility | [`docs/agents/skills/aspire-source-navigation.md`](docs/agents/skills/aspire-source-navigation.md) |
| Documentation refactoring or placement | [`docs/README.md`](docs/README.md) |
| Canonical sources disagree | [`docs/agents/deviation-protocol.md`](docs/agents/deviation-protocol.md) |

Read the relevant canon before proposing or changing design. Source and tests define runtime
behavior; current documentation explains intent and constraints. Dated research and handovers may
intentionally describe superseded states.

## Operating Style

- Be direct, practical, and clear. Challenge decisions instead of agreeing into bad architecture.
- Prefer the smallest correct change. Do not add compatibility code without a concrete consumer or
  persisted/shipped contract.
- Deniz communicates in Turkish and English interchangeably; respond in the language that best
  matches the current message.
- If implementation and current canon disagree, stop the affected work and follow the deviation
  protocol. Never pick a convenient winner silently.

## Approval Gate

### Require Explicit Approval

- Start a feature, refactor production code, or apply a production-code bug fix not already
  requested with an action such as `go`, `apply`, `proceed`, `başla`, or `yap`.
- Change build or package behavior (`Directory.Build.props`, `Directory.Packages.props`, MSBuild,
  `.editorconfig` analyzer policy, `Dockerfile`, or `tools/badgesmith.cs`).
- Change CI/CD under `.github/workflows/**`.
- Change agent policy, approval gates, repository routing, discovery relays, agent-integration
  behavior, or documentation authority/lifecycle policy in `docs/README.md`.
- Weaken, skip, delete, or substantially rewrite tests to change verified behavior.
- Change CDK/infrastructure under `build/`, public HTTP routes, or response schemas.
- Run CDK deploy, Lambda publish/release builds, or any AWS mutation.
- Commit, amend, push, create a PR, or publish a release.

Production CDK operations must target `BadgeSmithStack` explicitly; never use `--all` for
production synth, diff, or deploy. The local-performance app must never be deployed to AWS.

### Allowed Without Additional Approval

- Read-only discovery, dry-run checks, diagnosis, and test-failure investigation.
- Documentation-only edits that do not alter agent policy, routing, approval gates, or harness
  behavior.
- Broken internal-link fixes and behavior-neutral comment improvements.

Test-code approval follows the behavior under change. Removing or weakening coverage always
requires approval. Diagnose bugs before proposing or applying production changes.

### Before Any Commit

Present a concise change summary and proposed Conventional Commit message, then ask for approval.
Use `feat`, `fix`, `docs`, `test`, `refactor`, `build`, `ci`, or `chore`; do not add AI attribution
trailers.

## Sources Of Truth

Read change-prone facts from their mechanical owners instead of copying them into prose:

- SDK and roll-forward policy: `global.json`
- Target framework, language/analysis level, warnings-as-errors, and CPM enablement: `Directory.Build.props`
- Diagnostic severities and scoped analyzer arbitration: `.editorconfig`
- Package versions: `Directory.Packages.props`
- Banned APIs: `BannedSymbols.txt`
- Package metadata and references: project files under `src/`, `build/`, and `tests/`
- Executed CI/CD behavior and pinned Node/CDK CLI versions: `.github/workflows/**`
- Runtime behavior: source and tests first, then `ARCHITECTURE.md` and `README.md`
- Backlog and current operational state: `docs/ROADMAP.md`

Repository files and current canon beat memory, transient plans, dated research, and handovers.

## Native AOT And Lambda Safety

These constraints apply to `src/BadgeSmith.Api` and its production artifact:

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
- Prefer OneOf result types over exceptions for expected failures.

## Quality And Tests

- Keep the analyzer wall and warnings-as-errors clean. Narrowly arbitrate valid exceptions; never
  weaken analyzer policy globally to make a change pass.
- Tests use xUnit v3 + Moq on VSTest. Use plain `dotnet test` and standard `--filter`; this is not
  TUnit or Microsoft.Testing.Platform, and `--treenode-filter` is wrong here.
- Test names use `Subject_Should_Expected_Behavior_When_Condition`. Keep code identifiers intact;
  underscore other words, put `Should` immediately after the subject, and end conditions with
  `When...`.
- Ordinary validation is restore/build/test for the affected scope. Native AOT publishing goes
  through `tools/badgesmith.cs lambda build` and is not part of a normal `dotnet build` loop.
- Package versions belong in `Directory.Packages.props`; do not add versions to individual project
  files. LocalStack-backed work requires Docker.
- Data access uses DynamoDB and Secrets Manager through the AWS SDK. Do not introduce EF or SQL
  patterns into this architecture.
- Treat HMAC, nonce, secrets, and replay-protection changes as security-sensitive. Review the exact
  protocol, threat model, and tests before mutation.
- After LLM-authored code, project, analyzer, or test changes, run Slopwatch when available:
  `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"`.
  `external/**` contains ignored upstream source checkouts, not BadgeSmith-owned code.
- Documentation-only changes do not require build/test unless they change commands or technical
  claims that need mechanical validation; always validate affected links.

## Documentation Discipline

- Preserve useful unique information during refactors by relocating it before deleting the old
  copy. Shorter is not better if context, rationale, safety, or provenance is lost.
- Give each current fact one canonical owner; other mentions are audience-specific summaries or
  brief relays. Follow `docs/README.md` for document roles and lifecycle.
- Research is dated evidence, not current policy. Plans are active-only. Keep `docs/ROADMAP.md`
  current and concise.
- Code comments and XML docs must be self-contained. Do not cite plans, ADRs, specs, or external
  file paths from code comments.
- `AGENTS.md` is canonical. `CLAUDE.md`, `.github/copilot-instructions.md`, and project discovery shims
  are discovery relays and cannot override it.
- Never commit secrets, OAuth tokens, personal machine paths, local MCP configuration, or personal
  OpenCode model-routing configuration.

## Runtime Tooling

Use runtime-discovered tools when they materially help the task; committed documentation does not
pin native skill, agent, plugin, or model identifiers. Repository canon controls behavior and
quality regardless of which optional tooling is available.

For compatibility-sensitive Aspire, AWS, or LocalStack work, follow the repository's
[upstream-source workflow](docs/agents/skills/aspire-source-navigation.md). Official Aspire runtime
tooling is for CLI-launched AppHost lifecycle, resources, logs, and traces. It cannot see the
in-process AppHosts used by contract tests.

## Semantic Navigation

When Rider MCP is connected to this solution, prefer semantic symbol, call, dependency, and
diagnostic tools for C#. If Rider is absent or open on another solution, use the harness LSP before
text search. Use text search for docs, manifests, comments, and literal strings. On-disk files are
the source of truth before edits.

## Reviews

Lead with findings ordered by severity, with file/line references. If there are no findings, say so
and identify residual risk or unrun verification. For interactive reviews, present a recommended
option with effort, risk, impact, and maintenance cost, plus reasonable alternatives; confirm before
large changes.
