# 🏗️ Phase 1: Foundation Implementation

> **Last Updated**: August 20, 2025

## 🎯 Phase Overview

**Duration**: Weeks 1-2
**Objective**: Establish the core infrastructure foundation with .NET Aspire development environment, AWS CDK infrastructure stack, Lambda emulation, and critical security foundations.

## 🏛️ Architecture Approach

Phase 1 implements a **hybrid development approach** combining:

- **AWS CDK .NET** for infrastructure (DynamoDB, Secrets Manager, IAM)
- **AWS Aspire Lambda Integration** for local Lambda development
- **LocalStack** for AWS service emulation
- **Security-first design** with HMAC replay protection
- **Native AOT validation** from day one
- **Shared infrastructure patterns** for future production deployment

## 📦 Project Structure Setup

### Core Projects

```
src/
├── BadgeSmith.Api/                 # Lambda function project
│   ├── Function.cs                 # Lambda entry point
│   ├── Routing/                    # Lightweight routing system
│   │   ├── Router.cs              # Route matching and dispatch
│   │   └── RoutePatterns.cs       # Source-generated regex patterns
│   ├── Models/
│   │   ├── Responses/             # API response DTOs
│   │   ├── Requests/              # API request DTOs
│   │   └── Internal/              # Internal data models
│   ├── Services/                   # Business logic interfaces
│   ├── Infrastructure/             # External integrations
│   ├── Extensions/                 # Service registration
│   └── Json/                      # AOT-compatible serialization
│
├── BadgeSmith.Host/               # Aspire development host
│   ├── Program.cs                 # Aspire AppHost configuration
│   ├── BadgeSmithInfrastructureStack.cs  # CDK infrastructure
│   ├── appsettings.json          # Production configuration
│   └── appsettings.Development.json      # LocalStack configuration
│
└── BadgeSmith.Shared/             # Shared infrastructure components
    └── InfrastructureComponents.cs # Reusable CDK constructs
```

## 🔧 Implementation Steps

### Step 1: BadgeSmith.Host Project Setup

Create the Aspire host project with AWS CDK integration:

#### Project File Configuration

```xml
<!-- BadgeSmith.Host/BadgeSmith.Host.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" />
    <PackageReference Include="Aspire.Hosting.AWS" />
    <PackageReference Include="Amazon.CDK.Lib" />
    <PackageReference Include="Amazon.CDK.AWS.DynamoDB" />
    <PackageReference Include="Amazon.CDK.AWS.IAM" />
    <PackageReference Include="Amazon.CDK.AWS.SecretsManager" />
    <PackageReference Include="Amazon.CDK.AWS.Lambda" />
    <PackageReference Include="Amazon.CDK.AWS.APIGateway" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BadgeSmith.Api\BadgeSmith.Api.csproj" />
  </ItemGroup>
</Project>
```

#### Aspire AppHost Configuration

```csharp
// BadgeSmith.Host/Program.cs
#pragma warning disable CA2252 // Preview features for AWS Lambda integration

using Amazon;
using Aspire.Hosting.AWS;

var builder = DistributedApplication.CreateBuilder(args);

// AWS SDK Configuration for EU-Central-1
var awsConfig = builder.AddAWSSDKConfig()
    .WithRegion(RegionEndpoint.EUCentral1);

// LocalStack configuration for local development
var localstack = builder.AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
{
    container.Lifetime = ContainerLifetime.Session;
    container.DebugLevel = 1;
    container.LogLevel = LocalStackLogLevel.Debug;
    container.WithEnvironment("SERVICES", "dynamodb,secretsmanager,iam");
    container.WithEnvironment("DEBUG", "1");
});

// Custom CDK Stack for infrastructure
var infrastructure = builder.AddAWSCDKStack<BadgeSmithInfrastructureStack>("BadgeSmithInfra")
    .WithReference(awsConfig);

// AWS Aspire Lambda emulation
var badgeSmithApi = builder.AddAWSLambdaFunction<Projects.BadgeSmith_Api>(
    "BadgeSmithApi",
    "BadgeSmith.Api")
    .WithReference(infrastructure);

// API Gateway emulator with CORS configuration
builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(badgeSmithApi, Method.Any, "/{proxy+}")
    .WithEnvironment("CORS_ALLOWED_ORIGINS", "*")
    .WithEnvironment("CORS_ALLOWED_METHODS", "GET,HEAD,OPTIONS,POST")
    .WithEnvironment("CORS_ALLOWED_HEADERS", "*");

builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);
```

#### Configuration Files

```json
// BadgeSmith.Host/appsettings.json (Production)
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AWS": {
    "UseLocalStack": false,
    "Region": "eu-central-1"
  }
}
```

```json
// BadgeSmith.Host/appsettings.Development.json (LocalStack)
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  },
  "AWS": {
    "UseLocalStack": true,
    "LocalStackUrl": "http://localhost:4566",
    "Region": "eu-central-1"
  }
}
```

### Step 2: Custom CDK Infrastructure Stack

```csharp
// BadgeSmith.Host/BadgeSmithInfrastructureStack.cs
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.SecretsManager;
using Constructs;

namespace BadgeSmith.Host;

public class BadgeSmithInfrastructureStack : Stack
{
    public BadgeSmithInfrastructureStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // DynamoDB Tables with optimized access patterns
        var secretsTable = CreateSecretsTable();
        var testResultsTable = CreateTestResultsTable();
        var nonceTable = CreateNonceTable();

        // IAM Roles with principle of least privilege
        var lambdaRole = CreateLambdaExecutionRole(secretsTable, testResultsTable, nonceTable);

        // Initial secrets for development
        CreateInitialSecrets();

        // Exports for Lambda function environment variables
        ExportResourceReferences(secretsTable, testResultsTable, nonceTable, lambdaRole);
    }

    private Table CreateSecretsTable()
    {
        return new Table(this, "BadgeSecretsTable", new TableProps
        {
            TableName = "badge-secrets",
            BillingMode = BillingMode.PAY_PER_REQUEST,
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "PK",
                Type = AttributeType.STRING
            },
            SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "SK",
                Type = AttributeType.STRING
            },
            TimeToLiveAttribute = "TTL",
            RemovalPolicy = RemovalPolicy.DESTROY // Development only
        });
    }

    private Table CreateTestResultsTable()
    {
        var table = new Table(this, "BadgeTestResultsTable", new TableProps
        {
            TableName = "badge-test-results",
            BillingMode = BillingMode.PAY_PER_REQUEST,
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "PK",
                Type = AttributeType.STRING
            },
            SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "SK",
                Type = AttributeType.STRING
            },
            TimeToLiveAttribute = "TTL",
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // GSI for efficient latest result queries
        table.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
        {
            IndexName = "GSI1",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "GSI1PK",
                Type = AttributeType.STRING
            },
            SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "GSI1SK",
                Type = AttributeType.STRING
            },
            ProjectionType = ProjectionType.ALL
        });

        return table;
    }

    private Table CreateNonceTable()
    {
        return new Table(this, "HmacNonceTable", new TableProps
        {
            TableName = "hmac-nonce",
            BillingMode = BillingMode.PAY_PER_REQUEST,
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "PK",
                Type = AttributeType.STRING
            },
            SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "SK",
                Type = AttributeType.STRING
            },
            TimeToLiveAttribute = "TTL",
            RemovalPolicy = RemovalPolicy.DESTROY
        });
    }

    private Role CreateLambdaExecutionRole(Table secretsTable, Table testResultsTable, Table nonceTable)
    {
        var role = new Role(this, "BadgeSmithLambdaRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            }
        });

        // DynamoDB permissions (least privilege)
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:Query", "dynamodb:UpdateItem" },
            Resources = new[]
            {
                secretsTable.TableArn,
                testResultsTable.TableArn,
                $"{testResultsTable.TableArn}/*", // For GSI access
                nonceTable.TableArn
            }
        }));

        // Secrets Manager permissions
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "secretsmanager:GetSecretValue" },
            Resources = new[] { $"arn:aws:secretsmanager:{Region}:{Account}:secret:badge/*" }
        }));

        return role;
    }

    private void CreateInitialSecrets()
    {
        // Example repository secret for development
        new Secret(this, "ExampleRepoSecret", new SecretProps
        {
            SecretName = "badge/repo/localstack-dotnet/badge-smith",
            Description = "HMAC key for localstack-dotnet/badge-smith repository",
            SecretStringValue = SecretValue.UnsafePlainText("""
                {
                  "hmac_key": "dGVzdC1obWFjLWtleS1mb3ItZGV2ZWxvcG1lbnQ=",
                  "type": "repo_hmac"
                }
                """)
        });

        // Example GitHub token for development
        new Secret(this, "ExampleGitHubToken", new SecretProps
        {
            SecretName = "badge/github/localstack-dotnet",
            Description = "GitHub PAT for localstack-dotnet organization",
            SecretStringValue = SecretValue.UnsafePlainText("""
                {
                  "token": "ghp_example_token_for_development",
                  "type": "github_pat"
                }
                """)
        });
    }

    private void ExportResourceReferences(Table secretsTable, Table testResultsTable, Table nonceTable, Role lambdaRole)
    {
        new CfnOutput(this, "SecretsTableName", new CfnOutputProps
        {
            Value = secretsTable.TableName,
            ExportName = "BadgeSmith-SecretsTable"
        });

        new CfnOutput(this, "TestResultsTableName", new CfnOutputProps
        {
            Value = testResultsTable.TableName,
            ExportName = "BadgeSmith-TestResultsTable"
        });

        new CfnOutput(this, "NonceTableName", new CfnOutputProps
        {
            Value = nonceTable.TableName,
            ExportName = "BadgeSmith-NonceTable"
        });

        new CfnOutput(this, "LambdaRoleArn", new CfnOutputProps
        {
            Value = lambdaRole.RoleArn,
            ExportName = "BadgeSmith-LambdaRole"
        });
    }
}
```

### Step 3: BadgeSmith.Api Lambda Project

#### Project Configuration

```xml
<!-- BadgeSmith.Api/BadgeSmith.Api.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    <AWSProjectType>Lambda</AWSProjectType>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <PublishReadyToRun>true</PublishReadyToRun>

    <!-- Native AOT Configuration -->
    <PublishAot Condition="'$(Configuration)' == 'Release'">true</PublishAot>
    <InvariantGlobalization Condition="'$(Configuration)' == 'Release'">true</InvariantGlobalization>
    <TrimMode Condition="'$(Configuration)' == 'Release'">full</TrimMode>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" />
    <PackageReference Include="Amazon.Lambda.APIGatewayEvents" />
    <PackageReference Include="Amazon.Lambda.RuntimeSupport" />
    <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" />
    <PackageReference Include="AWSSDK.DynamoDBv2" />
    <PackageReference Include="AWSSDK.SecretsManager" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.Extensions.Logging" />
    <PackageReference Include="Microsoft.Extensions.Caching.Memory" />
  </ItemGroup>

  <ItemGroup>
    <None Include="aws-lambda-tools-defaults.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

#### Lambda Entry Point

```csharp
// BadgeSmith.Api/Function.cs
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Extensions;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializerContext>))]

namespace BadgeSmith.Api;

public class Function
{
    private static readonly ServiceProvider ServiceProvider = CreateServiceProvider();

    /// <summary>
    /// Lambda function handler for API Gateway HTTP API v2 requests
    /// </summary>
    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        var logger = ServiceProvider.GetRequiredService<ILogger<Function>>();

        try
        {
            logger.LogInformation("Processing request: {Method} {Path}", request.RequestContext.Http.Method, request.RequestContext.Http.Path);

            return await Router.HandleAsync(request, context, ServiceProvider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in Lambda function");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 500,
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json",
                    ["Access-Control-Allow-Origin"] = "*"
                },
                Body = """{"message": "Internal server error"}"""
            };
        }
    }

    /// <summary>
    /// Main entry point for Native AOT
    /// </summary>
    private static async Task Main()
    {
        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper(FunctionHandler);
        using var bootstrap = new LambdaBootstrap(handlerWrapper);
        await bootstrap.RunAsync();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information);
        });

        // Register application services
        services.AddBadgeSmithServices();

        return services.BuildServiceProvider();
    }
}
```

#### Service Registration

```csharp
// BadgeSmith.Api/Extensions/ServiceCollectionExtensions.cs
using BadgeSmith.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;

namespace BadgeSmith.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBadgeSmithServices(this IServiceCollection services)
    {
        // Memory cache for performance
        services.AddMemoryCache();

        // Core services
        services.AddSingleton<IHealthService, HealthService>();

        // HTTP clients for external APIs
        services.AddHttpClient();

        return services;
    }
}
```

### Step 4: Lightweight Routing System

#### Route Patterns with Source Generators

```csharp
// BadgeSmith.Api/Routing/RoutePatterns.cs
using System.Text.RegularExpressions;

namespace BadgeSmith.Api.Routing;

public static partial class RoutePatterns
{
    [GeneratedRegex(@"^/health$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex HealthCheck();

    [GeneratedRegex(@"^/badges/packages/(?<provider>nuget|github)/(?:(?<org>[^/]+)/)?(?<package>[^/]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex PackageBadge();

    [GeneratedRegex(@"^/badges/tests/(?<platform>linux|windows|macos)/(?<owner>[^/]+)/(?<repo>[^/]+)/(?<branch>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex TestBadge();

    [GeneratedRegex(@"^/tests/results$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex TestIngestion();

    [GeneratedRegex(@"^/redirect/test-results/(?<platform>linux|windows|macos)/(?<owner>[^/]+)/(?<repo>[^/]+)/(?<branch>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex TestRedirect();
}
```

#### Router Implementation

```csharp
// BadgeSmith.Api/Routing/Router.cs
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Models.Responses;
using BadgeSmith.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BadgeSmith.Api.Routing;

public static class Router
{
    private static readonly Dictionary<string, string> CorsHeaders = new()
    {
        ["Access-Control-Allow-Origin"] = "*",
        ["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS, POST",
        ["Access-Control-Allow-Headers"] = "Content-Type, X-Signature, X-Repo-Secret, X-Timestamp, X-Nonce"
    };

    public static async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context,
        IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<Router>>();
        var path = request.RequestContext.Http.Path;
        var method = request.RequestContext.Http.Method;

        logger.LogDebug("Routing request: {Method} {Path}", method, path);

        // Handle CORS preflight requests
        if (method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            return CreateResponse(200, string.Empty);
        }

        try
        {
            // Health check endpoint
            if (RoutePatterns.HealthCheck().IsMatch(path) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                var healthService = services.GetRequiredService<IHealthService>();
                var health = await healthService.GetHealthAsync();
                return CreateJsonResponse(200, health);
            }

            // Package badge endpoint (Phase 2)
            if (RoutePatterns.PackageBadge().IsMatch(path) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Implement in Phase 2
                return CreateJsonResponse(501, new { message = "Package badges not yet implemented" });
            }

            // Test badge endpoint (Phase 4)
            if (RoutePatterns.TestBadge().IsMatch(path) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Implement in Phase 4
                return CreateJsonResponse(501, new { message = "Test badges not yet implemented" });
            }

            // Test ingestion endpoint (Phase 4)
            if (RoutePatterns.TestIngestion().IsMatch(path) && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Implement in Phase 4
                return CreateJsonResponse(501, new { message = "Test ingestion not yet implemented" });
            }

            // Test redirect endpoint (Phase 4)
            if (RoutePatterns.TestRedirect().IsMatch(path) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Implement in Phase 4
                return CreateJsonResponse(501, new { message = "Test redirects not yet implemented" });
            }

            // No route matched
            logger.LogWarning("No route matched for: {Method} {Path}", method, path);
            return CreateJsonResponse(404, new ErrorResponse { Message = "Endpoint not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing route: {Method} {Path}", method, path);
            return CreateJsonResponse(500, new ErrorResponse { Message = "Internal server error" });
        }
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateResponse(int statusCode, string body)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = statusCode,
            Headers = CorsHeaders,
            Body = body
        };
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateJsonResponse<T>(int statusCode, T data)
    {
        var headers = new Dictionary<string, string>(CorsHeaders)
        {
            ["Content-Type"] = "application/json"
        };

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = statusCode,
            Headers = headers,
            Body = JsonSerializer.Serialize(data, JsonSerializerOptions.Web)
        };
    }
}
```

### Step 5: Basic Response Models

```csharp
// BadgeSmith.Api/Models/Responses/HealthResponse.cs
namespace BadgeSmith.Api.Models.Responses;

public record HealthResponse
{
    public string Status { get; init; } = "ok";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Version { get; init; } = "1.0.0-phase1";
    public Dictionary<string, object> Details { get; init; } = new();
}
```

```csharp
// BadgeSmith.Api/Models/Responses/ErrorResponse.cs
namespace BadgeSmith.Api.Models.Responses;

public record ErrorResponse
{
    public string Message { get; init; } = "";
    public List<ErrorDetail> ErrorDetails { get; init; } = new();
}

public record ErrorDetail
{
    public string ErrorCode { get; init; } = "";
    public string PropertyName { get; init; } = "";
}
```

```csharp
// BadgeSmith.Api/Models/Responses/ShieldsBadgeResponse.cs
namespace BadgeSmith.Api.Models.Responses;

public record ShieldsBadgeResponse
{
    public int SchemaVersion { get; init; } = 1;
    public string Label { get; init; } = "";
    public string Message { get; init; } = "";
    public string Color { get; init; } = "blue";
}
```

### Step 6: Health Service Implementation

```csharp
// BadgeSmith.Api/Services/IHealthService.cs
using BadgeSmith.Api.Models.Responses;

namespace BadgeSmith.Api.Services;

public interface IHealthService
{
    Task<HealthResponse> GetHealthAsync();
}
```

```csharp
// BadgeSmith.Api/Services/HealthService.cs
using BadgeSmith.Api.Models.Responses;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Services;

public class HealthService : IHealthService
{
    private readonly ILogger<HealthService> _logger;

    public HealthService(ILogger<HealthService> logger)
    {
        _logger = logger;
    }

    public async Task<HealthResponse> GetHealthAsync()
    {
        _logger.LogDebug("Health check requested");

        var response = new HealthResponse
        {
            Status = "ok",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0-phase1",
            Details = new Dictionary<string, object>
            {
                ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                ["dotnet_version"] = Environment.Version.ToString(),
                ["process_id"] = Environment.ProcessId
            }
        };

        return await Task.FromResult(response);
    }
}
```

### Step 7: JSON Serialization for AOT

```csharp
// BadgeSmith.Api/Json/LambdaFunctionJsonSerializerContext.cs
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Models.Responses;
using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Json;

[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ShieldsBadgeResponse))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
}
```

## ✅ Success Criteria

### Infrastructure Success Criteria

- ✅ **BadgeSmith.Host project setup** with Aspire + AWS CDK integration
- ✅ **Custom CDK stack** deploys DynamoDB tables with nonce storage to LocalStack
- ✅ **AWS Lambda function** runs via Aspire emulator with LocalStack
- ✅ **API Gateway emulator** routes requests to Lambda function
- ✅ **LocalStack + Aspire + AWS Lambda integration** works seamlessly
- ✅ **CDK stack can be deployed to real AWS** (for future production)

### Development Environment Success Criteria

- ✅ **Native AOT compilation** succeeds without reflection issues
- ✅ **Health endpoint** returns 200 OK via emulated API Gateway
- ✅ **Invalid routes** return proper 404 errors with consistent error schema
- ✅ **Basic error handling** works across the stack
- ✅ **CORS headers** are correctly applied to all responses

### Code Quality Success Criteria

- ✅ **Zero warnings policy** enforced at build time
- ✅ **Source-generated regex patterns** compile successfully
- ✅ **JSON serialization context** supports all required types
- ✅ **Dependency injection** container configured correctly
- ✅ **Logging** provides useful debugging information

## 🧪 Testing the Foundation

### Manual Testing Steps

1. **Start the development environment**:

   ```powershell
   cd src/BadgeSmith.Host
   dotnet run
   ```

2. **Verify LocalStack services**:

   ```powershell
   # Check if DynamoDB tables are created
   aws --endpoint-url=http://localhost:4566 dynamodb list-tables

   # Check if secrets are created
   aws --endpoint-url=http://localhost:4566 secretsmanager list-secrets
   ```

3. **Test health endpoint**:

   ```powershell
   curl http://localhost:5000/health
   ```

4. **Test 404 handling**:

   ```powershell
   curl http://localhost:5000/nonexistent
   ```

5. **Test CORS preflight**:

   ```powershell
   curl -X OPTIONS http://localhost:5000/health
   ```

### Expected Responses

**Health Check Response**:

```json
{
  "status": "ok",
  "timestamp": "2025-08-20T10:30:00.000Z",
  "version": "1.0.0-phase1",
  "details": {
    "environment": "Development",
    "dotnet_version": "8.0.0",
    "process_id": 1234
  }
}
```

**404 Error Response**:

```json
{
  "message": "Endpoint not found",
  "errorDetails": []
}
```

## 🔄 Next Steps

After Phase 1 completion, proceed to:

- **[Phase 2: Package Endpoints](../03-implementation/Phase-2-package-endpoints.md)** - Implement package badge functionality
- **[Phase 3: Response Formatting](../03-implementation/Phase-3-response-formatting.md)** - Enhanced Shields.io responses and caching
- **[Phase 4: Authentication](../03-implementation/Phase-4-authentication.md)** - HMAC authentication and test result ingestion

## 🔗 Related Documentation

- **[System Architecture](../02-architecture/01-system-architecture.md)** - Technical design and component relationships
- **[Security Design](../02-architecture/03-security-design.md)** - Security patterns and authentication
- **[Requirements](../01-foundation/02-requirements.md)** - Phase 1 success criteria and requirements
- **[Project Overview](../01-foundation/01-project-overview.md)** - High-level project vision
