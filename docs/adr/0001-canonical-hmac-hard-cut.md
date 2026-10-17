# Canonical HMAC replaces body-only signing without a legacy verifier

Date: 2026-08-07

Body-only signatures left the route, timestamp and nonce unauthenticated, so a captured
body/signature could be replayed with fresh headers or redirected within an organization's
secret scope. BadgeSmith signs one deterministic canonical request and moves the verifier
and first-party producers together, rather than retaining legacy verification, negotiation
or a deprecation window; old clients fail authentication by design. The logical ingestion
path is independent of deployment prefixes, and shared canonical construction prevents
producer/verifier drift.

**Retrospective provenance:** reconstructed from the approved design bearing the date above:
[threat model and compatibility decision](https://github.com/localstack-dotnet/badge-smith/blob/621f2cec25ad725880d90804a2877fce80c89eb2/docs/superpowers/specs/2026-08-07-pr5-second-pass-review-remediation-design.md#L9-L55),
[envelope and shared-construction consequences](https://github.com/localstack-dotnet/badge-smith/blob/621f2cec25ad725880d90804a2877fce80c89eb2/docs/superpowers/specs/2026-08-07-pr5-second-pass-review-remediation-design.md#L122-L145).
[Implementation 4d9c699](https://github.com/localstack-dotnet/badge-smith/commit/4d9c6999d0e86b247f4d4eaaf6347db5726da5c0)
and [PR #5 closeout](../research/2026-08-08-pr5-second-pass-closeout.md) corroborate the
delivered choice. This records the design's stated date, not an inferred private discussion
or a claim that its Git commit preceded implementation. The
[current security owner](../architecture/security-and-storage.md#canonical-hmac-authentication)
owns the exact protocol and current transport requirements.
