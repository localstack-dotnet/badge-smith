# BadgeSmith Architecture

> **Architectural overview and design decisions for a high-performance, secure badge service**

BadgeSmith is optimized for **AWS Lambda** cold start performance with a focus on separation of concerns. This document outlines the key architectural decisions and system design.

## 🏗️ **System Overview**

### **Request Flow**

```
Client → CloudFront → API Gateway → Lambda → DynamoDB
                                         ↓
                                   Secrets Manager
```

**Components:**

- **CloudFront**: Global edge caching with endpoint-specific TTLs
- **API Gateway HTTP v2**: Request routing and CORS handling
- **Lambda Function**: .NET 10 Native AOT runtime
- **DynamoDB**: NoSQL storage with optimized access patterns
- **Secrets Manager**: Secure credential storage

### **Cache Strategy**

BadgeSmith implements **multi-layer caching** with an origin-controlled CloudFront cache
policy:

1. **CloudFront Edge Cache**: A custom policy bounds TTL while Lambda response headers
   select endpoint-specific cache lifetimes
2. **Lambda Memory Cache**: In-memory caching with TTL
3. **Conditional Requests**: ETag support for bandwidth optimization

Cache headers are **managed by the Lambda function** within the minimum and maximum TTL
bounds enforced by the CloudFront policy.

## 🎯 **Core Design Decisions**

### **Native AOT Optimization**

BadgeSmith prioritizes **cold start performance** and **deployment efficiency**:

**Motivations:**

- **No JIT startup overhead** in the Lambda runtime
- **Smaller self-contained deployment artifacts**
- **Lower memory footprint** for cost optimization
- **Native executable deployment** on the Lambda `provided.al2023` runtime

**Implementation Choices:**

- **No ASP.NET Core Host**: Direct Lambda runtime integration
- **Lambda-only composition**: `ApplicationRegistry` replaces dependency injection in
  `src/BadgeSmith.Api`; the separate CLI intentionally uses normal .NET DI
- **Lambda-only configuration**: the Lambda reads required environment variables
  directly; tooling may use `IConfiguration`
- **Source Generators**: JSON serialization without reflection

### **Conditional Compilation Flags**

```xml
<EnableTelemetry>true</EnableTelemetry>      <!-- Development: telemetry enabled -->
<EnableLocalStack>true</EnableLocalStack>    <!-- Development: LocalStack integration -->
```

**Production optimization**: ordinary builds default both properties to `true`, while
the production Docker publish passes `-p:EnableTelemetry=false` and
`-p:EnableLocalStack=false` to:

- Remove telemetry dependencies from deployment package
- Exclude LocalStack client libraries
- Reduce final binary size
- Improve cold start performance

The `Dockerfile` owns those publish properties. `tools/badgesmith.cs lambda build` drives
the Docker targets but does not independently redefine the compilation flags.

## 📊 **Data Architecture**

### **DynamoDB Table Design**

BadgeSmith uses **three DynamoDB tables** with optimized access patterns:

DynamoDB and Secrets Manager are deliberate architecture boundaries. BadgeSmith does not use a
relational store; do not introduce Entity Framework or SQL persistence patterns.

#### **1. Organization Secrets Table**

**Purpose**: Maps GitHub organizations to their authentication secrets

**Access Pattern**:

- Lookup secrets by organization name and token type
- Supports multiple token types per organization (Package, TestData)

#### **2. Test Results Table**

**Purpose**: Stores CI/CD test results with efficient latest-result queries

**Key Design**:

- **Partition Key**: Repository identifier
- **Sort Key**: Platform + branch + timestamp for chronological ordering
- **GSI**: Optimized for "latest result" queries without scanning

#### **3. Nonce Table**

**Purpose**: Prevents HMAC replay attacks

**Features**:

- **TTL-based expiry**: Automatic cleanup of old nonces
- **Atomic operations**: Conditional writes prevent race conditions
- **Cost-optimized**: 45-minute retention window

### **Database Seeding**

The **`badgesmith secrets seed`** command (in `tools/badgesmith.cs`) provides:

- **Local development setup**: Seeds test data for LocalStack
- **Production deployment**: Can seed real AWS resources (with appropriate credentials)
- **Configuration-driven**: JSON-based organization and secret management
- **Idempotent operations**: Safe to run multiple times

See `tools/README.md` for secret mapping format and the org-scoped secret name
`badgesmith/github/{org}/{key}`.

## 🚦 **Routing Infrastructure**

### **High-Performance Routing**

BadgeSmith implements **custom routing** optimized for Lambda environments:

**Design Principles:**

- **Allocation-conscious** route matching with span-based operations on hot paths
- **Pattern-based routing**: Template patterns (`{param}`) and exact matches
- **Handler resolution**: Direct function calls via `ApplicationRegistry`
- **Route-first validation**: Parameters validated before handler execution

**Route Types:**

- **Exact patterns**: Static routes like `/health`
- **Template patterns**: Parameterized routes like `/badges/packages/{provider}/{package}`
- **Method-aware**: GET/POST routing with proper HTTP semantics

### **Centralized Service Registry**

**`ApplicationRegistry`** replaces traditional dependency injection:

**Benefits:**

- **No DI overhead**: Direct service resolution
- **Lazy initialization**: Services created only when needed
- **Singleton management**: Shared instances across requests
- **Clear dependencies**: Explicit service wiring

## 🔐 **Security Architecture**

### **Canonical HMAC Authentication**

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

### Upstream And Transport Modes

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

The stored `url_html` value is the click target behind the public test-result redirect.
It may point to dorny, Allure, ReportPortal, or another white-label HTTPS report host;
it is not restricted to the GitHub workflow origin. Choosing that target is an explicit
capability of an organization-authorized HMAC ingester. Both stored result URLs must be
absolute HTTPS URLs without embedded credentials.

**Security Features:**

- **Organization isolation**: Each organization has separate secrets
- **Token type separation**: Different secrets for package access vs test ingestion
- **Authentication logging**: Successful authentication and invalid signatures are
  logged; API access logs record request outcomes
- **No secrets in code**: All credentials externalized to AWS services

### **Public Endpoints**

Package badge endpoints are **unauthenticated** and include:

- **Input validation** with comprehensive error responses
- **Stale-if-error cache directives** for eligible cached responses during origin
  failures

The production stack does not currently configure an application-specific WAF or API
throttling policy. Explicit abuse controls remain roadmap work; generic AWS service
quotas are not treated as an application rate-limit contract.

## ⚡ **Performance Optimizations**

### **Cold Start Mitigation**

- **Native AOT compilation**: Eliminates JIT overhead
- **Minimal dependencies**: Reduced assembly loading time
- **Shared service instances**: Avoid repeated initialization
- **Optimized JSON serialization**: Source generators instead of reflection

### **Runtime Performance**

- **Span-based operations**: Allocation-conscious string processing on hot paths
- **Memory caching**: Reduces external API calls
- **Connection pooling**: Reused HTTP clients and AWS SDK clients
- **Efficient data structures**: Optimized for read-heavy workloads

## 🛠️ **Development Tooling**

### **`badgesmith` CLI**

**`tools/badgesmith.cs`** is the file-based .NET CLI that owns BadgeSmith-specific
Lambda builds, test execution and ingestion, badge updates, and secret seeding. Exact
commands, defaults, dry-run behavior, and secret mapping live in `tools/README.md`.

### **`scripts/`**

**`scripts/`** holds the remaining load-testing fixtures:

- **`k6-perf-test.js`**: HTTP load testing with realistic traffic patterns
- **`sample-test-payload.json`**: Example test result payload for `tests ingest`

## 🏗️ **Code Organization**

### **Feature-Based Organization**

```
src/BadgeSmith.Api/
├── Core/                    # Shared infrastructure concerns
│   ├── Security/           # Authentication, HMAC, secrets
│   ├── Routing/            # HTTP routing and response handling
│   ├── Caching/            # Memory caching with TTL
│   └── Observability/      # Logging and telemetry
└── Features/               # Business capabilities (vertical slices)
    ├── NuGet/              # NuGet package badge functionality
    ├── GitHub/             # GitHub package badge functionality
    ├── TestResults/        # Test result ingestion and badge generation
    └── HealthCheck/        # System health monitoring
```

Shared source is compiled into consumers rather than exposed through a project reference.
`BadgeSmith.Api` links `Constants.cs`, `BadgeSmithApiActivitySource.cs`, and
`HmacCanonicalRequest.cs`; the Host and CDK projects link `Constants.cs`; and the file-based CLI
includes the HMAC helper. Before changing `src/shared/`, search project-file `Compile Include`
paths for `shared` or `SharedDir` and file-app directives for `#:include`, then validate every
consumer.

**Benefits:**

- **Feature isolation**: Changes to one feature don't affect others
- **Clear boundaries**: Each feature contains its models, services, and handlers
- **Team development**: Different teams can work on different features
- **Flexibility**: Features can be extracted to separate services if needed

### **Result Pattern**

BadgeSmith uses **OneOf result types** instead of exceptions for predictable error handling:

- **Type-safe errors**: Compile-time validation of error cases
- **Performance**: No exception overhead for expected failures
- **Explicit handling**: All failure modes must be handled
- **HTTP mapping**: Clear mapping from domain failures to HTTP status codes

## 🔧 **Infrastructure as Code**

### **AWS CDK Integration**

**`build/`** contains shared constructs and two separate .NET CDK app entrypoints. The
production app constructs only `BadgeSmithStack`; the local-performance app constructs
only `BadgeSmithPerformanceStack` and is never deployed to AWS. Their project paths,
working directories, artifacts, context, and safe commands live in the matching READMEs
under `build/`.

The deferred `badgesmith perf baseline` command will consume the local-performance app
as its infrastructure boundary when implemented. See the
[production](build/BadgeSmith.CDK/README.md) and
[local-performance](build/BadgeSmith.CDK.LocalPerformance/README.md) guides.

### **Local Development**

**`.NET Aspire`** provides local development experience:

- **LocalStack integration**: AWS service emulation
- **Lambda emulation**: Local function execution
- **Contract tests**: Aspire Testing starts `src/BadgeSmith.Host` and calls `APIGatewayEmulator` over HTTP; the test suite does not use Lambda RIE.

**Local benchmark infrastructure** uses Docker, LocalStack, and CDK. Production keeps API Gateway
HTTP v2, while the local-performance stack also exposes a Lambda Function URL for compatibility
with the LocalStack workflow. The observed limitation that introduced this fallback is preserved in
the dated [Iteration 0 and Wave 1 closeout evidence](docs/research/2026-07-09-iteration0-wave1-closeout.md).

## 🚀 **Deployment Strategy**

### **Multi-Stage Docker Build**

**`Dockerfile`** implements **optimized multi-stage builds**:

1. **Build stage**: .NET SDK with Native AOT compilation
2. **Lambda image**: Minimal runtime for container deployment
3. **Zip export**: Artifact generation for .zip deployment

### **Build Tooling**

**`tools/badgesmith.cs lambda build`** provides **cross-platform build automation**:

- **Multi-architecture**: x64 and ARM64 support
- **Build targets**: ZIP artifacts and container images
- **Docker-backed publish**: Drives the Docker targets that own production conditional compilation flags

## 📈 **Performance Characteristics**

### **Performance Goals**

The following non-functional requirements are design targets, not statements that every
deployed revision already satisfies:

| Objective | Target | Measurement source |
| --- | --- | --- |
| Lambda initialization | CloudWatch `Init Duration` p95 ≤100 ms | Cold-start Lambda `REPORT` lines for the deployed production revision |
| Runtime memory | CloudWatch `Max Memory Used` p95 ≤50 MB | Lambda `REPORT` lines for the deployed production revision |
| Deployment size | Production ARM64 ZIP ≤6 MB (6,000,000 bytes) | Hosted CI artifact before deployment |

The initialization goal covers Lambda's CloudWatch `Init Duration`, not the total
latency of the first request. First-invoke AWS client, credential, TLS, and upstream work
is measured separately as effective cold-request duration. No numeric effective
cold-request target is set yet; report it alongside INIT so optimization does not merely
move latency between phases.

### **Measurement Policy**

Native AOT removes JIT compilation from Lambda startup, but cold-start latency and
memory usage must be measured for each deployed revision rather than treated as fixed
architecture guarantees. Dated measurements live under `docs/research/` and
`docs/research/baselines/`; CloudWatch Lambda `REPORT` lines are the source of truth for
production INIT duration, execution duration, and memory usage. The initial production
baseline and optimization plan are recorded in
[`docs/research/2026-07-02-performance-opportunities.md`](docs/research/2026-07-02-performance-opportunities.md).

Goal verification uses this protocol:

- Measure the deployed production ARM64 revision on `provided.al2023`; record its commit,
  configured memory, deployment timestamp, and observation window. Do not mix samples
  from different deployment intervals.
- Use the most recent slice of up to 30 days wholly contained in one deployment interval,
  with at least 100 cold-start `REPORT` samples for `Init Duration`. If the interval has
  fewer samples, report the goal as unverified.
- Calculate each p95 with the nearest-rank method: sort the numeric samples ascending and
  select rank `ceil(0.95 × sample-count)`. Preserve the Logs Insights query or exported
  sample set with the result.
- Calculate `Max Memory Used` p95 over all `REPORT` samples in the same deployment slice,
  with at least 100 total samples.
- Measure the exact byte length of the ARM64 ZIP consumed by deployment; pre-deploy CI
  artifacts that are not deployed are validation inputs, not production results.

The 2026-07-02 baseline predates this percentile-based protocol. It remains useful
historical evidence but does not by itself establish pass/fail for these goals.

### **Scalability**

- **Stateless design**: Horizontal scaling without session affinity
- **Database optimization**: DynamoDB with appropriate partition key design
- **Caching strategy**: Reduces database load and external API calls

## 🔄 **CI/CD Integration**

### **CI Composite Actions**

**`.github/workflows/`** contains two composite actions with different scopes:

- **`run-dotnet-tests/`**: Repository-local multi-framework test execution
- **`update-test-badge/`**: Remotely reusable HMAC-authenticated badge updates
- **Cross-platform support**: Windows, Linux, macOS

The action definitions own exact inputs and execution; the reusable action's consumer
guide lives beside its `action.yml`.

### **Hosted Validation**

Eligible pull requests run the Release build, the full test suite, and the hosted ARM64
Lambda ZIP build. Live test-result publication is intentionally narrower:

- **Pull requests**: Build, tests, and ARM64 artifact validation without production
  mutation
- **Master pushes**: The same checks plus a best-effort authenticated badge update
- **Production CDK synth/deploy**: Separate approval-gated deployment workflow, not part of ordinary PR CI

Workflow files are the source of truth for triggers, runner images, action versions, and
deployment steps.

---

For detailed implementation examples and API documentation, see the main [README](README.md).
