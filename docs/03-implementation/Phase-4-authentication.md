# 🔐 Phase 4: Authentication & Test Results

> **Last Updated**: August 20, 2025

## 🎯 Phase Overview

**Duration**: Week 5-6
**Objective**: Implement HMAC authentication system and test result ingestion endpoints with DynamoDB integration. This phase builds upon the response formatting foundation to add secure test result functionality with proper authentication and data persistence.

## 🏛️ Architecture Approach

Phase 4 implements **secure test result management** with:

- **HMAC-SHA256 authentication** with replay protection using nonces
- **DynamoDB transaction-based idempotency** preventing duplicate test result ingestion
- **Test result badge endpoints** with real-time status display
- **Secrets management integration** for secure HMAC key storage
- **Test result redirect endpoints** for GitHub README integration
- **Production-grade error handling** with detailed audit logging

## 📊 Endpoints to Implement

### Test Result Endpoints

```http
GET  /badges/tests/{platform}/{owner}/{repo}/{branch}
POST /tests/results
GET  /redirect/test-results/{platform}/{owner}/{repo}/{branch}
```

### Authentication Flow

All POST endpoints require HMAC authentication with these headers:

```http
X-Signature: sha256=<hmac-sha256-hex>
X-Repo-Secret: <repo-secret-identifier>
X-Timestamp: <iso8601-timestamp>
X-Nonce: <unique-request-nonce>
Content-Type: application/json
```

## 🔧 Implementation Steps

### Step 1: Enhanced CDK Infrastructure Stack

Extend the infrastructure to support test results and secrets:

```csharp
// Update BadgeSmith.Host/BadgeSmithInfrastructureStack.cs
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.SecretsManager;

namespace BadgeSmith.Host;

public class BadgeSmithInfrastructureStack : Stack
{
    public Table SecretsTable { get; private set; }
    public Table TestResultsTable { get; private set; }
    public Table NonceTable { get; private set; }
    public Role LambdaRole { get; private set; }

    public BadgeSmithInfrastructureStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // Create DynamoDB tables
        SecretsTable = CreateSecretsTable();
        TestResultsTable = CreateTestResultsTable();
        NonceTable = CreateNonceTable();

        // Create Lambda execution role with DynamoDB permissions
        LambdaRole = CreateLambdaRole();
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
            RemovalPolicy = RemovalPolicy.DESTROY // Only for development
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
        return new Table(this, "BadgeNonceTable", new TableProps
        {
            TableName = "badge-nonces",
            BillingMode = BillingMode.PAY_PER_REQUEST,
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "Nonce",
                Type = AttributeType.STRING
            },
            TimeToLiveAttribute = "ExpiresAt",
            RemovalPolicy = RemovalPolicy.DESTROY
        });
    }

    private Role CreateLambdaRole()
    {
        var role = new Role(this, "BadgeSmithLambdaRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            }
        });

        // DynamoDB permissions
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "dynamodb:GetItem",
                "dynamodb:PutItem",
                "dynamodb:UpdateItem",
                "dynamodb:DeleteItem",
                "dynamodb:Query",
                "dynamodb:Scan",
                "dynamodb:TransactWriteItems",
                "dynamodb:TransactGetItems"
            },
            Resources = new[]
            {
                SecretsTable.TableArn,
                TestResultsTable.TableArn,
                NonceTable.TableArn,
                $"{TestResultsTable.TableArn}/index/*"
            }
        }));

        // Secrets Manager permissions
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "secretsmanager:GetSecretValue"
            },
            Resources = new[]
            {
                $"arn:aws:secretsmanager:{Region}:{Account}:secret:badge-smith/*"
            }
        }));

        return role;
    }
}
```

### Step 2: HMAC Authentication Service

Implement secure HMAC authentication with replay protection:

```csharp
// BadgeSmith.Api/Services/HmacAuthService.cs
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using BadgeSmith.Api.Models.Internal;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BadgeSmith.Api.Services;

public interface IHmacAuthService
{
    Task<AuthResult> ValidateRequestAsync(
        string signature,
        string repoSecret,
        string timestamp,
        string nonce,
        string payload,
        CancellationToken cancellationToken = default);
}

public class HmacAuthService : IHmacAuthService
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly ILogger<HmacAuthService> _logger;
    private readonly string _nonceTableName;

    public HmacAuthService(
        IAmazonDynamoDB dynamoDb,
        IAmazonSecretsManager secretsManager,
        ILogger<HmacAuthService> logger)
    {
        _dynamoDb = dynamoDb;
        _secretsManager = secretsManager;
        _logger = logger;
        _nonceTableName = Environment.GetEnvironmentVariable("NONCE_TABLE") ?? "badge-nonces";
    }

    public async Task<AuthResult> ValidateRequestAsync(
        string signature,
        string repoSecret,
        string timestamp,
        string nonce,
        string payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate timestamp (5-minute window)
            if (!DateTime.TryParse(timestamp, out var requestTime))
            {
                _logger.LogWarning("Invalid timestamp format: {Timestamp}", timestamp);
                return AuthResult.Failure("Invalid timestamp format");
            }

            var timeDiff = Math.Abs((DateTime.UtcNow - requestTime).TotalMinutes);
            if (timeDiff > 5)
            {
                _logger.LogWarning("Request timestamp outside allowed window: {TimeDiff} minutes", timeDiff);
                return AuthResult.Failure("Request timestamp outside allowed window");
            }

            // 2. Check nonce hasn't been used (replay protection)
            var nonceExists = await CheckNonceExistsAsync(nonce, cancellationToken);
            if (nonceExists)
            {
                _logger.LogWarning("Nonce replay detected: {Nonce}", nonce);
                return AuthResult.Failure("Nonce already used");
            }

            // 3. Get secret from Secrets Manager
            var secretValue = await GetSecretAsync(repoSecret, cancellationToken);
            if (string.IsNullOrEmpty(secretValue))
            {
                _logger.LogWarning("Secret not found: {RepoSecret}", repoSecret);
                return AuthResult.Failure("Invalid repository secret");
            }

            // 4. Validate HMAC-SHA256 signature
            var expectedSignature = GenerateSignature(secretValue, payload, timestamp, nonce);
            if (!signature.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("HMAC signature mismatch for repo secret: {RepoSecret}", repoSecret);
                return AuthResult.Failure("Invalid signature");
            }

            // 5. Store nonce with 45-minute TTL for replay prevention
            await StoreNonceAsync(nonce, cancellationToken);

            _logger.LogInformation("HMAC authentication successful for repo secret: {RepoSecret}", repoSecret);
            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during HMAC authentication");
            return AuthResult.Failure("Authentication error");
        }
    }

    private async Task<bool> CheckNonceExistsAsync(string nonce, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _dynamoDb.GetItemAsync(new GetItemRequest
            {
                TableName = _nonceTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Nonce"] = new AttributeValue(nonce)
                }
            }, cancellationToken);

            return response.Item.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking nonce existence: {Nonce}", nonce);
            return true; // Fail safe - assume nonce exists to prevent replay
        }
    }

    private async Task StoreNonceAsync(string nonce, CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(45).ToUnixTimeSeconds();

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _nonceTableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["Nonce"] = new AttributeValue(nonce),
                ["ExpiresAt"] = new AttributeValue { N = expiresAt.ToString() },
                ["CreatedAt"] = new AttributeValue(DateTime.UtcNow.ToString("O"))
            }
        }, cancellationToken);
    }

    private async Task<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = $"badge-smith/{secretId}"
            }, cancellationToken);

            // Parse the secret value (could be JSON or plain text)
            if (response.SecretString.StartsWith('{'))
            {
                var secretObj = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);
                return secretObj?.GetValueOrDefault("secret");
            }

            return response.SecretString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret: {SecretId}", secretId);
            return null;
        }
    }

    private static string GenerateSignature(string secret, string payload, string timestamp, string nonce)
    {
        var data = $"{timestamp}:{nonce}:{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

public record AuthResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = "";

    public static AuthResult Success() => new() { IsSuccess = true };
    public static AuthResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
```

### Step 3: Test Result Models and Repository

Define test result data models and repository patterns:

```csharp
// BadgeSmith.Api/Models/Internal/TestResult.cs
namespace BadgeSmith.Api.Models.Internal;

public record TestResult
{
    public string Owner { get; init; } = "";
    public string Repo { get; init; } = "";
    public string Platform { get; init; } = "";
    public string Branch { get; init; } = "";
    public string RunId { get; init; } = "";
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public int Total => Passed + Failed + Skipped;
    public string RunUrl { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public TestStatus Status => CalculateStatus();

    private TestStatus CalculateStatus()
    {
        if (Failed > 0 && Passed > 0) return TestStatus.Mixed;
        if (Failed > 0) return TestStatus.Failed;
        if (Passed > 0) return TestStatus.Passed;
        return TestStatus.Unknown;
    }
}

public enum TestStatus
{
    Unknown,
    Passed,
    Failed,
    Mixed
}

// BadgeSmith.Api/Models/Requests/TestResultRequest.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Models.Requests;

public record TestResultRequest
{
    [Required]
    [JsonPropertyName("owner")]
    public string Owner { get; init; } = "";

    [Required]
    [JsonPropertyName("repo")]
    public string Repo { get; init; } = "";

    [Required]
    [JsonPropertyName("platform")]
    public string Platform { get; init; } = "";

    [Required]
    [JsonPropertyName("branch")]
    public string Branch { get; init; } = "";

    [Required]
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = "";

    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("passed")]
    public int Passed { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("failed")]
    public int Failed { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("skipped")]
    public int Skipped { get; init; }

    [Required]
    [JsonPropertyName("runUrl")]
    public string RunUrl { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }
}
```

### Step 4: Test Result Repository Service

Implement DynamoDB operations with transaction-based idempotency:

```csharp
// BadgeSmith.Api/Services/TestResultsRepository.cs
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BadgeSmith.Api.Models.Internal;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Services;

public interface ITestResultsRepository
{
    Task<TestResult?> GetLatestResultAsync(string owner, string repo, string platform, string branch, CancellationToken cancellationToken = default);
    Task<Result> SaveResultAsync(TestResult result, CancellationToken cancellationToken = default);
}

public class TestResultsRepository : ITestResultsRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<TestResultsRepository> _logger;
    private readonly string _tableName;

    public TestResultsRepository(IAmazonDynamoDB dynamoDb, ILogger<TestResultsRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _logger = logger;
        _tableName = Environment.GetEnvironmentVariable("TEST_RESULTS_TABLE") ?? "badge-test-results";
    }

    public async Task<TestResult?> GetLatestResultAsync(string owner, string repo, string platform, string branch, CancellationToken cancellationToken = default)
    {
        try
        {
            var gsi1Pk = $"LATEST#{owner}#{repo}#{platform}#{branch}";

            var response = await _dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                IndexName = "GSI1",
                KeyConditionExpression = "GSI1PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new AttributeValue(gsi1Pk)
                },
                ScanIndexForward = false, // Get latest first
                Limit = 1
            }, cancellationToken);

            if (!response.Items.Any())
            {
                _logger.LogDebug("No test results found for {Owner}/{Repo}/{Platform}/{Branch}", owner, repo, platform, branch);
                return null;
            }

            var item = response.Items[0];
            return MapFromDynamoDb(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest test result for {Owner}/{Repo}/{Platform}/{Branch}", owner, repo, platform, branch);
            return null;
        }
    }

    public async Task<Result> SaveResultAsync(TestResult result, CancellationToken cancellationToken = default)
    {
        try
        {
            var timestampEpochSeconds = ((DateTimeOffset)result.Timestamp).ToUnixTimeSeconds();

            // Transaction: RunSeen + TestResult write for idempotency
            var runSeenPk = $"RUN#{result.Owner}#{result.Repo}";
            var runSeenSk = result.RunId;

            var resultPk = $"TEST#{result.Owner}#{result.Repo}#{result.Platform}#{result.Branch}";
            var resultSk = $"{timestampEpochSeconds:D19}#{result.RunId}";

            var gsi1Pk = $"LATEST#{result.Owner}#{result.Repo}#{result.Platform}#{result.Branch}";
            var gsi1Sk = $"{timestampEpochSeconds:D19}";

            var transactRequest = new TransactWriteItemsRequest
            {
                TransactItems = new List<TransactWriteItem>
                {
                    // Check runId hasn't been processed
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _tableName,
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["PK"] = new AttributeValue(runSeenPk),
                                ["SK"] = new AttributeValue(runSeenSk),
                                ["Type"] = new AttributeValue("RunSeen"),
                                ["TTL"] = new AttributeValue { N = (DateTimeOffset.UtcNow.AddMinutes(45)).ToUnixTimeSeconds().ToString() }
                            },
                            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
                        }
                    },
                    // Store test result
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _tableName,
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["PK"] = new AttributeValue(resultPk),
                                ["SK"] = new AttributeValue(resultSk),
                                ["Type"] = new AttributeValue("TestResult"),
                                ["GSI1PK"] = new AttributeValue(gsi1Pk),
                                ["GSI1SK"] = new AttributeValue(gsi1Sk),
                                ["Owner"] = new AttributeValue(result.Owner),
                                ["Repo"] = new AttributeValue(result.Repo),
                                ["Platform"] = new AttributeValue(result.Platform),
                                ["Branch"] = new AttributeValue(result.Branch),
                                ["RunId"] = new AttributeValue(result.RunId),
                                ["Passed"] = new AttributeValue { N = result.Passed.ToString() },
                                ["Failed"] = new AttributeValue { N = result.Failed.ToString() },
                                ["Skipped"] = new AttributeValue { N = result.Skipped.ToString() },
                                ["RunUrl"] = new AttributeValue(result.RunUrl),
                                ["Timestamp"] = new AttributeValue { N = timestampEpochSeconds.ToString() }
                                // No TTL - keep test results indefinitely for historical importance
                            }
                        }
                    }
                }
            };

            await _dynamoDb.TransactWriteItemsAsync(transactRequest, cancellationToken);

            _logger.LogInformation("Successfully saved test result for {Owner}/{Repo}/{Platform}/{Branch} run {RunId}",
                result.Owner, result.Repo, result.Platform, result.Branch, result.RunId);

            return Result.Success();
        }
        catch (ConditionalCheckFailedException)
        {
            _logger.LogWarning("Test run {RunId} already processed for {Owner}/{Repo}", result.RunId, result.Owner, result.Repo);
            return Result.Failure($"Test run {result.RunId} already processed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving test result for {Owner}/{Repo}/{Platform}/{Branch}",
                result.Owner, result.Repo, result.Platform, result.Branch);
            return Result.Failure("Failed to save test result");
        }
    }

    private static TestResult MapFromDynamoDb(Dictionary<string, AttributeValue> item)
    {
        return new TestResult
        {
            Owner = item["Owner"].S,
            Repo = item["Repo"].S,
            Platform = item["Platform"].S,
            Branch = item["Branch"].S,
            RunId = item["RunId"].S,
            Passed = int.Parse(item["Passed"].N),
            Failed = int.Parse(item["Failed"].N),
            Skipped = int.Parse(item["Skipped"].N),
            RunUrl = item["RunUrl"].S,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(item["Timestamp"].N)).DateTime
        };
    }
}
```

### Step 5: Test Result Request Handlers

Implement the test result endpoints:

```csharp
// BadgeSmith.Api/Handlers/TestIngestionHandler.cs
using BadgeSmith.Api.Models.Internal;
using BadgeSmith.Api.Models.Requests;
using BadgeSmith.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BadgeSmith.Api.Handlers;

public static class TestIngestionHandler
{
    public static async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<TestIngestionHandler>>();
        var hmacAuthService = services.GetRequiredService<IHmacAuthService>();
        var testResultsRepo = services.GetRequiredService<ITestResultsRepository>();
        var responseBuilder = services.GetRequiredService<IHttpResponseBuilder>();

        try
        {
            // Extract HMAC headers
            var headers = request.Headers ?? new Dictionary<string, string>();

            if (!TryGetHmacHeaders(headers, out var signature, out var repoSecret, out var timestamp, out var nonce))
            {
                logger.LogWarning("Missing required HMAC authentication headers");
                return responseBuilder.CreateErrorResponse(401, "Missing authentication headers", request.RawPath);
            }

            // Validate HMAC authentication
            var authResult = await hmacAuthService.ValidateRequestAsync(signature, repoSecret, timestamp, nonce, request.Body ?? "");
            if (!authResult.IsSuccess)
            {
                logger.LogWarning("HMAC authentication failed: {Error}", authResult.ErrorMessage);
                return responseBuilder.CreateErrorResponse(401, authResult.ErrorMessage, request.RawPath);
            }

            // Parse and validate request body
            if (string.IsNullOrEmpty(request.Body))
            {
                logger.LogWarning("Empty request body for test result ingestion");
                return responseBuilder.CreateErrorResponse(400, "Request body is required", request.RawPath);
            }

            TestResultRequest? testRequest;
            try
            {
                testRequest = JsonSerializer.Deserialize<TestResultRequest>(request.Body, JsonSerializerOptions.Web);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Invalid JSON in test result request");
                return responseBuilder.CreateErrorResponse(400, "Invalid JSON format", request.RawPath);
            }

            if (testRequest == null)
            {
                return responseBuilder.CreateErrorResponse(400, "Invalid request format", request.RawPath);
            }

            // Validate request model
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(testRequest);
            if (!Validator.TryValidateObject(testRequest, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(vr => new ErrorDetail
                {
                    Field = vr.MemberNames.FirstOrDefault() ?? "",
                    Message = vr.ErrorMessage ?? "",
                    Code = "VALIDATION_ERROR"
                }).ToList();

                var errorResponse = new ErrorResponse
                {
                    Message = "Validation failed",
                    Details = errors,
                    Timestamp = DateTime.UtcNow,
                    Path = request.RawPath
                };

                return responseBuilder.CreateErrorResponse(400, errorResponse);
            }

            // Create test result
            var testResult = new TestResult
            {
                Owner = testRequest.Owner,
                Repo = testRequest.Repo,
                Platform = testRequest.Platform,
                Branch = testRequest.Branch,
                RunId = testRequest.RunId,
                Passed = testRequest.Passed,
                Failed = testRequest.Failed,
                Skipped = testRequest.Skipped,
                RunUrl = testRequest.RunUrl,
                Timestamp = testRequest.Timestamp ?? DateTime.UtcNow
            };

            // Save test result with idempotency
            var saveResult = await testResultsRepo.SaveResultAsync(testResult);
            if (!saveResult.IsSuccess)
            {
                logger.LogWarning("Failed to save test result: {Error}", saveResult.ErrorMessage);

                if (saveResult.ErrorMessage.Contains("already processed"))
                {
                    return responseBuilder.CreateErrorResponse(409, saveResult.ErrorMessage, request.RawPath);
                }

                return responseBuilder.CreateErrorResponse(500, "Failed to save test result", request.RawPath);
            }

            logger.LogInformation("Successfully ingested test result for {Owner}/{Repo}/{Platform}/{Branch} run {RunId}",
                testResult.Owner, testResult.Repo, testResult.Platform, testResult.Branch, testResult.RunId);

            // Return success response
            var successResponse = new
            {
                message = "Test result saved successfully",
                runId = testResult.RunId,
                timestamp = testResult.Timestamp.ToString("O")
            };

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 201,
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json",
                    ["Access-Control-Allow-Origin"] = "*"
                },
                Body = JsonSerializer.Serialize(successResponse, JsonSerializerOptions.Web)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing test result ingestion");
            return responseBuilder.CreateErrorResponse(500, "Internal server error", request.RawPath);
        }
    }

    private static bool TryGetHmacHeaders(
        Dictionary<string, string> headers,
        out string signature,
        out string repoSecret,
        out string timestamp,
        out string nonce)
    {
        signature = headers.GetValueOrDefault("X-Signature") ?? "";
        repoSecret = headers.GetValueOrDefault("X-Repo-Secret") ?? "";
        timestamp = headers.GetValueOrDefault("X-Timestamp") ?? "";
        nonce = headers.GetValueOrDefault("X-Nonce") ?? "";

        return !string.IsNullOrEmpty(signature) &&
               !string.IsNullOrEmpty(repoSecret) &&
               !string.IsNullOrEmpty(timestamp) &&
               !string.IsNullOrEmpty(nonce);
    }
}
```

### Step 6: Test Badge Handler

Create test badge endpoints:

```csharp
// BadgeSmith.Api/Handlers/TestBadgeHandler.cs
using BadgeSmith.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Web;

namespace BadgeSmith.Api.Handlers;

public static class TestBadgeHandler
{
    public static async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        IServiceProvider services,
        Match routeMatch)
    {
        var logger = services.GetRequiredService<ILogger<TestBadgeHandler>>();
        var testResultsRepo = services.GetRequiredService<ITestResultsRepository>();
        var badgeFactory = services.GetRequiredService<IBadgeResponseFactory>();
        var responseBuilder = services.GetRequiredService<IHttpResponseBuilder>();
        var cacheService = services.GetRequiredService<IResponseCacheService>();

        try
        {
            // Extract route parameters
            var platform = HttpUtility.UrlDecode(routeMatch.Groups["platform"].Value);
            var owner = HttpUtility.UrlDecode(routeMatch.Groups["owner"].Value);
            var repo = HttpUtility.UrlDecode(routeMatch.Groups["repo"].Value);
            var branch = HttpUtility.UrlDecode(routeMatch.Groups["branch"].Value);

            // Generate cache key
            var cacheKey = cacheService.GenerateCacheKey("test", platform, owner, repo, branch);

            // Check cache first
            var cachedBadge = await cacheService.GetAsync<TestBadgeResponse>(cacheKey);
            if (cachedBadge != null)
            {
                logger.LogDebug("Cache hit for test badge: {Platform}/{Owner}/{Repo}/{Branch}", platform, owner, repo, branch);
                return responseBuilder.CreateBadgeResponse(cachedBadge);
            }

            logger.LogInformation("Processing test badge request: {Platform}/{Owner}/{Repo}/{Branch}", platform, owner, repo, branch);

            // Get latest test result
            var testResult = await testResultsRepo.GetLatestResultAsync(owner, repo, platform, branch);

            TestBadgeResponse badge;
            if (testResult == null)
            {
                logger.LogInformation("No test results found for {Platform}/{Owner}/{Repo}/{Branch}", platform, owner, repo, branch);
                badge = badgeFactory.CreateTestNotFoundBadge(platform);
            }
            else
            {
                badge = badgeFactory.CreateTestBadge(testResult, platform);
                logger.LogInformation("Created test badge for {Platform}/{Owner}/{Repo}/{Branch}: {Status}",
                    platform, owner, repo, branch, testResult.Status);
            }

            // Cache the response (shorter TTL for test results)
            await cacheService.SetAsync(cacheKey, badge, TimeSpan.FromMinutes(5));

            return responseBuilder.CreateBadgeResponse(badge);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing test badge request");

            var errorBadge = TestBadgeResponse.Unknown(routeMatch.Groups["platform"].Value, "error");
            return responseBuilder.CreateBadgeResponse(errorBadge);
        }
    }
}
```

### Step 7: Test Redirect Handler

Add redirect functionality for GitHub README integration:

```csharp
// BadgeSmith.Api/Handlers/TestRedirectHandler.cs
using BadgeSmith.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Web;

namespace BadgeSmith.Api.Handlers;

public static class TestRedirectHandler
{
    public static async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        IServiceProvider services,
        Match routeMatch)
    {
        var logger = services.GetRequiredService<ILogger<TestRedirectHandler>>();
        var testResultsRepo = services.GetRequiredService<ITestResultsRepository>();
        var responseBuilder = services.GetRequiredService<IHttpResponseBuilder>();

        try
        {
            // Extract route parameters
            var platform = HttpUtility.UrlDecode(routeMatch.Groups["platform"].Value);
            var owner = HttpUtility.UrlDecode(routeMatch.Groups["owner"].Value);
            var repo = HttpUtility.UrlDecode(routeMatch.Groups["repo"].Value);
            var branch = HttpUtility.UrlDecode(routeMatch.Groups["branch"].Value);

            logger.LogInformation("Processing test redirect request: {Platform}/{Owner}/{Repo}/{Branch}", platform, owner, repo, branch);

            // Get latest test result
            var testResult = await testResultsRepo.GetLatestResultAsync(owner, repo, platform, branch);

            string redirectUrl;
            if (testResult == null || string.IsNullOrEmpty(testResult.RunUrl))
            {
                // Redirect to repository if no test results or run URL
                redirectUrl = $"https://github.com/{owner}/{repo}";
                logger.LogInformation("No test results found, redirecting to repository: {RedirectUrl}", redirectUrl);
            }
            else
            {
                redirectUrl = testResult.RunUrl;
                logger.LogInformation("Redirecting to test run: {RedirectUrl}", redirectUrl);
            }

            return responseBuilder.CreateRedirectResponse(redirectUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing test redirect request");

            // Fallback to repository URL
            var owner = routeMatch.Groups["owner"].Value;
            var repo = routeMatch.Groups["repo"].Value;
            var fallbackUrl = $"https://github.com/{owner}/{repo}";

            return responseBuilder.CreateRedirectResponse(fallbackUrl);
        }
    }
}
```

### Step 8: Update Router with New Endpoints

```csharp
// Update BadgeSmith.Api/Routing/RoutePatterns.cs
public static partial class RoutePatterns
{
    // Existing patterns...

    [GeneratedRegex(@"^/badges/tests/(?<platform>[^/]+)/(?<owner>[^/]+)/(?<repo>[^/]+)/(?<branch>[^/]+)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    public static partial Regex TestBadge();

    [GeneratedRegex(@"^/tests/results/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    public static partial Regex TestIngestion();

    [GeneratedRegex(@"^/redirect/test-results/(?<platform>[^/]+)/(?<owner>[^/]+)/(?<repo>[^/]+)/(?<branch>[^/]+)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    public static partial Regex TestRedirect();
}

// Update BadgeSmith.Api/Routing/Router.cs
public static class Router
{
    public static async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        IServiceProvider services)
    {
        var path = request.RawPath ?? "";
        var method = request.RequestContext?.Http?.Method ?? "";

        // Health check endpoint
        if (RoutePatterns.Health().IsMatch(path) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return await HealthHandler.HandleAsync(request, services);
        }

        // Package badge endpoint
        if (RoutePatterns.PackageBadge().IsMatch(path) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            var match = RoutePatterns.PackageBadge().Match(path);
            return await PackageBadgeHandler.HandleAsync(request, services, match);
        }

        // Test badge endpoint
        if (RoutePatterns.TestBadge().IsMatch(path) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            var match = RoutePatterns.TestBadge().Match(path);
            return await TestBadgeHandler.HandleAsync(request, services, match);
        }

        // Test result ingestion
        if (RoutePatterns.TestIngestion().IsMatch(path) && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            return await TestIngestionHandler.HandleAsync(request, services);
        }

        // Test redirect endpoint
        if (RoutePatterns.TestRedirect().IsMatch(path) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            var match = RoutePatterns.TestRedirect().Match(path);
            return await TestRedirectHandler.HandleAsync(request, services, match);
        }

        // 404 Not Found
        return CreateNotFoundResponse(path);
    }

    // ... existing helper methods
}
```

### Step 9: Service Registration Updates

```csharp
// Update BadgeSmith.Api/Extensions/ServiceCollectionExtensions.cs
public static IServiceCollection AddBadgeSmithServices(this IServiceCollection services)
{
    // Memory cache for performance
    services.AddMemoryCache();

    // Core services
    services.AddSingleton<IHealthService, HealthService>();
    services.AddSingleton<ISecretsService, SecretsService>();

    // Response services
    services.AddSingleton<IBadgeResponseFactory, BadgeResponseFactory>();
    services.AddSingleton<IResponseCacheService, ResponseCacheService>();
    services.AddSingleton<IHttpResponseBuilder, HttpResponseBuilder>();

    // Authentication and data services
    services.AddSingleton<IHmacAuthService, HmacAuthService>();
    services.AddSingleton<ITestResultsRepository, TestResultsRepository>();

    // Package providers
    services.AddSingleton<NuGetProvider>();
    services.AddSingleton<GitHubProvider>();

    // AWS services
    services.AddAWSService<IAmazonDynamoDB>();
    services.AddAWSService<IAmazonSecretsManager>();

    // HTTP clients for external APIs (existing configuration)
    services.AddHttpClient<NuGetProvider>(client =>
    {
        client.BaseAddress = new Uri("https://api.nuget.org/");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "BadgeSmith/1.0");
    });

    services.AddHttpClient<GitHubProvider>(client =>
    {
        client.BaseAddress = new Uri("https://api.github.com/");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "BadgeSmith/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    });

    return services;
}
```

### Step 10: Update JSON Serialization Context

```csharp
// Update BadgeSmith.Api/Json/LambdaFunctionJsonSerializerContext.cs
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ErrorDetail))]
[JsonSerializable(typeof(ShieldsBadgeResponse))]
[JsonSerializable(typeof(TestBadgeResponse))]
[JsonSerializable(typeof(TestResult))]
[JsonSerializable(typeof(TestResultRequest))]
[JsonSerializable(typeof(AuthResult))]
[JsonSerializable(typeof(PackageInfo))]
[JsonSerializable(typeof(NuGetVersionIndex))]
[JsonSerializable(typeof(GitHubPackage[]))]
[JsonSerializable(typeof(GitHubPackageVersion[]))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<ErrorDetail>))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
}
```

## ✅ Success Criteria

### Authentication Success Criteria

- ✅ **HMAC-SHA256 authentication** works correctly with proper signature validation
- ✅ **Nonce replay protection** prevents duplicate requests effectively
- ✅ **Timestamp validation** enforces 5-minute request window
- ✅ **Secrets Manager integration** securely stores and retrieves HMAC keys
- ✅ **Authentication error handling** provides clear feedback without leaking information

### Test Result Success Criteria

- ✅ **DynamoDB transaction-based idempotency** prevents duplicate test result ingestion
- ✅ **Test result badges** display correct status and counts
- ✅ **GSI queries** for latest results perform efficiently
- ✅ **Test result validation** ensures data integrity
- ✅ **Historical data retention** preserves test results indefinitely for analysis

### API Integration Success Criteria

- ✅ **POST /tests/results** endpoint accepts authenticated test results
- ✅ **GET /badges/tests/{platform}/{owner}/{repo}/{branch}** returns correct badges
- ✅ **GET /redirect/test-results/** redirects to appropriate test runs
- ✅ **Error responses** follow consistent schema across all endpoints
- ✅ **CORS headers** enable browser-based integration

## 🧪 Testing Authentication & Test Results

### Manual Testing Steps

1. **Create a test secret in Secrets Manager**:

   ```powershell
   # Using AWS CLI (or LocalStack)
   aws secretsmanager create-secret --name "badge-smith/test-repo" --secret-string "my-secret-key"
   ```

2. **Generate HMAC signature for test**:

   ```powershell
   # PowerShell script to generate HMAC signature
   $secret = "my-secret-key"
   $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
   $nonce = [System.Guid]::NewGuid().ToString()
   $payload = '{"owner":"test-org","repo":"test-repo","platform":"github","branch":"main","runId":"run-123","passed":10,"failed":2,"skipped":1,"runUrl":"https://github.com/test-org/test-repo/actions/runs/123"}'

   $data = "$timestamp:$nonce:$payload"
   $hmac = New-Object System.Security.Cryptography.HMACSHA256
   $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
   $hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($data))
   $signature = "sha256=" + [System.BitConverter]::ToString($hash).Replace("-", "").ToLower()

   Write-Output "Timestamp: $timestamp"
   Write-Output "Nonce: $nonce"
   Write-Output "Signature: $signature"
   ```

3. **Test result ingestion**:

   ```powershell
   curl -X POST "http://localhost:5000/tests/results" `
     -H "Content-Type: application/json" `
     -H "X-Signature: $signature" `
     -H "X-Repo-Secret: test-repo" `
     -H "X-Timestamp: $timestamp" `
     -H "X-Nonce: $nonce" `
     -d $payload
   ```

4. **Test badge retrieval**:

   ```powershell
   curl "http://localhost:5000/badges/tests/github/test-org/test-repo/main"
   ```

5. **Test redirect endpoint**:

   ```powershell
   curl -I "http://localhost:5000/redirect/test-results/github/test-org/test-repo/main"
   ```

### Expected Responses

**Successful Test Result Ingestion**:

```json
{
  "message": "Test result saved successfully",
  "runId": "run-123",
  "timestamp": "2025-08-20T10:30:00.000Z"
}
```

**Test Badge Response**:

```json
{
  "schemaVersion": 1,
  "label": "github",
  "message": "10 passed, 2 failed",
  "color": "yellow",
  "cacheSeconds": 300
}
```

**Test Redirect Response**:

```http
HTTP/1.1 302 Found
Location: https://github.com/test-org/test-repo/actions/runs/123
Access-Control-Allow-Origin: *
Cache-Control: no-cache
```

## 🔄 Next Steps

After Phase 4 completion, proceed to:

- **[Phase 5: Production Readiness](../03-implementation/Phase-5-production-readiness.md)** - Monitoring, logging, and deployment optimization
- **[Phase 6: Migration & Documentation](../03-implementation/Phase-6-migration-documentation.md)** - Complete migration and comprehensive documentation

## 🔗 Related Documentation

- **[Security Design](../02-architecture/03-security-design.md)** - HMAC authentication architecture and security patterns
- **[System Architecture](../02-architecture/01-system-architecture.md)** - DynamoDB integration and transaction patterns
- **[Requirements](../01-foundation/02-requirements.md)** - Test result requirements (FR-4, FR-5)
- **[Phase 3 Response Formatting](../03-implementation/Phase-3-response-formatting.md)** - Response formatting foundation this phase builds upon
