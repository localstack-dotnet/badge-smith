# Research

Dated research notes and investigations for BadgeSmith (performance findings, AWS/AOT
behavior, dependency evaluations, upstream compatibility checks).

Research is historical evidence, not current policy. It may intentionally describe a
superseded implementation or conclusion; current owners are mapped in [the documentation
policy](../engineering/documentation.md).

Every research doc MUST include its evidence snapshot date. Naming:
`YYYY-MM-DD-<topic>.md`.

## Recent performance evidence

- [2026-10-19 template-construction release validation](2026-10-19-template-construction-release.md):
  fresh current-package allocation RED/GREEN, full tests and a new Native/AL2023 compatibility smoke;
  separate from the older Native counter study and package rollout.
- [2026-10-19 latest-stable package refresh](2026-10-19-package-refresh.md): package/API adaptations,
  preserved VSTest behavior and fresh managed validation; new Native qualification remains separate.
- [2026-10-19 AWS packages and Native AOT](2026-10-19-aws-native-aot-package-review.md):
  live package currency, annotated reflection versus reflection-free code, the upstream 3.0.1
  trimmability fix and official guidance; no package upgrade or performance result.
- [2026-10-19 Native first-request attribution](2026-10-19-native-first-request-attribution.md):
  provider BDN, framing/TLS/certificate-input diagnostics, official AL2023 execution, retained
  failures and offline-verifiable numeric evidence. Local observations, not production SLO proof.
- [2026-10-18 template-construction candidate](2026-10-18-template-construction-optimization.md):
  separate allocation-only candidate, not a demonstrated startup/RSS improvement.
- [2026-10-18 complete post-deploy window](2026-10-18-post-deploy-complete-window.md):
  deployed-revision observations and production qualification limits.
