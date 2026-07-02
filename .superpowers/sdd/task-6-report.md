# Task 6 Implementation Report

Status: DONE

## Changes

- Added recorded NuGet WireMock response body at `tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/__files/nuget-contracttest-index.json` using the required real API shape and truncated version list: `3.5.8`, `4.0.1`, `4.0.2`, `13.0.4-beta1`, `13.0.3`.
- Added WireMock mappings for NuGet success/missing-package and GitHub package versions success/unauthorized responses.
- Added `PackageBadgeContractTests` with `Integration`, `Functional`, and `AotContract` traits covering NuGet stable/prerelease/missing/invalid-range/cache and GitHub stable/missing-secret/unknown-provider behavior.
- Fixed the contract-test harness blocker in `LambdaRieClient`: query-string requests are now represented as API Gateway v2 events with `rawPath`, `rawQueryString`, and `queryStringParameters` separated correctly.

## TDD / Iteration Notes

- Initial filtered test run after adding mappings/tests failed 2/8:
  - `NuGetBadge_WithPrerelease_Should_ReturnPrereleaseVersion`: expected 200, actual 404.
  - `NuGetBadge_InvalidVersionRange_Should_Return400`: expected 400, actual 404.
- Diagnosis: the test harness passed query strings in `rawPath`/`requestContext.http.path`, so the production router did not match query-string requests. This was a contract-test harness blocker, not a production-code issue.
- Applied the minimal test-infrastructure fix and re-ran the same filtered command successfully.

## Verification

- `dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract&FullyQualifiedName~PackageBadgeContractTests"` — PASS: 8 passed, 0 failed, 0 skipped.
- `slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"` — PASS: 0 issues found.

## Review Coverage Gap Fix

- Seeded a package secret mapping for `unauthorized-org` so GitHub package badge requests pass local secret lookup and reach the WireMock GitHub upstream.
- Changed the GitHub unauthorized contract test to call `/badges/packages/github/unauthorized-org/any.pkg`, exercising `github-versions-401.json` and preserving the expected Lambda RIE 401 response.
- Verification after the fix:
  - `dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj --filter "Category=AotContract&FullyQualifiedName~PackageBadgeContractTests"` — PASS: 8 passed, 0 failed, 0 skipped.
  - `slopwatch analyze --fail-on warning --exclude "artifacts/**,**/bin/**,**/obj/**"` — PASS: 0 issues found.

## Spec Compliance Fix

- Re-added the separate missing-org-secret GitHub contract test for `/badges/packages/github/unknown-org/some.pkg` while keeping the upstream unauthorized WireMock 401 coverage for `unauthorized-org`.
