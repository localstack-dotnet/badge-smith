# Roadmap First Increments Implementation Plan

**Goal:** Implement the approved delivery, benchmark-instrument, and CORS logging increments
without deployment or unrelated performance work.

**Approval:** Deniz approved the consolidated first-increment package on 2026-10-16. The scope
below records that approval; earlier discovery reports are evidence, not additional permission.

**Architecture:** Three independent worktrees based on `d778400bd16cee44994f1c9bb68121e6a54d66cf`.
The PM coordinator owns roadmap priorities, scope, dependencies, shared product documentation and
human approval boundaries. Domain leads own approved implementation, tests, independent child
reviews and fixes. An integration/quality lead owns technical evidence, validation scheduling and
the final independent combined review. Standards and Spec remain separate acceptance axes.

**Tech Stack:** Existing .NET 10, xUnit v3 on VSTest, BenchmarkDotNet, Node built-ins, and GitHub Actions.

**Spec:** The approved scope and acceptance criteria in this document.

## Global Constraints

- No commit, amend, push, PR, merge, release, AOT publish, CDK synth/deploy, or AWS operation is
  authorized. Production HTTP smoke is implemented but not run against a live deployment.
- No new package, analyzer relaxation, test weakening, Docker/Aspire startup, or precision
  benchmark run is authorized. Existing dependencies and local fixture-based validation suffice.
  Restoring the repository's existing declared NuGet dependencies is allowed in the validation
  slot; adding/updating packages or installing tools is not.
- Use the repository's relevant skills, canonical documents, coding style, test-first development,
  verification, and Slopwatch rules. Do not repeat skill-availability audits.
- Preserve Native AOT safety, HTTP contracts, and logging behavior except the explicitly approved
  CORS event metadata change below.
- Keep changes uncommitted in their existing worktrees. Do not overwrite another stream's work.
- PM owns this plan, ROADMAP, and ARCHITECTURE. Domain leads may update only their assigned
  operational documentation. Do not generate separate overlapping plans or a shared framework.
- Ordinary .NET build/test jobs follow the integration/quality lead's validation scheduling. Node fixture
  tests may run independently. Benchmark dry runs prove executability, not performance.
- Each domain lead owns file-level evidence and independent review within its approved scope.
  Report concise outcomes, next deliverables, cross-domain dependencies and genuine blockers to PM;
  route detailed diff/review/validation traffic directly to the integration/quality lead.

## Review Focus

- Untrusted workflow inputs or artifact metadata must not reach a shell or filesystem unsafely.
- Missing, expired, mismatched, or ambiguous artifact evidence must fail before deployment.
- HTTP 200 with invalid content, unexpected redirects, timeouts, and permanent failures are not
  successful smoke checks. No production data is seeded or mutated.
- Benchmark labels and setup boundaries must describe what is actually measured. Microbenchmark
  output never establishes production INIT, memory, or ARM64 artifact-size compliance.
- Disabled logging, nullable header values, structured fields, and HTTP behavior remain correct;
  the new CORS event ID/name are the only approved observable logging change.

## Task 1: Delivery Hardening

**Owner:** Delivery worktree.

**Files:**
- Modify `.github/workflows/ci-cd.yml` and `.github/workflows/deploy.yml`.
- Create `.github/scripts/delivery/artifact.mjs`, `smoke.mjs`, and `delivery.test.mjs`.
- Update delivery operations in `build/BadgeSmith.CDK/README.md`.
- Relocate dated pinned-CLI evidence to `docs/research/2026-10-16-delivery-cdk-diff-semantics.md`;
  keep operational rules and a link in the production guide. Correct only the now-stale deploy
  build-use sentence in `tools/README.md`; no perf/tooling ownership expansion.

**Consumes:** Explicit CI run/artifact identifiers and explicit smoke target configuration.
**Produces:** Validated provenance and a bounded, fixture-testable GET smoke implementation.

- [x] Test rejection of wrong repository/workflow/event/branch, unsuccessful or unfinished runs,
  mismatched SHA/attempt, missing artifacts, and incorrect archive or inner-ZIP digests.
- [x] Bind promotion to a successful same-repository `ci-cd.yml` push on `master`. Checkout the
  validated source SHA for CDK. Record archive digest separately from ZIP SHA-256 and exact bytes.
- [x] Remove the deploy-time `build_lambda` path; deploy consumes the verified CI artifact.
- [x] Remove error swallowing from CDK diff. Verify the pinned CLI's difference-versus-failure
  semantics without contacting AWS; do not replace that verification with an assumption.
- [x] Validate required smoke configuration before deployment. Do not invent badge identifiers.
- [x] Implement bounded GET health and badge checks for direct Gateway and public-domain paths.
  Validate JSON and expected response contracts, not merely status 200. Retry only transient
  failures. Preserve the proposed total 180-second budget, 10-second request timeout, and at most
  five attempts, with deterministic fixture tests for deadline enforcement.
- [x] Missing required inputs or smoke failure fails the workflow. No automatic rollback.
- [x] Test malformed JSON, HTML 200, unexpected redirects, permanent errors, timeout, cancellation,
  and retry exhaustion. Real CDK failures prevent deploy; fixture tests never invoke real AWS.
- [x] Run Node fixture tests, available workflow static checks, diff checks, and Slopwatch. Report
  hosted/deployment checks as unrun; do not claim production readiness from local fixtures alone.
- [x] Preserve safe failure diagnostics: emit only owned, finite error categories and validated
  smoke context (check/origin label, numeric status/attempt); never log arbitrary exception text,
  response bodies, signed storage URLs, tokens, or credentials. Test diagnostic usefulness and
  redaction independently. Network/unknown errors retain generic safe output.

## Task 2: Repair Benchmark Instruments

**Owner:** Performance worktree; scope replaces the deferred local-core CLI implementation.

**Files:**
- `tests/BadgeSmith.Api.Performance.Tests/Program.cs`
- `tests/BadgeSmith.Api.Performance.Tests/BufferAllocationBenchmarks.cs`
- `tests/BadgeSmith.Api.Performance.Tests/RoutingBenchmarks.cs`
- `tests/BadgeSmith.Api.Performance.Tests/ProviderBenchmarks.cs`: remove only `[SimpleJob]` so
  an explicitly selected CLI Dry job cannot coexist with that class-declared default job.
- Focused supporting tests or helpers only where necessary to verify these changes; report any
  proposed additional paths before broadening ownership. No project/package/build-policy changes.

**Consumes:** Existing benchmark suites and actual production route contracts.
**Produces:** A truthful, filterable microbenchmark entry point and corrected measurement setup.

- [x] Establish focused failing checks for argument forwarding, measurement setup, and current
  ingestion-route semantics. Do not accidentally launch the entire old benchmark suite to test
  ignored arguments; keep validation bounded and terminate any spawned process safely.
- [x] Remove custom `--type` selection and use standard BenchmarkDotNet selection/filtering.
  Pass CLI arguments to the runner; require noninteractive automated invocations.
- [x] Remove the class-level job declarations in Buffer, Routing, and Provider benchmarks. Keep
  diagnostic/category attributes and provider corpus, setup, and benchmark bodies unchanged.
  Verify the selected cases resolve to one Dry job each before executing bounded dry validation.
- [x] Replace misleading Current/Optimized comparisons of the same resolver with truthful
  measurements. Move resolver construction outside route-resolution measurement; retain genuine
  buffer-allocation comparisons rather than deleting unrelated coverage.
- [x] Correct stale ingestion patterns to
  `/tests/results/{platform}/{owner}/{repo}/{branch}`. Synthetic mechanisms may remain synthetic,
  but must not claim to represent an obsolete production route.
- [x] Under the validation slot, perform affected Release build and bounded list/dry execution
  proving selection and route success. Do not run precision Short/default jobs without a separate
  scheduling decision. Preserve raw command/output evidence; dry results are not performance data.
- [x] Run Slopwatch and diff checks. No provider corpus expansion, caching optimization, local-core
  CLI, production runtime changes, or production-performance claims.

## Task 3: CORS Source-Generated Logging

**Owner:** Logging worktree.

**Files:**
- `src/BadgeSmith.Api/Core/Routing/Cors/CorsHandler.cs`
- `tests/BadgeSmith.Api.Tests/TestBase.cs`
- `tests/BadgeSmith.Api.Tests/Routing/CorsHandler/HandlePreflightTests.cs`

**Consumes:** Existing preflight inputs and test logger helpers.
**Produces:** One source-generated CORS log with explicit tested metadata.

- [x] Add focused tests for enabled Debug logging: one record, exact template and structured
  Origin/Method/Headers fields and values, nullable/empty/whitespace cases, null exception, and
  unchanged HTTP behavior. Test disabled Debug produces no log call.
- [x] Migrate the single CORS Debug call to source-generated logging. Explicitly approved metadata:
  `EventId=1000`, `EventName=CorsPreflightRequest`. Preserve field order, template, values, level,
  call order, and response behavior.
- [x] Extend existing logger helpers narrowly without deleting unused helpers or the TestBase Moq
  pragma. Do not couple assertions to generated internal state-type names.
- [x] Remove only the CORS CA1873 pragma pair after the migrated call no longer requires it.
- [x] Under the validation slot, restore/build affected scope, run focused CORS and Unit tests,
  and verify the API compiles with telemetry/localstack disabled without AOT publishing.
- [x] Inspect generated event metadata and run Slopwatch/diff checks. HMAC, Nonce, providers,
  LambdaRequestProcessor, and analyzer policy remain untouched.

## Task 4: Coordinator Documentation and Review

- [x] Correct the factual validator-store wording in ARCHITECTURE: 304 avoids upstream body
  transfer, but cached raw JSON is still deserialized and versions parsed/filtered per request.
  Preserve every-request revalidation and do not implement selected-result/freshness caching.
- [x] Mark only the approved first slices active or integration pending in ROADMAP; retain local-core
  CLI and broader migration as deferred work. Preserve dated research as historical evidence.
- [x] Obtain independent domain Standards and Spec verdicts on stable uncommitted diff packages.
  Include new files and exact scope in review inputs; route fixes back to the owning worker.
- [x] Record an identified ARM64 artifact size baseline for `d778400` before integrating logging.
  Prefer an existing verified CI artifact; if obtaining it requires a prohibited operation, report
  that gate as unverified and ask separately. ZIP bytes prove size only, not INIT or memory p95.
  Evidence: [ARM64 CI artifact baseline](../research/baselines/2026-10-16-d778400-arm64-artifact.json).
  The archive digest matched GitHub metadata. The inner ZIP is 6,000,572 bytes, already 572 bytes
  above the 6,000,000-byte goal. Its production-deployed identity and candidate artifact size remain
  unverified; this observation does not authorize extra optimization or change the goal.
- [x] Verify cross-stream ownership and report remaining unrun hosted/AOT/production checks. Do not
  merge or commit the independent branches under this approval. Final independent whole-package
  review completed: Standards READY / Spec READY (round 1), no Critical/Important findings; its
  Minor closeout items (durable local-core CLI backlog entry, final-review status, dead Wave 3 plan
  link) were applied in the local closeout. Local commits were later authorized separately.

## Accepted Task Reviews

- Task 1: implementation accepted for the next integration gate after independent R3 confirmed
  N1 ADDRESSED, Standards ready (Hazır), and Spec ready (Hazır, unchanged) on the scoped test-only
  delta; R2 had approved both axes for the full delivery package. The eight-file diff remains
  frozen and uncommitted. Node fixtures passed 33/33. Hosted workflow execution and production
  smoke remain unrun; AWS trust (H2) is unverified and GitHub H1/H4 are observed missing. This is
  not merge, deployment, AOT, size/performance, or production-readiness approval.
- Task 2: implementation accepted for the next integration gate after independent R1 APPROVE
  on both Standards and Spec (zero Critical/Important). The four-file diff remains frozen and
  uncommitted. This is not a merge, precision, production, or performance-compliance approval.
  Author's bounded validation evidence remains identified separately from reviewer static checks.
- BDN R1 declined-to-judge rulings:
  - D1/S1: keep the currently equivalent mirrored route table in this narrow increment. Moving
    to live RouteTable.Routes or clarifying the mirror comment is deferred maintenance, not a
    blocking defect today. Cost: future route drift remains possible and must not be ignored
    when designing later benchmark coverage.
  - D2/P2: retain existing standard runner exit-code behavior for this increment; do not invent a
    second custom argument/summary policy without a reproducible failure and targeted requirements.
    Document that consumers must inspect selection, validation and result reports, not exit code
    alone. Cost: this runner is not yet a fail-closed automated benchmark gate.
  - D3/P1: coordinator added explicit noninteractive discovery and bounded Dry commands to the
    existing API test guide. No benchmark runner wrapper or new CI behavior is introduced.
  - D4/S3: defer benchmark-name style changes. The applicability of the unit-test When convention
    to measurement labels is unresolved; avoid another case-identity change in this increment.
  - D5: defer the unchanged allowed-method list-copy measurement issue; do not interpret that
    benchmark's allocation as solely the resolver's internal allocation.
  - D6: defer the unchanged void RouteValues result-consumption/DCE risk; later optimization work
    must validate these instruments before relying on their allocation columns.
  - D7: do not expand the benchmark corpus. Negative method/bare-path correctness was checked by
    the focused probe; broader failure-path timing belongs to a separate scope.
  - D8: retain scratch red/green evidence for this repair; no new permanent xUnit suite is added.
    Cost: automatic future regression detection remains weaker than a dedicated instrument suite.
  - S2: retain distinct four-route resolution and five-route allowed-method fixtures as requested;
    treat their results as different workloads. A clarifying comment is deferred, not an equivalence
    claim or permission to compare their numbers as the same operation.
  These minors and limits remain inputs to the final cross-stream review, not silently discarded.
- Task 3: implementation accepted for the next integration gate after independent R1 UYUMLU
  on both Standards and Spec (no blocking/High/Medium findings). The reviewer independently
  inspected generated metadata and verified frozen hashes; author evidence records CORS 29/29,
  Unit 508/508, clean Release/feature-disabled builds and Slopwatch. No performance approval follows.
- Logging R1 rulings:
  - Keep the explicitly approved single EventId 1000/name without inventing a repository-wide
    numbering policy. Broader migration must revisit allocation/collision rules with approval.
  - Keep the valid static partial logger method; do not impose a new global static/instance style.
  - Candidate ARM64 size and INIT/memory remain unverified, with the existing baseline already
    over the size goal. Code review acceptance is not merge/release acceptance; AOT publishing
    still needs separate authorization, not an automatic follow-up job.
  - Defer the Low disabled-Debug plus null-header-dictionary test combination. Enabled null and
    populated disabled cases are protected and no current defect was found. Cost: that combined
    future-refactor regression is not directly pinned.
  - Retain existing ToString-based rendered-message helper and all-level enabled toggle: current
    generated state behavior is equivalent and level is independently asserted. Do not broaden
    helper refactoring for nits.
  - Record non-generic SetupILogger's unset IsEnabled as a broader-migration hazard; it remains
    outside this three-file increment. Future generated-log migrations must address it before
    treating negative log assertions as reliable.

## Coordination Record

- Operating-model correction explicitly directed by Deniz: PM is not the mandatory technical
  reviewer or artifact packager. Delivery, logging and performance retain their domain ownership
  and may delegate independent subagent review themselves. Integration/quality owns combined
  evidence and final review. Earlier coordinator-only-review instructions are superseded as an
  execution arrangement, not as permission to expand feature scope or perform external actions.
- Approved execution: independent existing top-level workstreams with one independent reviewer;
  no automatic commit or merge. The complete approval is recorded above rather than inherited
  from earlier read-only discovery instructions.
- File overlap check: delivery and performance do not share a CLI registry; delivery uses Node
  scripts. Logging and performance touch separate production/test areas. Shared docs remain with
  the coordinator. All baseline/candidate observations retain exact revision identity.
- Initial validation-slot owner: performance, for bounded BDN correctness checks. Logging may
  prepare tests and code but requests the slot before executing any .NET build/test commands.
- Progress: all three implementation scopes dispatched on their existing worktrees. Independent
  reviewer informed of the approved scope. Delivery's six-file uncommitted diff was frozen and its
  fingerprints independently matched; a complete package including new files entered independent
  review round 1. Its Spec verdict was ready for the next gate, with hosted trust settings then unknown;
  Standards required scoped diagnostics/documentation corrections. Fix round 1 was then frozen with
  all eight file hashes matched. Fresh R2 approved Standards and Spec, with prior blockers addressed.
  One confirmed Low test-gap follow-up was assigned: forged known-category/context errors had to
  actually exercise the runtime guard, including an offline mutation check. Only the Node test file
  changed; production modules remained frozen. Its fingerprint and sole-file delta were verified,
  and fresh scoped review round 3 confirmed N1 ADDRESSED, Standards ready and Spec ready.
  Task 1 is accepted for the next gate. Performance released the .NET validation slot after its
  authorized checks and server shutdown; the coordinator independently observed no matching
  validation processes. Logging subsequently completed its
  approved validation and released the slot. Delivery subsequently ran its authorized focused
  existing workflow contracts (5/5), shut down build servers and released the slot; there is no
  current .NET slot owner. No new jobs are authorized merely because the slot is free. Performance's
  four-file diff and full evidence report were captured in a separate immutable package; its author
  patch hash and reverse-check matched current files. Independent R1 approved both axes; Task 2
  is accepted for the next gate with documented minor deferrals. Logging's three-file diff and author validation report
  are also frozen and fingerprinted; its independent review round 1 completed with approval on
  both axes. Task 3 is accepted for the next gate with explicit Low/Nit deferrals. All three
  scopes are accepted for integration; no repeated validation is authorized merely because
  final review was pending; that review has since completed.
- Combined local validation is complete in the existing `work/first-increments-validation` copy
  based on `d778400`: all 21 package file hashes matched; Unit 508/508, focused CORS 29/29,
  workflow contracts 5/5, Node fixtures 33/33 and two selected BDN Dry cases passed. Affected
  Release and feature-disabled builds, Slopwatch and diff checks were clean; no source fixes
  were needed. The validation slot was released. The package excludes the pre-existing untracked
  Wave 2 handover. This is local evidence, not production or merge approval. After the documentation
  refresh, the final independent whole-package review returned Standards READY / Spec READY
  (round 1) with no Critical/Important findings.
- Coordinator docs were authored in the root checkout at `f3ab12b`; integration/quality refreshed
  them as documentation-only deltas onto the validated `d778400` candidate. The earlier r1
  coordinator-docs snapshot is superseded, and root HEAD was never used as a commit base.
- Next-wave read-only preparation was pipelined during Delivery R2: logging examines a non-security
  router/request-processor batch; performance scopes parsed-version reuse under unchanged upstream
  revalidation; the standing reviewer scopes hosted environment/OIDC trust evidence. These are
  bounded read-only proposals outside the accepted diffs, not additional implementation approval.
- Coordinator's GitHub-only GET verification is recorded in
  [delivery trust evidence](../research/baselines/2026-10-16-github-delivery-trust.json).
  Production has no deployment branch restriction and master has no observed protection/effective
  rules. The repository reports its default OIDC template. AWS trust, actual token claims and the
  tag-run hypothesis remain unverified. No setting, IAM policy, workflow or frozen candidate was
  changed for this discovery. Any hosted remediation or AWS read still requires its separate gate.
- Ruling: permit removing only ProviderBenchmarks' `[SimpleJob]` in Task 2. Source inspection
  confirms the attribute exists, and separate class jobs can undermine the approved CLI Dry-only
  validation boundary. This is a bounded instrument-configuration correction, not permission to
  expand the corpus or change provider behavior. Historical comparisons must use explicit matching
  job settings; assuming old and new defaults are identical would invalidate a comparison.
- Delivery R1 rulings (all declined-to-judge items accounted for):
  - D1: fix safe diagnostics, not blanket redaction or raw exception logging. The cost of an
    incorrect classification is either leaked sensitive data or loss of post-deploy diagnosis;
    local tests must cover both boundaries before scoped re-review.
  - D2: relocate pinned version/hash/source-line evidence to dated research without losing it.
    The production guide keeps the current operating rule and follows workflow-owned version pins.
  - D3: permit the delivery worker to correct the single obsolete `tools/README.md` statement;
    coordinator retains ownership of unrelated shared documentation.
  - D4: retain the correct nested numeric-PR format expression and existing contract tests for
    this increment. Flattening it is a deferred minor, not grounds to weaken or expand the test
    contract. Cost: small local expression complexity remains.
  - D5: document required production environment branch restrictions and expected OIDC trust
    boundaries; hosted verification stays explicitly unverified. No hosted/IAM change is approved.
  - D6: retain non-master job skipping as a documented no-deployment outcome; do not add a new
    failure-policy change for this low-severity UX observation.
  - D7: explicit historical master-run promotion remains permitted when every new provenance
    gate passes. The approved contract requires immutable selected successful runs, not latest
    master only; older artifacts lacking the new manifest still fail closed.
  - S5: add code fences to CI provenance summary while editing the existing owned workflow.
  - S6: preserve current checkout layout; the reviewer found no current inheritance defect.
  Any future source/control configuration inheritance requires a new focused decision.
- Delivery R2 rulings:
  - N1: fix only the forged-category/context regression test gap now. The reviewer demonstrated
    that deleting instanceof checks in a scratch copy left the old tests green. Actual production
    guards were correct; no production mutation is authorized for this follow-up.
  - N2: defer default diagnostic-category labeling nits; no success/failure gate or redaction
    boundary is affected today.
  - N3: generic unknown diagnostics for malformed external responses/output files remain consistent
    with the safe-error ruling; avoid expanding error output during a test-only fix.
  - N4: defer the long tools-guide line as cosmetic. D7 historical-run promotion policy remains
    as previously recorded, not silently changed by reviewer delegation.
- Delivery R3 ruling: N1 is ADDRESSED; Standards ready and Spec ready (unchanged). I1 and I2
  are information-only notes, not new defects: the fetch fixture is sanitized before the guard,
  which the new outer-CLI test exercises; existing positive/negative context assertions suffice
  without optional exact-output equality. No further fix is required. Prior N2/N3/N4 deferrals
  and D7 historical successful master-run promotion policy remain unchanged.
- Next performance decision: measurement preparation precedes any production cache feature.
  The proposed immutable JSON-name snapshot does not remove semantic-version parsing or network
  revalidation. Extra lazy state, raw-plus-name retention, and exception memoization need evidence
  and explicit approval; no cache source change, corpus expansion, or new run is authorized yet.
- Local closeout: Deniz authorized four local Conventional Commits (ci, test, refactor, docs) on
  `work/first-increments-validation`, based on `d778400`. They are not merged to master, pushed,
  released or deployed. Hosted Actions, AWS trust (H2), CDK deploy, production smoke, AOT, candidate
  ARM64 size, INIT and memory remain unverified; GitHub H1/H4 remain observed missing. Deferred Nit:
  the dated CDK-diff research note still lists the production branch restriction as unverified,
  whereas the trust evidence records it as observed missing; refresh it in a later delivery change.
