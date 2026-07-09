# BadgeSmith Tooling

The `badgesmith` CLI is a file-based .NET 10 program (`tools/badgesmith.cs`) that
owns the BadgeSmith-specific build, test, ingestion, badge-update, and secret-seed
workflows. It supersedes the retired shell-based Lambda build / test-ingestion
helpers and the standalone DynamoDB seeder project.

The remaining `scripts/` directory only holds the k6 load-test scenario and the
sample ingestion payload (see [Performance testing](#performance-testing-k6)).

## Invocation

`badgesmith.cs` is a shebang-style file-based program. The working directory for
all path-based options is the repository root unless the option says otherwise.

**Unix / Linux / macOS** (CI runners, WSL):

```bash
./tools/badgesmith.cs <command> [options]
# or from anywhere:
"$(pwd)/tools/badgesmith.cs" <command> [options]
```

**Windows** (PowerShell, local dev):

```powershell
dotnet run --file tools/badgesmith.cs -- <command> [options]
```

The shebang form requires `dotnet` on `PATH` and a Unix shell; the
`dotnet run --file` form works on every platform.

## Commands

```text
badgesmith lambda build    Build the Lambda ZIP or container image.
badgesmith tests run       Run a .NET test project once per target framework.
badgesmith tests ingest    Post a test result payload to BadgeSmith (HMAC-signed).
badgesmith badge update    Post GitHub Actions test results to BadgeSmith.
badgesmith secrets seed    Seed GitHub org secret mappings into AWS resources.
```

Run `badgesmith <command> --help` for the full option list. `--dry-run` is
available on `tests ingest`, `badge update`, and `secrets seed` to print the
planned request without mutating anything.

### `lambda build`

Multi-arch Docker build of the Native AOT Lambda. Used by CI and the deploy
workflow. Defaults to `linux-arm64` and `zip` target to match production.

```bash
./tools/badgesmith.cs lambda build --target zip --rid linux-arm64 --clean --verbose
./tools/badgesmith.cs lambda build --target image --rid linux-x64 --image-tag badgesmith-lambda:local
./tools/badgesmith.cs lambda build --target both --rid linux-arm64 --push
```

| Option | Default | Description |
| --- | --- | --- |
| `-t, --target` | `zip` | `zip`, `image`, or `both` |
| `-r, --rid` | `linux-arm64` | `linux-arm64` or `linux-x64` |
| `-i, --image-tag` | `badgesmith-lambda:local` | Docker image tag (image/both targets) |
| `-f, --dockerfile` | `src/BadgeSmith.Api/Dockerfile` | Dockerfile path |
| `-c, --context` | `.` | Docker build context |
| `-o, --out` | `artifacts` | Output directory for ZIP artifacts |
| `--push` | off | Push the image after build (image/both targets) |
| `--clean` | off | Delete the output directory before building |
| `-v, --verbose` | off | Print external commands before running them |

This command is **not** part of the ordinary `dotnet build` loop. AOT/trim
warnings emitted here are blocking — do not suppress them to get a green build.

### `tests run`

Runs `dotnet test` once per target framework declared on the project, writing a
unique TRX file per framework. Used by the `run-dotnet-tests` composite action.

```bash
./tools/badgesmith.cs tests run \
  --project-path tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj \
  --results-dir test-results \
  --configuration Release
```

| Option | Default |
| --- | --- |
| `--project-path` | `tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj` |
| `--results-dir` | `test-results` |
| `--configuration` | `Release` |
| `-v, --verbose` | off |

### `tests ingest`

Posts a single test-result payload to `POST /tests/results/{platform}/{owner}/{repo}/{branch}`
with HMAC-SHA256 signature, timestamp, and nonce headers. Useful for local
end-to-end checks against a running BadgeSmith (Aspire AppHost or deployed API).

```bash
./tools/badgesmith.cs tests ingest \
  --base-url http://localhost:9474 \
  --owner localstack-dotnet --repo localstack.client \
  --platform linux --branch main \
  --secret "$HMAC_SECRET" \
  --payload-file scripts/sample-test-payload.json --verbose

# Inline payload, no network call:
./tools/badgesmith.cs tests ingest \
  --owner localstack-dotnet --repo localstack.client \
  --platform linux --branch main \
  --secret "$HMAC_SECRET" \
  --payload '{"platform":"Linux","passed":190,"failed":0,"skipped":0,"total":190}' \
  --dry-run
```

See [ARCHITECTURE.md](../ARCHITECTURE.md) for the HMAC authentication flow,
replay protection, and the expected success/error response shapes.

### `badge update`

Posts GitHub Actions test results to BadgeSmith and writes the badge markdown
to the GitHub Actions step summary. This is the command behind the
`update-test-badge` composite action.

```bash
./tools/badgesmith.cs badge update \
  --platform Linux \
  --test-passed 190 --test-failed 0 --test-skipped 0 \
  --repository localstack-dotnet/badge-smith \
  --hmac-secret "$HMAC_SECRET" \
  --dry-run
```

By default a failed post does not fail CI; pass `--fail-on-error` to opt into a
non-zero exit. Branch is auto-detected from the GitHub Actions environment when
`--branch` is omitted.

### `secrets seed`

Seeds the GitHub organization → secret mapping into DynamoDB and Secrets
Manager. Reads from `tools/organization-pat-mapping.json` by default.

```bash
# Validate the mapping without touching AWS:
./tools/badgesmith.cs secrets seed --dry-run

# Seed against LocalStack (set AWS_RESOURCE_ORG_SECRETS_TABLE first):
./tools/badgesmith.cs secrets seed \
  --config tools/organization-pat-mapping.json \
  --table-name "$AWS_RESOURCE_ORG_SECRETS_TABLE" \
  --localstack

# Seed against real AWS:
./tools/badgesmith.cs secrets seed \
  --config tools/organization-pat-mapping.json \
  --table-name badge-smith-github-org-secrets \
  --aws-region eu-central-1 --aws-profile my-profile --no-localstack
```

The DynamoDB table name defaults to the `AWS_RESOURCE_ORG_SECRETS_TABLE`
environment variable when `--table-name` is omitted. `--localstack` and
`--no-localstack` toggle LocalStack-backed AWS SDK clients; the
`LocalStack:UseLocalStack` configuration key is also honored.

## Secret mapping

`secrets seed` reads a JSON mapping that lists GitHub organizations and the
secrets attached to them. A tracked template is shipped at
`tools/organization-pat-mapping.json.dist`:

```json
{
  "secrets": [
    {
      "org_name": "<org-name>",
      "name": "package",
      "secret": "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
      "type": "Package",
      "description": "Example GitHub Packages PAT"
    },
    {
      "org_name": "<org-name>",
      "name": "testdata",
      "secret": "your-hmac-secret-here",
      "type": "TestData",
      "description": "Example HMAC secret for test result ingestion"
    }
  ]
}
```

**Local setup:**

```bash
cp tools/organization-pat-mapping.json.dist tools/organization-pat-mapping.json
# then edit tools/organization-pat-mapping.json with real values
```

`tools/organization-pat-mapping.json` is gitignored (see `.gitignore`). The
`name` field is lowercased and combined with the lowercase `org_name` to form
the Secrets Manager secret name:

```text
badgesmith/github/{org}/{key}
```

For example, `org_name: localstack-dotnet`, `name: testdata` becomes
`badgesmith/github/localstack-dotnet/testdata`. The DynamoDB org-secrets table
maps `ORG#{org}` → `CONST#GITHUB#{type}` to that secret name.

### PAT safety

- **Never commit `tools/organization-pat-mapping.json`** with real PATs or
  HMAC secrets. Only the `.dist` template is tracked. The repository
  `.gitignore` covers `**/organization-pat-mapping.json`, so the real file
  is ignored wherever it lives.
- Treat the local mapping file the same as any other secret — restrict file
  permissions on shared machines and rotate credentials if it leaks.
- `secrets seed --dry-run` validates the mapping and prints the planned
  secret names without writing to AWS.
- Prefer **least-privilege fine-grained PATs** (or GitHub organization
  secrets) scoped to exactly the repositories and permissions BadgeSmith
  needs: read-only package access for the `Package` secret type and the
  test-result ingestion HMAC for the `TestData` secret type. Avoid classic
  PATs with broad `repo` / `write:packages` scope.

### PAT rotation checklist

Follow these operator steps whenever a new developer onboards, a token
expires, or a token may have leaked:

1. **Copy the template** — start from the tracked placeholder file, never
   from a teammate's real mapping:

   ```bash
   cp tools/organization-pat-mapping.json.dist tools/organization-pat-mapping.json
   ```

2. **Fill placeholders only on the developer machine** — edit
   `tools/organization-pat-mapping.json` locally. Do not paste real values
   into the `.dist` template, chat, tickets, or screenshots.

3. **Never commit the real file** — confirm git treats it as ignored before
   staging anything in `tools/`:

   ```bash
   git check-ignore -v tools/organization-pat-mapping.json
   git status --short
   ```

   Expected: `git check-ignore` reports the
   `**/organization-pat-mapping.json` rule, and `git status --short` does
   not list the real mapping.

4. **Rotate on leak or expiry** — if a token may have leaked historically
   (committed, screenshotted, or shared), rotate it in GitHub first
   (Settings → Developer settings → Personal access tokens, or the org
   secret), then update the local `tools/organization-pat-mapping.json`
   with the replacement value. Re-run `secrets seed` (against LocalStack or
   AWS) to refresh DynamoDB and Secrets Manager.

5. **Re-seed after rotation** — `secrets seed --dry-run` to confirm the new
   mapping parses, then `secrets seed` against the target environment so
   DynamoDB and Secrets Manager hold the rotated value.

## Performance testing (k6)

HTTP load testing still uses the k6 scenario at `scripts/k6-perf-test.js`
directly — it is not wrapped by the `badgesmith` CLI. Install k6 from
<https://k6.io/docs/getting-started/installation/>.

```bash
# Quick smoke test
k6 run --duration 2m --vus 10 scripts/k6-perf-test.js

# Standard load test
k6 run --duration 5m --vus 50 scripts/k6-perf-test.js

# Override the target endpoint
K6_API_URL=https://your-api-gateway-url.amazonaws.com k6 run scripts/k6-perf-test.js
```

The `badgesmith perf baseline` command — LocalStack seed + k6 invocation
orchestration that previously lived in the retired perf-baseline shell scripts — is
deferred. See `docs/ROADMAP.md` (Inbox / Untriaged) for the planned re-home
under `tools/Commands/PerfBaselineCommand.cs`.
