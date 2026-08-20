# Agent Integration Guide

This directory contains repository-owned guidance for coding agents. It intentionally does not
inventory harness-native skills, agents, plugins, marketplaces, models, or local configuration.
Those names and capabilities change independently of BadgeSmith and must be discovered from the
running environment.

## Authority

[`AGENTS.md`](../../AGENTS.md) is the canonical always-on contract. Relay and discovery files may
point to it or to a narrower project guide, but they do not own policy. Shared documentation roles
and lifecycle are defined in [`docs/README.md`](../README.md).

Repository canon controls behavior regardless of which runtime tools are available. Use suitable
runtime-discovered tools when they help, but do not infer permission, architecture, or quality
policy from their presence. Missing optional tooling is not a reason to invent an identifier,
install a substitute without an explicit request, or weaken a required check.

Changes to `AGENTS.md`, approval gates, repository routing, discovery relays, or this integration
policy require explicit approval even when the edit is Markdown-only.

## Repository Files

| File | Purpose |
| --- | --- |
| [`AGENTS.md`](../../AGENTS.md) | Canonical repository contract |
| [`CLAUDE.md`](../../CLAUDE.md) | Thin relay to `AGENTS.md` |
| [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md) | Thin relay to `AGENTS.md` |
| `docs/agents/README.md` | Repository agent-integration boundaries (this file) |
| [`docs/agents/KNOWN_ISSUES.md`](KNOWN_ISSUES.md) | Unique triage notes that do not belong in the always-on contract |
| [`docs/agents/deviation-protocol.md`](deviation-protocol.md) | Canonical contradiction classification and escalation |
| [`docs/agents/handover-prompts/session-pickup-template.md`](handover-prompts/session-pickup-template.md) | Temporary session-handover structure |
| [`docs/agents/skills/aspire-source-navigation.md`](skills/aspire-source-navigation.md) | Canonical upstream-source workflow for Aspire/AWS/LocalStack compatibility |
| [`.opencode/skills/aspire-source-navigation/SKILL.md`](../../.opencode/skills/aspire-source-navigation/SKILL.md) | Native discovery shim; never the policy owner |

## Project-Owned Guidance

Compatibility-sensitive Aspire, AWS, and LocalStack work must follow the canonical
[upstream-source workflow](skills/aspire-source-navigation.md). Agents can read that guide directly;
native discovery shims are optional adapters and must stay thin. Do not add equivalent relays for
every environment merely to normalize names.

Official Aspire runtime tooling can inspect AppHosts launched through the Aspire CLI. It cannot see
the in-process `DistributedApplicationTestingBuilder` AppHosts used by contract tests, so those
sessions remain test-log and debugger territory. Upstream compatibility conclusions come from the
verified package-matching source workflow, not from whichever runtime tool happens to be installed.

## Maintenance

- Keep mandatory cross-environment policy in `AGENTS.md` and project-specific details in their
  canonical guides.
- Keep `KNOWN_ISSUES.md` limited to unique, current triage facts.
- Keep relay and discovery files short; they link to canon and never restate it.
- Keep native capability inventories, marketplace repair steps, LSP setup, model routing, and other
  per-developer configuration outside the repository.
- Update `docs/README.md` when document ownership or lifecycle changes.
