# Research

Dated research notes and investigations for BadgeSmith (performance findings, AWS/AOT
behavior, dependency evaluations, upstream compatibility checks).

Research is historical evidence, not current policy. It may intentionally describe a
superseded implementation or conclusion; current owners are mapped in [the documentation
policy](../engineering/documentation.md).

Every research doc MUST include its evidence snapshot date. Naming:
`YYYY-MM-DD-<topic>.md`.

## Recent performance evidence

- [2026-10-19 Native first-request attribution](2026-10-19-native-first-request-attribution.md):
  provider BDN, framing/TLS/certificate-input diagnostics, official AL2023 execution, retained
  failures and offline-verifiable numeric evidence. Local observations, not production SLO proof.
- [2026-10-18 template-construction candidate](2026-10-18-template-construction-optimization.md):
  separate allocation-only candidate, not a demonstrated startup/RSS improvement.
- [2026-10-18 complete post-deploy window](2026-10-18-post-deploy-complete-window.md):
  deployed-revision observations and production qualification limits.
