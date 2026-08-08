# BadgeSmith Tooling

The `badgesmith` CLI is a file-based .NET 10 program (`tools/badgesmith.cs`) that
owns the BadgeSmith-specific build, test, ingestion, badge-update, and secret-seed
workflows. It supersedes the retired shell-based Lambda build / test-ingestion
helpers and the standalone DynamoDB seeder project.

The remaining `scripts/` directory only holds the k6 load-test scenario and the
sample ingestion payload (see [Performance testing](#performance-testing-k6)).

## Invocation

`badgesmith.cs` is a shebang-style file-based program. It discovers the repository root
by walking upward from the process working directory. Test project/result paths, Lambda
output, and secret-mapping config resolve from that root; Docker runs with the repository
root as its working directory, so relative Dockerfile and build-context paths do too.
`tests ingest --payload-file` is the exception: a relative payload path resolves from the
process working directory.

**Unix / Linux / macOS** (CI runners, WSL):

```bash
./tools/badgesmith.cs <command> [options]
# or via an absolute path while the working directory is inside this repository:
/absolute/path/to/badge-smith/tools/badgesmith.cs <command> [options]
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

### Authenticated test-result requests

Both `tests ingest` and `badge update` sign the canonical `POST` method, logical
ingestion route, trimmed timestamp and nonce, and exact request body. The resulting
`X-Signature` uses `sha256=` followed by 64 lowercase hexadecimal characters. See
[ARCHITECTURE.md](../ARCHITECTURE.md#canonical-hmac-authentication) for the exact field
order, normalization, escaping, timestamp policy, and nonce behavior.

Both commands read the shared secret from `BADGESMITH_HMAC_SECRET`; neither accepts
the secret as a command argument.

For either command, `--dry-run` may print the URL, payload, timestamp, and nonce, but it
does not print the signature or digest.

### `lambda build`

Multi-arch Docker build of the Native AOT Lambda. Used by CI and the deploy workflow.
Defaults to `linux-arm64` and `zip` to match production, while the normal local
AOT/LocalStack path uses `linux-x64`.

```bash
./tools/badgesmith.cs lambda build --target zip --rid linux-x64 --clean --verbose
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
Local ARM64 execution additionally requires an ARM64 host or a buildx builder with
ARM64 execution support. See the [CDK app guide](../build/BadgeSmith.CDK/README.md) for
the local-x64 and production-ARM64 boundaries.

### `tests run`

Runs `dotnet test --no-build` once per target framework declared on the project, writing
a unique TRX file per framework. Build the selected configuration first; the
`run-dotnet-tests` composite action relies on the preceding solution build in CI.

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
The base URL must use HTTPS; HTTP is accepted only for loopback hosts (`localhost`,
`127.0.0.0/8`, or `::1`) used during local development.

```bash
export BADGESMITH_HMAC_SECRET="$HMAC_SECRET"

./tools/badgesmith.cs tests ingest \
  --base-url http://localhost:9474 \
  --owner localstack-dotnet --repo localstack.client \
  --platform linux --branch main \
  --payload-file scripts/sample-test-payload.json --verbose

# Inline payload, no network call:
./tools/badgesmith.cs tests ingest \
  --base-url http://localhost:9474 \
  --owner localstack-dotnet --repo localstack.client \
  --platform linux --branch main \
  --payload '{"platform":"Linux","passed":190,"failed":0,"skipped":0,"total":190}' \
  --dry-run
```

See [ARCHITECTURE.md](../ARCHITECTURE.md#canonical-hmac-authentication) for the HMAC
authentication contract and replay protection.

### `badge update`

Posts GitHub Actions test results to BadgeSmith and writes the badge markdown
to the GitHub Actions step summary. This is the command behind the
`update-test-badge` composite action.

```bash
export BADGESMITH_HMAC_SECRET="$HMAC_SECRET"
./tools/badgesmith.cs badge update \
  --base-url https://badges.example.com/api \
  --platform Linux \
  --test-passed 190 --test-failed 0 --test-skipped 0 \
  --repository localstack-dotnet/badge-smith \
  --dry-run
```

`--base-url` is required and accepts an absolute HTTPS deployment URL, including a
custom port or path prefix. HTTP is accepted only for loopback hosts (`localhost`,
`127.0.0.0/8`, or `::1`) used during local development. Public HTTP URLs fail command
validation before a request is created. `badge update` reads the HMAC secret from
`BADGESMITH_HMAC_SECRET`; it does not accept the secret as a command argument.

By default a failed post does not fail CI; pass `--fail-on-error` to opt into a
non-zero exit. Branch is auto-detected from the GitHub Actions environment when
`--branch` is omitted.

### `secrets seed`

Seeds the GitHub organization → secret mapping into DynamoDB and Secrets
Manager. Reads from `tools/organization-pat-mapping.json` by default.

```bash
# Validate mapping content without a table name or AWS clients:
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

The Aspire AppHost defaults to `BADGESMITH_UPSTREAM_MODE=Live`; in that mode this
mapping file is required and startup fails when it is missing. Contract tests set mode
to `Mock`, point both upstream URLs at WireMock, and seed deterministic fake secrets
through their fixture instead of reading this file.

### Secret safety

- Never commit or share `tools/organization-pat-mapping.json`; only the `.dist`
  template is tracked. Verify with `git check-ignore` before staging.
- `Package` requires a classic GitHub PAT with `read:packages` and package read
  access. GitHub Packages does not support fine-grained PAT authentication.
- `TestData` is an independent HMAC secret, not a GitHub PAT.
- Validate changes with `secrets seed --dry-run`, then run `secrets seed`
  against the target environment.
- Runtime secrets are cached for up to 15 minutes. After rotation, warm Lambda
  instances may retain the previous value; invalidate them or allow the cache
  window to expire before considering rotation complete.

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
orchestration — remains deferred. When implemented, it will consume the dedicated
LocalStack-only `BadgeSmith.CDK.LocalPerformance` app as its infrastructure boundary;
the app is never deployed to AWS. See the
[CDK app guide](../build/BadgeSmith.CDK/README.md) for the current manual build and
synthesis workflow, and `docs/ROADMAP.md` (Inbox / Untriaged) for the planned command.
