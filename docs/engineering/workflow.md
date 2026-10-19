# Repository Workflow

This document owns review and commit flow, agent-integration boundaries, and harness-neutral
session-handoff authorship. The root [autonomy and approval rules](../../AGENTS.md#autonomy-and-approval)
and operating style apply to every task; [quality gates](quality-gates.md) owns verification and
[documentation](documentation.md) owns placement and lifecycle.

## Reviews And Git

[Slop review](slop-review.md#review-reporting) owns severity-ordered findings, file/line references,
honest residual/unrun reporting, and fix recommendations. Commits, amends, pushes, PRs, and releases
follow the [root rules](../../AGENTS.md#autonomy-and-approval), which also own the allowed
Conventional Commit types.

## Agent-Integration Boundaries

`AGENTS.md` is the canonical always-on contract. `CLAUDE.md` and project discovery shims are thin
relays: they may point to it or a narrower project guide but cannot override it or own independent
policy.

Repository canon controls behavior regardless of available runtime tools. Discover suitable native
capabilities from the running environment; committed documentation does not inventory or pin
native skills, agents, plugins, models, or local-tool identifiers. Do not infer
permission, architecture, or quality policy from a tool's presence. Missing optional tooling is not
a reason to invent an identifier, install a substitute without an explicit request, or weaken a
required check.

Keep mandatory cross-environment policy in `AGENTS.md`, project-specific details in their canonical
guides, and unique current triage facts in [local development](local-development.md#test-triage).
Keep relays short and link canon instead of restating it. Update
[documentation ownership](documentation.md#document-roles) when ownership or lifecycle changes.

Compatibility-sensitive Aspire/AWS/LocalStack work follows
[source navigation](aspire-source-navigation.md). Read that guide directly; native discovery shims
are optional adapters. Do not add equivalent relays for every environment merely to normalize
names. [Runtime-tool boundaries](local-development.md#runtime-tool-boundaries) distinguish
CLI-launched AppHosts from in-process test sessions and package-matched source evidence.

## Session Handoffs

Write a pickup only for architectural waves, multi-commit refactors, roadmap/planning landings, or
genuinely stateful in-flight work that needs primed successor context. Do **not** write one for
routine refreshes, doc-only edits, or a single-bug fix. Use the same-repository
[handoff location and lifecycle](../handoffs/README.md); this is harness-neutral authoring guidance,
not a required native frontmatter/model format.

### Authoring Snapshot

- Start with a session-specific title, a short description of the landed wave/task and state, and
  any optional focus/constraints that could override the recommended next step.
- Open with one concise paragraph for the incoming engineer: what landed, its commit SHA and date,
  and the most important state observation. Every “just landed” claim names a specific commit.
- Mark claims **current as of authoring**, with the snapshot date and wave identity. The successor
  must verify against live Git, configuration, and current canon before acting; a pickup goes stale.
- Verify before claiming state: inspect `git log --oneline -5` and a targeted repository search.
  Do not turn an author's snapshot date into a fresh verification date.
- Fill session-specific content and remove placeholders or inapplicable sections. Length is not a
  virtue: useful pickups are typically 100–200 lines rather than padded 250+ line reports. Preserve
  everything load-bearing for the successor, not every step of the session.

### Change Ledger And Orientation

For each distinct wave or concern, record **what** changed (paths, signatures, added/removed types),
**why** (one or two lines anchored to an actual settled decision, roadmap workstream, or research
finding), and **verification** (actual build/xUnit v3, Slopwatch, or manual-check outcomes and limits).
Tables work well for mechanical version, rename, or documentation waves. Include deferrals and
carry-forward items with their concern; do not omit useful qualifications to shorten the ledger.

An optional onboarding delta contains only true durable orientation changes not yet obvious from
canon. Link `AGENTS.md`, [documentation](documentation.md), and relevant architecture/operational
guides instead of copying the standing stack, approval, AOT, runner, or composition rules. In
particular, do not merge the Lambda registry model with the CLI's intentional Host/DI model.

### State Until Verified

Use concrete, verifiable values for the fields the successor needs:

- HEAD branch, SHA, and commit summary; clean/unstaged worktree expectations.
- Relevant pinned versions from `Directory.Packages.props`, not a timeless version inventory.
- Actual test counts/skips and verification limits; explicitly label relevant checks **not run**.
- Active workstream and active plan, or that no work is in flight.
- Needed local-only/ignored state, which does not travel with Git, without committed personal paths,
  secrets, or native-capability configuration.
- Background/parallel work and whether it is finished, still running, or unverified.

Omit irrelevant fields rather than writing `TBD`; absence of a relevant run must not read as a pass.
Vague status is worse than an absent optional entry.

### Recommended Next Step

Recommend one concrete next action with one or two reasonable alternatives, not five parallel
futures. For each option include its name/classification (well-scoped, multi-session, or optional),
preflight reads/checks, exact files/sections, and acceptance criteria. Name the option the
successor starts on unless the user redirects it. In this successor-step guidance, default to the
current branch unless there is a concrete reason to branch; this is not a universal branch
prohibition. Link the [autonomy and approval rules](../../AGENTS.md#autonomy-and-approval), do not
paraphrase them.

### Scope-Adjusted Grounding

Give a read order the next session actually needs; skip irrelevant entries:

1. `AGENTS.md`: always-on contract; relays cannot override it.
2. [Documentation](documentation.md): authority, ownership, lifecycle, and relocation.
3. Root `README.md` and relevant [consumer guide](../guide/README.md),
   [runtime](../architecture/runtime-and-routing.md), [security](../architecture/security-and-storage.md),
   or [platform](../architecture/platform-and-delivery.md) sections; [GLOSSARY](../../GLOSSARY.md)
   when domain language matters.
4. [ROADMAP](../ROADMAP.md): current status, backlog, and Inbox / Untriaged.
5. An active `docs/plans/` plan only when its workstream is in flight.
6. [Agent-integration boundaries](#agent-integration-boundaries) and relevant
   [local triage](local-development.md) when tools or environment matter.
7. [Coding style](coding-style.md), [testing style](testing-style.md), and
   [quality gates](quality-gates.md) for code/tests; [deviation protocol](deviation-protocol.md)
   when current sources disagree.
8. The affected source, tests, CDK/tool/test/action READMEs, workflows, and configuration.

Optional scope-specific guardrails link canon and name only two or three unusually tempting risks
for the next increment. Omit them when links suffice; do not copy the approval rules, package/AOT/
runner rules, Slopwatch command, or integration policy into each pickup. End with a short, specific
steering note (one or two paragraphs): useful direction, not a hard mandate about session rhythm.

### Drift And Closeout

Title/description/focus, opening, change ledger, onboarding delta, state, next step, and final
steering are session-specific. The authoring-as-of reminder is stable; grounding is stable only
until document topology changes. Update grounding when a plan, roadmap section, or owner moves or
retires. Guardrails stay optional, linked, and specific to upcoming risks.

Update the `docs/ROADMAP.md` Status table in the same change: the handoff supplies deep successor
context, while the roadmap row is the permanent concise index. Move durable rules/rationale/evidence
to their [natural owners](documentation.md#document-roles) before the temporary handoff is consumed
or superseded.
