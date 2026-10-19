# Agent Instructions

Harness-neutral entry point for coding agents working in BadgeSmith: always-on rules plus a routing
table for task-specific guidance. Documentation ownership and lifecycle are defined in
[`docs/engineering/documentation.md`](docs/engineering/documentation.md).

## Purpose

BadgeSmith is a high-performance, Shields.io-compatible badge service shipped as a .NET 10 Native
AOT AWS Lambda behind API Gateway and CloudFront, with DynamoDB and Secrets Manager. Cold-start
performance, AOT/trim safety, secure request handling, and predictable HTTP contracts are core
behavior. Aspire and LocalStack integrations are consumed for local development, not built here.

## Autonomy And Approval

Work toward the goal of the request and carry it through to verified completion. Make the code,
test, build, CI, CDK, and documentation changes the goal needs without stopping to ask, and call
out anything beyond the obvious scope in your summary. Answer a question or review request without
editing unless it asks for the fix. Diagnose a bug before changing production code for it.

Ask first only before destructive or outward-facing actions. A request that names the action is the
approval. When you reach one, finish the rest of the work first and ask once at the end. A plan,
handoff, or tool's presence is not that approval.

- CDK deploy, Lambda publish, or any other AWS mutation.
- Push, create a PR, or publish a release.
- Amend or rewrite Git history, discard uncommitted changes you did not make, or delete files
  unrelated to the task.
- Delete, skip, or weaken a test, or change what an existing test asserts, unless the request asked
  for the behavior change that test covers.
- Loosen a rule in this section or in Always-On Safety.

Production CDK operations must target `BadgeSmithStack` explicitly; never use `--all` for
production synth, diff, or deploy. The local-performance app must never be deployed to AWS.

Commit only when the request asks for it; otherwise end with a concise change summary and a
proposed Conventional Commit message. Use `feat`, `fix`, `docs`, `test`, `refactor`, `build`, `ci`,
or `chore`, and do not add AI attribution trailers.

## Always-On Safety

These constraints prevent runtime-invisible Lambda failures and repository-wide quality regressions:

- `PublishAot=true` is load-bearing. Avoid reflection, runtime code generation, and unannotated
  trim-unsafe patterns. Treat trim/AOT warnings as blocking runtime risks.
- Register every serialized type in `LambdaFunctionJsonSerializerContext`; missing registrations
  can compile successfully and fail only in the Native AOT Lambda.
- The Lambda uses `ApplicationRegistry` with `Lazy<T>`, not a DI container, and reads required
  configuration directly from environment variables. `tools/` (DI and `IConfiguration`) and
  `src/BadgeSmith.Host` (Aspire) compose differently on purpose; do not normalize them.
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

Read the relevant canon before changing design. Source and tests define runtime behavior; dated
research and handovers may intentionally describe superseded states.

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
| Reviews, commits, or session handoffs | [Workflow](docs/engineering/workflow.md); [slop review](docs/engineering/slop-review.md) for review reporting and narrow arbitration |
| Agent relays or discovery shims | [Agent-integration boundaries](docs/engineering/workflow.md#agent-integration-boundaries) |
| Runtime-tool visibility or version limits | [Runtime-tool boundaries](docs/engineering/local-development.md#runtime-tool-boundaries) |
| Aspire/AWS/LocalStack source compatibility | [Aspire source navigation](docs/engineering/aspire-source-navigation.md) |
| Documentation placement, refactoring, research, or ADRs | [Documentation](docs/engineering/documentation.md); [ADR convention](docs/adr/README.md) when a real decision needs a record |
| Domain terminology | [`GLOSSARY.md`](GLOSSARY.md) |
| Backlog, current work, or deferred decisions | [`docs/ROADMAP.md`](docs/ROADMAP.md); use dated research only as evidence |
| Implementation and canon, or two canonical sources, disagree | [Deviation protocol](docs/engineering/deviation-protocol.md) |

## Operating Style

- Challenge decisions instead of agreeing into bad architecture.
- Prefer the smallest correct change. Do not add compatibility code without a concrete consumer or
  persisted/shipped contract.
- Deniz communicates in Turkish and English interchangeably; respond in the language that best
  matches the current message.

## Repository Hygiene

- `AGENTS.md` is canonical; relay files point here and cannot override it.
- Never commit secrets, OAuth tokens, personal machine paths, or personal harness, MCP, or
  model-routing configuration.
- Committed documentation does not pin harness-specific skill, agent, plugin, model, or tool
  identifiers.
