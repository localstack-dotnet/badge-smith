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

### `## Onboarding Delta` *(optional)*

Record only durable orientation that changed during the session and is not yet obvious
from current canon. Link `AGENTS.md`, `docs/README.md`, and the relevant architecture or
operational guide instead of copying the standing stack, approval, AOT, test-runner, or
composition rules. In particular, do not collapse the Lambda's `ApplicationRegistry`
model and the CLI's intentional `HostApplicationBuilder` DI model into one repo-wide rule.

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
- **Pre-flight steps** — what to read or verify before starting, including any
  project-owned guidance that applies
- **Specific files / sections to touch** — concrete paths
- **Acceptance criteria** — what "done" looks like

End with: "Talk to Deniz before committing to which one." Default to working on the
current branch; do not branch unless there is a concrete reason. Link the `AGENTS.md`
approval gate instead of restating it.

### `## Mandatory Grounding (read in this order)`

A numbered read order. Adjust per scope, but the canonical core stays:

1. `AGENTS.md` — canonical always-on contract; relays cannot override it.
2. `docs/README.md` — document authority, lifecycle, and lossless relocation.
3. `README.md` + relevant `ARCHITECTURE.md` sections — product behavior and current design.
4. `docs/ROADMAP.md` — current status, backlog, and Inbox / Untriaged.
5. `docs/plans/{{active-workstream-plan}}` — only when a workstream is actively in flight.
6. `docs/agents/README.md` + `docs/agents/KNOWN_ISSUES.md` — agent-integration boundaries and unique triage hints.
7. `docs/engineering/coding-style.md` for hand-written C#; use
   `docs/agents/deviation-protocol.md` if current sources disagree.
8. The relevant `src/`, `tests/`, `build/`, workflow, or configuration files.

Skip entries the next session does not need; do not pad with everything.

### `## Scope-Specific Guardrails` *(optional)*

Link the relevant `AGENTS.md` or canonical section and list only two or three rules that
are unusually tempting to violate in the recommended next increment. Do not copy the
approval gate, package policy, AOT constraints, test framework, Slopwatch command, or
agent-integration rules into every pickup. Omit this section when the links are sufficient.

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
| Onboarding Delta | Session-specific; include only real durable change |
| Current State | Session-specific — rewrite |
| Recommended Next Step | Session-specific — rewrite |
| Mandatory Grounding | Stable; adjust if doc topology shifts |
| Scope-Specific Guardrails | Optional; links plus upcoming-work risks only |
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
