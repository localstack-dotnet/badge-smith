# Code Review Findings — Full Codebase Deep-Dive

Date: 2026-07-02

Full read-through of `src/BadgeSmith.Api` (~4.1k LOC), `build/` CDK, `src/BadgeSmith.Host`,
`tests/`, and `scripts/`. Findings are ordered by severity. File references point at the
line as of commit `671e40e`.

## 1. Bugs (behavior-affecting)

### 1.1 HMAC `repoIdentifier` built wrong — `Repo` twice, `Platform` missing

`src/BadgeSmith.Api/Core/Security/HmacAuthenticationService.cs:42`:

```csharp
var repoIdentifier = $"{authContext.Owner}/{authContext.Repo}/{authContext.Repo}/{authContext.Branch}";
```

Consequences:

- Nonce partition key (`NONCE#{repoIdentifier}`) has no platform scope; the same nonce
  value used for two platforms of the same repo/branch collides.
- The ingestion response `Repository` field returns `owner/repo/repo/branch` to clients
  (`Features/TestResults/Handlers/TestResultIngestionHandler.cs:86`).
- Log lines carry the malformed identifier.

Fix: `{Owner}/{Repo}/{Platform}/{Branch}` (decide canonical order once; nonce keys in
DynamoDB are TTL-bound (45 min), so a key-shape change has no migration cost).

### 1.2 `GetLatestTestResultAsync` builds GSI1PK from non-normalized values

`src/BadgeSmith.Api/Features/TestResults/TestResultsService.cs:86-93` computes four
`ToLowerInvariant()` locals and then never uses them:

```csharp
var gsi1Pk = $"LATEST#{owner}#{repo}#{platform}#{branch}"; // raw, not normalized
```

Write path stores lowercase keys (`TestResultEntity.FromPayload`, called with normalized
values at `TestResultsService.cs:50`). Any badge/redirect query with an uppercase
character is a guaranteed 404. Currently masked because README badge URLs use lowercase.

### 1.3 Build-script default RID mismatches CDK artifact expectation

- `scripts/build-lambda.ps1:5` and `scripts/build-lambda.sh:5` default to `RID=linux-x64`.
- CDK requires `../artifacts/badge-lambda-linux-arm64.zip` + `Architecture.ARM_64`
  (`build/BadgeSmith.CDK.Shared/Constructs/BadgeSmithFunctionConstruct.cs:35`).

CI passes `--rid linux-arm64` explicitly, so deploys work; a default local build produces
an artifact CDK cannot find. Align the default (arm64) or parameterize CDK by RID.

### 1.4 Seeder onboarding template is invalid JSON

`tests/seeders/BadgeSmith.DynamoDb.Seeders/organization-pat-mapping.json.dist:5,12` —
missing closing quote on `"name": "<secret-name>,`. Copying the template yields a parse
failure that the seeder swallows with a warning (`OrgSecretSeeder.cs:122-126`).

### 1.5 Plaintext GitHub PAT on disk (rotate)

`tests/seeders/BadgeSmith.DynamoDb.Seeders/organization-pat-mapping.json` contains a
real-looking `ghp_…` token. The file is gitignored (verified), but it is plaintext on
disk and copied into every `bin/` output (`CopyToOutputDirectory=Always`). If live,
rotate it; consider sourcing the secret from user-secrets/env instead of a JSON file.

## 2. Security / robustness improvements

- **Nonce burned before signature validation.**
  `HmacAuthenticationService.ValidateRequestAsync` order is timestamp → nonce (DynamoDB
  write) → secret → signature. Every invalid-signature request costs a DynamoDB write +
  secret lookup, and a legitimate retry after a signature mistake is rejected as replay.
  Prefer: timestamp → secret + signature → nonce last.
- **Exception messages leak to clients.** `Core/Routing/ApiRouter.cs:66` returns
  `$"Unhandled error: {ex.Message}"` in the 500 body (Program.cs catch uses a generic
  message — inconsistent). `NonceService.cs:88` and `TestResultsService.cs:73` embed
  `ex.Message` into `Error` results that handlers serialize to clients.
- **`HttpUtility.UrlDecode` wrong for path segments.**
  `Core/Routing/RouteValues.cs:51,67` — `+` decodes to space, corrupting segments that
  legitimately contain `+`. Use `Uri.UnescapeDataString`; also replaces
  `HttpUtility.UrlEncode` at `Features/GitHub/GitHubPackageService.cs:52` and drops the
  `System.Web` dependency.
- **Production DynamoDB tables: `RemovalPolicy.DESTROY`, no PITR, no deletion
  protection** (`build/BadgeSmith.CDK.Shared/Constructs/DynamoDbTablesConstruct.cs:40,76,93`).
- **`Request.Headers` null-safety inconsistent.** Ingestion handler checks null; badge
  handlers dereference directly (`TestResultsBadgeHandler.cs:52`,
  `NuGetPackageBadgeHandler.cs:59`, `GithubPackagesBadgeHandler.cs:75`) → NRE → 500.
- **GitHub versions endpoint has no pagination.**
  `GitHubPackageService.GetLatestVersionAsync` reads only the first page (default 30
  items); a `?version=` range targeting older versions can silently miss.

## 3. Refactoring opportunities (duplication / design)

- **Bootstrap duplicated** across `Program.cs` (`#if !ENABLE_TELEMETRY`) and
  `Program.Telemetry.cs` (`#if ENABLE_TELEMETRY`): `FunctionCoreAsync` and handler setup
  are copies. Extract the shared core; keep only the tracer wrapper conditional.
- **`TryExtractRouteParameters` copy-pasted 3×** (~35 lines each) across
  TestResults ingestion/badge/redirection handlers. One shared extractor.
- **Provider dispatch belongs in the route table.** Registering
  `/badges/packages/nuget/{package}` and `/badges/packages/github/{org}/{package}`
  literal routes removes both handlers' `TryValidateRequest` provider checks and
  cross-provider hint blocks.
- **NuGet/GitHub package services ~70% identical** (conditional GET, ETag/304 handling,
  cache write: `NuGetPackageService.cs:47-91` vs `GitHubPackageService.cs:56-105`;
  copy-paste evidence: "NuGet API error" message at `GitHubPackageService.cs:96`).
  Extract a shared cached-conditional-fetch helper.
- **Zero-alloc routing self-defeats.** Span-based `RouteValues` is immediately
  materialized into an `ImmutableDictionary` per request (`ApiRouter.cs:54`), and
  `RouteResolver.TryResolve` heap-allocates the param buffer per request
  (`RouteResolver.cs:18`). Either carry spans through, or use a plain `Dictionary`.
- **`Lazy<T>` used eagerly in three places** — value constructor receives an invoked
  result instead of a factory delegate: `Core/Observability/LoggerFactory.cs:16`,
  `Core/Http/HttpClientFactory.cs:16-17`, `Core/ApplicationRegistry.cs:34`.
- **Dead code:** `Core/Observability/Loggers/SimpleLogger.cs` (no references),
  `Core/Routing/Patterns/RegexPattern.cs` (unused in production, yet 325 lines of tests),
  `RouteTable.Routes` public setter (never assigned), narrow `ResponseHelper.Redirect`
  overload shadowed by the flexible one.
- Cosmetic: `Core/Settings.cs:14` typo `DefaulEnableTelemetryFactoryPerfLogs`;
  `src/shared/Constants.cs:13,21,29` doubled words (`TestResultsTableTableName` etc.).

## 4. Test suite gaps

Only routing is tested (7 classes, good quality). Zero tests for: HMAC/nonce/secrets
security stack, all feature handlers/services, caching, retry handler, `ResponseHelper`
(ETag/If-None-Match logic), `ApiRouter`, and the real `RouteTable`.

- `RouteResolverTests` fabricates its own route table which has drifted from production
  (`TestIngestion` modeled as `ExactPattern("/tests/results")`; production uses a
  4-parameter `TemplatePattern`).
- `RegexPattern` is heavily tested but not wired into production.
- `TestBase.VerifyLogging` and `SetupILogger` are dead; no `.Verify` interaction checks.
- `RouteTestBuilder`/`RouteTestExtensions` duplicated verbatim between the unit and
  performance test projects; unit test csproj references BenchmarkDotNet needlessly.
- Benchmark suite: `_Current` vs `_Optimized` pairs in `BufferAllocationBenchmarks`
  execute identical code paths (vestigial); no committed baseline, no CI perf gate.

Highest-value first tests: `NuGetVersionService`, `ResponseHelper` (both pure),
`HmacAuthenticationService` (would have caught bug 1.1), `ResilienceRetryHandler`,
`ApiRouter` + real `RouteTable`.

## 5. Local-dev / scripts

- `scripts/localstack.yml` is dead (zero references; stale compose version, removed
  `PORT_WEB_UI` var, quoted `DEBUG` value). Delete or wire up and document.
- k6: hardcoded deployed URL (`k6-perf-test.js:45`); README documents `K6_API_URL` /
  `K6_DURATION` / `K6_VUS` env vars that the script never reads.
- `test-ingestion.sh:120` uses GNU-only `date %3N` — broken on macOS despite README
  claiming support; writes `response.tmp` into CWD instead of `mktemp`.
- `build-lambda.ps1` help text shows GNU-style `--clean/--push` flags the script cannot
  parse; `build-lambda.sh` lacks the ps1's output-zip existence check.
- Seeder: `WORKER_TIMEOUT_IN_SECONDS` only bounds shutdown, not the seeding work
  (`StartupTimeout` never set); AppHost injects `300` while launchSettings says `60`;
  null-guard misses `OrgName`/`Type` before `ToLowerInvariant()`
  (`OrgSecretSeeder.cs:135-142`).
- Local/prod parity nit: prod sets `APP_NAME` / `APP_ENABLE_TELEMETRY_FACTORY_PERF_LOGS`
  (values equal the code defaults), Aspire host sets neither.

## 6. Suggested wave plan

1. **Wave 1 — correctness:** bugs 1.1–1.4, nonce ordering, error-message hygiene, PAT
   rotation (1.5).
2. **Wave 2 — safety net:** tests for HMAC/ResponseHelper/real RouteTable +
   `NuGetVersionService`; align resolver tests with production routes.
3. **Wave 3 — hygiene:** DRY refactors, dead-code removal, script/docs drift fixes,
   DynamoDB PITR/removal-policy decision.

Performance opportunities are tracked separately in
[2026-07-02-performance-opportunities.md](2026-07-02-performance-opportunities.md).
