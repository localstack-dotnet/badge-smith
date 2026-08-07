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

- **CloudFront**: Global edge caching with configurable TTL
- **API Gateway HTTP v2**: Request routing and CORS handling
- **Lambda Function**: .NET 10 Native AOT runtime
- **DynamoDB**: NoSQL storage with optimized access patterns
- **Secrets Manager**: Secure credential storage

### **Cache Strategy**

BadgeSmith implements **multi-layer caching** without CloudFront cache policies:

1. **CloudFront Edge Cache**: Configured via Lambda response headers
2. **Lambda Memory Cache**: In-memory caching with TTL
3. **Conditional Requests**: ETag support for bandwidth optimization

Cache headers are **managed by the Lambda function** to maintain full control over cache behavior across different endpoint types.

## 🎯 **Core Design Decisions**

### **Native AOT Optimization**

BadgeSmith prioritizes **cold start performance** and **deployment efficiency**:

**Motivations:**

- **Sub-100ms cold starts** vs 500ms+ with traditional .NET hosting
- **Smaller deployment packages** (~6MB zipped vs ~50MB)
- **Lower memory footprint** for cost optimization
- **Predictable performance** without JIT compilation overhead

**Implementation Choices:**

- **No ASP.NET Core Host**: Direct Lambda runtime integration
- **No Dependency Injection**: Centralized `ApplicationRegistry` for service management
- **No Configuration Framework**: Environment variables with direct access
- **Source Generators**: JSON serialization without reflection

### **Conditional Compilation Flags**

```xml
<EnableTelemetry>true</EnableTelemetry>      <!-- Development: telemetry enabled -->
<EnableLocalStack>true</EnableLocalStack>    <!-- Development: LocalStack integration -->
```

**Production Optimization**: Both flags are **disabled during Docker builds** to:

- Remove telemetry dependencies from deployment package
- Exclude LocalStack client libraries
- Reduce final binary size
- Improve cold start performance

Controlled via build arguments in `Dockerfile` and the `tools/badgesmith.cs lambda build` command.

## 📊 **Data Architecture**

### **DynamoDB Table Design**

BadgeSmith uses **three DynamoDB tables** with optimized access patterns:

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

- **Zero allocation** route matching with span-based operations
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
never the signature or digest. Public `badge update` targets require HTTPS; HTTP is
accepted only for loopback hosts (`localhost`, `127.0.0.0/8`, or `::1`). The explicit
local/deployed probe command, `tests ingest`, accepts both HTTP and HTTPS.

**Security Features:**

- **Organization isolation**: Each organization has separate secrets
- **Token type separation**: Different secrets for package access vs test ingestion
- **Audit logging**: All authentication attempts logged
- **No secrets in code**: All credentials externalized to AWS services

### **Public Endpoints**

Package badge endpoints are **unauthenticated** but include:

- **Rate limiting** (via CloudFront and API Gateway)
- **Input validation** with comprehensive error responses
- **Graceful degradation** during upstream service failures

## ⚡ **Performance Optimizations**

### **Cold Start Mitigation**

- **Native AOT compilation**: Eliminates JIT overhead
- **Minimal dependencies**: Reduced assembly loading time
- **Shared service instances**: Avoid repeated initialization
- **Optimized JSON serialization**: Source generators instead of reflection

### **Runtime Performance**

- **Span-based operations**: Zero-allocation string processing
- **Memory caching**: Reduces external API calls
- **Connection pooling**: Reused HTTP clients and AWS SDK clients
- **Efficient data structures**: Optimized for read-heavy workloads

### **Caching Strategy**

**Multi-tier caching** with appropriate TTL for each content type:

## 🛠️ **Development Tooling**

### **`badgesmith` CLI**

**`tools/badgesmith.cs`** is the file-based .NET CLI that owns BadgeSmith-specific
build, test, ingestion, badge-update, and secret-seed workflows:

- **`lambda build`**: Multi-arch Docker builds for Lambda deployment (ZIP and container)
- **`tests run`**: Per-target-framework `dotnet test` execution with TRX output
- **`tests ingest`**: HMAC-authenticated test result ingestion against a running API
- **`badge update`**: GitHub Actions test result posting used by the `update-test-badge` workflow
- **`secrets seed`**: Seeds GitHub org secret mappings into DynamoDB and Secrets Manager

See `tools/README.md` for full option reference and secret mapping setup.

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

**`build/`** contains shared constructs and two separate .NET CDK app entrypoints:

| Purpose | App project | CDK working directory | Native stack ID |
| --- | --- | --- | --- |
| Production | `build/BadgeSmith.CDK/BadgeSmith.CDK.csproj` | `build` | `BadgeSmithStack` |
| Local performance | `build/BadgeSmith.CDK.LocalPerformance/BadgeSmith.CDK.LocalPerformance.csproj` | `build/BadgeSmith.CDK.LocalPerformance` | `BadgeSmithPerformanceStack` |

The production app constructs only the production stack. Production deployment remains
approval-gated, must target `BadgeSmithStack` explicitly, and must never use `--all`.
The local-performance app constructs only LocalStack benchmarking infrastructure and is
never deployed to AWS.

The deferred `badgesmith perf baseline` command will consume the local-performance app
as its infrastructure boundary when that command is implemented. See the
[BadgeSmith CDK app guide](build/BadgeSmith.CDK/README.md) for the exact build and safe
synthesis commands for each app.

### **Local Development**

**`.NET Aspire`** provides local development experience:

- **LocalStack integration**: AWS service emulation
- **Lambda emulation**: Local function execution
- **Contract tests**: Aspire Testing starts `src/BadgeSmith.Host` and calls `APIGatewayEmulator` over HTTP; the test suite does not use Lambda RIE.

**Local benchmark execution** uses Docker, LocalStack, CDK, and k6. Production keeps API Gateway HTTP v2, but the local performance stack exposes a Lambda Function URL fallback because LocalStack Community 4.6 does not deploy API Gateway v2 CloudFormation resources in this workflow.

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
- **Production optimization**: Conditional compilation flags

## 📈 **Performance Characteristics**

### **Benchmarks**

| Metric | BadgeSmith | Traditional .NET |
|--------|------------|------------------|
| **Cold Start** | ~50-100ms | ~500ms+ |
| **Memory Usage** | ~50MB | ~128MB+ |
| **Package Size** | ~6MB zipped | ~50MB+ |

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

### **Hosted Validation**

Eligible pull requests run the Release build, the full test suite, and the hosted ARM64
Lambda ZIP build. Live test-result publication is intentionally narrower:

- **Pull requests**: Build, tests, and ARM64 artifact validation without production
  mutation
- **Master pushes**: The same checks plus a best-effort authenticated badge update
- **Production CDK synth/deploy**: Separate approval-gated deployment workflow, not part
  of the ordinary PR CI pipeline

---

For detailed implementation examples and API documentation, see the main [README](README.md).
