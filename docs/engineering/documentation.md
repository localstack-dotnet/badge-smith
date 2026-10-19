# Documentation

BadgeSmith documentation uses progressive disclosure: the root agent contract contains universal
safety and routing, while each technical or operational subject has one natural owner. This file
defines those owners and the lifecycle of each document class.

## Document Roles

| Home | Audience | Owns | Lifecycle |
| --- | --- | --- | --- |
| [`AGENTS.md`](../../AGENTS.md) | All coding agents | Always-on safety, autonomy and approval rules, source pointers, and task routing | Living policy |
| [`README.md`](../../README.md) | Users and contributors | Product purpose, positioning, public endpoints, examples, showcase, and first-run paths | Living public surface |
| [`docs/guide/`](../guide/README.md) | Badge users and test-result publishers | How to use the shipped package/test badges and supported publishing paths | Living consumer guidance |
| [`GLOSSARY.md`](../../GLOSSARY.md) | Users, maintainers, and agents | Current domain vocabulary, not implementation or protocol mechanics | Living domain language |
| [`docs/architecture/runtime-and-routing.md`](../architecture/runtime-and-routing.md) | Maintainers and contributors | Request handling, routes, HTTP results, caching, and scaling | Living current architecture |
| [`docs/architecture/security-and-storage.md`](../architecture/security-and-storage.md) | Maintainers and contributors | Storage, canonical HMAC, nonce, secrets, upstream/transport security, and report URLs | Living current architecture |
| [`docs/architecture/platform-and-delivery.md`](../architecture/platform-and-delivery.md) | Maintainers and contributors | Topology, Native AOT, CDK/delivery boundaries, and performance measurement policy | Living current architecture |
| `docs/engineering/` | Contributors and agents | Authoring, quality, workflow, documentation, deviation, local-development, and source-evidence practice | Living engineering policy and guidance |
| [`coding-style.md`](coding-style.md) / [`testing-style.md`](testing-style.md) | C# and test contributors | Decomposition, scoped composition, allocations, layout, and test authorship that analyzers cannot decide | Living engineering policy |
| [`quality-gates.md`](quality-gates.md) / [`slop-review.md`](slop-review.md) | Contributors and reviewers | Existing validation, evidence limits, anti-weakening, and review rules | Living engineering policy |
| [`workflow.md`](workflow.md) | Contributors and agents | Collaboration, integration boundaries, and session-handoff authorship | Living workflow policy |
| `docs/engineering/documentation.md` | Maintainers and agents | Documentation ownership, lifecycle, and relocation rules | Living documentation policy |
| [`docs/ROADMAP.md`](../ROADMAP.md) | Maintainers | Current status, backlog, deferred work, and concise completed-work index | Operational; expected to change and stay concise |
| [`docs/plans/`](../plans/) | Maintainers implementing active work | Detailed implementation plan for work currently in flight | Temporary; delete after the durable outcome reaches current canon and the roadmap |
| [`docs/research/`](../research/) | Maintainers | Dated evidence, experiments, measurements, and historical findings | Historical; may intentionally contain superseded conclusions |
| [`docs/adr/`](../adr/README.md) | Maintainers | Context and trade-offs for accepted, hard-to-reverse decisions | Created lazily when a real decision needs one |
| [`docs/handoffs/`](../handoffs/README.md) | Successor sessions | Scope-specific state needed to resume genuinely stateful work | Temporary; verify against live state, then remove after consumption or supersession |
| [`tools/README.md`](../../tools/README.md) | CLI users | BadgeSmith CLI commands, defaults, secret handling, and performance-tool invocation | Living operational reference |
| [`build/**/README.md`](../../build/) | Infrastructure operators | Per-app CDK boundaries, working directories, artifacts, and safe commands | Living operational reference |
| [`tests/BadgeSmith.Api.Tests/README.md`](../../tests/BadgeSmith.Api.Tests/README.md) | Test maintainers | Test categories, Aspire/LocalStack contract topology, and performance harnesses | Living test reference |
| [`tests/BadgeSmith.CDK.Tests/README.md`](../../tests/BadgeSmith.CDK.Tests/README.md) | Infrastructure test maintainers | In-process CDK assertion scope, requirements, and execution | Living test reference |
| [`.github/workflows/update-test-badge/README.md`](../../.github/workflows/update-test-badge/README.md) | External action consumers | Reusable action usage and consumer-facing behavior | Living action reference; `action.yml` owns exact inputs |

Current canon, historical evidence, active plans, and temporary handoffs have distinct lifecycles
inside this repository. Agent relay and discovery files point at canon; they own neither independent
policy nor native-tool inventories. [Workflow](workflow.md#agent-integration-boundaries) owns those
integration boundaries, and [local development](local-development.md) keeps unique current triage
facts rather than a second always-on policy layer.

## Authority

Authority depends on the claim:

- Runtime behavior comes from source and tests.
- SDK, package, analyzer, build, and workflow facts come from their configuration files.
- Current design and engineering intent come from `docs/architecture/` and `docs/engineering/`.
- ADRs explain why a current rule was accepted; they do not replace the current rule.
- `GLOSSARY.md` owns domain vocabulary, not a competing technical specification.
- `docs/ROADMAP.md` owns what is current, next, deferred, or complete.
- Research preserves what was observed at a date or commit. It is evidence, not a current-work list.
- Plans, handoffs, chat, and memory are transient and must be checked against the repository.

If two current canonical sources disagree, do not choose one silently. Follow the
[deviation protocol](deviation-protocol.md).

## One Owner, Useful Relays

A repeated fact is acceptable when another audience needs enough context to act, but only one place
owns the full current rule. A relay should summarize, link, and avoid copying volatile values.

The public README may intentionally repeat concise technical summaries for positioning and
usability. Do not remove product story, proof points, live examples, showcase content, or
contributor-facing context merely because detailed technical facts are canonical elsewhere.
Consumer guides explain BadgeSmith use and link exact operational owners; they are not an index of
contributor policy or a second security specification.

Examples:

- [Security and storage](../architecture/security-and-storage.md#canonical-hmac-authentication)
  owns the exact HMAC protocol; the public README, consumer guide, and CLI guide summarize it.
- Runtime and routing owns cache semantics; platform and delivery links that contract.
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
needs. A retired plan or handoff must first deliver its durable outcome to current canon, rationale,
dated evidence, and the roadmap as appropriate; temporary status is not evergreen architecture.

## Dates

Living documents rely on Git history and do not need a manually maintained date. Use explicit dates
only where the date is part of the meaning:

- Research: evidence snapshot date.
- ADR: decision date.
- Handoff: authoring snapshot date.
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
  [code-documentation rules](coding-style.md#code-documentation).
- Do not create an ADR unless the decision is hard to reverse, surprising without context, and has a
  real trade-off. If any leg is missing, use current architecture, engineering guidance, or research.
  [ADR convention](../adr/README.md) owns record format and provenance; do not invent a decision,
  rationale, rejected alternative, or date to populate the directory.
