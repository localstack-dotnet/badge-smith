# Test badges and report links

A test badge displays the latest test-count outcome for a repository, platform and branch.
Its click target is a separate report redirect, so the same README link can follow later
submitted results without naming one workflow run permanently.

## Display a badge and link its report

```text
GET /badges/tests/{platform}/{owner}/{repo}/{branch}
GET /redirect/test-results/{platform}/{owner}/{repo}/{branch}
```

Use the badge endpoint through Shields.io and the redirect as the outer Markdown link:

```markdown
[![Tests](https://img.shields.io/endpoint?url=https%3A%2F%2Fapi.localstackfor.net%2Fbadges%2Ftests%2Flinux%2Flocalstack-dotnet%2Fbadge-smith%2Fmaster)](https://api.localstackfor.net/redirect/test-results/linux/localstack-dotnet/badge-smith/master)
```

Replace the platform, owner, repository and branch with the context you publish. Linux,
Windows and macOS are examples, not a closed platform list. Encode each route segment;
a slash inside a branch is `%2F`. When nesting the badge URL inside Shields.io's `url`,
encode that URL too, including any existing percent escapes.

The redirect uses the submitted test-report URL (`url_html`). That may be a dorny, Allure,
ReportPortal or other white-label HTTPS report destination, not necessarily the workflow's
origin. The workflow-run URL identifies the execution; it can also serve as the report
destination when no separate report is supplied. Both stored URLs must be absolute HTTPS
URLs without embedded credentials. Choosing the report target is a capability of the
organization-authorized submitter, not a GitHub-only redirect guarantee.

## Publish from GitHub Actions

Use the maintained reusable action after your test reporter has produced counts and, optionally,
a report URL. It posts the result and writes badge Markdown to the Actions step summary.

```yaml
- name: Update test badge
  uses: localstack-dotnet/badge-smith/.github/workflows/update-test-badge@v1
  with:
    platform: 'Linux'
    test_passed: '${{ steps.test-results.outputs.passed }}'
    test_failed: '${{ steps.test-results.outputs.failed }}'
    test_skipped: '${{ steps.test-results.outputs.skipped }}'
    test_url_html: '${{ steps.test-results.outputs.url_html }}'
    commit_sha: '${{ github.sha }}'
    run_id: '${{ github.run_id }}'
    repository: '${{ github.repository }}'
    server_url: '${{ github.server_url }}'
    api_base_url: 'https://api.localstackfor.net'
    hmac_secret: '${{ secrets.TESTDATASECRET }}'
```

`steps.test-results` is the caller's reporter step, not a step created by this action.
If `test_url_html` is omitted, the action uses the workflow-run URL. `TESTDATASECRET`
must match the organization's configured test-data secret; it is not a package PAT.
The [action guide](../../.github/workflows/update-test-badge/README.md) owns supported usage
and the [action definition](../../.github/workflows/update-test-badge/action.yml) owns exact inputs.
The action obtains BadgeSmith's pinned SDK and tool sources itself; callers do not need to
copy those sources. The repository-local `run-dotnet-tests` action is not a portable runner contract.

## Publish with the CLI

For an existing payload, use [`tests ingest`](../../tools/README.md#tests-ingest).
For GitHub Actions counts and step-summary output, use
[`badge update`](../../tools/README.md#badge-update). The tooling reference owns all options,
payload-file resolution, branch detection and failure behavior. A local request preview is:

```bash
./tools/badgesmith.cs tests ingest \
  --base-url http://localhost:9474 \
  --owner localstack-dotnet --repo localstack.client \
  --platform linux --branch main \
  --payload-file scripts/sample-test-payload.json --dry-run
```

Set `BADGESMITH_HMAC_SECRET` through your secret environment before invoking either uploader;
neither accepts it as a command argument. A dry run does not send a request and does not
print a signature or digest. Public upload URLs require HTTPS; HTTP is allowed only for
loopback development endpoints. For Windows invocation, use the
[cross-platform invocation reference](../../tools/README.md#invocation).

Operators own secret provisioning through [secret mapping and safety](../../tools/README.md#secret-mapping).
The [canonical HMAC contract](../architecture/security-and-storage.md#canonical-hmac-authentication)
owns signing, freshness and replay rules. Use the supported producers rather than implementing
a second signing recipe from this guide. Local setup belongs to
[local development](../engineering/local-development.md#apphost-and-contract-tests).
