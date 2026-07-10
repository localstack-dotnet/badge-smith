# PR #5 Agent Documentation And Closeout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct BadgeSmith's Aspire source-ownership guidance, reject the Rider/Mend identity collision narrowly, and collect all evidence required to move PR #5 out of draft.

**Architecture:** Keep the OpenCode skill file as a thin discovery relay and put detailed consumer-oriented source navigation in the repository documentation. Treat the Rider/Mend finding as external scanner state with an evidence-backed rejection rather than a code suppression. Run all local and hosted gates only after the three implementation plans are complete.

**Tech Stack:** Markdown skill adapters, NuGet audit, Rider/Mend security inspection, .NET 10 build/test/Native AOT, GitHub Actions, actionlint, Slopwatch, GitHub CLI.

## Global Constraints

- Follow `docs/superpowers/specs/2026-07-10-pr5-merge-readiness-remediation-design.md`.
- BadgeSmith consumes `LocalStack.Aspire.Hosting` and `Aspire.Hosting.AWS`; it does not build or publish those packages.
- Map `LocalStack.Aspire.Hosting` to `localstack-dotnet/dotnet-aspire-for-localstack`.
- Keep `LocalStack.Client` mapped to `localstack-dotnet/localstack-dotnet-client` only for SDK/client configuration investigations.
- Keep `.opencode/skills/aspire-source-navigation/SKILL.md` as a relay to `docs/agents/skills/aspire-source-navigation.md`.
- Reject only `bootstrap@1.0.0` as an identity collision; add no MSBuild, NuGet, Rider, Mend, or repository-wide suppression.
- Keep `<AssemblyName>bootstrap</AssemblyName>` unchanged because AWS Lambda custom runtimes require that executable name.
- Keep PR #5 draft until all local and hosted gates pass and the user explicitly approves ready-for-review.

---

## File Structure

- Modify `.opencode/skills/aspire-source-navigation/SKILL.md`: keep discovery metadata accurate for BadgeSmith consumer work.
- Modify `docs/agents/skills/aspire-source-navigation.md`: own the full source-navigation workflow and package-to-upstream map.
- External state only: Rider/Mend `bootstrap@1.0.0` finding rejection.
- Verification only: source, tests, workflows, packages, PR #5 checks, and uploaded ARM64 artifact.

### Task 1: Correct Aspire Source Navigation Ownership And Mapping

**Files:**
- Modify: `.opencode/skills/aspire-source-navigation/SKILL.md:1-10`
- Modify: `docs/agents/skills/aspire-source-navigation.md:1-169`

**Interfaces:**
- Consumes: package versions from `Directory.Packages.props` and ignored upstream checkouts under `external/`.
- Produces: a relay description and canonical workflow that distinguish LocalStack Aspire hosting integration source from LocalStack SDK/client source.

- [ ] **Step 1: Update the relay metadata without duplicating canonical guidance**

Replace the frontmatter in `.opencode/skills/aspire-source-navigation/SKILL.md` with:

```yaml
---
name: aspire-source-navigation
description: Use when BadgeSmith's compatibility-sensitive Aspire, AWS, or LocalStack consumer work depends on upstream source, package-version alignment, AddLocalStack/UseLocalStack/WithReference behavior, endpoint/configuration flow, or AWS SDK wiring.
---
```

Keep these relay lines unchanged:

```markdown
Canonical skill content lives in [docs/agents/skills/aspire-source-navigation.md](../../../docs/agents/skills/aspire-source-navigation.md).

Read that file and follow it. This file is a native OpenCode discovery relay, not the source of truth.
```

- [ ] **Step 2: Correct the canonical overview and required upstream mapping**

Replace the canonical overview's first paragraph with:

```markdown
BadgeSmith consumes Aspire hosting integrations for local development; it does not
build or publish those packages. Compatibility-sensitive work depends on this repo's
package versions and on matching upstream source checkouts, not on memory or upstream
default branches.
```

Replace Required Workflow step 2 with:

```markdown
2. Map the packages to their upstream repositories: Aspire packages to `dotnet/aspire`,
   `Aspire.Hosting.AWS` to `aws/integrations-on-dotnet-aspire-for-aws`,
   `LocalStack.Aspire.Hosting` to `localstack-dotnet/dotnet-aspire-for-localstack`, and
   `LocalStack.Client` packages to `localstack-dotnet/localstack-dotnet-client` only
   when SDK/client configuration behavior is involved.
```

- [ ] **Step 3: Separate hosting integration and client SDK rows**

Replace the Package-To-Source Map table with:

```markdown
| Package or behavior | Upstream source | Local checkout root |
| --- | --- | --- |
| `Aspire.Hosting`, `Aspire.Hosting.AppHost`, `Aspire.Hosting.Testing` | `dotnet/aspire` | `external/aspire/{ref}/` |
| `Aspire.Hosting.AWS`, CloudFormation, CDK, Lambda emulator integration | `aws/integrations-on-dotnet-aspire-for-aws` | `external/aws-integrations/{ref}/` |
| `LocalStack.Aspire.Hosting`, `AddLocalStack`, `UseLocalStack`, LocalStack resource and endpoint wiring | `localstack-dotnet/dotnet-aspire-for-localstack` | `external/dotnet-aspire-for-localstack/{ref}/` |
| `LocalStack.Client`, `LocalStack.Client.Extensions`, `ILocalStackOptions`, session/config options | `localstack-dotnet/localstack-dotnet-client` | `external/localstack-dotnet-client/{ref}/` |
```

Replace the Local Checkout Layout block with:

```text
external/aspire/{ref}/
external/aws-integrations/{ref}/
external/dotnet-aspire-for-localstack/{ref}/
external/localstack-dotnet-client/{ref}/
```

- [ ] **Step 4: Make missing-source evidence distinguish the two LocalStack repositories**

Replace the example under Missing Or Stale Source with:

```text
Upstream source status:
- Aspire.Hosting {version}: no matching local checkout under external/aspire/{ref}; using targeted GitHub fallback for {symbols/files} only.
- Aspire.Hosting.AWS {version}: local checkout {path} verified at {ref-or-sha}.
- LocalStack.Aspire.Hosting {version}: local checkout {path} verified at {ref-or-sha}.
- LocalStack.Client {version}: not involved in this change.
```

In Search Guidance, keep client searches under the LocalStack client bullet and add this separate hosting bullet:

```markdown
- LocalStack hosting integration: `AddLocalStack`, `UseLocalStack`, LocalStack resource
  annotations, endpoint propagation, and AppHost environment wiring.
```

- [ ] **Step 5: Verify the ownership statement and source map**

Run:

```bash
rg -n "maintains an Aspire hosting integration|LocalStack.Aspire.Hosting|dotnet-aspire-for-localstack|localstack-dotnet-client|source of truth" .opencode/skills/aspire-source-navigation/SKILL.md docs/agents/skills/aspire-source-navigation.md
git diff --check
```

Expected: no maintainer claim remains; hosting integration and client SDK have distinct upstream repositories and checkout roots; the OpenCode file still identifies the documentation file as canonical.

- [ ] **Step 6: Inspect and commit the documentation correction**

Run:

```bash
git status --short
```

Expected: only source-navigation ownership, mapping, layout, and evidence examples changed.

After presenting the required pre-commit summary and receiving approval, run:

```bash
git add .opencode/skills/aspire-source-navigation/SKILL.md docs/agents/skills/aspire-source-navigation.md
```

Expected: one documentation commit matching the approved boundary.

## External Rider/Mend Rejection

- [ ] Confirm the project still contains the Lambda executable contract:

```bash
rg -n "<AssemblyName>bootstrap</AssemblyName>" src/BadgeSmith.Api/BadgeSmith.Api.csproj
```

Expected: exactly one match.

- [ ] Confirm no NuGet dependency named `bootstrap` exists and the NuGet vulnerability audit is clean:

```bash
dotnet package list --project src/BadgeSmith.Api/BadgeSmith.Api.csproj --include-transitive
```

Expected: no package named `bootstrap`; the vulnerability command reports no vulnerable packages.

- [ ] Reject only the `bootstrap@1.0.0` Rider/Mend finding with this exact rationale:

```text
False-positive identity collision. BadgeSmith intentionally names its .NET 10 Native AOT AWS Lambda custom-runtime executable "bootstrap" and retains the default assembly version 1.0.0. The project has no NuGet, npm, or other dependency on twbs/bootstrap. `dotnet package list --vulnerable --include-transitive` reports no vulnerable NuGet packages, and the cited CVEs concern browser-side XSS in the unrelated Bootstrap JavaScript library. Reject this component finding only; do not suppress other dependency findings.
```

If Rider MCP cannot mutate the finding, enter the same rationale manually in Rider/Mend. This external state produces no repository commit.

## Final Local Verification

- [ ] **Run formatting and static repository checks**

Run:

```bash
git diff --check
go run github.com/rhysd/actionlint/cmd/actionlint@v1.7.7 .github/workflows/ci-cd.yml .github/workflows/update-test-badge/action.yml .github/workflows/run-dotnet-tests/action.yml
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"
```

Expected: all commands exit `0` with no new finding.

- [ ] **Run the release build and complete test project**

Run:

```bash
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --configuration Release --no-build
```

Expected: zero build warnings and all tests pass. The known baseline is 350 tests before remediation; the final count must be at least 350 because this work adds coverage.

- [ ] **Compile and exercise the file-based CLI boundary**

Run:

```bash
dotnet run --file tools/badgesmith.cs -- --help
dotnet run --file tools/badgesmith.cs -- badge update --help
dotnet run --file tools/badgesmith.cs -- tests ingest --help
```

Expected: all commands exit `0`; badge help exposes `--base-url` and not `--api-domain` or `--hmac-secret`.

- [ ] **Repeat the explicit Native AOT analyzer gate**

First verify the approved temporary output parent exists:

```bash
ls /tmp/opencode
```

Then run:

```bash
dotnet publish src/BadgeSmith.Api/BadgeSmith.Api.csproj --configuration Release --runtime linux-x64 --output /tmp/opencode/badgesmith-pr5-final-aot -p:TrimmerSingleWarn=false -p:EnableTelemetry=false -p:EnableLocalStack=false
```

Expected: exit code `0` with no trim or AOT warning, including `IL3050` and `IL3053`.

- [ ] **Run a final full-PR review**

Invoke the `requesting-code-review` skill and review the complete range from PR #5's base commit through `HEAD`, not only the four remediation commits.

Expected: no Critical or Important finding. Resolve any such finding before continuing; record lower-severity residual risks separately.

## Hosted PR Verification

- [ ] **Present the local evidence and request push approval**

Before pushing, present:

- the four remediation commit SHAs and messages;
- release build and full-test counts;
- actionlint and Slopwatch results;
- NuGet vulnerability result;
- Native AOT analyzer result;
- final review findings;
- confirmation that the only Rider/Mend rejection was `bootstrap@1.0.0`.

After explicit push approval, run:

```bash
git push origin feature/iteration0-aot-contract-tier
```

Expected: the existing draft PR #5 updates without creating another PR.

- [ ] **Wait for both PR workflow jobs**

Run:

```bash
gh pr checks 5 --watch --interval 20
```

Expected: `build-and-test` and `continuous-deployment` both pass.

- [ ] **Verify the ARM64 artifact was uploaded**

Run:

```bash
gh run list --workflow "CI Pipeline" --branch feature/iteration0-aot-contract-tier --limit 1 --json databaseId,status,conclusion,url
gh run view "$(gh run list --workflow "CI Pipeline" --branch feature/iteration0-aot-contract-tier --limit 1 --json databaseId --jq '.[0].databaseId')" --json jobs
gh run download "$(gh run list --workflow "CI Pipeline" --branch feature/iteration0-aot-contract-tier --limit 1 --json databaseId --jq '.[0].databaseId')" --name lambda-zip-pr-5 --dir /tmp/opencode/badgesmith-pr5-artifact
ls /tmp/opencode/badgesmith-pr5-artifact
```

Expected: the `continuous-deployment` job and its ARM64 build step succeed; downloading `lambda-zip-pr-5` produces `badge-lambda-linux-arm64.zip`. The workflow retains the hosted artifact for 30 days.

- [ ] **Request ready-for-review approval**

Keep PR #5 draft while reporting all local and hosted evidence. Only after explicit user approval run:

```bash
gh pr ready 5
```

Expected: PR #5 becomes ready for review; no merge, deployment, or release is performed.

## Plan Verification

- BadgeSmith is described only as a consumer of Aspire hosting integrations.
- `LocalStack.Aspire.Hosting` and `LocalStack.Client` map to their distinct upstream repositories.
- The OpenCode relay remains thin and points at the canonical repository documentation.
- The only rejected security component is the unrelated `bootstrap@1.0.0` identity collision.
- Release build, full tests, file-based CLI, actionlint, Slopwatch, NuGet audit, and explicit Native AOT analysis pass.
- The hosted ARM64 job uploads the expected artifact.
- PR #5 stays draft until the final explicit approval.
