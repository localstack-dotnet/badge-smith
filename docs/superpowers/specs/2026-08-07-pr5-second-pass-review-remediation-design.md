# PR #5 Second-Pass Review Remediation Design

Date: 2026-08-07

Status: Implemented locally in `4d9c699`, `9e1344c`, `eae8df3`, and `621f2ce`;
hosted ARM64 verification and the separate production synth gate remain pending.

## Context

PR #5 passed its hosted build-and-test and ARM64 artifact jobs after the first
merge-readiness remediation. A subsequent whole-PR review found that the HMAC
authentication contract still signs only the request body. Two independent security
reviews confirmed that the timestamp, nonce, HTTP target, and route values are not
cryptographically bound to the signature.

A captured body and signature can therefore be reused with a fresh timestamp and nonce.
Within the same organization secret, the request can also be redirected to another
repository, platform, or branch because storage uses route values that are not signed.
The existing nonce table blocks an identical nonce, but it cannot provide replay
protection when the nonce itself is unauthenticated.

The review also found three adjacent merge-readiness issues:

- the reusable badge action can send HMAC-authenticated requests over plaintext HTTP;
- `LocalStack.Client.Extensions` 2.0.1 fixes the real-AWS client-construction path that
  BadgeSmith exposes through `secrets seed --no-localstack`;
- the CDK app uses a custom, fail-open context switch to choose production or local
  performance infrastructure, while the production workflow deploys `--all`.

These findings are resolved on the existing draft PR before the final review resumes.
Backward compatibility for the body-only HMAC contract is explicitly not required. The
Lambda verifier, file-based CLI, reusable action, and repository workflows move together
in one hard cut.

## Goals

- Bind the request method, logical ingestion path, timestamp, nonce, and exact body to
  one HMAC signature.
- Preserve exception-free digest parsing and constant-time digest comparison.
- Keep nonce marking after successful signature validation.
- Remove body-only signing without a compatibility branch or protocol-version token.
- Require HTTPS for reusable badge updates while retaining loopback HTTP for local
  ingestion workflows.
- Upgrade `LocalStack.Client.Extensions` to 2.0.1 and verify both LocalStack and real-AWS
  client construction.
- Separate production and local-performance CDK entrypoints so stack topology is not
  selected through custom context.
- Make production CDK workflow targets explicit instead of using `--all`.
- Finish with focused security, tooling, package, CDK, full-build, and hosted-pipeline
  evidence.

## Non-Goals

- Supporting or detecting the previous body-only signature contract.
- Adding signature-version headers, prefixes, negotiation, fallback, or deprecation
  windows.
- Changing the JSON test-result payload schema.
- Changing the DynamoDB test-result or nonce table schema.
- Changing organization-level secret ownership.
- Implementing the deferred `perf baseline` command.
- Removing `LocalPerformanceStack` or changing its benchmark topology.
- Renaming the existing production CloudFormation stack.
- Deploying production infrastructure during implementation or verification.
- Broad refactors of routing, AWS clients, CDK constructs, or the file-based CLI.

## Decision Summary

| Area | Decision |
| --- | --- |
| HMAC input | Sign one deterministic canonical request instead of the body alone |
| Compatibility | Hard cut; no legacy verifier and no protocol-version marker |
| Signature text | Keep `sha256=` followed by 64 lowercase hexadecimal characters |
| Route identity | Canonicalize the logical ingestion path from normalized route values |
| Body identity | Include the lowercase SHA-256 digest of the exact UTF-8 request body |
| Timestamp and nonce | Include their trimmed header values in the signed input |
| Nonce order | Validate signature first, then atomically mark the nonce |
| Badge transport | HTTPS required; HTTP allowed only for loopback local development |
| LocalStack client | `LocalStack.Client.Extensions` 2.0.1; `LocalStack.Client` remains 2.0.0 |
| CDK topology | Separate production and local-performance app entrypoints |
| CDK selection | Use native stack IDs; do not use context to select stack topology |
| Production workflow | Target `BadgeSmithStack` explicitly; never deploy `--all` |
| Performance command | Remains a separate deferred workstream |

## Workstream 1: Canonical HMAC Contract

### Signed Message

The signature remains HMAC-SHA256 with the organization test-data secret. The HMAC input
is the UTF-8 encoding of this newline-delimited message:

```text
BADGESMITH-HMAC
POST
/tests/results/{platform}/{owner}/{repo}/{branch}
{timestamp}
{nonce}
{sha256-body}
```

There is no trailing newline.

The fields have these rules:

- `POST` is the uppercase HTTP method accepted by the ingestion route.
- The path is the BadgeSmith logical route, independent of CloudFront, API Gateway stage,
  custom-domain, or deployment base-path prefixes.
- `platform`, `owner`, and `repo` are lowercased with invariant rules before escaping.
- `branch` preserves its decoded case and value.
- Every route value is escaped independently with `Uri.EscapeDataString`.
- The helper receives logical decoded route values. On the server this means the value
  after the existing single `HttpUtility.UrlDecode` pass in `RouteValues`; on the client
  this means the original logical CLI/action value before URL construction.
- `timestamp` and `nonce` are the trimmed header values that the server validates and
  passes to nonce storage.
- `sha256-body` is lowercase hexadecimal SHA-256 over the exact request-body UTF-8 bytes.

The final `X-Signature` remains:

```text
sha256={lowercase HMAC-SHA256 hex}
```

Keeping the signature envelope avoids an unnecessary header-format change. Its meaning
changes atomically from body-only authentication to canonical-request authentication.
Old clients fail authentication by design.

### Shared Implementation

One small reflection-free helper under `src/shared` owns canonical message construction.
It is linked into the Native AOT API and included by the file-based CLI. The test project
does not compile a third copy because it already references the API assembly and receives
internal access through `InternalsVisibleTo`; its linked tool sources consume the
API-linked helper. This prevents client and server canonicalization from drifting without
creating duplicate fully-qualified types or `CS0436` warnings.

The helper:

- accepts method-independent route fields plus timestamp, nonce, and body;
- constructs the logical ingestion path;
- hashes the exact body;
- returns the canonical text or UTF-8 bytes required by the signer;
- uses only trim-safe BCL APIs;
- contains no JSON serialization, reflection, ambient configuration, or clock access.

The helper does not own secret lookup, timestamp freshness, nonce persistence, HTTP
transport, or response mapping.

### Server Validation

`HmacAuthenticationService` reconstructs the canonical request from `HmacAuthContext`
and verifies the supplied digest against it. The order remains:

1. Validate required authentication context values.
2. Parse and validate timestamp freshness.
3. Resolve the organization test-data secret.
4. Construct the canonical request.
5. Parse the supplied digest without throwing.
6. Compare exact-length digests in constant time.
7. Mark the nonce atomically for the signed route identity.
8. Return the authenticated repository identity.

The nonce key can retain its current repository identifier because route tampering now
changes the signature. No table migration is required.

### Client Signing

`badge update` and `tests ingest` generate timestamp and nonce before signing, then pass
the same values, route fields, and exact body to the shared canonical helper. The
request must send exactly those signed values.

Dry-run output may show the target URL, timestamp, nonce, and payload, but it does not
print the reusable signature value. The command still proves validation and canonical
construction without turning CI logs into a request credential source.

### Security Tests

Tests require fixed known-answer vectors for `/`, a literal `%2F`, `+`, and space in
route values so the client and the server prove identical one-pass decode and re-escape
behavior. Starting from one valid signature, authentication must fail when any one of
these changes:

- HTTP route repository;
- HTTP route owner;
- HTTP route platform;
- HTTP route branch;
- timestamp;
- nonce;
- request body.

The valid request succeeds and marks its nonce only after signature verification.
Malformed, short, long, non-hex, and valid-length incorrect digests remain HTTP 401 and
do not mark a nonce. Exact same-route replay with the same nonce remains rejected.

Functional coverage derives signatures that are valid for their original route and
headers, then sends only the tampered request to a unique empty target. Timestamp, nonce,
and body mutations use an empty original route; changed-route mutations use an empty
changed route. The tampered requests must return 401, and a subsequent query of the
target route must show that no result was stored. A separate valid-ingestion case proves
the unmodified canonical request succeeds.

## Workstream 2: Transport Safety

The reusable action is a public CI integration and must not transmit HMAC credentials
over plaintext transport.

- `badge update` accepts HTTPS base URLs.
- HTTP is accepted only when the host is loopback (`localhost`, `127.0.0.0/8`, or `::1`).
- `tests ingest` retains its existing HTTP support for LocalStack and local Aspire
  endpoints because it is explicitly a local-development command as well as a deployed
  endpoint probe.
- Shared URL parsing continues to reject credentials, query strings, fragments,
  relative URLs, and non-HTTP schemes.
- The reusable action relies on command validation rather than duplicating URL policy in
  PowerShell and Bash.

Tests cover public HTTP rejection, public HTTPS acceptance, loopback HTTP acceptance for
badge command tests, and unchanged local HTTP behavior for `tests ingest`.

## Workstream 3: LocalStack Client Compatibility

`ToolAwsClientFactory` always registers `LocalStack.Client.Extensions`, then resolves
either LocalStack-backed or real AWS SDK clients. Version 2.0.0 performs an exact
reflection lookup for an internal `AWSSDK.Extensions.NETCore.Setup` constructor. AWS
changed that constructor in 4.0.4, breaking the `UseLocalStack=false` path.

`LocalStack.Client.Extensions` 2.0.1 replaces exact-signature lookup with tolerant
constructor resolution and raises the minimum
`AWSSDK.Extensions.NETCore.Setup` version to 4.0.100.5. Its dependency graph raises
`AWSSDK.Core` to at least 4.0.100.6. Public LocalStack client APIs do not change.

The update applies only to development tooling and tests. The package is not added to
the shipped Native AOT Lambda, and `LocalStack.Client` remains at 2.0.0.

Verification must inspect actual resolved package versions for both the file-based tool
and the test project. Tests must construct DynamoDB and Secrets Manager clients with
`UseLocalStack` both true and false without making AWS mutations.

## Workstream 4: CDK Application Boundaries

### Problem

The current production CDK entrypoint reads `-c stack=local-performance`. Any missing or
unknown value silently falls through to production. This uses context to select app
topology rather than synthesis configuration and makes a typo unsafe.

Instantiating both stacks unconditionally in one app is also unsuitable. CDK constructs
and synthesizes the complete app before applying CLI stack selection. Production and
local-performance stacks require different Lambda ZIP assets and environments, while
production also performs a Route53 hosted-zone lookup.

### App Split

Use two small executable app projects backed by `BadgeSmith.CDK.Shared`:

- `BadgeSmith.CDK` constructs only `ProductionStack` with ID `BadgeSmithStack`.
- `BadgeSmith.CDK.LocalPerformance` constructs only `LocalPerformanceStack` with ID
  `BadgeSmithPerformanceStack`.

The production app retains its existing construct ID and derived CloudFormation stack
name. It does not set a new `StackProps.StackName`, so deployment identity does not
change.

The local-performance app owns only benchmark synthesis settings:

- Lambda ZIP path;
- Lambda architecture;
- LocalStack endpoint;
- NuGet and GitHub upstream endpoints;
- local account and region defaults.

Those remain valid CDK context values because they configure one known stack rather than
selecting which stack exists.

The local app runs with `build/BadgeSmith.CDK.LocalPerformance` as its CDK working
directory. Its default Lambda ZIP path is therefore
`../../artifacts/badge-lambda-linux-x64.zip`, not the production app's current
`../artifacts`-relative value.

Each app has an explicit `cdk.json` execution boundary. Production commands run from the
existing `build` app. Local benchmark orchestration runs the local-performance app and
targets `BadgeSmithPerformanceStack` by its native stack ID.

### Workflow Safety

Production workflow commands become:

```text
cdk synth BadgeSmithStack
cdk diff BadgeSmithStack
cdk deploy BadgeSmithStack --require-approval never
```

The workflow never uses `--all`. The local-performance app is not deployed to AWS by any
production workflow.

No production deploy is part of verification. CDK checks stop at compilation and
synthesis unless the user separately approves a LocalStack-backed smoke run.

## Documentation And Roadmap

- `docs/ROADMAP.md` tracks this second-pass remediation as a separate workstream linked
  to this design and its implementation plan.
- The original PR #5 remediation remains historical evidence for commit `34fe5f7`.
- `ARCHITECTURE.md`, root `README.md`, tooling docs, and reusable-action docs describe the
  canonical HMAC inputs and HTTPS requirement after implementation.
- CDK documentation names the production and local-performance app entrypoints and their
  native stack IDs.
- The deferred `perf baseline` command remains a separate roadmap item. Its future
  implementation consumes the local-performance app created here.

## Error Handling

- Any canonical-field mismatch returns `InvalidSignature` and maps to HTTP 401.
- Malformed digest text never escapes as an exception.
- Signature failure never marks a nonce.
- Public plaintext badge endpoints fail command validation before request creation.
- Unknown CDK stack-mode context no longer exists.
- Missing Lambda assets fail only the app that owns that asset.
- Package downgrade or compatibility warnings remain build failures.

## Verification Strategy

1. Fixed-vector canonical HMAC unit tests.
2. Per-field mutation and nonce-order unit tests.
3. LocalStack-backed ingestion contract tests for valid, replayed, and tampered requests,
   including an owner mapped to the same test secret.
4. CLI signer and dry-run tests with no signature disclosure.
5. URL transport-policy tests.
6. File-based tool, test-project, and AppHost dependency graph inspection.
7. LocalStack and real-AWS client-construction tests without mutation.
8. Production and local-performance CDK project builds.
9. Production and local-performance synth checks with their own Lambda assets.
10. Release solution build and full xUnit test project.
11. File-based CLI build, action contract tests, `git diff --check`, and Slopwatch.
12. Native AOT ARM64 artifact build with zero trim/AOT warnings.
13. Hosted PR pipeline rerun.
14. Final whole-PR review with no unresolved High or Critical findings.

PR #5 remains draft until these gates pass and the user explicitly approves moving it
to ready for review.

## Commit Boundaries

Keep the remediation reviewable through focused commits:

1. `fix: bind HMAC authentication to canonical requests`
2. `fix: enforce secure badge update transport`
3. `build: update LocalStack client compatibility`
4. `refactor: separate production and performance CDK apps`
5. `docs: document second-pass PR remediation`

Commit creation still requires a separate pre-commit summary and explicit approval.
