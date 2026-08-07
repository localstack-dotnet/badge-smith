# PR #5 Native AOT Compatibility Implementation Plan

Status: Implemented in `34fe5f7`. The checkboxes below preserve the approved execution
sequence; current workstream status is tracked in `docs/ROADMAP.md`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore a warning-free .NET 10 Native AOT publish by pinning the AWS Lambda System.Text.Json serializer to the last package version with the required trimming metadata.

**Architecture:** Keep BadgeSmith's existing source-generated Lambda serializer and JSON context unchanged. Change only the centrally managed package version, document why the temporary pin exists, and prove the resolved graph and Native AOT analyzer are clean before relying on the ARM64 workflow.

**Tech Stack:** .NET SDK 10.0.301, Central Package Management, .NET Native AOT, AWS Lambda, `Amazon.Lambda.Serialization.SystemTextJson` 2.4.5.

## Global Constraints

- Follow `docs/superpowers/specs/2026-07-10-pr5-merge-readiness-remediation-design.md`.
- Keep `SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>` unchanged.
- Pin `Amazon.Lambda.Serialization.SystemTextJson` to exactly `2.4.5`.
- Do not suppress `IL3050`, `IL3053`, trim warnings, AOT warnings, or warnings-as-errors.
- Do not switch to `DefaultLambdaJsonSerializer` or add a BadgeSmith-owned serializer.
- Do not rename the Lambda `bootstrap` executable.
- Do not open an upstream AWS issue in this workstream.

---

## File Structure

- Modify `Directory.Packages.props`: own the central serializer pin and its AOT rationale.
- Verify `src/BadgeSmith.Api/Program.cs`: retain the non-telemetry source-generated serializer registration.
- Verify `src/BadgeSmith.Api/Program.Telemetry.cs`: retain the telemetry source-generated serializer registration.
- Verify `src/BadgeSmith.Api/Core/Infrastructure/LambdaFunctionJsonSerializerContext.cs`: retain the AOT-safe JSON registrations.

### Task 1: Restore Serializer Package Compatibility

**Files:**
- Modify: `Directory.Packages.props:29`
- Verify unchanged: `src/BadgeSmith.Api/Program.cs`
- Verify unchanged: `src/BadgeSmith.Api/Program.Telemetry.cs`
- Verify unchanged: `src/BadgeSmith.Api/Core/Infrastructure/LambdaFunctionJsonSerializerContext.cs`

**Interfaces:**
- Consumes: `Amazon.Lambda.Serialization.SystemTextJson` package through Central Package Management.
- Produces: resolved package version `2.4.5` while preserving `SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>` at both Lambda entry points.

- [ ] **Step 1: Confirm the failing package and serializer baseline**

Run:

```bash
dotnet package list --project src/BadgeSmith.Api/BadgeSmith.Api.csproj --include-transitive
```

Expected before the change: the output resolves `Amazon.Lambda.Serialization.SystemTextJson` to `3.0.0`.

Run:

```bash
rg -n "SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>|DefaultLambdaJsonSerializer" src/BadgeSmith.Api/Program.cs src/BadgeSmith.Api/Program.Telemetry.cs
```

Expected: both entry points contain `SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>` and neither contains `DefaultLambdaJsonSerializer`.

- [ ] **Step 2: Update the centrally managed package with the .NET CLI**

Run:

```bash
dotnet add src/BadgeSmith.Api/BadgeSmith.Api.csproj package Amazon.Lambda.Serialization.SystemTextJson --version 2.4.5
```

Expected: the command succeeds and updates the central `PackageVersion` rather than adding a version to `BadgeSmith.Api.csproj`.

- [ ] **Step 3: Add the self-contained AOT rationale**

Replace the serializer package line in `Directory.Packages.props` with:

```xml
    <!-- 3.0.0's net10.0 asset lacks the trimmability metadata required by BadgeSmith's Native AOT build. -->
    <PackageVersion Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.4.5" />
```

Do not modify the unversioned `PackageReference` in `src/BadgeSmith.Api/BadgeSmith.Api.csproj`.

- [ ] **Step 4: Restore and verify the resolved graph**

Run:

```bash
dotnet restore src/BadgeSmith.Api/BadgeSmith.Api.csproj
dotnet package list --project src/BadgeSmith.Api/BadgeSmith.Api.csproj --include-transitive
```

Expected: restore succeeds and the graph resolves `Amazon.Lambda.Serialization.SystemTextJson` to `2.4.5` with no downgrade or compatibility warning.

- [ ] **Step 5: Run Native AOT analysis with single-warning aggregation disabled**

First verify the approved temporary output parent exists:

```bash
ls /tmp/opencode
```

Then run:

```bash
dotnet publish src/BadgeSmith.Api/BadgeSmith.Api.csproj --configuration Release --runtime linux-x64 --output /tmp/opencode/badgesmith-pr5-aot -p:TrimmerSingleWarn=false -p:EnableTelemetry=false -p:EnableLocalStack=false
```

Expected: exit code `0`, no `IL3050`, no `IL3053`, and no other trim or AOT warning.

- [ ] **Step 6: Verify the serializer implementation was not changed**

Run:

```bash
git diff -- src/BadgeSmith.Api/Program.cs src/BadgeSmith.Api/Program.Telemetry.cs src/BadgeSmith.Api/Core/Infrastructure/LambdaFunctionJsonSerializerContext.cs
```

Expected: no output.

- [ ] **Step 7: Inspect and commit only the package compatibility change**

Run:

```bash
git diff --check
```

Expected: only `Directory.Packages.props` is modified for this task, and `BadgeSmith.Api.csproj` has no inline package version.

After presenting the required pre-commit summary and receiving approval, run:

```bash
git add Directory.Packages.props
```

Expected: one commit containing the package pin and rationale.

## Plan Verification

- The package graph resolves exactly `2.4.5`.
- Both Lambda entry points still use the source-generated serializer.
- `TrimmerSingleWarn=false` exposes no hidden trim or AOT warning.
- No warning suppression or serializer replacement is introduced.
- ARM64 artifact upload is deferred to the PR closeout plan because it requires the hosted ARM runner.
