# Update Test Results Badge

Composite GitHub Action that posts CI test results to the BadgeSmith API with
HMAC authentication and writes the badge markdown to the GitHub Actions step
summary.

## Inputs

See [`action.yml`](./action.yml) for the canonical input list. Required inputs:
`platform`, `test_passed`, `test_failed`, `test_skipped`, `commit_sha`,
`run_id`, `repository`, `server_url`, and `hmac_secret`. `api_domain` defaults
to `api.localstackfor.net` and `test_url_html` is optional.

## What it runs

The action shells out to the file-based `badgesmith` CLI:

```bash
"${{ github.workspace }}/tools/badgesmith.cs" badge update \
  --platform "${{ inputs.platform }}" \
  --test-passed "${{ inputs.test_passed }}" \
  --test-failed "${{ inputs.test_failed }}" \
  --test-skipped "${{ inputs.test_skipped }}" \
  --hmac-secret "${{ inputs.hmac_secret }}" \
  ...
```

See [`tools/README.md`](../../../tools/README.md) for the `badge update` option
reference, including `--dry-run` and `--fail-on-error`.

## Usage

```yaml
- name: Update test badge
  uses: ./.github/workflows/update-test-badge
  with:
    platform: 'Linux'
    test_passed: '${{ steps.test-results.outputs.passed }}'
    test_failed: '${{ steps.test-results.outputs.failed }}'
    test_skipped: '${{ steps.test-results.outputs.skipped }}'
    commit_sha: '${{ github.sha }}'
    run_id: '${{ github.run_id }}'
    repository: '${{ github.repository }}'
    server_url: '${{ github.server_url }}'
    api_domain: 'api.localstackfor.net'
    hmac_secret: '${{ secrets.TESTDATASECRET }}'
```

The `TESTDATASECRET` repository secret must hold the HMAC shared secret
configured for the organization via `badgesmith secrets seed` (secret name
`badgesmith/github/{org}/{key}`).
