# W1.7 Closeout And Platform Refresh Design

Date: 2026-07-09

## Summary

W1.7 is a single consolidated wave that closes unfinished Wave 1 correctness work and
W1.5 tooling migration, and refreshes package pins so Wave 2 starts on a clean baseline.

Primary drivers:

- Remaining Wave 1 bugs from the 2026-07-02 deep-dive (GSI1PK, nonce ordering,
  error-message hygiene, PAT rotation).
- Remaining W1.5 work after the `52d038a` file-based CLI checkpoint (workflows, script
  retirement, docs, perf-baseline ship-or-defer).
- Platform refresh: `LocalStack.Aspire.Hosting` 13.4.0, Aspire 13.4.6 line, explicit
  `Aspire.Hosting.AWS` 13.3.1, full Central Package Management (CPM) bump, and removal of
  the direct `MessagePack` dependency (no longer required for the LocalStack Aspire
  security workaround).

Approach: **A — single consolidated wave**, ordered packages → stabilize → finish W1.5 →
remaining W1 bugs → ROADMAP closeout.

## Current State (as of design)

- Branch: `feature/iteration0-aot-contract-tier`.
- Iteration 0: done (`991769e`).
- Wave 1: partial — HMAC `repoIdentifier` fixed (`845440f`); naming hygiene (`5cbf87b`);
  remaining correctness still open.
- W1.5: checkpoint only (`52d038a`) — hosted `tools/badgesmith.cs` CLI and seeder
  migration landed; workflows and tracked `.sh`/`.ps1` remain.
- Packages today: Aspire / LocalStack.Aspire.Hosting / Aspire.Hosting.Testing `13.1.0`;
  direct `MessagePack` `3.1.7` on Host and test projects; no explicit
  `Aspire.Hosting.AWS` PackageReference (consumed transitively).

## Goals

1. Full CPM bump to current stable versions at implementation time.
2. Align Aspire/LocalStack hosting stack with published LocalStack.Aspire.Hosting 13.4.0.
3. Remove direct MessagePack usage and the CPM entry.
4. Finish W1.5 acceptance criteria (workflows, no tracked shell/PowerShell scripts, docs,
   perf-baseline implemented or explicitly deferred).
5. Finish remaining Wave 1 correctness items with minimal pinning tests.
6. Update ROADMAP so Wave 2 is the next active engineering backlog item.

## Non-Goals

- Wave 2 broad test safety net (HMAC full suite, ResponseHelper, real RouteTable,
  NuGetVersionService expansion) — follows after W1.7.
- Wave 3 DRY / dead-code / DynamoDB PITR hygiene.
- Performance pass from the inbox (edge 404 caching, INIT warm-up, TrimMode, etc.).
- CDK deploy, Lambda publish, or any live AWS mutation.
- New product features or public HTTP contract shape changes (beyond bug-fix behavior).
- Replacing `scripts/k6-perf-test.js` (stays as k6 scenario input).

## Success Criteria

W1.7 is complete when all of the following hold:

1. Solution restores and builds Release with zero warnings.
2. Unit tests and Aspire contract/functional tests pass after the package refresh.
3. `LocalStack.Aspire.Hosting` is `13.4.0`; Aspire AppHost / Testing / AppHost.Sdk are
   `13.4.6`; `Aspire.Hosting.AWS` is explicit at `13.3.1` in CPM and Host.
4. No direct `MessagePack` PackageReference or CPM `PackageVersion` remains.
5. CI/workflows call `tools/badgesmith.cs` (Unix direct path; Windows
   `dotnet run --file`); planned tracked `.sh`/`.ps1` helpers are gone.
6. GSI1PK case normalization, nonce-after-signature ordering, and client error hygiene are
   fixed and pinned by tests.
7. PAT rotation checklist is executed (ops) and documented for local secret handling.
8. `docs/ROADMAP.md` marks Wave 1, W1.5, and W1.7 done; Wave 2 is next.

## Workstreams

### A. Package upgrade (first)

**Authoritative pins for the Aspire/LocalStack cluster:**

| Package | Target |
| --- | --- |
| `LocalStack.Aspire.Hosting` | 13.4.0 |
| `Aspire.Hosting.AppHost` | 13.4.6 |
| `Aspire.Hosting.Testing` | 13.4.6 |
| `Aspire.AppHost.Sdk` (Host csproj Sdk attribute) | 13.4.6 |
| `Aspire.Hosting.AWS` | 13.3.1 (new explicit CPM + Host PackageReference) |
| `MessagePack` | remove from CPM, Host, and test projects |

Rationale: LocalStack.Aspire.Hosting 13.4.0 declares dependencies on Aspire.Hosting
13.4.6 and Aspire.Hosting.AWS 13.3.1 and does not require a direct MessagePack pin.

**Rest of CPM:** bump remaining packages in `Directory.Packages.props` to latest stable
at implementation time (AWS SDK v4 line, OpenTelemetry, analyzers, Microsoft.Extensions
10.x, Spectre/CliWrap, xUnit/Testcontainers, CDK/Constructs, tool packages, etc.).

Rules:

- Versions live only in CPM (`Directory.Packages.props`); do not hand-edit versions into
  individual project files.
- Use package-management discipline (dotnet CLI / approved tooling), not ad-hoc XML
  version edits when practical.
- Do not introduce MessagePack back as a transitive workaround; if a package forces an
  insecure transitive, document and choose the supported upgrade path.

**Process:**

1. Bump Aspire/LocalStack/AWS-hosting cluster; restore Host + tests.
2. Add explicit `Aspire.Hosting.AWS` reference on Host; remove MessagePack.
3. Broad CPM bump; fix compile breaks.
4. Run unit + Aspire contract tests; treat green contract suite as the gate for this
   workstream.

**Risks:**

- Aspire 13.1 → 13.4 API/behavior changes in Host or contract fixtures.
- Aspire.Hosting.AWS has newer 13.4/13.5 packages on NuGet; W1.7 **stays on 13.3.1** to
  match LocalStack.Aspire.Hosting 13.4.0’s declared dependency.

### B. Finish W1.5 tooling (second)

Carry forward existing W1.5 plan acceptance criteria; do not redesign the CLI.

Foundation already landed in `52d038a` (`tools/badgesmith.cs` hosted DI CLI, seeder
migration, SDK pin, tool tests).

Remaining work:

1. **Workflows** — update `.github/workflows/ci-cd.yml`, `deploy.yml`,
   `run-dotnet-tests/action.yml`, `update-test-badge/action.yml` to invoke
   `tools/badgesmith.cs` (Unix: executable path; Windows: `dotnet run --file`); install
   SDK from `global.json`.
2. **Delete tracked helpers** per W1.5 delete list, including:
   - `scripts/build-lambda.sh` / `.ps1`
   - `scripts/perf-baseline.sh` / `.ps1` / `perf-baseline-seed.sh`
   - `scripts/test-ingestion.sh` / `.ps1`
   - `.github/workflows/run-dotnet-tests/run-unix.sh` / `run-win.ps1`
3. **Keep** non-script assets: `scripts/k6-perf-test.js`, sample JSON payloads.
   `scripts/localstack.yml` only if still referenced; otherwise delete with a docs note.
4. **`perf baseline` command** — implement in the C# tool **or** record an explicit
   deferral in ROADMAP with rationale. Preference: implement if effort stays bounded;
   documented deferral is an allowed W1.7 exit so tooling closeout is not blocked forever.
5. **Docs** — `tools/README.md` is the home for tool usage; retire
   `scripts/README-TEST-INGESTION.md` / `scripts/README-PERF-TESTING.md` when content is
   moved; update current-facing refs in `AGENTS.md`, `ARCHITECTURE.md`, `README.md`.
   Historical plans/research/handover prompts may retain old paths.

**Gate:** `git ls-files "*.sh" "*.ps1"` returns no files (target: empty). Workflows and
current docs must not reference deleted scripts.

### C. Remaining Wave 1 correctness (third)

Source: `docs/research/2026-07-02-code-review-findings.md` §1–2.
Already fixed: HMAC `repoIdentifier` (`845440f`). Tool path already defaults lambda RID
to `linux-arm64` and fixed seeder `.dist` JSON (`52d038a`); finishing W1.5 retires the
legacy scripts that still defaulted to x64 / bad template.

| Item | Change | Verification |
| --- | --- | --- |
| GSI1PK case | `GetLatestTestResultAsync` must build GSI1PK from lowercase-normalized owner/repo/platform/branch (same as write path) | Mixed-case badge/redirect routes resolve stored data |
| Nonce order | `ValidateRequestAsync`: timestamp → secret + signature → **nonce last** | Invalid signature does not burn nonce; legitimate retry works; replay still fails |
| Error hygiene | No `ex.Message` / internal detail in client-facing bodies (`ApiRouter`, `NonceService`, `TestResultsService`, and sibling leak paths found in the same pass) | Generic client messages; details only in logs |
| PAT rotation | Ops: if local `organization-pat-mapping.json` holds a real `ghp_…`, rotate in GitHub and update the local gitignored file only. Document safe handling in `tools/README.md` | No real token in the repo; `.dist` stays placeholders |

**Testing discipline:** failing test first per bug where practical. Update only pins that
intentionally change with the fix. Do not expand into full Wave 2 coverage here; add
minimal tests that lock each fix.

### D. Closeout (last)

1. Update `docs/ROADMAP.md`:
   - Wave 1 → done
   - W1.5 → done
   - W1.7 → done (new row linking this design and the implementation plan)
   - Backlog: Wave 2 next; remove completed residual Wave 1 bullets
2. Optional short handover prompt under `docs/agents/handover-prompts/` if useful for the
   next session.
3. Run Slopwatch on LLM-touched code when available.

## Execution Order

1. Packages (Aspire/LocalStack/AWS-hosting + MessagePack removal + full CPM bump)
2. Stabilize (build + unit + contract/functional tests)
3. Finish W1.5 (workflows, script deletion, perf-baseline ship-or-defer, docs)
4. Wave 1 correctness (GSI1PK → nonce → error hygiene → PAT checklist)
5. ROADMAP / closeout verification

Stay on `feature/iteration0-aot-contract-tier` unless isolation is required mid-flight.

## Approval And Policy Notes

Per `AGENTS.md`:

- Package version changes, CI/workflow edits, production bug fixes, and script deletion
  require explicit approval (`go` / `apply` / `proceed` / `başla` / `yap`) before mutation.
- This design document is documentation-only and may be written without that gate; commits
  still require approval.
- Native AOT constraints remain for `src/BadgeSmith.Api`. Hosting packages and
  `LocalStack.Client.Extensions` stay out of the shipped Lambda.
- Do not commit real secrets, PAT files, or local machine paths.

## Verification Matrix

Minimum verification for W1.7:

| Check | Expectation |
| --- | --- |
| `dotnet build` solution Release | 0 warnings / 0 errors |
| `dotnet test` Api.Tests (unit + functional/contract as applicable) | pass |
| `dotnet build tools/badgesmith.cs` | pass |
| Package pins | Aspire 13.4.6 cluster; LocalStack.Aspire.Hosting 13.4.0; Aspire.Hosting.AWS 13.3.1 explicit; no MessagePack |
| `git ls-files "*.sh" "*.ps1"` | empty after W1.5 phase |
| Workflow/docs grep for deleted scripts | no current-facing hits |
| Slopwatch (when available) | no warnings under project exclude set |

## Relationship To Prior Plans

- W1.5 implementation plan remains the detailed checklist for tooling finish:
  `docs/superpowers/plans/2026-07-06-w1-5-file-based-tools-implementation-plan.md`
- Wave 1 bug detail remains:
  `docs/research/2026-07-02-code-review-findings.md`
- W1.7 does not reopen Iteration 0 design; it consumes the green contract harness as a
  regression gate for package upgrades and bug fixes.

## Open Decisions Resolved In This Design

| Question | Decision |
| --- | --- |
| Wave structure | Single consolidated W1.7 (Approach A) |
| Package breadth | Full CPM bump to current stable |
| Aspire version | Latest 13.4.x → pin **13.4.6** to match LocalStack.Aspire.Hosting 13.4.0 |
| Aspire.Hosting.AWS | Explicit CPM + Host at **13.3.1** |
| MessagePack | Remove direct dependency |
| Sequence | Packages first, then W1.5 finish, then remaining W1 bugs |
| W1.5 remaining | Full finish (workflows, script delete, docs, perf ship-or-defer) |
| Wave 1 remaining | All remaining items including PAT ops checklist |
| Perf baseline | Implement if bounded; explicit deferral allowed |

## Implementation Next Step

After this design is approved in-repo, produce an implementation plan under
`docs/superpowers/plans/` via the writing-plans workflow, then execute only with explicit
approval for mutating steps.
