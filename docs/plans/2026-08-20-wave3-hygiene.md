# Wave 3 Hygiene Specification

Status: ready for implementation; production, build-policy, and CDK mutations remain approval-gated

Baseline: `152dfaa64054b2c447c5b55e63e880efd3c25e9f` (`test: complete wave 2 safety net`)

## Objective

Complete the five validated Wave 3 hygiene items without shortcuts, compatibility shims, or
behaviorally vague abstractions. The result must preserve BadgeSmith's Native AOT Lambda model,
`ApplicationRegistry` composition, HTTP contracts except where this specification explicitly changes
them, and the origin-controlled CloudFront cache architecture.

The implementation should also improve the internal API shapes so the routing and response-building
parts can later be extracted into a reusable AWS API Gateway handler package. This wave does not
perform that package extraction.

## Success Conditions

Wave 3 is complete when:

- Telemetry and non-telemetry Lambda entry points share one tested request-processing core.
- Test-result route parameters have one feature-local, named extraction contract.
- NuGet and GitHub services carry identical, jointly tested conditional-GET/cache mechanics and one
  shared cache-entry type while retaining provider-specific request, status, parsing, and result
  policies.
- Redirect responses expose only typed redirect statuses and explicit typed cache behavior.
- The route table cannot be reassigned or mutated after declaration.
- Focused CDK template assertions protect the load-bearing CloudFront cache and transport contract.
- Current public route shapes, current cached test-result redirect behavior, and current source-
  generated serialization remain intact.
- Both Lambda compile modes, the affected unit/functional tests, Release analyzer build, Slopwatch,
  and repository diff checks pass.
- Current cache architecture and its infrastructure invariants are moved into living canon before
  this temporary plan is removed.

## Engineering Rules

- Prefer complete typed designs over raw-string escape hatches and primitive parameter lists.
- Do not introduce interfaces without a real production or test substitution seam.
- Do not introduce a generic package-provider base class, framework, or shared fetch collaborator
  for two providers; keep duplicated mechanics textually identical and jointly tested.
- Do not merge upstream HTTP validators with downstream BadgeSmith response validators.
- Do not use reflection, runtime code generation, or trim-unsafe patterns.
- Do not change `ApplicationRegistry` to a DI container.
- Do not add compatibility code for internal APIs that have no shipped external consumer.
- Do not use positional deconstruction where named route values cross the HMAC boundary.
- Minimum allocation is a project hallmark. The OneOf-driven result pattern is the project standard
  and its result instance is the one accepted allocation; beyond that, a hygiene refactor must not
  add per-request heap allocations on a success path relative to the current code. Prefer
  `readonly` structs, named presets, and precomputed header values over per-call construction.
  Every work item states its allocation delta.
- Allocation verification has two tiers. Synchronous hot paths (route binding, response and header
  composition, route resolution) are gated by Release-mode xUnit facts that measure
  `GC.GetAllocatedBytesForCurrentThread()` around a warmed-up call. Asynchronous HTTP paths are
  measured with BenchmarkDotNet `[MemoryDiagnoser]` in `tests/BadgeSmith.Api.Performance.Tests`
  and recorded, not gated, because thread hops make per-thread measurement unreliable.
- Preserve CloudFormation logical IDs across behavior-neutral CDK extraction. Any synthesized
  replacement is a blocker, not an acceptable refactor side effect.
- Use source and tests as runtime canon. Dated research remains evidence only.

## Scope

| Item | Classification | Required outcome |
| --- | --- | --- |
| Lambda bootstrap duplication | Active refactor | One request processor, two thin compile-mode wrappers |
| Test-result route parameters | Active refactor | One named value and one feature-local extractor |
| Provider consistency | Active refactor | Identical, jointly tested conditional-GET/cache mechanics; one shared cache-entry type; no collaborator |
| Redirect/cache response API | Active redesign | Typed status and explicit public-cache/no-store APIs |
| `RouteTable.Routes` setter | API tightening | Immutable route inventory (`ImmutableArray`) |
| CDK cache assertions | Cross-cutting safety net | Focused synthesized-template contract tests |
| Canonical cache documentation | Cross-cutting documentation | Expanded architecture plus CDK relay |

## Explicit Non-Goals

- Do not remove `RegexPattern`. It is intentionally retained with its tests as a future routing
  capability.
- Do not remove or redesign test logger helpers. Logging-test policy belongs to the separate source-
  generated logging workstream.
- Do not extract or publish a reusable NuGet package in this wave.
- Do not redesign provider dispatch or public route templates.
- Do not change CloudFront TTL values, cache-key behavior, transport settings, or DNS resources.
- Do not optimize route buffers, immutable dictionary materialization, Lambda memory, or artifact
  size as part of hygiene work.
- Do not add HTTP/1.0 response-header compatibility by emitting `Pragma` or `Expires`.
- Do not deploy CDK, publish Lambda artifacts, create a release, or mutate AWS.

## System Cache Contract

Wave 3 must preserve the distinction between four cache layers:

| Layer | Resource | Validator/policy owner |
| --- | --- | --- |
| Lambda memory cache | NuGet/GitHub upstream payload | Upstream ETag and Last-Modified |
| Lambda HTTP response | Badge or redirect response | `ResponseHelper` and typed response policy |
| CloudFront shared cache | Public GET/HEAD response | `s-maxage` within CDK TTL bounds |
| Browser/private cache | Viewer response | `max-age`, ETag, and no-store |

The upstream ETag identifies a provider response. The downstream strong ETag identifies serialized
BadgeSmith JSON. They are different validators and must never share a model or implementation.

The Lambda memory cache is a validator store, not a freshness cache: every badge request revalidates
upstream with conditional headers, and the 15-minute TTL bounds validator retention. A revalidation-
free freshness window is a separate roadmap decision, not part of this wave.

Production CloudFront remains origin-controlled:

- `MinTTL=0` is load-bearing so origin `no-store`, `no-cache`, and `private` are not overridden.
- `DefaultTTL=0` means the origin must explicitly opt a 2xx/3xx response into positive edge
  freshness.
- Error responses follow a different rule: CloudFront's Error Caching Minimum TTL (default 10
  seconds; a distribution setting, not part of the cache policy) caches 404/414/500/501/502/503/504
  for max(10 s, origin `max-age`/`s-maxage`) even without origin `Cache-Control`, and caches
  400/403/405/412/415 only when the origin sends `max-age`/`s-maxage`. BadgeSmith error responses
  carry no `Cache-Control`, so they are edge-cached for 10 seconds; `ProductionStack` sets no
  `ErrorResponses`. This wave documents and asserts that default; it does not change it.
- The maximum TTL remains bounded; `ProductionStack.cs` owns the exact cap.
- Path and all query strings form the representation key.
- Cookies and ordinary viewer headers do not vary the current representation key.
- `Accept-Encoding` gzip/Brotli variants remain enabled through the cache policy.
- Viewer headers are forwarded to the API origin except for `Host`, but they are not ordinary cache-
  key components.
- Direct API Gateway and local-performance paths have no CloudFront layer.

Any future response that varies by a request header requires a CloudFront cache-key review. In
particular, an OPTIONS response must not receive a positive edge TTL unless `Origin`,
`Access-Control-Request-Method`, and `Access-Control-Request-Headers` are handled by an appropriate
cache policy.

External behavior was last verified against RFC 9110, RFC 9111, and the Amazon CloudFront Developer
Guide on 2026-08-20:

- <https://www.rfc-editor.org/rfc/rfc9110.html#section-15.4>
- <https://www.rfc-editor.org/rfc/rfc9111.html#section-5.2>
- <https://www.rfc-editor.org/rfc/rfc9111.html#section-5.4>
- <https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/cache-key-understand-cache-policy.html>
- <https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/http-3xx-status-codes.html>
- <https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/HTTPStatusCodes.html>

## Work Item 1: Lambda Request Core

### Current Problem

`Program.cs` and `Program.Telemetry.cs` duplicate timeout creation, method/path fallback, request
scope logging, router invocation, and unhandled-error mapping. Ordinary project builds select the
telemetry entry point; the production Docker publish selects the non-telemetry entry point.

The deployment contract is also cross-project:

- CDK Lambda hard timeout: 20 seconds from shared `LambdaTimeoutInSeconds`.
- Release application cancellation budget: 18 seconds, preserving a 2-second shutdown margin.
- Production runtime: `provided.al2023`, ARM64, 512 MB.
- Production publish strips telemetry and LocalStack at compile time.

### Design

Add an internal sealed `LambdaRequestProcessor` in the API core. It accepts the existing `IApiRouter`
seam and an `ILogger` instance. It owns:

- The `CancellationTokenSource` based on `Settings.LambdaTimeout`.
- Request method/path fallback values.
- The AWS request-ID logging scope.
- Request-start logging.
- `IApiRouter.RouteAsync` invocation.
- Stable unhandled-exception logging and the existing safe 500 response.

Each top-level entry point creates one processor during initialization.

- The non-telemetry entry point passes the processor handler directly to Lambda bootstrap.
- The telemetry entry point starts `AWSLambdaWrapper.TraceAsync`; inside its invocation callback it
  first applies `SetHttpTags` to the wrapper-created `Activity.Current`, then invokes the same
  processor.
- JSON serializer creation remains source-generated.
- The logger category remains the existing `Program` category unless a separately approved logging
  contract changes it.

The telemetry order is a contract:

```text
AWSLambdaWrapper.TraceAsync(tracerProvider, (request, context) =>
{
    SetHttpTags(request, context);
    return processor.HandleAsync(request, context);
}, request, context)
```

`SetHttpTags` remains telemetry-only and must not move into `LambdaRequestProcessor`; calling it
before `TraceAsync` would tag the wrong activity or no activity.

Do not merge the files into a preprocessor-heavy single entry point. Do not make the processor depend
on OpenTelemetry types.

Allocation delta: the `Program` logger is created once at initialization instead of on every
request; the `CancellationTokenSource` and `BeginScope` remain per request as today.

### Tests

- Successful router response is returned unchanged.
- Null method and path use the current `UNKNOWN` and `/` fallbacks.
- Router exception produces the current safe 500 body.
- The router receives a cancellable token.
- The request-ID logging scope can be exercised without asserting implementation-only formatted log
  text.
- Release builds pass with telemetry enabled.
- Release restore/build passes with `EnableTelemetry=false` and `EnableLocalStack=false`. That
  restore rewrites `obj/project.assets.json`; run an ordinary `dotnet restore` again before the
  next `--no-restore` build.
- Processor tests obtain `ILambdaContext` from `Amazon.Lambda.TestUtilities` (`TestLambdaContext`),
  already pinned in `Directory.Packages.props`; add the unversioned package reference to
  `BadgeSmith.Api.Tests`.
- CDK assertions protect the 20-second Lambda timeout while source retains the 2-second Release
  cancellation margin.

### Acceptance Criteria

- Request processing exists in exactly one production method.
- Telemetry tags/wrapper remain telemetry-only.
- No reflection or DI framework is introduced.
- Existing API router and serializer contracts remain unchanged.

## Work Item 2: Test-Result Route Parameters

### Current Problem

The ingestion, badge, and redirect handlers each repeat the same four required parameters and the
same 400 responses. Their tuple order (`owner, repo, platform, branch`) differs from the public route
order (`platform, owner, repo, branch`), creating a positional maintenance risk at the HMAC boundary.

These 400 responses are also the only BadgeSmith error responses that bypass the standard
`ErrorResponse` contract: `ResponseHelper.BadRequest("Owner parameter is required")` emits a raw
string body under the default `application/json` content type. Package handlers already emit
`ErrorResponse` with `ErrorDetail` codes for the same class of failure.

### Design

Add one feature-local immutable value with a static extraction factory. There is exactly one
extraction implementation, shared by all three handlers:

```csharp
internal readonly record struct TestResultRouteParameters(
    string Platform,
    string Owner,
    string Repo,
    string Branch)
{
    public static TestResultRouteParametersResult Extract(RouteContext routeContext);
}
```

Add one failure type in the existing `FailureTypes` hierarchy so `ToErrorResponse()` is inherited:

```csharp
internal sealed record MissingRouteParameter(string Reason, string ParameterName)
    : ValidationFailure(Reason, "MISSING_ROUTE_PARAMETER", ParameterName);
```

Add the result type in the repository's standard OneOf-driven shape: a `[GenerateOneOf]` partial
class deriving from `OneOfBase`, exposing `IsSuccess`, a typed success accessor, and `Failure`.
`TryPick*`, `IsT*`, and `AsT*` stay inside the result class and never appear in handlers. Because
there is a single failure type, `Failure` returns `MissingRouteParameter` directly rather than an
inner `OneOf`.

```csharp
[GenerateOneOf]
internal sealed partial class TestResultRouteParametersResult
    : OneOfBase<TestResultRouteParameters, MissingRouteParameter>
{
    public bool IsSuccess => IsT0;

    public TestResultRouteParameters Parameters => IsT0
        ? AsT0
        : throw new InvalidOperationException("Result is a failure");

    public MissingRouteParameter Failure => IsT1
        ? AsT1
        : throw new InvalidOperationException("Result is successful");
}
```

Allocation delta: one result instance per request (the standard OneOf result allocation).
`MissingRouteParameter` and `ErrorResponse` allocate only on the failure path; the success path
performs the same four dictionary lookups as today and allocates nothing else.

Extraction rules:

- Validation order is the public route order: platform, owner, repo, branch. The first missing
  parameter wins. Route matching can only yield an empty value for an empty or whitespace segment,
  so this changes the returned message only when two or more segments are empty.
- Required means present and not null, empty, or whitespace, as today.
- The extractor must not decode URLs, normalize case, reconstruct paths, know route templates, or
  perform HMAC canonicalization. `RouteValues` and `ApiRouter` own decoding.

Handlers:

- `if (!result.IsSuccess) { return ResponseHelper.BadRequest(result.Failure.ToErrorResponse()); }`
- Consume `Parameters.Platform`, `Owner`, `Repo`, and `Branch` through named properties. Positional
  deconstruction is prohibited. `HmacAuthContext(Owner, Repo, Platform, Branch, ...)` and
  `StoreTestResultRequest(Owner, Repo, Platform, Branch, Payload)` keep their constructor order and
  are constructed with named arguments.

### Wire Contract

Missing-parameter responses use the standard error contract:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json; charset=utf-8

{"message":"Owner parameter is required","error_details":[{"error_code":"MISSING_ROUTE_PARAMETER","property_name":"owner"}]}
```

Messages stay `Platform parameter is required`, `Owner parameter is required`,
`Repo parameter is required`, and `Branch parameter is required`; `property_name` is the lowercase
route key. This is an intentional wire-visible change from the current raw-string body and is listed
under Intentional Behavior Changes.

### Tests

- One valid extraction preserving all four values, including a branch that `RouteValues` already
  URL-decoded.
- Missing, empty, and whitespace cases for each of the four parameters with exact status, exact
  `ErrorResponse` JSON, and the default content type.
- Precedence: when platform and owner are both empty, the platform failure is returned.
- Ingestion maps the named properties to `HmacAuthContext` without swapping platform and owner;
  badge and redirect pass them to `ITestResultsService` in its `(owner, repo, platform, branch)`
  order.
- All three handlers use the shared contract without changing success behavior.
- Production-backed route tests retain encoded-branch, wrong-method, and incomplete-path coverage.

Place extractor and processor tests under existing feature/routing-oriented test namespaces. Do not
create a `BadgeSmith.Api.Tests.Core.*` namespace; it would shadow existing relative `Core.Routing`
references in the CORS tests.

### Acceptance Criteria

- No handler contains a private copy of the four-field validation algorithm; there is one extractor.
- The named value order matches the public route order, and validation precedence follows it.
- Handlers use `IsSuccess`, `Parameters`, and `Failure`; they never call `TryPick*`, `IsT*`, or
  `AsT*`.
- The only per-request allocation added is the standard OneOf result instance.
- Ingestion, badge, and redirect success behavior is unchanged; the only wire change is the 400
  route-parameter body, which now uses `ErrorResponse`.

## Work Item 3: Provider Consistency (Conditional Upstream Fetch)

### Current Problem

`NuGetPackageService` and `GitHubPackageService` carry the same conditional-GET/cache mechanics:
cache lookup, `If-None-Match`/`If-Modified-Since`, send, 304 replay with validator merge, body read,
and cache write. The two copies have drifted in details that are not provider policy:

| Concern | `NuGetPackageService` | `GitHubPackageService` |
| --- | --- | --- |
| Generic upstream failure label | `NuGet API error` | `NuGet API error` (wrong provider) |
| Type sealing | `internal class` | `internal sealed class` |
| Constructor guards | none | `?? throw new ArgumentNullException` |
| URL segment escaping | none | `HttpUtility.UrlEncode` (form encoding; `+` for space) |
| Cache entry | `(string, string?, DateTimeOffset?)` tuple boxed into `IMemoryCache` | same |
| Upstream ETag storage | `EntityTagHeaderValue.Tag` drops the `W/` weak marker | same |

### Decision

Do not extract a shared fetch collaborator. Two providers are below the extraction threshold, the
inline flow keeps each provider's policy visible top to bottom, and a collaborator would add three
types and an indirection to save two forty-line blocks. Duplicated mechanics are acceptable only
under the controls below. A third provider is the trigger to extract, using three real examples.

### Design

Keep the conditional-GET/cache mechanics inline in both services and make the two blocks textually
identical in statement order, local names, and precedence:

```text
cacheKey → cache lookup → request construction (+ provider headers) → validators from cache →
SendAsync → status switch → cache write → deserialize → provider version/result mapping
```

Share one named cache entry type under `Core/Http`. This is a shared type, not shared behavior:

```csharp
internal sealed record UpstreamCacheEntry(string Payload, string? ETag, DateTimeOffset? LastModified);
```

It replaces the tuple across the `IAppCache` boundary (coding-style: no tuples across type
boundaries) and removes the box/unbox copy on read. `ETag` and `LastModified` remain nullable.
Provider services keep their `IAppCache` dependency; `ApplicationRegistry` wiring is unchanged.

Consistency fixes applied to both services:

- GitHub generic failure label becomes `GitHub API error: {status}`; NuGet keeps `NuGet API error`.
- `NuGetPackageService` becomes `sealed` after confirming no derivation exists. CA1852 is silent
  only because of `InternalsVisibleTo`.
- Both constructors guard dependencies with `?? throw new ArgumentNullException(...)`.
- Both URLs escape every path segment with `Uri.EscapeDataString` (NuGet package id; GitHub org and
  package id). `HttpUtility.UrlEncode` is removed.
- Upstream ETags are stored as `EntityTagHeaderValue.ToString()` so weak validators (`W/"..."`)
  survive the cache and are replayed verbatim in `If-None-Match`. GitHub commonly returns weak
  ETags; `If-None-Match` already uses weak comparison, so matching is unchanged but the stored
  validator is now correct.

304 behavior is exact and unchanged:

- 304 with a cache entry reuses the payload, merges response validators (response ETag overrides
  cached; Last-Modified precedence is content header, response `Date`, then cached), and writes the
  merged entry back to refresh its TTL.
- 304 without a cache entry returns `Error("Received 304 Not Modified without a cached entry")`,
  which maps to the existing 500 `ErrorResponse` path. It is a result state, not an exception.
- 4xx and 5xx responses are not cached and their bodies are not read.
- A successful 200 payload is cached before provider deserialization, preserving current
  empty/invalid-payload behavior.
- Transport exceptions are not caught in the services; the handler-level safe 500 remains.
- The 15-minute memory TTL is unchanged.

Allocation delta per upstream call: the boxed tuple (one heap object) is replaced by one
`UpstreamCacheEntry` record (one heap object, no unbox copy on read). No result type is added.

### Tests

Drift guard: one scenario matrix executed against both services. Build it as a shared test helper
(a stub `HttpMessageHandler` that records requests and scripts responses, plus a real
`MemoryAppCache`) and a shared `[Theory]` data set applied to `NuGetPackageService` and
`GitHubPackageService` separately:

- First 200 response sends no validators and writes the cache.
- Cached ETag produces `If-None-Match` verbatim, including a weak `W/` validator.
- Cached Last-Modified produces `If-Modified-Since`.
- 304 with cache reuses the payload.
- 304 with cache writes merged validators and refreshes the cache TTL.
- 304 without cache returns the `Error` result and the handler emits the safe 500 body.
- Response ETag overrides cached ETag when present.
- Last-Modified precedence is content header, response `Date`, then cached value.
- 4xx/5xx status is not cached and its body is not read.
- Cancellation is forwarded to `SendAsync`.
- Upstream ETag never becomes the downstream badge ETag.

Provider-specific tests:

- NuGet: escaped request path, 200, 404, generic `NuGet API error`, empty/invalid payload, and
  version mapping.
- GitHub: bearer/accept headers, escaped org and package path, 200, 401, 403, 404, generic
  `GitHub API error`, empty/invalid payload, and version mapping.

Place provider tests under `BadgeSmith.Api.Tests.Features.NuGet` and
`BadgeSmith.Api.Tests.Features.GitHub`, mirroring production; the shared matrix helper lives under
the existing `TestHelpers` folder.

Allocation: add a `--type=providers` suite to `tests/BadgeSmith.Api.Performance.Tests` that runs
`GetLatestVersionAsync` for both services against a stub handler with `[MemoryDiagnoser]` (cold
cache 200 path and warm cache 304 path). Record the numbers in the closeout; this is not a CI gate.

### Acceptance Criteria

- The conditional-GET/cache blocks in both services are textually identical apart from provider
  headers, URL construction, and the status-to-result switch.
- Provider policy (request headers, status mapping, deserialization, result union) remains visible
  in each service; no shared base class or fetch collaborator exists.
- The shared scenario matrix passes against both services.
- Tuple cache entries are gone; `UpstreamCacheEntry` is the only upstream cache-entry shape.
- Responses sent to BadgeSmith clients are unchanged; only requests sent upstream change (escaping,
  verbatim weak ETags).

## Work Item 4: Typed Response Cache and Redirect API

### Current Problem

`ResponseHelper` has a legacy raw `Cache-Control` redirect overload and a primitive-list structured
overload. Git history shows the sole raw-overload consumer was migrated by `49afc7f2` when structured
caching was introduced, leaving compatibility residue with no current consumer.

The current APIs allow:

- Raw malformed cache directives.
- Non-redirect `HttpStatusCode` values.
- Negative TTL values.
- Public-cache TTLs and `noStore=true` in the same call.
- A cache-policy-unspecified redirect whose client caching may be heuristic.

Three further hygiene defects live in the same area:

- The public badge policy `600/300/1200/3600` is written as literals in four handlers
  (`NuGetPackageBadgeHandler`, `GithubPackagesBadgeHandler`, `TestResultsBadgeHandler`,
  `TestResultRedirectionHandler`), and `BuildCacheHeaders` interpolates the `Cache-Control` string on
  every response.
- `OkHealthWithNoCache` makes `Core/Routing/Helpers` depend on `Features/HealthCheck`.
- `ComputeStrongEtag` allocates four times per cached response (UTF-8 byte array, hash array, hex
  string, quoted string).

### Types

`RedirectStatus` is a `readonly record struct` with a private constructor and static members for every
Location-driven redirect status. The repository uses no enums; the struct keeps the typed-value style
and avoids an enum's implicit integer conversions:

```csharp
internal readonly record struct RedirectStatus
{
    public static RedirectStatus MovedPermanently { get; } = new(301);
    public static RedirectStatus Found { get; } = new(302);
    public static RedirectStatus SeeOther { get; } = new(303);
    public static RedirectStatus TemporaryRedirect { get; } = new(307);
    public static RedirectStatus PermanentRedirect { get; } = new(308);

    public int Code { get; }
}
```

`default(RedirectStatus)` has `Code == 0`; every redirect API rejects it with an `ArgumentException`
so an uninitialized status cannot reach the wire.

Do not include 300 Multiple Choices (separate choice representation), 304 Not Modified (conditional
caching), or deprecated 305/reserved 306.

`PublicCachePolicy` is an immutable `sealed record class` expressed with `TimeSpan` values:

- Shared maximum age (`s-maxage`).
- Client maximum age (`max-age`).
- Stale while revalidate.
- Stale if error.

It is a class, not a struct, because it precomputes its `Cache-Control` header value once in the
constructor and is consumed through process-wide named presets; a struct would copy forty bytes per
call and expose a `default` instance with a null header value. Construction rejects negative values,
sub-second values that cannot be emitted losslessly as HTTP delta-seconds, and values outside the
supported integer delta-seconds range. Conversion uses ticks, not floating-point `TotalSeconds`:

- `value >= TimeSpan.Zero`.
- `value.Ticks % TimeSpan.TicksPerSecond == 0`.
- `value.Ticks / TimeSpan.TicksPerSecond <= int.MaxValue`.

The precomputed value is deterministic:

```text
public, s-maxage=<shared>, max-age=<client>, stale-while-revalidate=<swr>, stale-if-error=<sie>
```

Do not reject a shared max age merely because it exceeds BadgeSmith's current CloudFront 24-hour
maximum. The reusable response policy models valid HTTP delta-seconds; CloudFront owns its deployment
cap.

Add one product preset under `Features`:

```csharp
internal static class BadgeResponsePolicy
{
    public static PublicCachePolicy PublicCache { get; } = new(
        sharedMaxAge: TimeSpan.FromSeconds(600),
        clientMaxAge: TimeSpan.FromSeconds(300),
        staleWhileRevalidate: TimeSpan.FromSeconds(1200),
        staleIfError: TimeSpan.FromSeconds(3600));
}
```

All four current call sites use this preset. Per-call `PublicCachePolicy` construction in handlers is
prohibited; a new policy is a new named preset.

### API

Expose explicit methods without optional-parameter ambiguity:

```csharp
RedirectCached(string location, PublicCachePolicy cachePolicy)
RedirectCached(string location, RedirectStatus status, PublicCachePolicy cachePolicy)
RedirectNoStore(string location)
RedirectNoStore(string location, RedirectStatus status)
```

The convenience overloads use `Found`; the explicit overloads preserve every supported redirect
status. Do not expose a plain redirect with unspecified cache intent.

`OkCached` requires a `PublicCachePolicy` so public cache-control composition exists once. Remove the
hidden nullable/default policy path; every cached response selects a preset explicitly.

Move `OkHealthWithNoCache` out of `ResponseHelper`: `HealthCheckHandler` composes
`ResponseHelper.Ok(response, LambdaFunctionJsonSerializerContext.Default.HealthCheckResponse, ...)`
with `NoStoreHeaders` itself, and `ResponseHelper` no longer references `Features.*`.

Remove:

- Both old redirect overloads.
- Raw `Cache-Control` input.
- Nullable TTL primitive parameters.
- The `noStore` boolean.
- `CacheSettings`.

### Strong ETag Computation

`ComputeStrongEtag` keeps its contract (SHA-256 over the UTF-8 body, uppercase hex, quoted) and drops
to one allocation: encode into a `stackalloc` or pooled UTF-8 buffer sized by
`Encoding.UTF8.GetByteCount`, hash with `SHA256.HashData(ReadOnlySpan<byte>, Span<byte>)` into a stack
buffer, and build the quoted hex with `string.Create(66, ...)` and `Convert.TryToHexString`. Existing
ETag tests must pass unchanged.

### Header Contract

`RedirectCached` emits the policy's precomputed `Cache-Control` value. `RedirectNoStore` emits:

```text
Cache-Control: no-store
```

Do not emit response `Pragma` or `Expires`. They are HTTP/1.0 compatibility behavior and are not part
of the future reusable handler's default contract.

Rename `NoCacheHeaders` to `NoStoreHeaders`, use the same modern no-store composer, and remove the
legacy `Pragma`/`Expires` response headers from health and ingestion responses. This is an intentional
wire-visible cleanup.

The exact no-store contract for health, ingestion, and `RedirectNoStore` is:

```text
Cache-Control: no-store
```

The existing `Cache-Control: no-store, no-cache, must-revalidate` value is intentionally replaced;
this is not only removal of `Pragma` and `Expires`. `NoStoreHeaders` retains its content-type input so
ingestion can keep an explicit JSON content type, while `CreateResponse` continues to add the default
content type when no override is supplied. The per-response header dictionary stays per response
because `CorsHandler.ApplyResponseHeaders` mutates it; only its values are constants.

All redirect APIs use one guard message: `Location cannot be null, empty, or whitespace.`

The current public test-result redirect remains 302 with `BadgeResponsePolicy.PublicCache`
(600/300/1200/3600 seconds).

Allocation delta: `Cache-Control` string interpolation per cached response is removed (computed once
per preset); strong ETag computation drops from four allocations to one; no-store responses are
unchanged (per-response dictionary with constant values). No new per-request allocation is added.

### Tests

- Cached redirect for 301, 302, 303, 307, and 308.
- No-store redirect for 301, 302, 303, 307, and 308.
- `default(RedirectStatus)` is rejected by every redirect API.
- Exact `Location`, status, null body, default content type, and cache headers.
- Missing/empty/whitespace location rejection.
- Zero TTL acceptance.
- Negative, sub-second, and overflow TTL rejection.
- Public cache header value is deterministic and computed once per policy instance.
- No-store response contains no `Pragma` or `Expires`.
- Health, ingestion, and no-store redirect responses assert exact `Cache-Control: no-store`, not a
  substring match; `HealthContractTests` moves from `Contains` to an exact assertion.
- `OkCached` retains source-generated JSON, SHA-256 strong ETag, If-None-Match, Last-Modified, and
  exact public cache behavior with `BadgeResponsePolicy.PublicCache`.
- Strong ETag value is byte-for-byte identical before and after the single-allocation rewrite.
- Functional test-result redirect remains 302 and points to the stored `url_html`.
- Allocation gate (Release, warmed up, `GC.GetAllocatedBytesForCurrentThread()`): `RedirectCached`,
  `RedirectNoStore`, and `OkCached` with the preset each assert an explicit byte ceiling recorded in
  the test at implementation time; exceeding it fails.

### Acceptance Criteria

- Illegal redirect/cache combinations are not representable.
- Current cached redirect behavior remains wire-compatible.
- Intentional legacy no-store header removal is explicit in tests and final architecture notes.
- CloudFront 307/308 caching receives an explicit Cache-Control header through `RedirectCached`.
- `ResponseHelper` has no dependency on `Features.*`.
- Exactly one named public cache preset exists and all four call sites use it.

## Work Item 5: Route-Table Immutability

### Current Problem

`RouteTable.Routes` exposes a setter with no assignment callsite. The route resolver captures the
array reference during lazy `ApplicationRegistry.ApiRouter` initialization. Reassignment before
initialization changes behavior; reassignment after initialization does not update the existing
resolver. The same resolver also drives CORS allowed-method behavior. A getter-only array would still
allow element mutation (`RouteTable.Routes[0] = ...`), which is only half of "cannot be reassigned".

### Design

Expose the inventory as an immutable array and make the resolver consume it:

```csharp
public static ImmutableArray<RouteDescriptor> Routes { get; } = [...];
```

- `RouteResolver` takes `ImmutableArray<RouteDescriptor>`; its `foreach` uses the struct enumerator,
  so iteration cost and allocation are identical to the array.
- `RouteTestBuilder.CreateRouteResolver(params RouteDescriptor[] routes)` in both test projects wraps
  with `ImmutableArray.Create(routes)`; synthetic route tests are otherwise unchanged.
- Do not add a test-only setter or an immutable route-registry abstraction. Future package extraction
  should move application route composition into explicit constructor input rather than global state.

Allocation delta: none. `ImmutableArray<T>` is a struct wrapper over the same array.

### Tests

- Existing real route inventory test remains exact.
- Existing production-backed resolver matrix remains green.
- CORS allowed-method tests remain green.
- Semantic reference analysis confirms no assignment consumer was removed.

### Acceptance Criteria

- Route inventory cannot be reassigned or mutated element-wise.
- Route order, descriptors, handlers, resolver behavior, and public route behavior are unchanged.

## CDK Safety Net

### Project

Create `tests/BadgeSmith.CDK.Tests` as a dedicated xUnit v3/VSTest project and add it to the `tests`
solution folder. Use Central Package Management and direct project/package references; do not place
versions in the project file.

Match the repository test-project shape:

- `IsTestProject=true`.
- `OutputType=Exe`.
- The repository target framework.
- `Microsoft.NET.Test.Sdk`, `xunit.v3`, and `xunit.runner.visualstudio`.
- Do not add `xunit.v3.runner.console`.
- `[assembly: CollectionBehavior(DisableTestParallelization = true)]`: JSII runs one Node.js kernel
  per process, so the small suite runs sequentially instead of racing the kernel.

Reference:

- `build/BadgeSmith.CDK.Shared/BadgeSmith.CDK.Shared.csproj`.
- `Amazon.CDK.Lib` for `Amazon.CDK.Assertions` APIs.
- The repository-standard xUnit v3, VSTest SDK, and runner packages.

These are in-process template tests. They require neither Docker, AWS credentials, nor the AWS CDK
CLI: `Amazon.CDK.Assertions` synthesizes through the JSII runtime, which needs only a Node.js
executable on `PATH`. Use the Node.js major pinned by `deploy.yml`; do not invent a second version
policy.

Use `BadgeSmith.CDK.Tests` namespaces and the repository test-name convention. Add a narrow
`.editorconfig` section for `tests/BadgeSmith.CDK.Tests/**.cs` matching the existing xUnit
arbitrations for underscore names and public framework entry points: `CA1062`, `CA1515`, and
`CA1707`. Do not weaken analyzer policy globally.

### Test Seam

Do not instantiate `ProductionStack` in unit tests. Its hosted-zone context lookup and fixed relative
Lambda asset make that path depend on deployment context and repository working directory.

Extract a public static `BadgeSmithCloudFrontFactory` with this responsibility:

```text
Distribution Create(Construct scope, string originHostname, ICertificate certificate)
```

It is public like the sibling constructs in `BadgeSmith.CDK.Shared`: it is composition API, not a
test-only seam, so no `InternalsVisibleTo` is added. It creates the `HttpOrigin`, cache policy, and
distribution directly under `scope`, returns the distribution, and uses the existing IDs
`OriginControlledCachePolicy` and `BadgeSmithCloudFront`. It preserves origin `HTTPS_ONLY`, the
custom domain, cache behavior, and all current `DistributionProps`. Do not introduce a nested
`Construct` solely for testing; the extra construct path would change synthesized logical IDs.

`ProductionStack` passes itself as `scope` and remains the owner of API Gateway domain extraction,
certificate import, hosted-zone lookup, `ARecord`, outputs, and resource properties. The factory
must not absorb DNS or certificate lookup policy.

CloudFront tests call the factory on a plain test `Stack` with a dummy origin hostname and
`Certificate.FromCertificateArn` using a dummy ARN in `us-east-1` (CDK validates the distribution
certificate region) while the factory keeps `DomainNames` non-empty. They do not create Route53
resources or perform hosted-zone lookups.

Lambda assertions instantiate `BadgeSmithFunctionConstruct` directly with the existing
`BadgeSmithFunctionConfiguration` overload and a test-owned temporary asset directory or zip created
under the test's temp path. They do not use the production relative asset path. Use
`SharedInfrastructureConstruct` on the test stack to provide the three real CDK table constructs and
execution role; do not invent fake table or role implementations.

### Logical-ID Drift Proof

The factory extraction must be behavior-neutral. Prove it offline; no AWS credentials are needed:

1. `build/cdk.context.json` is tracked and already caches the `localstackfor.net` hosted zone for
   account `377140207735` / region `eu-central-1`, so `HostedZone.FromLookup` resolves from context.
2. Place a placeholder zip at `artifacts/badge-lambda-linux-arm64.zip`; the same file is used for both
   synths so asset hashes match.
3. Before the change, from `build/`:
   `cdk synth BadgeSmithStack --context account=377140207735 --context region=eu-central-1 --output cdk.out.before`.
4. After the change: the same command with `--output cdk.out.after`.
5. Diff `BadgeSmithStack.template.json` between the two outputs. Any resource replacement,
   logical-ID change, or property change outside the intended refactor is a blocker.
6. Remove the placeholder zip and both output directories; do not commit them.

The operator synth in the deployment workflow remains a separate gate and is not unit-test setup.

### Optional Tidy (separate commit)

While `ProductionStack` is open, fix two latent defects as an isolated, reviewable commit after the
drift proof: `LocalStackDotnetHostedZone` is a property that calls `HostedZone.FromLookup` on every
access (a second access throws a duplicate-construct error) and becomes a local inside
`CreateCustomDomainRecord`; `CreateCustomDomainRecord` becomes private and returns nothing because
its result is discarded. Re-run the drift proof after this commit.

### Assertions

CloudFront cache policy:

- Minimum TTL is zero.
- Default TTL is zero.
- Maximum TTL remains 86400 seconds.
- Query-string behavior is all.
- Cookie behavior is none.
- Ordinary header behavior is none.
- Gzip and Brotli cache variants are enabled.

Distribution behavior:

- Viewer protocol policy redirects HTTP to HTTPS.
- Allowed methods remain all.
- Cached methods remain GET, HEAD, and OPTIONS.
- The origin request policy remains all viewer headers except Host.
- The custom cache policy is attached to the default behavior.
- Compression, IPv6, Price Class 100, HTTP/2+HTTP/3, and TLS 1.2 2021 remain configured.

Distribution error caching:

- No `CustomErrorResponses` are configured, so CloudFront's default Error Caching Minimum TTL (10
  seconds) applies to cacheable 4xx/5xx. The assertion records the current default so a future
  change is deliberate.

Lambda/deployment contract where deterministically testable:

- Runtime is `provided.al2023`.
- Production architecture is ARM64.
- Timeout is 20 seconds.
- Memory is 512 MB.
- Handler is `bootstrap`.
- Required table and upstream-mode environment variables are present without asserting deploy-time
  token values.

### Acceptance Criteria

- Tests fail on positive MinTTL, positive DefaultTTL, unbounded/changed MaxTTL, cache-key drift,
  method drift, or viewer transport regression.
- Assertions target semantic CloudFormation properties, not generated logical-ID strings where a
  matcher can express the relationship.
- No full-template golden snapshot is used as a substitute for focused assertions.
- Tests run without the AWS CDK CLI and without AWS credentials.

## Canonical Documentation

Update `ARCHITECTURE.md#cache-strategy` to own:

- The four cache layers and their distinct resources/validators.
- `s-maxage` versus `max-age` ownership.
- Direct API Gateway versus CloudFront behavior.
- Origin-controlled positive freshness.
- The load-bearing zero minimum/default TTL invariants for 2xx/3xx responses.
- Error-response edge caching (Error Caching Minimum TTL) as distinct from origin-controlled
  freshness.
- Cache-key and header-variation review rules.
- Explicit redirect cache intent.
- The OPTIONS/preflight cache-key warning.

Keep exact CDK property values in `ProductionStack.cs`; architecture records invariant meaning rather
than a second volatile configuration inventory.

Update `build/BadgeSmith.CDK/README.md` with a short relay to the architecture cache contract and the
exact CDK source location. Do not duplicate the full policy there.

Update the test documentation for the dedicated CDK test project and its in-process, no-AWS test
boundary. Add `tests/BadgeSmith.CDK.Tests/README.md` as its natural owner and add the corresponding
document-role row to `docs/README.md`. Do not put CDK/JSII test guidance into the Aspire/LocalStack
test guide. Do not change the root public README.

Update `.github/workflows/ci-cd.yml` so the new CDK test project runs on hosted pull-request/push CI
and publishes a separate TRX result set. A local-only CDK project is not an effective safety net.
Add `actions/setup-node` with the Node.js major pinned by `deploy.yml`; do not install the AWS CDK
CLI in PR CI (in-process assertions do not need it) and do not create a second independently pinned
version policy. This workflow change is approval-gated and must remain explicit in the
implementation diff.

After implementation and validation:

- Mark Wave 3 done in `docs/ROADMAP.md` with a concise outcome.
- Relocate durable architecture facts before deleting this plan.
- Delete this temporary plan once current canon and roadmap own the completed outcome.

## Implementation Sequence

### Increment 1a: Api Test Safety Net

- Expand ResponseHelper tests around redirect status/cache and no-store decisions.
- Establish both Lambda compile-mode build commands.

### Increment 1b: CDK Safety Net

- Extract the public `BadgeSmithCloudFrontFactory` with the exact static seam above and prove the
  before/after production template has no logical-ID or property drift.
- Add the dedicated CDK assertions project and baseline template tests.
- Add the scoped test analyzer arbitration and hosted CI execution (Node.js setup only) for the CDK
  project.
- Apply the optional `ProductionStack` tidy as a separate commit and re-run the drift proof.

### Increment 2: Route Hygiene

- Add the named test-result route parameters, OneOf result, and extractor with their tests.
- Migrate all three handlers and switch the missing-parameter 400 body to `ErrorResponse`.
- Make `RouteTable.Routes` an `ImmutableArray<RouteDescriptor>` and adapt `RouteResolver` and the
  test builders.
- Run routing, CORS, handler, and full unit tests.

### Increment 3: Provider Consistency

- Add `UpstreamCacheEntry` and replace the tuple cache entries in both services.
- Align both conditional-GET blocks to the same statement order and names.
- Apply the consistency fixes: label, sealing, guards, `Uri.EscapeDataString`, weak ETag storage.
- Add the shared scenario matrix, provider-specific tests, and the providers benchmark suite.
- Run direct service, functional package, and full unit tests.

### Increment 4: Typed Response Cache and Redirects

- Add `RedirectStatus`, validated `PublicCachePolicy`, and the `BadgeResponsePolicy.PublicCache`
  preset.
- Centralize deterministic public-cache and no-store composition; move the health response out of
  `ResponseHelper`.
- Replace `OkCached` cache settings and all redirect/handler call sites with the preset.
- Replace `NoCacheHeaders` with the modern no-store contract.
- Remove both legacy redirect overloads and `CacheSettings`.
- Rewrite `ComputeStrongEtag` to a single allocation; add the allocation gates.
- Run ResponseHelper, health, ingestion, redirect functional, and full unit tests.

### Increment 5: Lambda Request Core

- Add and test `LambdaRequestProcessor`.
- Reduce both entry points to compile-mode-specific bootstrap wrappers.
- Build both compile modes and run the full affected test suite.

### Increment 6: Canon, Final Validation, and Closeout

- Update architecture, production CDK guide, test guide, and roadmap.
- Add the CDK test guide and its document-role entry.
- Run all validation gates.
- Remove this plan only after durable information has moved to canon.

Each increment must remain independently reviewable. Do not hide behavior changes in mechanical
cleanup commits.

## Validation Matrix

Run from the repository root unless a command says otherwise.

```bash
dotnet restore BadgeSmith.sln
dotnet build BadgeSmith.sln -c Release --no-restore
dotnet test tests/BadgeSmith.CDK.Tests/BadgeSmith.CDK.Tests.csproj -c Release --no-build
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj -c Release --no-build --filter "Category=Unit"
dotnet test tests/BadgeSmith.Api.Tests/BadgeSmith.Api.Tests.csproj -c Release --no-build --filter "Category=Functional"
```

Validate the production compile branch explicitly:

```bash
dotnet restore src/BadgeSmith.Api/BadgeSmith.Api.csproj \
  -p:EnableTelemetry=false -p:EnableLocalStack=false
dotnet build src/BadgeSmith.Api/BadgeSmith.Api.csproj -c Release --no-restore \
  -p:EnableTelemetry=false -p:EnableLocalStack=false
```

Record provider allocation numbers (not a gate):

```bash
dotnet run --project tests/BadgeSmith.Api.Performance.Tests -c Release -- --type=providers --mode=memory
```

Run repository quality gates:

```bash
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,**/bin/**,**/obj/**"
git diff --check
```

Synthesize only the native production stack as a separate read-only gate. Context is tracked in
`build/cdk.context.json`, and a placeholder ARM64 zip is sufficient for synth (see the logical-ID
drift proof):

```bash
cd build
cdk synth BadgeSmithStack \
  --context account=<aws-account-id> \
  --context region=eu-central-1
```

Never use `--all`. Do not deploy. Native AOT publication is not part of the ordinary implementation
loop and requires separate approval if requested.

## Risk Controls

| Risk | Control |
| --- | --- |
| Production-only bootstrap drift | Explicit telemetry-disabled Release restore/build |
| Logger category or safe-error drift | Shared processor characterization tests |
| Route error-message drift | Exact data-driven extractor tests |
| Provider mechanics drift | Identical block ordering; one scenario matrix executed against both services |
| Upstream/downstream validator conflation | Separate types, namespaces, and tests |
| Redirect heuristic caching | Require public-cache or no-store API |
| Invalid cache values | Validated `PublicCachePolicy` construction |
| CloudFront overriding no-store | CDK MinTTL zero assertion |
| CORS preflight cache poisoning | DefaultTTL zero assertion and architecture warning |
| CloudFormation replacement | Offline before/after synth diff and stable resource scope/IDs |
| Error responses edge-cached by default | Documented in canon; CDK assertion records the absence of `ErrorResponses` |
| AOT/trim regression | Source-generated serialization unchanged and both compile modes built |
| Test-only reward hacking | Analyzer wall, full affected suite, and Slopwatch |

## Intentional Behavior Changes

- GitHub generic upstream failures say `GitHub API error`, not `NuGet API error`.
- Upstream requests escape path segments with `Uri.EscapeDataString` and replay cached ETags
  verbatim, including weak `W/` validators. These change requests sent to NuGet/GitHub, not
  responses sent to BadgeSmith clients.
- Missing test-result route parameters return the standard `ErrorResponse` JSON body with
  `MISSING_ROUTE_PARAMETER` and the lowercase route key instead of a raw string body, and validation
  precedence follows route order.
- No-store responses stop emitting deprecated HTTP/1.0 `Pragma` and `Expires` headers.
- Health, ingestion, and no-store redirect responses change `Cache-Control` from
  `no-store, no-cache, must-revalidate` to exactly `no-store`.
- Internal redirect construction no longer accepts raw Cache-Control, arbitrary HttpStatusCode,
  partial nullable TTLs, or conflicting no-store/public-cache inputs.

All other HTTP route, status, body, serializer, ETag, Last-Modified, cache TTL, CORS, and redirect
target behavior must remain unchanged.

## Implementation Approval Gate

This specification authorizes planning only. Before changing production, build/package, solution,
CI, or CDK code, present the planned first increment and obtain Deniz's explicit `go`, `apply`,
`proceed`, `basla`, or `yap` approval under `AGENTS.md`.
