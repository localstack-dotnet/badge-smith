# Engineering Quality Gates

This document owns existing validation requirements and the limits of evidence claims. Test
authorship lives in [testing style](testing-style.md), authoring contracts in
[coding style](coding-style.md), and autonomy and approval rules in
[AGENTS.md](../../AGENTS.md#autonomy-and-approval).

## Mechanical Authorities And Analyzer Policy

Read SDK, language, package, analyzer, and build facts from `global.json`, `.editorconfig`,
`Directory.Build.props`, `Directory.Packages.props`, `BannedSymbols.txt`, and affected project files.
Package versions belong in the central props file, not individual projects or a prose inventory.

Keep the analyzer wall and warnings-as-errors clean. A valid exception is narrow, documented beside
its file-scoped arbitration, and names the contract that wins; never weaken a rule globally to make
a change pass. [Coding-style enforcement](coding-style.md#enforcement) defines that arbitration;
[slop review](slop-review.md) applies the existing anti-weakening and review rules.

## Affected-Scope Validation

Ordinary validation is restore/build/test for the affected scope. Tests use xUnit v3 + Moq on
VSTest: use plain `dotnet test` and standard `--filter`, not TUnit/Microsoft.Testing.Platform
arguments or `--treenode-filter`. The [API test guide](../../tests/BadgeSmith.Api.Tests/README.md)
owns categories and HTTP/performance harness details; the
[CDK test guide](../../tests/BadgeSmith.CDK.Tests/README.md) owns in-process CDK test requirements.

Documentation-only changes do not require build or test unless changed commands or technical
claims need mechanical validation. Always validate affected links, as required by
[documentation policy](documentation.md#writing-rules).

`PublishAot=true` and trim/AOT warnings are load-bearing runtime-safety boundaries. Check serialized
types against `LambdaFunctionJsonSerializerContext`; a successful ordinary compilation does not
prove Native AOT runtime safety. Native AOT publishing goes through
`tools/badgesmith.cs lambda build` and is not part of a normal build loop. The [tooling guide](../../tools/README.md) owns exact build operations.

## Shared-Source Consumers

Shared source is compiled into consumers rather than exposed through a project reference.
`BadgeSmith.Api` links `Constants.cs`, `BadgeSmithApiActivitySource.cs`, and
`HmacCanonicalRequest.cs`; Host and CDK projects link `Constants.cs`; the file-based CLI includes
the HMAC helper. The API tests also link tooling source that the file-based CLI includes.

Before changing shared or linked source, find **all** compiling consumers: search project-file
`Compile Include` paths (including `shared`, `SharedDir`, and linked tooling paths) and file-app
`#:include` directives. Follow the matching paths/globs and validate every consumer, not only the
nearest project or a project-reference graph. The project files and file-app directives, not this
illustrative list, own the current consumer set.

## Slopwatch

After LLM-authored code, project, analyzer, or test changes, run Slopwatch when available:

```bash
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"
```

`external/**` contains ignored upstream source checkouts, not BadgeSmith-owned code. The availability
boundary does not authorize installing a substitute or weakening a check; report unavailable or
unrun tooling honestly. This trigger does not add a Slopwatch run for documentation-only edits.

## Hosted Validation

`.github/workflows/` contains two composite actions with different scopes:

- `run-dotnet-tests/`: repository-local multi-framework test execution.
- `update-test-badge/`: remotely reusable HMAC-authenticated badge updates.
- The actions support Windows, Linux, and macOS; their definitions own exact inputs and execution.
  The reusable action's [consumer guide](../../.github/workflows/update-test-badge/README.md)
  lives beside its `action.yml`.

Eligible pull requests run the Release build, full test suite, and hosted ARM64 Lambda ZIP build.
Live test-result publication is intentionally narrower:

- **Pull requests:** build, tests, and ARM64 artifact validation without production mutation.
- **Master pushes:** the same checks plus a best-effort authenticated badge update.
- **Production CDK synth/deploy:** a separate approval-gated deployment workflow, not ordinary PR CI.

Workflow files own triggers, runner images, action versions, and deployment steps. This describes
the existing hosted boundary; it adds no merge-queue, reviewer, publication, or CI-completion gate.

## Evidence And Performance Claims

Report what ran and what did not. Local checks, merged source, hosted tests, artifact eligibility,
deployment, and execution of the published Native AOT artifact are different evidence states.
Hosted ZIP validation is not executed `provided.al2023`, deployed-identity, INIT, or memory proof.
Dated measurements and operator reports remain attributed evidence, not freshly verified live state.

[Platform and delivery](../architecture/platform-and-delivery.md#performance-goals) owns unchanged
performance targets and the complete deployed-revision measurement protocol. The
[allocation discipline](coding-style.md#allocation-discipline) owns synchronous gates versus
asynchronous memory evidence. Exact BenchmarkDotNet/k6 invocation and interpretation live in the
[test guide](../../tests/BadgeSmith.Api.Tests/README.md#performance-harnesses); a Dry run validates
execution, not performance, and runner exit status alone is not a correctness gate.
