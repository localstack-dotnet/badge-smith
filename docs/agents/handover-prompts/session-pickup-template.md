# Session Pickup Prompt — Template

> **How to use:** at the end of a session that closed a non-trivial wave, copy this file
> to `s<N>-<short-summary>.prompt.md` (e.g. `s1-contract-adoption.prompt.md`). Replace
> every `{{placeholder}}` with session-specific content. Drop any section that has no
> content. Length is not a virtue — useful pickups are 100-200 lines, not 250+.
>
> **When NOT to write a pickup:** doc-only edits, single-bug fixes, or routine
> refreshes. Pickups are for handing off **architectural waves, multi-commit refactors,
> roadmap/planning landings, or genuinely stateful in-flight work** that the next
> session needs primed context to continue.

---

## Frontmatter (replace every field)

```yaml
---
name: "S{{N}} {{short-summary-of-what-just-closed}}"
description: "Priming prompt for the next agent entering BadgeSmith after {{wave-or-task}} landed at {{sha}} on {{date}}. {{one-sentence-state-summary}}. Recommended next: {{A | B | C}}."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "{{model identifier — e.g. Claude Opus 4.8}}"
---
```

---

## Body skeleton

### Opening paragraph

One paragraph stating: who you are (engineer entering the repo), what just landed (wave
+ sha + date), and the one most important state observation. No fluff.

### `## First Principle`

> Treat every claim here as **current-as-of-authoring (`{{date}}` — `{{wave-tag}}`)** and
> verify against the live repo, git log, `Directory.Packages.props`, `ARCHITECTURE.md`,
> and canonical docs before acting.

This block stays nearly verbatim across pickups — it's the standing reminder that pickup
prompts go stale.

### `## What Just Happened`

The detailed change ledger. Use sub-headers for distinct waves / concerns. For each:

- **What changed** — file paths, signatures, removed / added types
- **Why** — 1-2 lines anchored to a settled decision / roadmap workstream / research
  finding
- **Verification** — build/test outcomes (xUnit v3), Slopwatch results, manual checks

For mechanical changes (version bumps, rename waves, doc consolidations) prefer
**tables**: denser than prose, easier for the next agent to skim. Include any inline
deferrals or known carry-forward items at the end of each sub-section.

This is the longest section. Don't pad it; do not omit anything load-bearing for the
next agent.

### `## Onboarding Snapshot` *(optional — drop if the previous pickup already covered it well and nothing shifted)*

Quick re-orientation. Brief stack reminder and the locked-decisions list. **Most of this
is durable across pickups** — copy from the previous pickup and adjust where things
shifted.

Durable stack facts for this repo:

- **Product:** BadgeSmith — a Shields.io-compatible badge service for NuGet/GitHub
  packages and CI/CD test results, shipped as a **.NET 10 Native AOT AWS Lambda
  application** deployed via **AWS CDK** (API Gateway HTTP v2 + CloudFront, DynamoDB,
  Secrets Manager). It consumes `LocalStack.Aspire.Hosting` for local dev only.
- **Tooling:** .NET SDK per `global.json` (net10.0); **xUnit v3 + Moq on VSTest**;
  BenchmarkDotNet for perf tests; Central Package Management (`Directory.Packages.props`);
  strict analyzers + warnings-as-errors via shared MSBuild.
- **Architecture invariants:** Native AOT (`PublishAot`), no DI (`ApplicationRegistry` +
  `Lazy<T>`), no configuration framework (environment variables), System.Text.Json
  source generation (`LambdaFunctionJsonSerializerContext`), OneOf result pattern,
  custom span-based routing.
- **Source layout:** `src/BadgeSmith.Api` (Lambda), `src/BadgeSmith.Host` (Aspire
  AppHost), `src/shared`, `build/` (CDK), `tests/` (Api.Tests, Api.Performance.Tests,
  seeders), `docs/`.

### `## Current State You Should Assume Until Verified`

A short bullet list with concrete current values:

- **HEAD** (`{{branch}}`): `{{sha}}` — `{{commit-summary}}`
- **Worktree expectation**: `{{clean | unstaged X}}`
- **Pinned versions** (`Directory.Packages.props`): `{{Aspire.Hosting.AppHost X, AWSSDK.* Y, xunit.v3 Z}}`
- **Tests (xUnit v3)**: `{{N passed / M skipped | "not run this session"}}`
- **Active workstream**: `{{WS# + next steps in its docs/plans plan, or "none in-flight"}}`
- **Local-only state**: `{{anything gitignored the next session may need — e.g. LocalStack containers, opencode local agents}}`
- **Background/parallel work**: `{{state}}`

Always verifiable, always specific. Vague status entries are worse than absent ones.

### `## Recommended Next Step`

1-3 numbered options. Each option:

- **Name + classification** — e.g. "lightweight, well-scoped" / "multi-session arc" /
  "optional"
- **Pre-flight steps** — what to read / verify before starting (incl. which capability
  to invoke)
- **Specific files / sections to touch** — concrete paths
- **Acceptance criteria** — what "done" looks like

End with: "Talk to Deniz before committing to which one." Default to working on the
current branch; do not branch unless there is a concrete reason. No commit without
explicit "go / apply / proceed / başla / yap" (AGENTS.md approval gate).

### `## Mandatory Grounding (read in this order)`

A numbered read order. Adjust per scope, but the canonical core stays:

1. `AGENTS.md` — canonical repository contract: communication style, approval gate,
   Native AOT/Lambda constraints, capability routing (`CLAUDE.md` and
   `.github/copilot-instructions.md` are relay-only).
2. `README.md` + `ARCHITECTURE.md` — product behavior, endpoints, and design decisions.
3. `docs/ROADMAP.md` — backlog, **Status & Plan Mapping table** (progress tracker),
   and the **Inbox / Untriaged** capture spot.
4. `docs/plans/{{active-workstream-plan}}` — the live plan for the workstream in flight.
5. `docs/agents/README.md` (harness guide + capability mapping) +
   `docs/agents/KNOWN_ISSUES.md` (triage hints).
6. The relevant `src/` / `tests/` / `build/` files for the scope.

Skip entries the next session does not need; do not pad with everything.

### `## Locked Policy Recap`

Curated invariants list. Most carry over verbatim from session to session. This section
is for the **most-likely-to-be-tempting-to-violate** rules in the upcoming work, not a
full mirror of `AGENTS.md`.

- No commit without explicit "go / apply / proceed / başla / yap". Conventional Commits
  (`feat|fix|docs|test|refactor|build|ci|chore`); **no AI attribution trailers**.
- Do not start a feature, refactor production code, change build/CI, change CDK infra,
  or run CDK deploy / Lambda publish / release without approval. Docs-only edits, link
  fixes, and read-only discovery are allowed.
- Package versions live in `Directory.Packages.props` (Central Package Management) —
  **never hand-edit versions into individual `.csproj` files**.
- Strict analyzers + warnings-as-errors are on. Run `slopwatch analyze --fail-on warning`
  after LLM-authored code/test changes when available.
- Native AOT discipline: no reflection patterns, register JSON types in the
  source-gen context, treat trim/AOT warnings as blocking, `DateTime.UtcNow` only.
- Tests are xUnit v3 on VSTest — plain `dotnet test` / `--filter`, not TUnit.
- `AGENTS.md` is canonical; `CLAUDE.md` and `.github/copilot-instructions.md` stay
  relay-only. `aspire-source-navigation` is the only custom project skill and is exposed
  through OpenCode only; its canonical guide and the curated roster live under
  `docs/agents/`.

### `## Final Steering Note`

1-2 paragraphs. Closing direction. Hint at the natural rhythm for the next session — not
a hard mandate. End short, specific, motivating.

---

## Drift sensitivity per section

| Section | Drift sensitivity |
| --- | --- |
| Frontmatter | Session-specific — rewrite |
| Opening paragraph | Session-specific — rewrite |
| First Principle | Stable |
| What Just Happened | Session-specific — rewrite |
| Onboarding Snapshot | Mostly stable; adjust per shift |
| Current State | Session-specific — rewrite |
| Recommended Next Step | Session-specific — rewrite |
| Mandatory Grounding | Stable; adjust if doc topology shifts |
| Locked Policy Recap | Stable; source from `AGENTS.md` |
| Final Steering Note | Session-specific — rewrite |

When the doc topology shifts (new plan doc, renamed roadmap section, retired doc), update
Mandatory Grounding in this template too — that's the entry point the next pickup author
copies from.

---

## Authoring discipline

- **Verify before claiming.** Don't write "X is at Y state" without `git log --oneline -5`
  + a quick repo grep. Pickup prompts are read by future agents who will treat your
  claims as current.
- **Anchor to commit SHAs.** Every "just landed" claim should reference a specific commit.
- **Flag local-only state.** Gitignored artifacts do not travel with the repo — if the
  next session needs them, say so explicitly.
- **Drop sections that don't apply.** A pickup with no test runs should not have a
  "Tests: TBD" line — drop the bullet.
- **Don't re-derive `AGENTS.md`.** If the next session needs it, point at it; do not
  paraphrase.
- **Sign off with a concrete next-action recommendation, not five parallel futures.**
  Default + alternatives, not buffet.
- **Update the `docs/ROADMAP.md` Status table in the same change** — the pickup is the
  deep handover; the roadmap Status row is the permanent index entry.
