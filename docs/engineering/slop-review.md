# Slop Review

This is the review home for BadgeSmith's existing anti-shortcut, narrow-arbitration, and reporting
rules. [Coding style](coding-style.md) and [testing style](testing-style.md) own authorship;
[quality gates](quality-gates.md) owns execution. This document adds no separate catalog or gate.

## Scope And Design Review

Prefer the smallest correct change. Compatibility code needs a concrete consumer or persisted/shipped
contract. Check both named decomposition failures: private-method soup and speculative interfaces,
factories, or layers. Apply the actual [extraction signals](coding-style.md#private-methods-and-extraction)
and [real substitution seams](coding-style.md#composition-boundaries), not an abstraction quota.
The [two-provider threshold](coding-style.md#duplication-thresholds) is intentional; it is not a
reason to extract at the second provider or let the copies drift.

## Do Not Make A Check Lie

Keep warnings-as-errors and the analyzer wall intact. A valid analyzer exception is narrow,
documented beside its file-scoped arbitration, and names the winning contract; do not weaken the
global policy to avoid a local design decision. Apply [authoring enforcement](coding-style.md#enforcement).

Deleting, skipping, or weakening a test, or changing what an existing test asserts, is an
ask-first action under the [root rules](../../AGENTS.md#autonomy-and-approval) unless the request
asked for that behavior change. A raised allocation ceiling is a reviewed performance-contract
change with a stated reason, not a test edit. Diagnose a failure before changing production code
for it.

Use the existing [Slopwatch trigger and command](quality-gates.md#slopwatch) when available. Missing
optional tooling is not permission to invent a substitute or weaken a required check. State unrun
verification and distinguish evidence from an assumption; follow the
[deviation protocol](deviation-protocol.md) when current canon disagrees.

## Review Reporting

Lead with findings ordered by severity, with file/line references. If there are no findings, say so
and identify residual risk or unrun verification.

When a finding has more than one reasonable fix, recommend one with its **effort, risk, impact,
and maintenance cost**, and name an alternative only when it is close. A review request is answered
with findings; implement fixes only when the request asks for them.
