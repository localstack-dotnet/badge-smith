# HTTP contract coverage and published AOT verification are separate proofs

Date: 2026-07-04

BadgeSmith replaced the RIE/event-envelope contract foundation with Aspire Testing and
ordinary HTTP requests through `APIGatewayEmulator`. This keeps broad HTTP contracts in
the existing development topology, but explicitly does not prove the published Native
AOT artifact: artifact execution remains a separate verification concern rather than a
claim made from a successful project-harness test or hosted build. A failed local AOT
target does not reintroduce RIE as the fallback.

**Retrospective provenance:** reconstructed from the approved-direction document dated
2026-07-04, preserved in the 2026-07-05 closeout commit:
[decision and replaced RIE foundation](https://github.com/localstack-dotnet/badge-smith/blob/991769eb65e956478674a381018eb8d1a3446603/docs/plans/2026-07-04-rie-free-aspire-localstack-redesign.md#L8-L35),
[proof limitation and fallback](https://github.com/localstack-dotnet/badge-smith/blob/991769eb65e956478674a381018eb8d1a3446603/docs/plans/2026-07-04-rie-free-aspire-localstack-redesign.md#L170-L186).
The [superseded artifact/RIE design](https://github.com/localstack-dotnet/badge-smith/blob/991769eb65e956478674a381018eb8d1a3446603/docs/plans/2026-07-02-iteration0-aot-contract-tier-design.md#L23-L56)
records the actual earlier alternative. The [dated image spike](../research/2026-07-04-localstack-lambda-image-spike.md)
records a then-observed limitation, not current upstream compatibility. Current
[test topology](../../tests/BadgeSmith.Api.Tests/README.md#contract-tests) and
[validation boundaries](../engineering/quality-gates.md) do not claim a future AOT smoke
tier is complete, the latest ZIP has executed, or production INIT/memory has been measured.
