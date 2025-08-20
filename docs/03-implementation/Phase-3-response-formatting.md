# 🎨 Phase 3: Response Formatting & Caching

> **Last Updated**: August 20, 2025

## 🎯 Phase Overview

**Duration**: Week 4
**Objective**: Implement enhanced response formatting with Shields.io specification compliance, intelligent caching strategies, and comprehensive error handling. This phase builds upon Phase 2's package providers to create production-grade response formatting.

## 🏛️ Architecture Approach

Phase 3 implements **comprehensive response formatting** with:

- **Shields.io specification compliance** with exact schema matching
- **Intelligent caching strategies** with CloudFront and Lambda-level caching
- **Comprehensive error handling** with consistent error schemas
- **Color coding logic** for semantic status representation
- **Performance optimization** with cache headers and CDN integration
- **Response validation** ensuring API contract compliance

## 📊 Response Types to Implement

### Core Response Models

All responses follow the Shields.io specification for maximum compatibility:

```csharp
// BadgeSmith.Api/Models/Responses/ShieldsBadgeResponse.cs
using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Models.Responses;

public record ShieldsBadgeResponse
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("color")]
    public string Color { get; init; } = "blue";

    [JsonPropertyName("namedLogo")]
    public string? NamedLogo { get; init; }

    [JsonPropertyName("logoColor")]
    public string? LogoColor { get; init; }

    [JsonPropertyName("labelColor")]
    public string? LabelColor { get; init; }

    [JsonPropertyName("style")]
    public string? Style { get; init; }

    [JsonPropertyName("cacheSeconds")]
    public int? CacheSeconds { get; init; }
}
```

### Error Response Models

```csharp
// BadgeSmith.Api/Models/Responses/ErrorResponse.cs
namespace BadgeSmith.Api.Models.Responses;

public record ErrorResponse
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("details")]
    public IList<ErrorDetail> Details { get; init; } = [];

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("path")]
    public string Path { get; init; } = "";
}

public record ErrorDetail
{
    [JsonPropertyName("field")]
    public string Field { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("code")]
    public string Code { get; init; } = "";
}
```

### Test Result Response Models

```csharp
// BadgeSmith.Api/Models/Responses/TestBadgeResponse.cs
namespace BadgeSmith.Api.Models.Responses;

public record TestBadgeResponse : ShieldsBadgeResponse
{
    public static TestBadgeResponse Success(int passed, int total, string platform) => new()
    {
        SchemaVersion = 1,
        Label = platform,
        Message = $"{passed}/{total} passing",
        Color = passed == total ? "brightgreen" : passed > 0 ? "yellow" : "red",
        CacheSeconds = 300 // 5 minutes
    };

    public static TestBadgeResponse Failed(int failed, int total, string platform) => new()
    {
        SchemaVersion = 1,
        Label = platform,
        Message = $"{failed}/{total} failed",
        Color = "red",
        CacheSeconds = 300
    };

    public static TestBadgeResponse Unknown(string platform, string reason = "no data") => new()
    {
        SchemaVersion = 1,
        Label = platform,
        Message = reason,
        Color = "lightgrey",
        CacheSeconds = 60 // Shorter cache for unknown status
    };
}
```

## 🔧 Implementation Steps

### Step 1: Response Factory Service

Create a centralized response factory for consistent badge creation:

```csharp
// BadgeSmith.Api/Services/BadgeResponseFactory.cs
using BadgeSmith.Api.Models.Responses;
using BadgeSmith.Api.Models.Internal;

namespace BadgeSmith.Api.Services;

public interface IBadgeResponseFactory
{
    ShieldsBadgeResponse CreatePackageBadge(PackageInfo packageInfo, string provider);
    ShieldsBadgeResponse CreatePackageNotFoundBadge(string provider);
    ShieldsBadgeResponse CreateServiceUnavailableBadge(string provider);
    ShieldsBadgeResponse CreateErrorBadge(string provider, string error);
    TestBadgeResponse CreateTestBadge(TestResult testResult, string platform);
    TestBadgeResponse CreateTestNotFoundBadge(string platform);
}

public class BadgeResponseFactory : IBadgeResponseFactory
{
    public ShieldsBadgeResponse CreatePackageBadge(PackageInfo packageInfo, string provider)
    {
        var color = DeterminePackageColor(packageInfo);
        var message = FormatVersionMessage(packageInfo);

        return new ShieldsBadgeResponse
        {
            SchemaVersion = 1,
            Label = provider.ToLowerInvariant(),
            Message = message,
            Color = color,
            NamedLogo = GetProviderLogo(provider),
            CacheSeconds = 300 // 5 minutes for package badges
        };
    }

    public ShieldsBadgeResponse CreatePackageNotFoundBadge(string provider)
    {
        return new ShieldsBadgeResponse
        {
            SchemaVersion = 1,
            Label = provider.ToLowerInvariant(),
            Message = "not found",
            Color = "red",
            NamedLogo = GetProviderLogo(provider),
            CacheSeconds = 3600 // 1 hour for not found (longer cache)
        };
    }

    public ShieldsBadgeResponse CreateServiceUnavailableBadge(string provider)
    {
        return new ShieldsBadgeResponse
        {
            SchemaVersion = 1,
            Label = provider.ToLowerInvariant(),
            Message = "unavailable",
            Color = "lightgrey",
            NamedLogo = GetProviderLogo(provider),
            CacheSeconds = 60 // 1 minute for service issues (short cache)
        };
    }

    public ShieldsBadgeResponse CreateErrorBadge(string provider, string error)
    {
        return new ShieldsBadgeResponse
        {
            SchemaVersion = 1,
            Label = provider.ToLowerInvariant(),
            Message = "error",
            Color = "red",
            NamedLogo = GetProviderLogo(provider),
            CacheSeconds = 60 // Short cache for errors
        };
    }

    public TestBadgeResponse CreateTestBadge(TestResult testResult, string platform)
    {
        return testResult.Status switch
        {
            TestStatus.Passed => TestBadgeResponse.Success(testResult.Passed, testResult.Total, platform),
            TestStatus.Failed => TestBadgeResponse.Failed(testResult.Failed, testResult.Total, platform),
            TestStatus.Mixed => CreateMixedTestBadge(testResult, platform),
            _ => TestBadgeResponse.Unknown(platform)
        };
    }

    public TestBadgeResponse CreateTestNotFoundBadge(string platform)
    {
        return TestBadgeResponse.Unknown(platform, "no tests");
    }

    private static string DeterminePackageColor(PackageInfo packageInfo)
    {
        return packageInfo.IsPrerelease switch
        {
            true => "orange",      // Prerelease versions
            false when packageInfo.Version.StartsWith("0.") => "yellow", // Pre-1.0 versions
            false => "blue"        // Stable versions
        };
    }

    private static string FormatVersionMessage(PackageInfo packageInfo)
    {
        var version = packageInfo.Version;

        // Remove 'v' prefix if present
        if (version.StartsWith('v'))
        {
            version = version[1..];
        }

        return version;
    }

    private static string? GetProviderLogo(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "nuget" => "nuget",
            "github" => "github",
            "npm" => "npm",
            "pypi" => "pypi",
            _ => null
        };
    }

    private static TestBadgeResponse CreateMixedTestBadge(TestResult testResult, string platform)
    {
        var passed = testResult.Passed;
        var failed = testResult.Failed;
        var total = testResult.Total;

        var message = failed > 0
            ? $"{passed} passed, {failed} failed"
            : $"{passed}/{total} passing";

        var color = failed > 0 ? "yellow" : "brightgreen";

        return new TestBadgeResponse
        {
            SchemaVersion = 1,
            Label = platform,
            Message = message,
            Color = color,
            CacheSeconds = 300
        };
    }
}
```

### Step 2: Response Caching Service

Implement intelligent caching with multiple levels:

```csharp
// BadgeSmith.Api/Services/ResponseCacheService.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace BadgeSmith.Api.Services;

public interface IResponseCacheService
{
    Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string cacheKey, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    string GenerateCacheKey(string category, params string[] components);
    string GenerateETag(object response);
}

public class ResponseCacheService : IResponseCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ResponseCacheService> _logger;

    public ResponseCacheService(IMemoryCache memoryCache, ILogger<ResponseCacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(cacheKey, out T? cachedValue))
        {
            _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
            return Task.FromResult(cachedValue);
        }

        _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string cacheKey, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions();

        if (expiry.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiry.Value;
        }
        else
        {
            // Default TTL based on cache key category
            options.AbsoluteExpirationRelativeToNow = GetDefaultTtl(cacheKey);
        }

        // Set priority based on cache key type
        options.Priority = GetCachePriority(cacheKey);

        _memoryCache.Set(cacheKey, value, options);
        _logger.LogDebug("Cached value for key: {CacheKey}, TTL: {Ttl}", cacheKey, options.AbsoluteExpirationRelativeToNow);

        return Task.CompletedTask;
    }

    public string GenerateCacheKey(string category, params string[] components)
    {
        var key = $"{category}:{string.Join(':', components.Select(c => c.ToLowerInvariant()))}";

        // Hash long keys to avoid memory issues
        if (key.Length > 250)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            var hashString = Convert.ToHexString(hash)[..16]; // First 16 chars
            return $"{category}:{hashString}";
        }

        return key;
    }

    public string GenerateETag(object response)
    {
        var json = JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return $"\"{Convert.ToHexString(hash)[..16]}\"";
    }

    private static TimeSpan GetDefaultTtl(string cacheKey)
    {
        return cacheKey.Split(':')[0] switch
        {
            "package" => TimeSpan.FromMinutes(15),     // Package versions change infrequently
            "test" => TimeSpan.FromMinutes(5),         // Test results change more frequently
            "secret" => TimeSpan.FromHours(1),         // Secrets rarely change
            "health" => TimeSpan.FromMinutes(1),       // Health checks need to be fresh
            _ => TimeSpan.FromMinutes(10)              // Default
        };
    }

    private static CacheItemPriority GetCachePriority(string cacheKey)
    {
        return cacheKey.Split(':')[0] switch
        {
            "package" => CacheItemPriority.High,      // Package badges are frequently accessed
            "test" => CacheItemPriority.Normal,       // Test results are moderately accessed
            "secret" => CacheItemPriority.High,       // Secrets are expensive to fetch
            "health" => CacheItemPriority.Low,        // Health checks are least important
            _ => CacheItemPriority.Normal
        };
    }
}
```

### Step 3: HTTP Response Builder

Create a standardized HTTP response builder:

```csharp
// BadgeSmith.Api/Services/HttpResponseBuilder.cs
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Models.Responses;
using System.Net;

namespace BadgeSmith.Api.Services;

public interface IHttpResponseBuilder
{
    APIGatewayHttpApiV2ProxyResponse CreateBadgeResponse(ShieldsBadgeResponse badge, string? etag = null);
    APIGatewayHttpApiV2ProxyResponse CreateErrorResponse(int statusCode, string message, string path = "");
    APIGatewayHttpApiV2ProxyResponse CreateErrorResponse(int statusCode, ErrorResponse error);
    APIGatewayHttpApiV2ProxyResponse CreateNotModifiedResponse();
    APIGatewayHttpApiV2ProxyResponse CreateRedirectResponse(string location);
}

public class HttpResponseBuilder : IHttpResponseBuilder
{
    private readonly IResponseCacheService _cacheService;

    public HttpResponseBuilder(IResponseCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public APIGatewayHttpApiV2ProxyResponse CreateBadgeResponse(ShieldsBadgeResponse badge, string? etag = null)
    {
        etag ??= _cacheService.GenerateETag(badge);

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Access-Control-Allow-Origin"] = "*",
            ["Access-Control-Allow-Methods"] = "GET, OPTIONS",
            ["Access-Control-Allow-Headers"] = "Content-Type, X-Amz-Date, Authorization, X-Api-Key, X-Amz-Security-Token",
            ["ETag"] = etag
        };

        // Set cache headers based on badge cache seconds
        if (badge.CacheSeconds.HasValue)
        {
            var maxAge = badge.CacheSeconds.Value;
            headers["Cache-Control"] = $"public, max-age={maxAge}, s-maxage={maxAge}";
        }
        else
        {
            headers["Cache-Control"] = "public, max-age=300, s-maxage=300"; // Default 5 minutes
        }

        // Add Vary header for content negotiation
        headers["Vary"] = "Accept-Encoding, User-Agent";

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Headers = headers,
            Body = JsonSerializer.Serialize(badge, JsonSerializerOptions.Web)
        };
    }

    public APIGatewayHttpApiV2ProxyResponse CreateErrorResponse(int statusCode, string message, string path = "")
    {
        var errorResponse = new ErrorResponse
        {
            Message = message,
            Timestamp = DateTime.UtcNow,
            Path = path
        };

        return CreateErrorResponse(statusCode, errorResponse);
    }

    public APIGatewayHttpApiV2ProxyResponse CreateErrorResponse(int statusCode, ErrorResponse error)
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Access-Control-Allow-Origin"] = "*",
            ["Cache-Control"] = "no-cache, no-store, must-revalidate" // Don't cache errors
        };

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = statusCode,
            Headers = headers,
            Body = JsonSerializer.Serialize(error, JsonSerializerOptions.Web)
        };
    }

    public APIGatewayHttpApiV2ProxyResponse CreateNotModifiedResponse()
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 304,
            Headers = new Dictionary<string, string>
            {
                ["Access-Control-Allow-Origin"] = "*",
                ["Cache-Control"] = "public, max-age=300"
            }
        };
    }

    public APIGatewayHttpApiV2ProxyResponse CreateRedirectResponse(string location)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 302,
            Headers = new Dictionary<string, string>
            {
                ["Location"] = location,
                ["Access-Control-Allow-Origin"] = "*",
                ["Cache-Control"] = "no-cache"
            }
        };
    }
}
```

### Step 4: Enhanced Package Badge Handler

Update the package badge handler to use the new response formatting:

```csharp
// Update BadgeSmith.Api/Handlers/PackageBadgeHandler.cs
using BadgeSmith.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Web;

namespace BadgeSmith.Api.Handlers;

public static class PackageBadgeHandler
{
    public static async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        IServiceProvider services,
        Match routeMatch)
    {
        var logger = services.GetRequiredService<ILogger<PackageBadgeHandler>>();
        var badgeFactory = services.GetRequiredService<IBadgeResponseFactory>();
        var responseBuilder = services.GetRequiredService<IHttpResponseBuilder>();
        var cacheService = services.GetRequiredService<IResponseCacheService>();

        try
        {
            // Extract route parameters
            var provider = routeMatch.Groups["provider"].Value.ToLowerInvariant();
            var org = routeMatch.Groups["org"].Success ? HttpUtility.UrlDecode(routeMatch.Groups["org"].Value) : null;
            var package = HttpUtility.UrlDecode(routeMatch.Groups["package"].Value);

            // Parse query parameters for version filtering
            var filters = ParseVersionFilters(request.QueryStringParameters);

            // Generate cache key
            var cacheKey = cacheService.GenerateCacheKey("package", provider, org ?? "", package, filters.GetHashCode().ToString());

            // Check for conditional requests (ETag)
            var ifNoneMatch = request.Headers?.GetValueOrDefault("If-None-Match");
            if (!string.IsNullOrEmpty(ifNoneMatch))
            {
                var cachedResponse = await cacheService.GetAsync<ShieldsBadgeResponse>(cacheKey);
                if (cachedResponse != null)
                {
                    var cachedETag = cacheService.GenerateETag(cachedResponse);
                    if (ifNoneMatch == cachedETag)
                    {
                        logger.LogDebug("ETag match, returning 304 Not Modified for {Provider}/{Package}", provider, package);
                        return responseBuilder.CreateNotModifiedResponse();
                    }
                }
            }

            // Check cache first
            var cachedBadge = await cacheService.GetAsync<ShieldsBadgeResponse>(cacheKey);
            if (cachedBadge != null)
            {
                logger.LogDebug("Cache hit for package badge: {Provider}/{Package}", provider, package);
                return responseBuilder.CreateBadgeResponse(cachedBadge);
            }

            logger.LogInformation("Processing package badge request: {Provider} {Org}/{Package}", provider, org, package);

            // Get appropriate provider
            var packageProvider = GetPackageProvider(services, provider);
            if (packageProvider == null)
            {
                logger.LogWarning("Unsupported package provider: {Provider}", provider);
                var errorBadge = badgeFactory.CreateErrorBadge(provider, "unsupported provider");
                return responseBuilder.CreateBadgeResponse(errorBadge);
            }

            // Fetch package information
            var result = await packageProvider.GetLatestVersionAsync(org, package, filters);

            ShieldsBadgeResponse badge;
            if (!result.IsSuccess)
            {
                logger.LogInformation("Package error: {Provider} {Package} - {Error}", provider, package, result.ErrorMessage);

                badge = result.StatusCode switch
                {
                    404 => badgeFactory.CreatePackageNotFoundBadge(provider),
                    503 => badgeFactory.CreateServiceUnavailableBadge(provider),
                    _ => badgeFactory.CreateErrorBadge(provider, "error")
                };
            }
            else
            {
                badge = badgeFactory.CreatePackageBadge(result.Value!, provider);
                logger.LogInformation("Successfully created badge for {Provider} {Package}: {Version}",
                    provider, package, result.Value!.Version);
            }

            // Cache the response
            await cacheService.SetAsync(cacheKey, badge);

            return responseBuilder.CreateBadgeResponse(badge);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing package badge request");

            var errorBadge = badgeFactory.CreateErrorBadge("package", "error");
            return responseBuilder.CreateBadgeResponse(errorBadge);
        }
    }

    // ... existing helper methods remain the same
}
```

### Step 5: Health Check with Enhanced Response

```csharp
// BadgeSmith.Api/Handlers/HealthHandler.cs
using BadgeSmith.Api.Models.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

public static class HealthHandler
{
    public static async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<HealthHandler>>();
        var responseBuilder = services.GetRequiredService<IHttpResponseBuilder>();
        var cacheService = services.GetRequiredService<IResponseCacheService>();

        try
        {
            // Check cache first
            var cacheKey = cacheService.GenerateCacheKey("health", "status");
            var cachedHealth = await cacheService.GetAsync<HealthResponse>(cacheKey);

            if (cachedHealth != null)
            {
                logger.LogDebug("Health check cache hit");
                return CreateHealthResponse(cachedHealth, responseBuilder);
            }

            // Perform health checks
            var healthResponse = new HealthResponse
            {
                Status = "ok",
                Timestamp = DateTime.UtcNow,
                Version = GetVersion(),
                Environment = GetEnvironment(),
                Checks = await PerformHealthChecks(services)
            };

            // Cache health response for 1 minute
            await cacheService.SetAsync(cacheKey, healthResponse, TimeSpan.FromMinutes(1));

            logger.LogInformation("Health check completed: {Status}", healthResponse.Status);
            return CreateHealthResponse(healthResponse, responseBuilder);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed");

            var errorHealth = new HealthResponse
            {
                Status = "error",
                Timestamp = DateTime.UtcNow,
                Version = GetVersion(),
                Environment = GetEnvironment()
            };

            return CreateHealthResponse(errorHealth, responseBuilder, 503);
        }
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateHealthResponse(
        HealthResponse health,
        IHttpResponseBuilder responseBuilder,
        int statusCode = 200)
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Access-Control-Allow-Origin"] = "*",
            ["Cache-Control"] = "no-cache, no-store, must-revalidate"
        };

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = statusCode,
            Headers = headers,
            Body = JsonSerializer.Serialize(health, JsonSerializerOptions.Web)
        };
    }

    private static async Task<Dictionary<string, object>> PerformHealthChecks(IServiceProvider services)
    {
        var checks = new Dictionary<string, object>();

        // Memory cache check
        try
        {
            var cacheService = services.GetRequiredService<IResponseCacheService>();
            var testKey = $"health-test-{Guid.NewGuid()}";
            await cacheService.SetAsync(testKey, "test", TimeSpan.FromSeconds(1));
            var result = await cacheService.GetAsync<string>(testKey);
            checks["memory-cache"] = result == "test" ? "ok" : "fail";
        }
        catch
        {
            checks["memory-cache"] = "fail";
        }

        // Add more health checks as needed
        checks["lambda-runtime"] = "ok";

        return checks;
    }

    private static string GetVersion()
    {
        // In production, this could read from assembly version or environment variable
        return Environment.GetEnvironmentVariable("APP_VERSION") ?? "1.0.0";
    }

    private static string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    }
}

public record HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    [JsonPropertyName("environment")]
    public string Environment { get; init; } = "";

    [JsonPropertyName("checks")]
    public Dictionary<string, object> Checks { get; init; } = [];
}
```

### Step 6: Service Registration Updates

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

    // Package providers
    services.AddSingleton<NuGetProvider>();
    services.AddSingleton<GitHubProvider>();

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

### Step 7: Update JSON Serialization Context

```csharp
// Update BadgeSmith.Api/Json/LambdaFunctionJsonSerializerContext.cs
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ErrorDetail))]
[JsonSerializable(typeof(ShieldsBadgeResponse))]
[JsonSerializable(typeof(TestBadgeResponse))]
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

### Response Format Success Criteria

- ✅ **Shields.io specification compliance** with exact schema matching
- ✅ **Color coding logic** works correctly for all badge types
- ✅ **Logo integration** displays provider logos correctly
- ✅ **Cache headers** set appropriately for different response types
- ✅ **ETag support** for conditional requests and bandwidth optimization

### Caching Success Criteria

- ✅ **Multi-level caching** with memory cache and CDN integration
- ✅ **Intelligent TTL strategies** based on content type and volatility
- ✅ **Cache key generation** handles complex parameters correctly
- ✅ **Cache priority levels** optimize memory usage
- ✅ **ETag generation** ensures response consistency

### Error Handling Success Criteria

- ✅ **Consistent error schemas** across all endpoints
- ✅ **HTTP status codes** match semantic meaning
- ✅ **Error detail information** provides useful debugging context
- ✅ **Graceful degradation** to cached responses when possible
- ✅ **No cache headers** for error responses to prevent stale errors

## 🧪 Testing Response Formatting

### Manual Testing Steps

1. **Test successful package badge**:

   ```powershell
   curl -H "Accept: application/json" "http://localhost:5000/badges/packages/nuget/Newtonsoft.Json"
   ```

2. **Test ETag conditional request**:

   ```powershell
   # First request to get ETag
   $response = curl -i "http://localhost:5000/badges/packages/nuget/Newtonsoft.Json"

   # Extract ETag from response headers
   $etag = ($response | Select-String 'ETag: (.*)').Matches[0].Groups[1].Value

   # Second request with If-None-Match header
   curl -H "If-None-Match: $etag" "http://localhost:5000/badges/packages/nuget/Newtonsoft.Json"
   ```

3. **Test error responses**:

   ```powershell
   curl "http://localhost:5000/badges/packages/nuget/NonExistentPackage12345"
   curl "http://localhost:5000/badges/packages/invalid-provider/SomePackage"
   ```

4. **Test health endpoint**:

   ```powershell
   curl "http://localhost:5000/health"
   ```

5. **Test cache headers**:

   ```powershell
   curl -i "http://localhost:5000/badges/packages/nuget/Newtonsoft.Json" | Select-String "Cache-Control"
   ```

### Expected Response Headers

**Successful Badge Response**:

```http
HTTP/1.1 200 OK
Content-Type: application/json
Cache-Control: public, max-age=300, s-maxage=300
ETag: "a1b2c3d4e5f67890"
Access-Control-Allow-Origin: *
Vary: Accept-Encoding, User-Agent
```

**Not Modified Response**:

```http
HTTP/1.1 304 Not Modified
Cache-Control: public, max-age=300
Access-Control-Allow-Origin: *
```

**Error Response**:

```http
HTTP/1.1 404 Not Found
Content-Type: application/json
Cache-Control: no-cache, no-store, must-revalidate
Access-Control-Allow-Origin: *
```

## 🔄 Next Steps

After Phase 3 completion, proceed to:

- **[Phase 4: Authentication](../03-implementation/Phase-4-authentication.md)** - HMAC authentication and test result ingestion
- **[Phase 5: Production Readiness](../03-implementation/Phase-5-production-readiness.md)** - Monitoring, logging, and deployment

## 🔗 Related Documentation

- **[System Architecture](../02-architecture/01-system-architecture.md)** - Response formatting and caching architecture
- **[Requirements](../01-foundation/02-requirements.md)** - Response format requirements (FR-2, FR-3)
- **[Phase 2 Package Endpoints](../03-implementation/Phase-2-package-endpoints.md)** - Package provider implementation this phase enhances
- **[Security Design](../02-architecture/03-security-design.md)** - CORS and security header implementation
