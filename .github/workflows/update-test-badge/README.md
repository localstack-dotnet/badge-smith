# Update Test Results Badge

Reusable composite action that posts CI test results to BadgeSmith with HMAC
authentication and writes badge markdown to the GitHub Actions step summary.

## Inputs

See [`action.yml`](./action.yml) for the canonical input list. Required inputs are
`platform`, `test_passed`, `test_failed`, `test_skipped`, `commit_sha`, `run_id`,
`repository`, `server_url`, `api_base_url`, and `hmac_secret`.
`test_url_html` is optional. When supplied, the badge redirect targets that HTTPS
test-report URL, such as `dorny/test-reporter`'s `url_html` output or a report hosted by
another provider. When omitted, the action falls back to the current GitHub workflow-run
URL. `api_base_url` must be an absolute HTTPS URL for public deployments and may include
a port or path prefix. Plain HTTP is accepted only for loopback hosts (`localhost`,
`127.0.0.0/8`, or `::1`) used by local development.

## Usage

External consumers use the maintained major action tag:

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

Consumers pin the supported major action tag. The action installs the SDK pinned by
BadgeSmith's `global.json` and runs `tools/badgesmith.cs` from the downloaded action
repository via `github.action_path`; the caller does not need to contain BadgeSmith's
tool sources.

The `TESTDATASECRET` repository secret must hold the HMAC shared secret
configured for the organization through `badgesmith secrets seed`.
