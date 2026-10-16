# Roadmap Second Wave — Approved Scope

**Approval:** Deniz explicitly approved starting the three-workstream second wave on 2026-10-16.
This is separate from the frozen first-increments package and its final quality review.

**Goal:** Strengthen artifact source identity, migrate the next small logging boundary, and obtain
a credible bounded provider baseline before considering a production cache feature.

## Ownership and Execution

- PM owns priorities, scope, cross-stream dependencies, roadmap state and human decision gates.
- Delivery, Logging and Performance leads own implementation, tests, independent child review and
  fix loops within the scope below. They report concise outcomes and real blockers, not every diff.
- Integration/Quality owns technical baselines, worktree/delta identity, resource scheduling and
  combined/final quality gates. It coordinates directly with domain leads.
- Leads use isolated second-wave copies/layers of the accepted first-increment prerequisites;
  they must not mutate any first-package candidate, original frozen diff, or immutable evidence.
  Uncommitted parent changes are not assumed to be copied into a new child checkout.
- Follow applicable skills and repository canon. Independent reviewers receive exact requirements,
  a stable delta and author evidence; a reviewer is not its own implementation author.

## Approval Boundaries

- Local implementation, existing dependency restore, focused builds/tests, fixture checks and the
  bounded measurement below are authorized. No package/analyzer/build-policy changes.
- No commit, staging, push, PR, branch merge, release, AOT publish, deployment, AWS operation,
  GitHub settings mutation, production HTTP probe or infrastructure startup is authorized.
- Hosted master/environment protection and IAM trust remain separate security decisions. Code
  ancestry validation does not replace those controls or prove the tag-run hypothesis.
- Ordinary independent work may overlap in separate worktrees. Quality Lead schedules shared
  validation resources. Measurement gets a quiet exclusive slot, not concurrent build/infra load.
- No repeated user approval is needed for routine technical choices inside this approved scope.
  A material scope change, external action or security-policy change is escalated to PM.

## 1. Delivery: Master-History Binding

**Owner:** Delivery Lead.

**Scope:** `.github/scripts/delivery/artifact.mjs`, its existing Node tests, and the production CDK
guide. Preserve current artifact/digest/attempt/checkout/smoke/redaction gates.

**Acceptance:**
- Resolve the explicit master branch head and prove the selected CI source is that commit or an
  ancestor. Selected older successful master runs remain permitted; master-tip-only is not required.
- Missing, malformed, inaccessible or divergent ancestry evidence fails closed before deployment.
- Keep safe finite diagnostic categories. Record the observed master/source identity, comparison
  evidence and selected run timestamp without exposing credentials or signed URLs.
- Test positive current/ancestor cases and wrong-direction/divergent/missing/unknown/API-error cases,
  including use of a resolved SHA rather than an ambiguous branch/tag name.
- No hosted settings, IAM changes, workflow-trigger changes or live attack probes.

## 2. Logging: LambdaRequestProcessor Only

**Owner:** Logging Lead.

**Scope:** `LambdaRequestProcessor.cs`, its existing HTTP tests, and the narrow non-generic logger
setup support in `TestBase.cs`, layered on the accepted CORS helper baseline.

**Approved observable metadata:**
- `1001 / LambdaRequestHandling`: Information, existing Method/Path template and order.
- `1002 / LambdaUnhandledError`: Error, existing template and exact caught exception identity.

**Acceptance:**
- Preserve request-ID string scope, scope lifetime/disposal, call order, fallback/raw values,
  request/token handoff, CTS/timeout construction, try boundaries and safe HTTP responses.
- Preserve current cancellation-to-safe-500 behavior; do not turn this migration into cancellation
  or exception-policy redesign.
- Test success, error, cancellation, scope isolation and per-level enabled/disabled combinations.
  Retain existing tests and the accepted CORS contract; no generic logging framework.
- ApiRouter, HMAC, Nonce, providers and repository-wide EventId policy remain outside this slice.

## 3. Performance: Measurement-First Harness

**Owner:** Performance Lead.

**Scope:** Existing provider benchmark and its fake-upstream infrastructure, layered on the accepted
BDN repair. Keep the original three-name fixture and add the proposed deterministic 32-name fixture.

**Acceptance:**
- Cover two providers by 200/304 and two corpus sizes: eight cases, no range/prerelease Cartesian
  expansion. Validate literal expected results and prime warm paths outside measurement.
- Use fake upstreams only: no network, credentials, production runtime/cache change or new package.
- Prove selection and one-job-per-case with list/Dry checks. Quality Lead may schedule one bounded
  Short baseline over those eight cases using explicit job/runtime and retained raw exports.
  No default/Long/full-suite precision campaign is authorized.
- Record source plus prerequisite/delta identity and timing/allocated-byte evidence. Do not equate
  MemoryDiagnoser with retained cache heap, or local microbenchmarks with production INIT/memory.
- Immutable JSON-name reuse, semantic-version-object caching, selected-result caching, lazy
  exception memoization and freshness shortcuts are not authorized production changes.
- A disappointing or inconclusive measurement is a valid result, not a reason to add cache layers.

## Workstream Milestones

- Logging: processor-only implementation and independent Standards/Spec review completed; ready
  for Quality Lead integration. No PM decision or blocking finding reported. Not yet integrated
  or committed; native size/performance remain unverified. Broader logging is outside this scope.
- Delivery: master-history binding implemented, independently accepted on both axes and ready for
  Quality Lead integration. Local fixtures passed; no PM blocker reported. Ancestry is a
  validation-time snapshot and oversized comparisons fail closed; hosted H1/H4 and AWS H2 remain
  separate unresolved gates. Not yet integrated or committed.
- Performance: the isolated two-file harness delta is independently accepted on both axes and
  ready for Quality Lead integration. The single quiet Short baseline completed eight cases;
  source and raw exports are frozen. Timing intervals are wide, retained heap and component/A-B
  benefit remain unmeasured, and no production cache change is justified or authorized by this
  outcome. No PM blocker reported.
- All three domain outcomes are ready for integration; combined second-wave validation and final
  quality acceptance remain separate gates. No domain commits or external operations are inferred.

## Delivery and Quality Gates

- Each lead supplies a stable scoped delta, actual validation evidence, independent Standards/Spec
  verdicts, residual risks and the next decision-ready outcome to Integration/Quality.
- Quality Lead manages technical integration and final review. PM does not become a per-file review
  bottleneck. First-package final review continues independently.
- Existing ARM64 CI baseline remains 6,000,572 bytes, 572 above the 6,000,000-byte goal. Neither
  candidate native size nor production deployed identity/INIT/memory is verified by this wave.
- GitHub H1/H4 were observed missing; AWS trust H2 remains unverified. No security/compliance or
  production-readiness claim follows from local code/tests.
