# BadgeSmith

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![AWS Lambda](https://img.shields.io/badge/AWS-Lambda-orange.svg)](https://aws.amazon.com/lambda/)
[![Native AOT](https://img.shields.io/badge/Native-AOT-blue.svg)](https://docs.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

> **Badge service** for .NET packages and CI/CD test results with secure authentication and performance optimizations.

**BadgeSmith** is a general-purpose, high-performance badge service that provides [Shields.io](https://shields.io)-compatible endpoints for **NuGet packages**, **GitHub packages**, and **CI/CD test results**. Built with .NET 10 Native AOT for sub-100ms cold starts and designed for extensibility.

**Successor to [localstack-nuget-badge-lambda](https://github.com/localstack-dotnet/localstack-nuget-badge-lambda)** with 5-10x performance improvements and security features.

## 🚀 **Live Examples**

### **LocalStack.NET Client Examples**

📦 **LocalStack.NET Client v1.x**
[![NuGet v1.x](https://img.shields.io/endpoint?url=https%3A%2F%2Fapi.localstackfor.net%2Fbadges%2Fpackages%2Fnuget%2FLocalStack.Client%3Fversion%3D(1.0%2C2.0))](https://www.nuget.org/packages/LocalStack.Client/)

📦 **LocalStack.NET Client v2.x**
[![NuGet v2.x](https://img.shields.io/endpoint?url=https%3A%2F%2Fapi.localstackfor.net%2Fbadges%2Fpackages%2Fnuget%2FLocalStack.Client)](https://www.nuget.org/packages/LocalStack.Client/)

## ✨ **Features**

### **🔒 Secure Authentication**

- **Canonical HMAC-SHA256 authentication** binds the method, logical ingestion route,
  timestamp, nonce, and exact request body
- **Organization-scoped `TestData` secrets** are isolated from package credentials
- **Timestamp validation** accepts requests up to five minutes old with at most one
  minute of future clock skew
- **Nonce-based replay prevention** atomically marks the nonce only after fixed-time
  signature verification succeeds

Canonical request construction is a hard-cut contract. See
**[ARCHITECTURE.md](ARCHITECTURE.md#canonical-hmac-authentication)** for the exact field
order, normalization, escaping, and signature envelope.

### **⚡ Performance Optimizations**

- **Native AOT compilation** for ~50-100ms cold starts (vs 500ms+ traditional)
- **DynamoDB with GSI** for efficient latest-result queries
- **Caching** with ETag support and configurable TTL
- **CloudFront-compatible** with proper cache headers

### **🎯 Flexible Design**

- **Multi-provider support**: NuGet.org, GitHub Packages (extensible)
- **Version filtering**: NuGet VersionRange support (`>=1.0.0`, `[6.0,8.0)`)
- **Platform-specific test badges**: Linux, Windows, macOS
- **Branch-aware**: Handles complex branch names with URL encoding

## 🌐 **API Endpoints**

### **Package Badges**

```bash
# NuGet packages
GET /badges/packages/nuget/{package}[?version={range}&prerelease={bool}]

# GitHub packages
GET /badges/packages/github/{org}/{package}[?version={pattern}&prerelease={bool}]
```

### **Test Result Badges**

```bash
# Display test badge
GET /badges/tests/{platform}/{owner}/{repo}/{branch}

# Test result ingestion (HMAC authenticated)
POST /tests/results/{platform}/{owner}/{repo}/{branch}

# Redirect to test results
GET /redirect/test-results/{platform}/{owner}/{repo}/{branch}
```

### **Examples**

```bash
# NuGet package badge
https://api.localstackfor.net/badges/packages/nuget/Newtonsoft.Json

# GitHub package with version filtering
https://api.localstackfor.net/badges/packages/github/localstack-dotnet/localstack.client?version=(1.0,2.0)
```

## 🏗️ **Architecture**

BadgeSmith is organized with feature-based architecture, optimized for AWS Lambda performance.

**Request Flow:**

```
Client → CloudFront → API Gateway → Lambda → DynamoDB/Secrets Manager
```

**Key Technologies:**

- **.NET 10 Native AOT** - Sub-100ms cold starts
- **AWS Lambda** - Serverless compute
- **DynamoDB** - NoSQL storage with GSI optimization
- **Custom routing** - High-performance request handling

For detailed architectural decisions, performance considerations, data design, and deployment strategies, see **[ARCHITECTURE.md](ARCHITECTURE.md)**.

## 🚀 **Quick Start**

### **Using the Public API**

```markdown
<!-- Add to your README.md -->
![NuGet](https://img.shields.io/endpoint?url=https://api.localstackfor.net/badges/packages/nuget/YourPackage)
![Tests](https://img.shields.io/endpoint?url=https://api.localstackfor.net/badges/tests/linux/your-org/your-repo/main)
```

### **Self-Hosting**

```bash
# Clone and compile the production CDK app (does not deploy)
git clone https://github.com/localstack-dotnet/badge-smith.git
cd badge-smith
dotnet build build/BadgeSmith.CDK/BadgeSmith.CDK.csproj -c Release
```

BadgeSmith has separate production and LocalStack-only performance CDK apps. Production
CDK commands run from `build` and target `BadgeSmithStack`; local-performance commands
run from `build/BadgeSmith.CDK.LocalPerformance` and target
`BadgeSmithPerformanceStack`. See the [CDK app guide](build/BadgeSmith.CDK/README.md)
for the required Lambda artifacts and safe synthesis commands. Production deployment is
approval-gated and must never use `--all`; the local-performance app is not deployed to
AWS.

### **Local Development**

```bash
# Start with .NET Aspire + LocalStack
aspire start --apphost src/BadgeSmith.Host/BadgeSmith.Host.csproj --non-interactive
```

### **Tooling**

The `badgesmith` file-based CLI (`tools/badgesmith.cs`) owns Lambda builds,
test runs, test-result ingestion, badge updates, and secret seeding. See
[`tools/README.md`](tools/README.md) for the full command reference.

```bash
# Local AOT/LocalStack validation
./tools/badgesmith.cs lambda build --target zip --rid linux-x64 --clean

# Production artifact; requires an ARM64-capable builder and is validated in hosted CI
./tools/badgesmith.cs lambda build --target zip --rid linux-arm64 --clean
```

## 🔄 **CI/CD Integration**

### **GitHub Actions**

The remotely reusable badge action posts test results to a BadgeSmith deployment. See the
[action guide](.github/workflows/update-test-badge/README.md) for the canonical input
list and the immutable reviewed commit used by the current example.

The repository-local `run-dotnet-tests` action is an internal BadgeSmith workflow
helper, not a portable test-runner contract.

## 🏢 **LocalStack.NET Organization**

While designed as a **white-label solution**, BadgeSmith was created to serve the [LocalStack.NET organization](https://github.com/localstack-dotnet) badge requirements:

- **NuGet package badges** for LocalStack.NET client libraries, two track support (v1.x and v2.x)
- **GitHub package badges** for given repository, including pre-release versions
- **Multi-repository test badges** with platform-specific results
- **Secure test result ingestion** from CI/CD workflows

## 🌟 **Modern .NET Development Showcase**

BadgeSmith demonstrates current .NET development practices:

### **[.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) Integration**

- Local development orchestration with service discovery
- Observability with structured logging
- Shared infrastructure between dev/prod environments

### **[AWS Aspire Integrations](https://github.com/aws/integrations-on-dotnet-aspire-for-aws)**

- AWS Lambda and API Gateway emulation for local development
- CDK stack provisioning from Aspire host

### **[LocalStack Aspire Integration](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack)**

- AWS service emulation for local development
- Identical schemas between local and production
- Local testing capabilities

### **Reusable CDK Patterns**

- Separate production and LocalStack-only performance app entrypoints
- Native stack selection with `BadgeSmithStack` and `BadgeSmithPerformanceStack`
- Shared constructs across the two app boundaries
- Type-safe infrastructure with .NET CDK

## 🤝 **Contributing**

Contributions are welcome! The codebase includes:

- Static analysis with multiple analyzers
- Zero warnings policy for code quality
- Native AOT compatibility throughout

## 📄 **License**

MIT License - see [LICENSE](LICENSE) file for details.

---

**Built with ❤️ by the [LocalStack.NET](https://github.com/localstack-dotnet) organization**
