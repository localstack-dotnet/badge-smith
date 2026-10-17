# BadgeSmith

BadgeSmith supplies package-version and CI test-result badges, with test-result submission and
report links for the repositories it serves.

## Language

**Badge**:
A compact display of a package version or test outcome whose content BadgeSmith supplies for Shields.io.

**Package badge**:
A badge showing the highest package version that matches the requested version and prerelease criteria at a selected package provider.

**Package provider**:
The package source a package badge refers to: NuGet.org or GitHub Packages.

**Test result**:
A CI run's reported test counts and report references for a repository, platform and branch, with its result timestamp, run identifier and commit.

**Latest test result**:
The test result selected by reported result timestamp for a particular repository, platform and branch.

**Test-result badge**:
A badge presenting the test-count outcome of the latest test result for a repository, platform and branch.

**Test-result ingestion**:
Submission of a CI test result for a repository, platform and branch so it can supply that context's badge and report link.

**Test-report URL**:
The human-readable report destination associated with a test result, which may be hosted separately from its workflow run.

**Workflow-run URL**:
The reference to the CI workflow execution that produced a test result, a different role from the test-report destination even when both use the same URL.

**Organization test-data secret**:
An organization's credential authorizing test-result submission, separate from its credential for reading GitHub packages.
