# Canonical Deviation Protocol

Use this protocol when implementation, external reality, or two current canonical documents appear
to disagree. Repository files beat memory, transient plans, dated research, and handovers.

When uncertain, classify at the higher level. Stop only the affected scope; unrelated work may
continue when it is safe and independent.

## Level 0: Implementation Detail

The current canon is silent or the choice is below its resolution.

Action: decide locally and continue. Record the choice only when a reviewer would reasonably wonder
why it was made.

## Level 1: Recorded Fallback

The preferred mechanism failed, but current architecture or an accepted decision already defines
the exact fallback or reversal trigger.

Action: use the recorded fallback, cite the trigger in the change summary, and update any current
document whose wording must change. No additional design stop is required.

## Level 2: Canonical Correction

Evidence contradicts a current non-decision claim such as roadmap status, a living operational
note, an agent-integration boundary, or an active plan constraint, and no fallback exists.

Action: stop the affected work, capture reproducible evidence, propose the corrected owner and text,
and obtain approval before continuing if the correction changes policy or behavior.

## Level 3: Locked Decision Challenge

Evidence contradicts normative architecture, engineering policy, the approval gate, a public
contract, or an accepted ADR.

Action: stop the affected slice. Re-evaluate the decision from source, tests, measurements, and
external evidence. Resume only after an approved outcome updates current canon and, when present,
revises or supersedes the ADR. Never reopen a decision merely because compliance is inconvenient.

## Cross-Cutting Rules

- A subagent reports the contradiction and evidence; the orchestrating session classifies and owns
  escalation.
- Never silently choose one canonical source over another.
- A correct deviation without a durable evidence trail is still a documentation defect.
- Documentation updates are part of the deviation change, not deferred cleanup.
- Dated research remains historical evidence. Add a supersession pointer when useful; do not rewrite
  the original observation as though it happened later.
