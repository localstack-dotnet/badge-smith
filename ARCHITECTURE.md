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
- **Lambda Function**: .NET 9 Native AOT runtime
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

Controlled via build arguments in `Dockerfile` and `build-lambda.sh` scripts.

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

The **`BadgeSmith.DynamoDb.Seeders`** project provides:

- **Local development setup**: Seeds test data for LocalStack
- **Production deployment**: Can seed real AWS resources (with appropriate credentials)
- **Configuration-driven**: JSON-based organization and secret management
- **Idempotent operations**: Safe to run multiple times

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

### **HMAC Authentication Flow**

**For test result ingestion endpoints:**

1. **Organization Lookup**: Extract organization from route parameters
2. **Secret Retrieval**: Query organization secrets from DynamoDB → Secrets Manager
3. **Signature Validation**: HMAC-SHA256 verification with constant-time comparison
4. **Replay Protection**: Nonce validation with DynamoDB conditional writes
5. **Timestamp Validation**: 5-minute window with clock skew protection

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

### **Scripts Directory**

**`scripts/`** contains development and testing tooling:

- **`build-lambda.sh/.ps1`**: Multi-platform Docker builds for Lambda deployment
- **`test-ingestion.sh/.ps1`**: HMAC authentication testing with real API calls
- **`k6-perf-test.js`**: Load testing with realistic traffic patterns
- **`sample-test-payload.json`**: Example test result payload

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

**`build/`** directory contains **CDK infrastructure**:

- **Shared constructs**: Common infrastructure patterns
- **Environment-agnostic**: Same code for local and production
- **Type-safe**: .NET CDK with compile-time validation
- **Aspire integration**: CDK stacks can be deployed from Aspire host

### **Local Development**

**`.NET Aspire`** provides local development experience:

- **LocalStack integration**: AWS service emulation
- **Lambda emulation**: Local function execution

## 🚀 **Deployment Strategy**

### **Multi-Stage Docker Build**

**`Dockerfile`** implements **optimized multi-stage builds**:

1. **Build stage**: .NET SDK with Native AOT compilation
2. **Lambda image**: Minimal runtime for container deployment
3. **Zip export**: Artifact generation for .zip deployment

### **Build Scripts**

**`build-lambda.sh/.ps1`** provide **cross-platform build automation**:

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

### **Reusable Workflows**

**`.github/workflows/`** contains **reusable GitHub Actions**:

- **`run-dotnet-tests/`**: Multi-framework test execution
- **`update-test-badge/`**: HMAC-authenticated badge updates
- **Cross-platform support**: Windows, Linux, macOS

### **Self-Hosting Validation**

BadgeSmith **validates itself** through CI/CD integration:

- **Real authentication**: HMAC signatures generated and validated
- **Live API calls**: Test results posted to production API
- **End-to-end verification**: Complete pipeline tested on every commit

---

For detailed implementation examples and API documentation, see the main [README](README.md).
