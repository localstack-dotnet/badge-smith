# Production and local-performance infrastructure have separate CDK apps

Date: 2026-08-07

The former context switch silently selected production for missing or unknown values,
while placing both stacks in one app would still construct and synthesize their different
assets, environments and production hosted-zone lookup before CLI stack selection.
BadgeSmith therefore uses two small app entrypoints backed by shared constructs, preserving
the existing production deployment identity and using context only to configure a known
stack. Production commands target `BadgeSmithStack` explicitly; the local-performance app
is not an AWS deployment target.

**Retrospective provenance:** reconstructed from the approved design bearing the date above:
[problem, rejected alternative and boundary](https://github.com/localstack-dotnet/badge-smith/blob/621f2cec25ad725880d90804a2877fce80c89eb2/docs/superpowers/specs/2026-08-07-pr5-second-pass-review-remediation-design.md#L237-L296).
[Implementation eae8df3](https://github.com/localstack-dotnet/badge-smith/commit/eae8df3d2bafa9d38ebe833e047991d88b4f0d96)
and [PR #5 closeout](../research/2026-08-08-pr5-second-pass-closeout.md) corroborate the
delivered choice, without establishing an earlier private approval timestamp or Git ordering.
The [current infrastructure boundary](../architecture/platform-and-delivery.md#infrastructure-boundaries)
and [production](../../build/BadgeSmith.CDK/README.md) /
[local-performance](../../build/BadgeSmith.CDK.LocalPerformance/README.md) guides own required
environment values, artifacts, working directories and safe commands; superseded local
environment defaults in the historical design are not current instructions.
