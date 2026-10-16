# Runtime And Routing Architecture

Current rules for request routing, cache behavior, result contracts, and runtime scaling. Read this
document before changing route resolution, response caching, redirects, or hot-path behavior.
[Security and storage](security-and-storage.md) owns authentication and data boundaries;
[platform and delivery](platform-and-delivery.md) owns Lambda deployment and performance measurement.
The [consumer guide](../guide/README.md) explains how to use the public endpoints.

## Cache Strategy

BadgeSmith caching is **origin-controlled**: the CloudFront policy bounds what *can* be cached,
and Lambda response headers decide what *is* cached per request. This section owns the invariant
meaning; exact CDK property values live in
[`ProductionStack`](../../build/BadgeSmith.CDK.Shared/ProductionStack.cs) and
[`BadgeSmithCloudFrontFactory`](../../build/BadgeSmith.CDK.Shared/BadgeSmithCloudFrontFactory.cs).

### Cache Layers

| Layer | Resource | Owner of validators/TTL | Purpose |
| --- | --- | --- | --- |
| CloudFront edge | Custom `CachePolicy` | Lambda `Cache-Control` selects freshness inside policy bounds | Global low-latency delivery |
| Client/browser | `max-age` directive | Lambda per endpoint | Client-side reuse window |
| Conditional requests | Strong ETag (SHA-256 over body, quoted uppercase hex) + `If-None-Match` / `Last-Modified` | `ResponseHelper.OkCached` | 304 bandwidth savings |
| Upstream validator store | `UpstreamCacheEntry` in provider `IAppCache` (15 min TTL) | Provider ETag / `Last-Modified` replay | Avoids upstream body transfer on 304; cached JSON is still deserialized and versions are parsed/filtered on each request. Every badge request **revalidates** upstream — it is a validator store, not a freshness cache |

`s-maxage` belongs to **CloudFront**; `max-age` belongs to the **browser**. Direct API Gateway
traffic bypasses both edge layers and exercises only conditional-request and client caching.

### Load-Bearing Zero-TTL Invariants

The production cache policy pins `MinTTL=0` and `DefaultTTL=0` with `MaxTTL=86400`. For 2xx/3xx
responses these zeros mean: an origin response **without** `Cache-Control` must not be cached at
the edge, and `no-store`/`no-cache` responses must expire immediately. These values gate the
origin-controlled model; changing them converts silence into implicit caching.

### Error-Response Edge Caching

CloudFront's **Error Caching Minimum TTL** (10 s AWS default; not expressed in our template)
caches status codes 404/414/500/501/502/503/504 at the edge regardless of `DefaultTTL=0` and even
without origin `Cache-Control`. Codes 400/403/405/412/415 are cached only when the origin sends
`max-age`/`s-maxage`. Code 401 is in neither list. `ProductionStack` intentionally sets no
`CustomErrorResponses`; the [CDK test suite](../../tests/BadgeSmith.CDK.Tests/README.md) asserts
that absence so drift is caught before deploy.

### Redirect Cache Intent

Every redirect declares explicit cache intent through the typed API: `RedirectCached` emits the
policy's precomputed `Cache-Control`, `RedirectNoStore` emits exactly `Cache-Control: no-store`
(no `Pragma`, no `Expires`). Unspecified-intent redirects are not representable, and non-3xx
statuses cannot reach a redirect helper. The public test-result redirect remains `302` under the
single named preset `BadgeResponsePolicy.PublicCache` (600/300/1200/3600 seconds).

### Cache-Key Review Rules

The cache key includes **all query strings** (badge version filters vary by query), **no headers**,
and **no cookies**. Because `OPTIONS` preflight shares the same key shape, adding header-dependent
behavior requires revisiting CORS preflight correctness first.

## Routing Infrastructure

BadgeSmith implements custom routing optimized for Lambda environments:

- **Allocation-conscious matching** uses span-based operations on hot paths.
- **Pattern-based routing** supports template patterns (`{param}`) and exact matches.
- **Handler resolution** uses direct function calls through `ApplicationRegistry`.
- **Route-first validation** validates parameters before handler execution.

Route types include exact static patterns such as `/health`, parameterized templates such as
`/badges/packages/{provider}/{package}`, and method-aware GET/POST routing with proper HTTP
semantics.

Shared infrastructure lives in `Core/`; feature models, services, and handlers live together in
vertical slices under `Features/`. The [layout rules](../engineering/coding-style.md#layout) own
the full source organization and growth guidance; [testing style](../engineering/testing-style.md)
owns test layout and contract seams.

Linked or included shared source must be validated in every consumer under the
[shared-source gate](../engineering/quality-gates.md#shared-source-consumers).

## Application Registry

In the Lambda, `ApplicationRegistry` replaces traditional dependency injection:

- **Direct service resolution** avoids DI-container overhead.
- **Lazy initialization** creates services only when needed.
- **Shared instances** reuse singleton services across requests.
- **Explicit wiring** keeps dependencies visible.

This is a Lambda-only boundary, not a repository-wide ban on DI. The CLI and AppHost retain their
own [composition models](../engineering/coding-style.md#composition-boundaries).

## Runtime Performance

Cold-start mitigation combines [Native AOT](platform-and-delivery.md#native-aot-and-composition),
minimal dependencies, shared service instances, and source-generated JSON serialization instead of
reflection. These mechanisms reduce startup and repeated initialization work; measured goals follow
the [deployed-revision protocol](platform-and-delivery.md#measurement-policy).

Hot paths use:

- **Span-based operations** for allocation-conscious string processing.
- **Memory caching** for repeated data and secret lookups; see the
  [secret lifecycle](security-and-storage.md#secret-lifecycle) and its operational owner. The
  provider validator store has the distinct [per-request revalidation contract](#cache-layers).
- **Connection pooling** through reused HTTP clients and AWS SDK clients.
- **Read-oriented data structures** for read-heavy workloads.

The [allocation discipline](../engineering/coding-style.md#allocation-discipline) owns authoring
rules and verification tiers; this architecture does not relax their success-path allocation
contract.

## Result And Error Contracts

BadgeSmith uses **OneOf result types** instead of exceptions for expected domain failures:

- Typed errors make error cases visible at compile time.
- Expected failures avoid exception overhead.
- Consumers must handle all failure modes explicitly.
- Domain failures map clearly to HTTP status codes.

The precise result shape, accepted result allocation, and distinction between expected failures and
violated contracts belong to [coding style](../engineering/coding-style.md#allocation-discipline)
and its [signature rules](../engineering/coding-style.md#signature-hygiene).

## Scalability

- **Stateless requests** allow horizontal scaling without session affinity.
- **DynamoDB access patterns** use appropriate partition keys and latest-result queries; see
  [data architecture](security-and-storage.md#data-architecture).
- **Caching** reduces origin traffic and repeated database/API work according to each layer's
  contract. Requests reaching a package provider still revalidate upstream; caching is not
  permission to add a freshness shortcut.

For public routes and examples, see the [README](../../README.md) and
[consumer guide](../guide/README.md).
