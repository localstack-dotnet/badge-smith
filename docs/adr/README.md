# Architecture decision records

Each ADR records one accepted decision's context, real trade-off and rationale. Current
normative rules belong in `docs/architecture/` and `docs/engineering/`; an ADR explains why
a rule was accepted rather than replacing it. Dated evidence stays in this repository's
`docs/research/` or in cited immutable primary records.

Create records lazily, only when all three hold:

1. Changing course later carries meaningful cost.
2. The choice would surprise a future reader without context.
3. Genuine alternatives existed and one was chosen for specific reasons.

Otherwise use the appropriate architecture, engineering or research owner. Name records
`NNNN-slug.md`, scan the directory for the highest number and increment it. The directory
listing is the index; numbers are record order, not a decision-date chronology.

The concise format is a title and one to three sentences covering context, choice and why.
Use `Date:` for an evidenced decision date. A retrospective record identifies its provenance
and distinguishes a recorded design date from implementation or commit dates; unknown dates
stay explicit. Add considered options or consequences only when useful, not to fill a template.

If an ADR and current canon appear to disagree, use the
[deviation protocol](../engineering/deviation-protocol.md). A reversal updates the current
owner and deliberately revises or supersedes the record, preserving the historical chain.
