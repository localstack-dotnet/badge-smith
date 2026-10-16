# Security And Storage Architecture

Current rules for storage, secret boundaries, canonical HMAC authentication, and transport. Read this
document before changing HMAC, nonces, secrets, replay protection, upstream modes, or stored result
URLs. [Runtime and routing](runtime-and-routing.md) owns response/cache behavior;
[platform and delivery](platform-and-delivery.md) owns deployment boundaries. The
[test-results guide](../guide/test-results.md) explains the supported consumer workflow without
replacing this protocol.

## Data Architecture

BadgeSmith uses **three DynamoDB tables** with optimized access patterns. DynamoDB and Secrets
Manager are deliberate architecture boundaries. BadgeSmith does not use a relational store; do not
introduce Entity Framework or SQL persistence patterns.

### Organization Secrets Table

The organization-secrets table maps GitHub organizations to their authentication secrets:

- Lookup is by organization name and token type.
- Each organization can have multiple token types: `Package` and `TestData`.
- Credential values remain in Secrets Manager; the table supplies the mapping.

### Test Results Table

The test-results table stores CI/CD results with efficient latest-result queries:

- **Partition key:** repository identifier.
- **Sort key:** platform + branch + timestamp for chronological ordering.
- **GSI:** latest-result queries without scanning.

### Nonce Table

The nonce table prevents HMAC replay attacks:

- TTL-based expiry cleans up old nonces automatically.
- Atomic conditional writes prevent race conditions.
- The cost-optimized retention window is 45 minutes.

Authentication controls when a nonce can be marked; see
[canonical HMAC authentication](#canonical-hmac-authentication).

## Secret Lifecycle

`badgesmith secrets seed` (in `tools/badgesmith.cs`) seeds organization mappings and secrets for
LocalStack development or real AWS resources with appropriate credentials. It uses JSON-based
organization/secret configuration and idempotent operations, so it is safe to repeat against the
intended target. Real AWS mutation remains subject to the [repository approval gate](../../AGENTS.md).

The [tooling guide](../../tools/README.md#secret-mapping) owns the mapping format, invocation,
rotation procedure, and organization-scoped secret name `badgesmith/github/{org}/{key}`. Runtime
secret caching and its rotation window are defined in that guide's
[secret-safety rules](../../tools/README.md#secret-safety).

## Canonical HMAC Authentication

`POST /tests/results/{platform}/{owner}/{repo}/{branch}` accepts only canonical-request
HMAC-SHA256 signatures. This contract is a hard cut: clients and the server must use the
same newline-delimited UTF-8 message in this exact field order:

```text
BADGESMITH-HMAC
POST
/tests/results/{platform}/{owner}/{repo}/{branch}
{timestamp}
{nonce}
{sha256-body}
```

There is no trailing newline. Canonical fields follow these rules:

- The path is the logical BadgeSmith ingestion route, without a deployment host, stage,
  custom base path, or query string.
- The decoded logical `platform`, `owner`, and `repo` values use `ToLowerInvariant()`.
- The decoded `branch` case and value are preserved.
- Every logical route segment is escaped independently with `Uri.EscapeDataString`.
- `timestamp` and `nonce` are the trimmed `X-Timestamp` and `X-Nonce` header values.
- `sha256-body` is lowercase hexadecimal SHA-256 over the exact UTF-8 request body.

Clients must emit `X-Signature` as `sha256=` followed by exactly 64 lowercase
HMAC-SHA256 hexadecimal characters. The verifier accepts case-insensitive scheme and
digest casing, but producer output remains canonical. The HMAC key is the organization's
`TestData` secret, separate from package-access credentials.

Authentication validates that the timestamp is no more than five minutes old and no
more than one minute in the future, resolves the organization-scoped secret, and
compares the exact-length signature digest in fixed time. Only after that comparison
succeeds is the trimmed nonce atomically marked in DynamoDB. A failed signature does not
consume the nonce.

Both `badgesmith tests ingest` and `badgesmith badge update` sign this canonical
request. Their dry-run output may include the URL, payload, timestamp, and nonce, but
never the signature or digest. Both commands require HTTPS; HTTP is accepted only for
loopback hosts (`localhost`, `127.0.0.0/8`, or `::1`).

The [canonical HMAC hard-cut ADR](../adr/0001-canonical-hmac-hard-cut.md) records the accepted
rationale and compatibility trade-off; the current bytes and verification rules are owned here.
Exact command options and secret input handling belong to the
[tooling guide](../../tools/README.md#authenticated-test-result-requests).

## Upstream And Transport Modes

`BADGESMITH_UPSTREAM_MODE` is an explicit `Live` or `Mock` contract. Missing values
default to `Live`.

- `Live` requires HTTPS for configured NuGet and GitHub upstream URLs. The Aspire
  AppHost also requires `tools/organization-pat-mapping.json` and fails before startup
  when it is missing.
- `Mock` is accepted only by builds compiled with `ENABLE_LOCALSTACK`. Both
  `HTTP_NUGET_BASE_URL` and `HTTP_GITHUB_BASE_URL` are required and may use HTTP for
  test-owned WireMock endpoints. The contract fixture owns deterministic secret seeding.
- Production CDK sets `Live` explicitly, and production builds reject `Mock` even if an
  environment variable is misconfigured.

Client commands that upload HMAC-authenticated test data do not inherit upstream mode.
Their BadgeSmith API base URL always requires HTTPS, with HTTP allowed only for loopback
development endpoints.

The [local-development guide](../engineering/local-development.md#apphost-and-contract-tests)
owns AppHost startup and Live-versus-Mock fixture setup.

## Stored Report URLs

The stored `url_html` value is the click target behind the public test-result redirect.
It may point to dorny, Allure, ReportPortal, or another white-label HTTPS report host;
it is not restricted to the GitHub workflow origin. Choosing that target is an explicit
capability of an organization-authorized HMAC ingester. Both stored result URLs must be
absolute HTTPS URLs without embedded credentials.

## Security Boundaries

- **Organization isolation:** each organization has separate secrets.
- **Token-type separation:** package access and test ingestion use different secrets.
- **Authentication logging:** successful authentication and invalid signatures are logged;
  API access logs record request outcomes.
- **Externalized credentials:** credentials live in AWS services, not in code.

HMAC, nonce, secret, and replay-protection changes are security-sensitive. Review the exact
protocol, threat model, and tests before mutation, under the [repository approval gate](../../AGENTS.md).

## Public Endpoints

Package badge endpoints are **unauthenticated** and provide input validation with comprehensive
error responses, plus stale-if-error cache directives for eligible cached responses during origin
failures. The [cache contract](runtime-and-routing.md#cache-strategy) owns the edge exceptions and
per-response cache intent.

The production stack does not currently configure an application-specific WAF or API
throttling policy. Explicit abuse controls remain [roadmap work](../ROADMAP.md); generic AWS service
quotas are not treated as an application rate-limit contract.

For public endpoint examples and supported use, see the [README](../../README.md) and
[consumer guide](../guide/README.md).
