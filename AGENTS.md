# Agent Instructions

Operating rules for LLM/code agents working in this repository.

## Purpose And Scope

This repository builds **BadgeSmith**, a high-performance, [Shields.io](https://shields.io)-compatible
badge service for NuGet packages, GitHub packages, and CI/CD test results.

BadgeSmith ships as a **.NET 10 Native AOT AWS Lambda application** deployed with
**AWS CDK**, fronted by API Gateway and CloudFront, backed by DynamoDB and Secrets
Manager. It *consumes* `LocalStack.Aspire.Hosting` and `Aspire.Hosting.AWS` for local
development only; it does not build or publish those packages. Cold-start performance,
AOT/trim safety, and predictable request handling are core project behavior.

`AGENTS.md` is the compact, harness-independent contract. Harness-specific files and
relays (`CLAUDE.md`, `.github/copilot-instructions.md`, `docs/agents/README.md`) are
adapters, not policy sources.

## Operating Style

- Be direct, practical, and clear.
- Challenge decisions when needed; do not yes-person your way into bad architecture.
- Prefer small correct changes over broad refactors.
- Deniz communicates in Turkish and English interchangeably; respond in the language
  that best matches the current message.
- For non-trivial work, inspect relevant docs and code before acting. Runtime behavior
  comes from source and tests first, then `ARCHITECTURE.md`/`README.md`.

## Approval Gate

### Do Not Without Explicit Approval

- Start coding a new feature.
- Refactor production code.
- Fix production-code bugs unless Deniz has explicitly asked you to fix, apply,
  proceed, or equivalent.
- Modify build system behavior (`Directory.Build.props`, `Directory.Packages.props`,
  MSBuild, `Dockerfile`, `tools/badgesmith.cs`).
- Change CI/CD pipelines (`.github/workflows/**`).
- Change agent policy, approval gates, capability routing, skill triggers, or harness
  adapter behavior.
- Weaken, skip, delete, or substantially rewrite tests to change what behavior is
  verified.
- Run CDK deploy, Lambda publish, `tools/badgesmith.cs lambda build` release, or any
  AWS mutation.
- Commit, amend, push, or create a PR.

Approval phrases include `go`, `apply`, `proceed`, `başla`, and `yap`.

### Allowed Without Additional Approval

- Documentation-only edits that do not change agent policy, approval gates, capability
  routing, skill triggers, or harness adapter behavior.
- Broken internal link fixes.
- Minor comment improvements that do not change behavior.
- Read-only discovery commands and dry-run checks.
- Bug diagnosis and test-failure investigation before proposing or applying a
  production-code fix.

### Before Any Commit

Present a concise change summary and proposed Conventional Commit message, then ask for
approval.

Commit messages must use `feat`, `fix`, `docs`, `test`, `refactor`, `build`, `ci`, or
`chore`. Do not add AI attribution trailers.

## First Decision Flow

- Read-only question: inspect the relevant docs/code and answer directly.
- Documentation-only edit: allowed when it does not alter agent policy, approval gates,
  capability routing, skill triggers, or harness adapter behavior; update the natural
  existing doc rather than creating a new one by default.
- Bug report or failing test: diagnose first with systematic debugging and the relevant
  domain capability; require approval before production-code edits unless already
  requested.
- Test-code changes: approval follows the behavior under change; weakening or removing
  coverage always requires explicit approval.
- New feature, refactor, build change, CI change, package/version change, deployment
  task, or public HTTP-contract change (route shape, response schema): require explicit
  approval before mutation.
- Native AOT / trimming / serialization-sensitive change: use the AOT and Lambda
  constraints below plus the relevant .NET capability before acting.
- CDK / infrastructure change (`build/**`): treat as build-system change; require
  approval; deployment remains approval-gated.
- Agent-instruction or skill-routing docs: treat changes as policy/infrastructure
  edits; require approval unless the task explicitly asks for them.
- Review request: use a code-review mindset; findings come first, ordered by severity,
  with file/line references when possible.

## Project Sources Of Truth

Read change-prone facts from their source files instead of copying them here:

- SDK and roll-forward policy: `global.json`
- Target framework, analyzers, warnings-as-errors, and Central Package Management:
  `Directory.Build.props`
- Package versions: `Directory.Packages.props`
- Banned APIs: `BannedSymbols.txt`
- Package metadata and project references: project files under `src/`, `build/`, and
  `tests/`
- Runtime behavior: source code and tests first, then `ARCHITECTURE.md` and `README.md`
- Backlog and progress: `docs/ROADMAP.md`

Repository layout:

- `src/BadgeSmith.Api`: the Native AOT Lambda (routing, features, security, caching,
  observability)
- `src/BadgeSmith.Host`: .NET Aspire AppHost for local development (LocalStack, Lambda
  and API Gateway emulation, DynamoDB seeding)
- `src/shared`: constants, ActivitySources, and canonical security helpers shared via
  linked compilation
- `build/`: AWS CDK shared constructs plus separate production and local-performance
  apps; see `build/BadgeSmith.CDK/README.md`
- `tests/BadgeSmith.Api.Tests`: xUnit v3 unit and Aspire/LocalStack functional contract
  tests
- `tests/BadgeSmith.Api.Performance.Tests`: BenchmarkDotNet benchmarks
- `tools/`: file-based `badgesmith` CLI (Lambda build, test run/ingest, badge update,
  secrets seed); see `tools/README.md`
- `scripts/`: remaining k6 load-test scenario and sample ingestion payload
- `docs/`: project documentation
- `docs/agents/`: harness adapter guide, capability mapping, and known agent notes

## Documentation Hygiene

- Update docs when behavior, topology, endpoints, or harness expectations change.
- Prefer consolidation over new docs when an existing doc is the natural home.
- Research docs must include a date.
- Keep `docs/ROADMAP.md` current as the backlog and progress source. Link detailed
  workstream plans from `docs/plans/` when they land.
- Code comments and XML docs must be self-contained; do not reference specs, plans,
  phases, or external file paths from code comments.

## Harness Independence

- `AGENTS.md` is the canonical repository contract.
- Harness-specific instructions and discovery relays are adapters, not policy sources.
  Canonical capability guides may live under `docs/agents/skills/`, but they cannot
  override this contract.
- `CLAUDE.md` and `.github/copilot-instructions.md` are relay-only; OpenCode reads
  `AGENTS.md` natively.
- Harness-native invocation names, LSP wiring, local-only setup notes, and skill
  maintenance live in `docs/agents/README.md`.
- Never commit secrets, OAuth tokens, personal machine paths, local MCP config, or
  personal OpenCode model-routing config.

## Native AOT And Lambda Constraints

BadgeSmith's Lambda (`src/BadgeSmith.Api`) publishes with `PublishAot=true`. Respect
these constraints on every code change:

- **AOT/trim safety.** Avoid reflection-based patterns, dynamic code, and
  unannotated generics that break trimming. Trim/AOT warnings during `PublishAot`
  can become runtime failures — treat them as blocking.
- **Source-generated JSON.** All serialized types must be registered in the System.Text.Json
  source-generation context (`LambdaFunctionJsonSerializerContext`). A missing
  registration fails only at runtime under AOT.
- **No dependency injection.** Services resolve through the centralized
  `ApplicationRegistry` with `Lazy<T>`; there is no DI container.
- **No configuration framework.** Configuration is read directly from environment
  variables (for example `AWS_RESOURCE_*` table names).
- **UTC time only.** `DateTime.Now`, `DateTimeOffset.Now`, and `DateTimeOffset.DateTime`
  are banned via `BannedSymbols.txt`; use `DateTime.UtcNow`, `DateTimeOffset.UtcNow`,
  and `DateTimeOffset.UtcDateTime`.
- **Conditional compilation.** `ENABLE_TELEMETRY` and `ENABLE_LOCALSTACK` are on for
  local development but disabled in production Docker builds; code guarded by these
  constants is absent from the shipped Lambda.
- **Result pattern.** Prefer OneOf result types over exceptions for expected failures.

## Quality Notes

- Use the standard .NET restore/build/test flow appropriate to the files changed.
- Tests are **xUnit v3 + Moq on VSTest** (`Microsoft.NET.Test.Sdk` +
  `xunit.runner.visualstudio`). Plain `dotnet test --project
  tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj` and standard `--filter` are
  correct. This is NOT TUnit — ignore any `--treenode-filter` guidance.
- Test and benchmark method names use `Subject_Should_Expected_Behavior_When_Condition`.
  Keep real code identifiers such as method, property, type, header, and route names
  intact; separate all other human-readable words with underscores. `Should` belongs
  immediately after the subject, and scenario/input conditions belong at the end with a
  `When...` suffix.
- Native AOT publishing goes through `tools/badgesmith.cs lambda build` (multi-arch ZIP /
  container targets); it is not part of the ordinary `dotnet build` loop.
- Strict analyzers and warnings-as-errors are enabled through shared project
  configuration; keep the zero-warning bar.
- Package versions live in `Directory.Packages.props` (Central Package Management); do
  not hand-edit package versions into individual project files.
- Integration and LocalStack-backed work requires Docker.
- Documentation-only changes do not require build/test unless they add or change
  commands that should be validated.
- If Slopwatch is available after LLM-authored code, project, or test changes, run
  `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"`.
  The existing baseline lives under `.slopwatch/`; `external/**` is excluded because it
  contains ignored upstream source checkouts for source navigation, not BadgeSmith-owned code.

## Capability Routing

Use capabilities, not memorized harness names. Resolve the harness-native invocation
from `docs/agents/README.md` before invoking a skill or specialist agent.

If a bootstrap or process skill is already injected by the harness, follow it
immediately; use `docs/agents/README.md` to map additional capabilities, not to delay
the active process workflow.

Capability tiers:

- **Tier 0**: process discipline; follow when injected by the harness.
- **Tier 1**: required when triggered for this repo's .NET / Native AOT / Aspire-local
  work and available in the harness.
- **Tier 2**: optional by judgment; use when it materially improves correctness,
  safety, test quality, or diagnostics.
- **Tier 3**: local-only convenience; use when present, never assume fresh checkouts
  have it.
- **Out of scope**: do not use unless this repo adds that technology or Deniz
  explicitly asks.

The full capability-to-harness mapping and the curated skill roster live in
`docs/agents/README.md`. Availability is not activation: except for a harness-injected
process bootstrap, skills do not run automatically — invoke the mapped capability when
its trigger applies, and do not invent an ID.

### Critical Routing

| Trigger | Preferred capability |
| --- | --- |
| Serialization / JSON context / AOT-trim-sensitive change | .NET serialization + type-design-performance capability, plus the AOT constraints above |
| Ordinary C# changes under `src/` or `tests/` | Relevant .NET coding-standards / concurrency capability |
| Lambda handler, routing engine, or CORS work | Relevant .NET capability; validate against `ARCHITECTURE.md` routing design |
| Security: HMAC, nonce, secrets, replay protection | Relevant .NET capability; treat as security-sensitive, review before mutation |
| DynamoDB / Secrets Manager access | Relevant .NET capability (no EF/SQL skills — this is AWS SDK) |
| CDK / infrastructure under `build/` | Treat as build-system change; approval-gated |
| Local Aspire orchestration (`src/BadgeSmith.Host`) | Aspire configuration capability |
| Running or filtering tests | .NET test-running capability (xUnit v3 on VSTest) |
| Performance work / benchmarks | Benchmark and performance-diagnostics capabilities; require measured data |
| Package version changes (`Directory.Packages.props`) | Package-management capability (CPM) |

## Aspire Source Compatibility

For read-only explanation questions, inspect this repository's docs/code first. Invoke `aspire-source-navigation` only when the answer depends on upstream internals, version-specific API shape, or a compatibility conclusion.

## Aspire MCP Server

Utilize Aspire MCP server for runtime resource state/logs/traces of CLI-launched AppHosts and LocalStack. And context7 for Aspire related documentation.

## Semantic Code Navigation

When Rider MCP tools are available, prefer semantic tools for C# symbol questions:

- Declarations and symbol meaning: `search_symbol`, `get_symbol_info`
- File analysis: `get_file_problems`
- Solution/project shape: `get_solution_projects`, `get_project_dependencies`
- Renames and type moves: semantic refactoring tools when approval allows mutation

When Rider is not running, use the current harness's headless LSP for the same symbol
questions before falling back to text search. Per-harness LSP wiring and known issues
live in `docs/agents/README.md`.

Use text search for docs, manifests, comments, literal strings, and when no semantic
tooling is available.

Agent-facing known notes live in `docs/agents/KNOWN_ISSUES.md`. Treat them as triage
hints, not permission to refactor unrelated code.

## When Deniz Asks For A Review

Use a code-review mindset. Findings come first, ordered by severity, with file and line
references when possible. If there are no findings, say so and mention residual risk or
testing gaps.

For interactive reviews, offer options for each issue:

- A recommended option with effort, risk, impact, and maintenance burden.
- One or two alternatives, including doing nothing when reasonable.
- Ask Deniz to confirm before large changes.
