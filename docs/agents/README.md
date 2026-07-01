# Agent Harness Guide

Date: 2026-07-01

This directory contains repository-specific guidance for AI coding agents.

## Source Of Truth

`AGENTS.md` is the canonical always-on contract. Harness-specific files relay to it or
expose native discovery points, but they do not own policy.

`AGENTS.md` intentionally stays compact and capability-oriented. Keep mandatory
repository policy there; keep harness-native names, local setup details, marketplace
repair notes, LSP wiring, and skill routing maintenance in this adapter guide.

Changes to `AGENTS.md`, approval gates, capability routing, skill triggers, or harness
adapter behavior are policy/infrastructure changes. They require explicit approval even
when the edit is Markdown-only.

| File | Purpose |
| --- | --- |
| `AGENTS.md` | Canonical repository contract |
| `CLAUDE.md` | Claude Code relay to `AGENTS.md` |
| `.github/copilot-instructions.md` | GitHub Copilot relay to `AGENTS.md` |
| `docs/agents/README.md` | Harness adapter guide and capability mapping (this file) |
| `docs/agents/KNOWN_ISSUES.md` | Agent-facing known notes and triage hints |
| `docs/agents/handover-prompts/` | Session-pickup templates for stateful handovers |

**No project skill is shipped.** BadgeSmith does not author a custom skill; it curates
the installed marketplace skills below. If a project skill is ever added, it should live
at `docs/agents/skills/<name>.md` (canonical body) with thin native relays under
`.claude/skills/<name>/SKILL.md`, `.opencode/skills/<name>/SKILL.md`, and
`.github/skills/<name>/SKILL.md`. Those folders do not exist today, by design.

## Harness Notes

Claude Code discovers project skills under `.claude/skills/{skill-name}/SKILL.md`.

OpenCode discovers project skills under `.opencode/skills/{skill-name}/SKILL.md`.
OpenCode loads skill files at session start, so restart OpenCode after changing
`.opencode/skills/**`; the current running session will not discover newly added
project skills.

GitHub Copilot in VS Code supports repository instructions through
`.github/copilot-instructions.md` and Agent Skills under `.github/skills/`.

These project-skill folders are currently absent because no project skill is shipped.
Do not create `.vscode` skill folders — that is not a canonical Agent Skills location
for this repository.

## Capability Mapping

`AGENTS.md` routes by capability so the contract stays harness-neutral. Resolve each
capability to the current harness's native invocation before acting.

Tier meanings:

- **Tier 0** — bootstrap/process discipline; follow when injected by the harness.
- **Tier 1** — required when triggered for this repo's .NET / Native AOT / Aspire-local
  work; invoke before acting when installed.
- **Tier 2** — optional by judgment; use when it materially improves correctness,
  safety, test quality, or diagnostics.
- **Tier 3** — local-only convenience; use when present, never assume fresh checkouts
  have it.
- **Out of scope** — do not use unless this repo adds that technology or Deniz
  explicitly asks.

OpenCode exposes skill *frontmatter* names, which differ from Claude/Copilot plugin
IDs and depend on the local install. Resolve the exact OpenCode name from the running
harness's `skill` list; the names below reflect the established local convention
(Microsoft skills carry `ms-dotnet-*` prefixes to avoid colliding with Aaron's
`dotnet-skills` set). Specialist agents are not skills in OpenCode; dispatch them with
`task` only when the harness exposes the matching `subagent_type`.

### Tier 0 — Process discipline

| Capability | Claude Code / Copilot CLI | OpenCode |
| --- | --- | --- |
| Brainstorming, planning, debugging, TDD, review, verification, plan execution | Harness-injected process skills, when installed (e.g. `superpowers:<name>`) | `<name>` via `skill`, when installed |

Follow a process skill immediately when the harness injects it; use this guide to map
additional capabilities after the active process workflow tells you what to invoke. Not
every harness ships a process-skill set — if none is present, apply the same discipline
manually.

### Tier 1 — .NET domain and Aspire-local (required when triggered)

| Capability | Claude Code / Copilot CLI | OpenCode |
| --- | --- | --- |
| System.Text.Json AOT source-generation / serialization contracts | `dotnet-skills:serialization` | `serialization` |
| Modern C# coding standards | `dotnet-skills:csharp-coding-standards` | `modern-csharp-coding-standards` |
| Type design and performance (seal, readonly struct, static pure) | `dotnet-skills:csharp-type-design-performance` | `type-design-performance` |
| Concurrency / async patterns | `dotnet-skills:csharp-concurrency-patterns` | `csharp-concurrency-patterns` |
| Project / MSBuild structure | `dotnet-skills:project-structure` | `dotnet-project-structure` |
| NuGet package management (CPM) | `dotnet-skills:package-management` | `package-management` |
| OpenTelemetry instrumentation | `dotnet-skills:opentelementry-dotnet-instrumentation` | resolve via `skill` |
| Slopwatch quality gate | `dotnet-skills:slopwatch` | `dotnet-slopwatch` |
| Aspire explicit configuration (Host AppHost) | `dotnet-skills:aspire-configuration` | `aspire-configuration` |

### Tier 2 — Test (xUnit v3; use by judgment)

| Capability | Claude Code / Copilot CLI | OpenCode |
| --- | --- | --- |
| Running / filtering tests | `dotnet-test:run-tests` | `ms-dotnet-test-run-tests` |
| Test anti-pattern audit | `dotnet-test:test-anti-patterns` | `ms-dotnet-test-test-anti-patterns` |
| Test gap (mutation-style) analysis | `dotnet-test:test-gap-analysis` | `ms-dotnet-test-test-gap-analysis` |
| Assertion quality analysis | `dotnet-test:assertion-quality` | `ms-dotnet-test-assertion-quality` |
| Test generation | `dotnet-test:code-testing-agent` | `ms-dotnet-test-code-testing-agent` |
| Find untested sources | `dotnet-test:find-untested-sources` | `ms-dotnet-test-find-untested-sources` |
| Grade a curated set of tests | `dotnet-test:grade-tests` | `ms-dotnet-test-grade-tests` |
| Coverage + CRAP analysis | `dotnet-test:coverage-analysis`, `dotnet-test:crap-score` | `ms-dotnet-test-coverage-analysis`, `ms-dotnet-test-crap-score` |
| Broad test-suite audit (agent) | `dotnet-test:test-quality-auditor` agent | `task` when exposed |
| Aspire integration testing | `dotnet-skills:aspire-integration-testing` | `aspire-integration-testing` |
| Snapshot testing (Verify) for badge JSON/HTTP output | `dotnet-skills:snapshot-testing` | `snapshot-testing` |

### Tier 2 — Performance and diagnostics (BenchmarkDotNet present)

| Capability | Claude Code / Copilot CLI | OpenCode |
| --- | --- | --- |
| Microbenchmarking (BenchmarkDotNet) | `dotnet-diag:microbenchmarking` | resolve via `skill` |
| Performance anti-pattern analysis | `dotnet-diag:analyzing-dotnet-performance` | resolve via `skill` |
| Performance optimization (agent) | `dotnet-diag:optimizing-dotnet-performance` agent | `task` when exposed |
| Benchmark design (agent) | `dotnet-skills:dotnet-benchmark-designer` agent | `dotnet-benchmark-designer` via `task` |
| Performance analysis of measured data (agent) | `dotnet-skills:dotnet-performance-analyst` agent | `dotnet-performance-analyst` via `task` |
| Concurrency / race analysis (agent) | `dotnet-skills:dotnet-concurrency-specialist` agent | `dotnet-concurrency-specialist` via `task` |
| Trace / dump collection | `dotnet-diag:dotnet-trace-collect`, `dotnet-diag:dump-collect` | resolve via `skill` |
| Decompile assemblies (AWS SDK / framework AOT-trim insight) | `dotnet-skills:ilspy-decompile` | `ilspy-decompile` |

### Tier 2 — Meta / maintenance

| Capability | Claude Code / Copilot CLI | OpenCode |
| --- | --- | --- |
| Maintain the `AGENTS.md` / this file's capability index | `dotnet-skills:skills-index-snippets` | `skills-index-snippets` |

### Tier 3 — Local-only

| Capability | Claude Code / Copilot CLI | OpenCode |
| --- | --- | --- |
| OpenCode local model routing | Not applicable | `subagent-model-routing` via `skill` when present |

### Out of scope

Do not invoke these unless the repo adds the technology or Deniz explicitly asks:

- **Akka.NET**: `akka-*` skills and `akka-net-specialist` — no Akka.NET here.
- **Test-framework mismatch**: `writing-mstest-tests` (this repo uses xUnit v3, not
  MSTest); `mtp-hot-reload` (this repo runs on VSTest, not Microsoft.Testing.Platform).
- **Data layer**: `efcore-patterns`, `database-performance` — storage is DynamoDB via
  the AWS SDK, not EF/SQL.
- **Email**: `mjml-email-templates`, `verify-email-snapshots`,
  `aspire-mailpit-integration`.
- **UI**: `playwright-blazor`, `playwright-ci-caching`.
- **Reactive**: `r3-reactive-extensions`.
- **Deliberately absent patterns**: `microsoft-extensions-dependency-injection`
  (no DI — `ApplicationRegistry`), `microsoft-extensions-configuration` (no config
  framework — env vars), `aspire-service-defaults` (no ServiceDefaults project).
- **Testability/static-wrapper**: `detect-static-dependencies`,
  `generate-testability-wrappers`, `migrate-static-to-wrapper`, `testability-migration`
  — conflict with the intentional static/AOT/no-DI design.
- **Redundant**: `crap-analysis` (OpenCover) — superseded by `dotnet-test:coverage-analysis`
  / `crap-score` (Cobertura).
- **Narrow/academic (only if explicitly asked)**: `test-smell-detection`, `test-tagging`.
- **Other**: `csharp-api-design` (assembly/NuGet API compat, not HTTP contract),
  `marketplace-publishing`, `docfx-specialist`, `roslyn-incremental-generator-specialist`
  (we consume OneOf.SourceGenerator, not author generators), `clr-activation-debugging`
  (.NET Framework), crash symbolication skills, `dotnet-devcert-trust`, `local-tools`,
  `setup-local-sdk`.

There is no dedicated Native AOT skill in the installed marketplaces; AOT coverage comes
from `serialization` + `csharp-type-design-performance` + `analyzing-dotnet-performance`,
backed by the "Native AOT And Lambda Constraints" section of `AGENTS.md`.

Availability is not activation. Except for the process bootstrap, skills do not run
automatically; invoke the mapped skill or dispatch the mapped specialist agent when the
trigger applies. If a mapped capability is not loaded in the current harness, skip
optional rows or ask before installing or substituting another tool. Do not invent an ID.

## Claude Code .NET Skill Marketplaces

Two .NET skill marketplaces are used, and their names are intentionally distinct:

- `Aaronontheweb/dotnet-skills` declares marketplace name `dotnet-skills` (the
  `dotnet-skills:*` convention skills).
- Microsoft's `dotnet/skills` declares marketplace name `dotnet-agent-skills` (the
  `dotnet-test:*` / `dotnet-diag:*` procedure skills).

Do not run `claude plugin marketplace add dotnet/skills` as a blind repair step. Claude
may key the marketplace by a repo-path-derived name (`dotnet-skills`), which collides
with Aaron's install directory and silently repoints the `dotnet-skills` registry entry
to Microsoft's repo. The result is an orphaned Aaron plugin: its clone/cache may remain
on disk but stop resolving.

If the registry is already wrong, back up the files first, then repair directly:

1. In `~/.claude/plugins/known_marketplaces.json` and `~/.claude/settings.json` ->
   `extraKnownMarketplaces`, point `dotnet-skills` to `Aaronontheweb/dotnet-skills`.
2. Add a separate `dotnet-agent-skills` entry pointing to `dotnet/skills`.
3. Ensure Microsoft's repo is cloned to
   `~/.claude/plugins/marketplaces/dotnet-agent-skills`.
4. Install Microsoft plugins with `claude plugin install <plugin>@dotnet-agent-skills`.
5. Verify with `claude plugin marketplace list`; both marketplaces must show their
   correct source repos.

## Semantic Code Navigation (LSP) By Harness

`AGENTS.md` carries the decision rule (Rider MCP first, then the harness headless LSP,
then text search). Harness wiring:

- **Rider MCP:** Preferred when Rider is running and its MCP tools are present — solution
  index, ReSharper analysis, and semantic refactors.
- **Claude Code:** The headless LSP is the `csharp-lsp` plugin (community `csharp-ls`).
  The official Microsoft `dotnet` plugin's Roslyn LSP does not load on Claude Code yet
  (dotnet/skills#846). Do not hand-edit vendored plugin caches to force it; when #846
  ships it loads automatically.
- **Copilot CLI:** The `dotnet@dotnet-agent-skills` plugin's `lsp.json` works out of the
  box (launches `roslyn-language-server` via `dnx`).
- **OpenCode:** Use native OpenCode LSP config; OpenCode does not consume Claude/Copilot
  plugin `lsp.json`. Restart OpenCode after config changes.

## Per-Developer OpenCode Setup

Some OpenCode conveniences are intentionally local-only and ignored by git:
`opencode.jsonc`, `.opencode/agents/`, and `.opencode/skills/subagent-model-routing/`.
They may define model-routed agents such as `deepseek-coder`, `deepseek-light`,
`codex-coder`, `glm-hardcore`, or `codex-review`, but they are not shipped project
infrastructure.

When present, `subagent-model-routing` can help choose among local OpenCode agents and
providers. Do not treat it as a required project skill, and do not assume another
checkout exposes the same `subagent_type` names.

## Skill Maintenance

Because no project skill is shipped, skill maintenance reduces to routing maintenance:

- Update `AGENTS.md` only for mandatory cross-harness policy.
- Update this file for adapter mechanics and the capability mapping table.
- Use the `skills-index-snippets` capability to keep the capability index consistent
  when skills are added, retired, or re-tiered.
- Change the roster deliberately: re-tier or drop a skill only when the repo's actual
  needs change, and keep this table and `AGENTS.md` in sync.
