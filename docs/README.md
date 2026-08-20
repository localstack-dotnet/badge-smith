# Documentation Guide

BadgeSmith documentation uses progressive disclosure: the root agent contract contains universal
safety and routing, while each technical or operational subject has one natural owner. This file
defines those owners and the lifecycle of each document class.

## Document Roles

| Home | Audience | Owns | Lifecycle |
| --- | --- | --- | --- |
| [`AGENTS.md`](../AGENTS.md) | All coding agents | Always-on safety, approval, source pointers, and task routing | Living policy; changes are approval-gated |
| [`README.md`](../README.md) | Users and contributors | Product purpose, positioning, public endpoints, examples, showcase, and first-run paths | Living public surface |
| [`ARCHITECTURE.md`](../ARCHITECTURE.md) | Maintainers and contributors | Current technical design, security contracts, topology, and performance measurement policy | Living current architecture |
| `docs/README.md` | Maintainers and agents | Documentation ownership, lifecycle, and relocation rules | Living documentation policy |
| [`docs/engineering/coding-style.md`](engineering/coding-style.md) | C# contributors and agents | Decomposition and composition rules analyzers cannot express | Living engineering policy |
| [`docs/ROADMAP.md`](ROADMAP.md) | Maintainers | Current status, backlog, deferred work, and concise completed-work index | Operational; expected to change and stay concise |
| [`docs/plans/`](plans/) | Maintainers implementing active work | Detailed implementation plan for work currently in flight | Temporary; delete after the durable outcome reaches current canon and the roadmap |
| [`docs/research/`](research/) | Maintainers | Dated evidence, experiments, measurements, and historical findings | Historical; may intentionally contain superseded conclusions |
| ADRs, when present under `docs/adr/` | Maintainers | Context and trade-offs for accepted, hard-to-reverse decisions | Created lazily when a real decision needs one |
| [`docs/agents/`](agents/) | Coding agents | Agent-integration boundaries, unique triage hints, project-owned source guidance, and temporary handovers | Living agent operation; not shared product policy |
| [`tools/README.md`](../tools/README.md) | CLI users | BadgeSmith CLI commands, defaults, secret handling, and performance-tool invocation | Living operational reference |
| [`build/**/README.md`](../build/) | Infrastructure operators | Per-app CDK boundaries, working directories, artifacts, and safe commands | Living operational reference |
| [`tests/BadgeSmith.Api.Tests/README.md`](../tests/BadgeSmith.Api.Tests/README.md) | Test maintainers | Test categories and Aspire/LocalStack contract topology | Living test reference |
| [`.github/workflows/update-test-badge/README.md`](../.github/workflows/update-test-badge/README.md) | External action consumers | Reusable action usage and consumer-facing behavior | Living action reference; `action.yml` owns exact inputs |

Agent relay and discovery files point at canon and do not own independent policy or native-tool
inventories.

## Authority

Authority depends on the claim:

- Runtime behavior comes from source and tests.
- SDK, package, analyzer, build, and workflow facts come from their configuration files.
- Current design and engineering intent come from `ARCHITECTURE.md` and `docs/engineering/`.
- ADRs explain why a current rule was accepted; they do not replace the current rule.
- `docs/ROADMAP.md` owns what is current, next, deferred, or complete.
- Research preserves what was observed at a date or commit. It is evidence, not a current-work list.
- Plans, handovers, chat, and memory are transient and must be checked against the repository.

If two current canonical sources disagree, do not choose one silently. Follow
[`docs/agents/deviation-protocol.md`](agents/deviation-protocol.md).

## One Owner, Useful Relays

A repeated fact is acceptable when another audience needs enough context to act, but only one place
owns the full current rule. A relay should summarize, link, and avoid copying volatile values.

The public README may intentionally repeat concise technical summaries for positioning and
usability. Do not remove product story, proof points, live examples, showcase content, or
contributor-facing context merely because detailed technical facts are canonical elsewhere.

Examples:

- `ARCHITECTURE.md` owns the exact HMAC protocol; the public README and CLI guide summarize it.
- CDK READMEs own working directories and artifact paths; architecture records only the boundary.
- Workflows own pinned Node/CDK CLI versions; READMEs direct operators to those workflows.
- `Directory.Packages.props` owns package versions; prose records compatibility policy, not a second
  version inventory.

## Lossless Relocation

Documentation refactoring is relocation before deletion:

1. Identify each unique claim, its audience, durability, evidence, and current owner.
2. Add or update the destination before removing the source text.
3. Preserve dates, commit SHAs, measurements, links, rejected alternatives, and safety warnings.
4. Distinguish historical evidence from obsolete current guidance; do not rewrite old research into
   present tense.
5. Verify links and grep for contract literals, environment variables, route names, and approval
   language after moves.
6. Keep contextual summaries where their audience needs them, but replace full duplicate rules with
   links.

Git history is recoverable provenance, not an accessible home for information the project still
needs.

## Dates

Living documents rely on Git history and do not need a manually maintained date. Use explicit dates
only where the date is part of the meaning:

- Research: evidence snapshot date.
- ADR: decision date.
- Handover: authoring snapshot date.
- External compatibility claim: use `Last verified:` only when a repeatable verification was
  actually performed.

Typo or link-only edits do not change historical dates.

## Writing Rules

- Update documentation in the same change as behavior, topology, endpoints, or workflow contracts.
- Prefer the existing natural owner over creating a new file.
- Keep current rules actionable and concise; retain detailed evidence in dated research.
- Documentation-only changes do not require build or test unless they alter commands or technical
  claims that need mechanical validation; always validate affected links.
- Documentation may cite code. Code comments and XML docs follow the self-contained
  [code-documentation rules](engineering/coding-style.md#code-documentation).
- Do not create an ADR unless the decision is hard to reverse, surprising without context, and has a
  real trade-off. If any leg is missing, use current architecture, engineering guidance, or research.
