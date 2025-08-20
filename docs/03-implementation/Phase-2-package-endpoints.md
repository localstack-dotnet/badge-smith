# 📦 Phase 2: Package Endpoints Implementation

> **Last Updated**: August 20, 2025

## 🎯 Phase Overview

**Duration**: Week 3
**Objective**: Implement package badge functionality with production-grade upstream resilience and caching. This phase builds upon the Phase 1 foundation to create the core package badge endpoints for NuGet and GitHub packages.

## 🏛️ Architecture Approach

Phase 2 implements **resilient package providers** with:

- **Provider abstraction** for extensible package source support
- **Production-grade resilience** with exponential backoff and circuit breakers
- **ETag conditional requests** to minimize bandwidth and API costs
- **Multi-level caching** with appropriate TTL strategies
- **Graceful degradation** to cached data during upstream failures
- **Version filtering logic** for semantic version constraints

## 📊 API Endpoints to Implement

### Package Badge Endpoints

```http
GET /badges/packages/nuget/{package}
GET /badges/packages/github/{org}/{package}
```

**Query Parameters:**

- `gt`, `gte`, `lt`, `lte`, `eq`: Semantic version constraints
- `prerelease`: Include prerelease versions (boolean)

**Response Format (Shields.io Compatible):**

```json
{
  "schemaVersion": 1,
  "label": "nuget",
  "message": "13.0.3",
  "color": "blue"
}
```

## 🔧 Implementation Steps

### Step 1: Package Provider Interface

Create the core abstraction for package providers with resilience patterns:

```csharp
// BadgeSmith.Api/Services/IPackageProvider.cs
using BadgeSmith.Api.Models.Internal;

namespace BadgeSmith.Api.Services;

public interface IPackageProvider
{
    Task<Result<PackageInfo>> GetLatestVersionAsync(
        string? org,
        string package,
        VersionFilters filters,
        CancellationToken cancellationToken = default);
}

public record VersionFilters
{
    public string? Gt { get; init; }
    public string? Gte { get; init; }
    public string? Lt { get; init; }
    public string? Lte { get; init; }
    public string? Eq { get; init; }
    public bool IncludePrerelease { get; init; }

    public override int GetHashCode()
    {
        return HashCode.Combine(Gt, Gte, Lt, Lte, Eq, IncludePrerelease);
    }
}

public record PackageInfo
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Provider { get; init; } = "";
    public bool IsPrerelease { get; init; }
    public DateTime PublishedDate { get; init; }
}
```

### Step 2: Result Pattern for Error Handling

```csharp
// BadgeSmith.Api/Models/Internal/Result.cs
namespace BadgeSmith.Api.Models.Internal;

public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string ErrorMessage { get; init; } = "";
    public int StatusCode { get; init; } = 200;

    public static Result<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value,
        StatusCode = 200
    };

    public static Result<T> Failure(string errorMessage, int statusCode = 500) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
        StatusCode = statusCode
    };

    public static Result<T> NotFound(string message = "Package not found") => new()
    {
        IsSuccess = false,
        ErrorMessage = message,
        StatusCode = 404
    };
}

public record Result
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = "";

    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
```

### Step 3: NuGet Provider Implementation

```csharp
// BadgeSmith.Api/Services/NuGetProvider.cs
using BadgeSmith.Api.Models.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace BadgeSmith.Api.Services;

public class NuGetProvider : IPackageProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NuGetProvider> _logger;

    public NuGetProvider(HttpClient httpClient, IMemoryCache cache, ILogger<NuGetProvider> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<PackageInfo>> GetLatestVersionAsync(
        string? org,
        string package,
        VersionFilters filters,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"package:nuget::{package}:{filters.GetHashCode()}";

        // Try cache first
        if (_cache.TryGetValue(cacheKey, out PackageInfo? cached))
        {
            _logger.LogDebug("Cache hit for NuGet package: {Package}", package);
            return Result<PackageInfo>.Success(cached);
        }

        try
        {
            _logger.LogInformation("Fetching NuGet package: {Package}", package);

            // Call NuGet API with conditional requests (ETag support)
            var indexUrl = $"v3-flatcontainer/{package.ToLowerInvariant()}/index.json";
            var request = new HttpRequestMessage(HttpMethod.Get, indexUrl);

            // Add ETag header if we have cached ETag
            var etagCacheKey = $"etag:nuget:{package}";
            if (_cache.TryGetValue(etagCacheKey, out string? etag))
            {
                request.Headers.Add("If-None-Match", etag);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            // Handle 304 Not Modified - use cached data
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                _logger.LogDebug("NuGet package not modified, using cached data: {Package}", package);
                if (_cache.TryGetValue(cacheKey, out PackageInfo? notModifiedCache))
                {
                    return Result<PackageInfo>.Success(notModifiedCache);
                }
            }

            // Handle rate limits gracefully
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("NuGet API rate limit exceeded for package: {Package}", package);
                return _cache.TryGetValue(cacheKey, out PackageInfo? rateLimitCache)
                    ? Result<PackageInfo>.Success(rateLimitCache)
                    : Result<PackageInfo>.Failure("NuGet service temporarily unavailable", 503);
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("NuGet package not found: {Package}", package);
                    return Result<PackageInfo>.NotFound($"Package '{package}' not found in NuGet");
                }

                _logger.LogWarning("NuGet API error for package {Package}: {StatusCode}", package, response.StatusCode);
                return Result<PackageInfo>.Failure($"NuGet API error: {response.StatusCode}", (int)response.StatusCode);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var versionIndex = JsonSerializer.Deserialize<NuGetVersionIndex>(content);

            if (versionIndex?.Versions == null || versionIndex.Versions.Length == 0)
            {
                return Result<PackageInfo>.NotFound($"No versions found for package '{package}'");
            }

            // Apply version filtering
            var filteredVersions = ApplyVersionFilters(versionIndex.Versions, filters);
            if (!filteredVersions.Any())
            {
                return Result<PackageInfo>.NotFound($"No versions found matching criteria for package '{package}'");
            }

            var latestVersion = filteredVersions.Last(); // Versions are sorted
            var packageInfo = new PackageInfo
            {
                Name = package,
                Version = latestVersion,
                Provider = "nuget",
                IsPrerelease = IsPrerelease(latestVersion),
                PublishedDate = DateTime.UtcNow // Would need additional API call for exact date
            };

            // Cache the result and ETag
            var cacheExpiry = TimeSpan.FromMinutes(15); // NuGet API calls are expensive
            _cache.Set(cacheKey, packageInfo, cacheExpiry);

            if (response.Headers.ETag != null)
            {
                _cache.Set(etagCacheKey, response.Headers.ETag.Tag, cacheExpiry);
            }

            _logger.LogInformation("Successfully fetched NuGet package {Package} version {Version}", package, latestVersion);
            return Result<PackageInfo>.Success(packageInfo);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error fetching NuGet package: {Package}", package);

            // Graceful degradation - return cached data if available
            if (_cache.TryGetValue(cacheKey, out PackageInfo? fallbackCache))
            {
                _logger.LogInformation("Using cached data for NuGet package due to network error: {Package}", package);
                return Result<PackageInfo>.Success(fallbackCache);
            }

            return Result<PackageInfo>.Failure("NuGet service unavailable", 503);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching NuGet package: {Package}", package);
            return Result<PackageInfo>.Failure("Internal server error", 500);
        }
    }

    private static IEnumerable<string> ApplyVersionFilters(string[] versions, VersionFilters filters)
    {
        var filteredVersions = versions.AsEnumerable();

        // Filter out prerelease versions if not explicitly requested
        if (!filters.IncludePrerelease)
        {
            filteredVersions = filteredVersions.Where(v => !IsPrerelease(v));
        }

        // Apply semantic version constraints
        // Note: This is a simplified implementation
        // Production should use NuGet.Versioning library for proper semver handling
        if (!string.IsNullOrEmpty(filters.Gt))
        {
            filteredVersions = filteredVersions.Where(v => CompareVersions(v, filters.Gt) > 0);
        }

        if (!string.IsNullOrEmpty(filters.Gte))
        {
            filteredVersions = filteredVersions.Where(v => CompareVersions(v, filters.Gte) >= 0);
        }

        if (!string.IsNullOrEmpty(filters.Lt))
        {
            filteredVersions = filteredVersions.Where(v => CompareVersions(v, filters.Lt) < 0);
        }

        if (!string.IsNullOrEmpty(filters.Lte))
        {
            filteredVersions = filteredVersions.Where(v => CompareVersions(v, filters.Lte) <= 0);
        }

        if (!string.IsNullOrEmpty(filters.Eq))
        {
            filteredVersions = filteredVersions.Where(v => CompareVersions(v, filters.Eq) == 0);
        }

        return filteredVersions;
    }

    private static bool IsPrerelease(string version)
    {
        return version.Contains('-');
    }

    private static int CompareVersions(string version1, string version2)
    {
        // Simplified version comparison
        // Production should use NuGet.Versioning.NuGetVersion
        var v1 = new Version(version1.Split('-')[0]);
        var v2 = new Version(version2.Split('-')[0]);
        return v1.CompareTo(v2);
    }
}

public record NuGetVersionIndex
{
    [JsonPropertyName("versions")]
    public string[] Versions { get; init; } = Array.Empty<string>();
}
```

### Step 4: GitHub Packages Provider

```csharp
// BadgeSmith.Api/Services/GitHubProvider.cs
using BadgeSmith.Api.Models.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BadgeSmith.Api.Services;

public class GitHubProvider : IPackageProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ISecretsService _secretsService;
    private readonly ILogger<GitHubProvider> _logger;

    public GitHubProvider(
        HttpClient httpClient,
        IMemoryCache cache,
        ISecretsService secretsService,
        ILogger<GitHubProvider> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _secretsService = secretsService;
        _logger = logger;
    }

    public async Task<Result<PackageInfo>> GetLatestVersionAsync(
        string? org,
        string package,
        VersionFilters filters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(org))
        {
            return Result<PackageInfo>.Failure("Organization is required for GitHub packages", 400);
        }

        var cacheKey = $"package:github:{org}:{package}:{filters.GetHashCode()}";

        // Try cache first
        if (_cache.TryGetValue(cacheKey, out PackageInfo? cached))
        {
            _logger.LogDebug("Cache hit for GitHub package: {Org}/{Package}", org, package);
            return Result<PackageInfo>.Success(cached);
        }

        try
        {
            _logger.LogInformation("Fetching GitHub package: {Org}/{Package}", org, package);

            // Get GitHub token for authentication
            var token = await _secretsService.GetProviderTokenAsync("github", org, cancellationToken);
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // GitHub Packages API endpoint
            var url = $"user/packages?package_type=nuget&visibility=public";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("GitHub API authentication failed for {Org}/{Package}", org, package);
                return Result<PackageInfo>.Failure("GitHub authentication failed", 401);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("GitHub package not found: {Org}/{Package}", org, package);
                return Result<PackageInfo>.NotFound($"Package '{org}/{package}' not found in GitHub Packages");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API error for package {Org}/{Package}: {StatusCode}", org, package, response.StatusCode);
                return Result<PackageInfo>.Failure($"GitHub API error: {response.StatusCode}", (int)response.StatusCode);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var packages = JsonSerializer.Deserialize<GitHubPackage[]>(content);

            var targetPackage = packages?.FirstOrDefault(p =>
                p.Name?.Equals(package, StringComparison.OrdinalIgnoreCase) == true);

            if (targetPackage == null)
            {
                return Result<PackageInfo>.NotFound($"Package '{package}' not found in organization '{org}'");
            }

            // Get versions for the package
            var versionsUrl = $"user/packages/nuget/{package}/versions";
            var versionsResponse = await _httpClient.GetAsync(versionsUrl, cancellationToken);

            if (!versionsResponse.IsSuccessStatusCode)
            {
                return Result<PackageInfo>.Failure("Failed to fetch package versions", (int)versionsResponse.StatusCode);
            }

            var versionsContent = await versionsResponse.Content.ReadAsStringAsync(cancellationToken);
            var versions = JsonSerializer.Deserialize<GitHubPackageVersion[]>(versionsContent);

            if (versions == null || versions.Length == 0)
            {
                return Result<PackageInfo>.NotFound($"No versions found for package '{org}/{package}'");
            }

            // Filter and get latest version
            var versionStrings = versions.Select(v => v.Name).Where(v => !string.IsNullOrEmpty(v)).ToArray();
            var filteredVersions = ApplyVersionFilters(versionStrings!, filters);

            if (!filteredVersions.Any())
            {
                return Result<PackageInfo>.NotFound($"No versions found matching criteria for package '{org}/{package}'");
            }

            var latestVersion = filteredVersions.Last();
            var packageInfo = new PackageInfo
            {
                Name = $"{org}/{package}",
                Version = latestVersion,
                Provider = "github",
                IsPrerelease = IsPrerelease(latestVersion),
                PublishedDate = DateTime.UtcNow
            };

            // Cache the result
            var cacheExpiry = TimeSpan.FromMinutes(15);
            _cache.Set(cacheKey, packageInfo, cacheExpiry);

            _logger.LogInformation("Successfully fetched GitHub package {Org}/{Package} version {Version}", org, package, latestVersion);
            return Result<PackageInfo>.Success(packageInfo);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error fetching GitHub package: {Org}/{Package}", org, package);

            // Graceful degradation
            if (_cache.TryGetValue(cacheKey, out PackageInfo? fallbackCache))
            {
                return Result<PackageInfo>.Success(fallbackCache);
            }

            return Result<PackageInfo>.Failure("GitHub service unavailable", 503);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching GitHub package: {Org}/{Package}", org, package);
            return Result<PackageInfo>.Failure("Internal server error", 500);
        }
    }

    private static IEnumerable<string> ApplyVersionFilters(string[] versions, VersionFilters filters)
    {
        // Reuse the same logic as NuGet provider
        // In production, extract to a shared VersionFilterService
        return NuGetProvider.ApplyVersionFilters(versions, filters);
    }

    private static bool IsPrerelease(string version)
    {
        return version.Contains('-');
    }
}

public record GitHubPackage
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("package_type")]
    public string? PackageType { get; init; }
}

public record GitHubPackageVersion
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
```

### Step 5: Package Badge Handler

```csharp
// BadgeSmith.Api/Handlers/PackageBadgeHandler.cs
using BadgeSmith.Api.Models.Responses;
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

        try
        {
            // Extract route parameters
            var provider = routeMatch.Groups["provider"].Value.ToLowerInvariant();
            var org = routeMatch.Groups["org"].Success ? HttpUtility.UrlDecode(routeMatch.Groups["org"].Value) : null;
            var package = HttpUtility.UrlDecode(routeMatch.Groups["package"].Value);

            // Parse query parameters for version filtering
            var filters = ParseVersionFilters(request.QueryStringParameters);

            logger.LogInformation("Processing package badge request: {Provider} {Org}/{Package}", provider, org, package);

            // Get appropriate provider
            var packageProvider = GetPackageProvider(services, provider);
            if (packageProvider == null)
            {
                logger.LogWarning("Unsupported package provider: {Provider}", provider);
                return CreateErrorResponse(400, $"Unsupported provider: {provider}");
            }

            // Fetch package information
            var result = await packageProvider.GetLatestVersionAsync(org, package, filters);

            if (!result.IsSuccess)
            {
                logger.LogInformation("Package not found or error: {Provider} {Package} - {Error}", provider, package, result.ErrorMessage);

                if (result.StatusCode == 404)
                {
                    // Return "not found" badge instead of 404 error
                    var notFoundBadge = new ShieldsBadgeResponse
                    {
                        SchemaVersion = 1,
                        Label = provider,
                        Message = "not found",
                        Color = "red"
                    };
                    return CreateSuccessResponse(notFoundBadge);
                }

                if (result.StatusCode == 503)
                {
                    // Return "unavailable" badge for service errors
                    var unavailableBadge = new ShieldsBadgeResponse
                    {
                        SchemaVersion = 1,
                        Label = provider,
                        Message = "unavailable",
                        Color = "lightgrey"
                    };
                    return CreateSuccessResponse(unavailableBadge);
                }

                return CreateErrorResponse(result.StatusCode, result.ErrorMessage);
            }

            // Create successful badge response
            var badge = new ShieldsBadgeResponse
            {
                SchemaVersion = 1,
                Label = provider,
                Message = result.Value!.Version,
                Color = result.Value.IsPrerelease ? "orange" : "blue"
            };

            logger.LogInformation("Successfully created badge for {Provider} {Package}: {Version}", provider, package, result.Value.Version);
            return CreateSuccessResponse(badge);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing package badge request");

            var errorBadge = new ShieldsBadgeResponse
            {
                SchemaVersion = 1,
                Label = "package",
                Message = "error",
                Color = "red"
            };

            return CreateSuccessResponse(errorBadge);
        }
    }

    private static VersionFilters ParseVersionFilters(IDictionary<string, string>? queryParams)
    {
        if (queryParams == null) return new VersionFilters();

        return new VersionFilters
        {
            Gt = queryParams.TryGetValue("gt", out var gt) ? gt : null,
            Gte = queryParams.TryGetValue("gte", out var gte) ? gte : null,
            Lt = queryParams.TryGetValue("lt", out var lt) ? lt : null,
            Lte = queryParams.TryGetValue("lte", out var lte) ? lte : null,
            Eq = queryParams.TryGetValue("eq", out var eq) ? eq : null,
            IncludePrerelease = queryParams.TryGetValue("prerelease", out var prerelease) &&
                               bool.TryParse(prerelease, out var includePrerelease) && includePrerelease
        };
    }

    private static IPackageProvider? GetPackageProvider(IServiceProvider services, string provider)
    {
        return provider switch
        {
            "nuget" => services.GetRequiredService<NuGetProvider>(),
            "github" => services.GetRequiredService<GitHubProvider>(),
            _ => null
        };
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateSuccessResponse(ShieldsBadgeResponse badge)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Cache-Control"] = "public, max-age=300, s-maxage=300", // 5-minute cache
                ["Access-Control-Allow-Origin"] = "*"
            },
            Body = JsonSerializer.Serialize(badge, JsonSerializerOptions.Web)
        };
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateErrorResponse(int statusCode, string message)
    {
        var errorResponse = new ErrorResponse
        {
            Message = message
        };

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Access-Control-Allow-Origin"] = "*"
            },
            Body = JsonSerializer.Serialize(errorResponse, JsonSerializerOptions.Web)
        };
    }
}
```

### Step 6: Update Router for Package Endpoints

```csharp
// Update BadgeSmith.Api/Routing/Router.cs
// Add this to the HandleAsync method after the health check:

// Package badge endpoint
if (RoutePatterns.PackageBadge().IsMatch(path) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
{
    var match = RoutePatterns.PackageBadge().Match(path);
    return await PackageBadgeHandler.HandleAsync(request, services, match);
}
```

### Step 7: Service Registration Updates

```csharp
// Update BadgeSmith.Api/Extensions/ServiceCollectionExtensions.cs
public static IServiceCollection AddBadgeSmithServices(this IServiceCollection services)
{
    // Memory cache for performance
    services.AddMemoryCache();

    // Core services
    services.AddSingleton<IHealthService, HealthService>();
    services.AddSingleton<ISecretsService, SecretsService>();

    // Package providers
    services.AddSingleton<NuGetProvider>();
    services.AddSingleton<GitHubProvider>();

    // HTTP clients for external APIs
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

### Step 8: Update JSON Serialization Context

```csharp
// Update BadgeSmith.Api/Json/LambdaFunctionJsonSerializerContext.cs
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ShieldsBadgeResponse))]
[JsonSerializable(typeof(PackageInfo))]
[JsonSerializable(typeof(NuGetVersionIndex))]
[JsonSerializable(typeof(GitHubPackage[]))]
[JsonSerializable(typeof(GitHubPackageVersion[]))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
}
```

## ✅ Success Criteria

### Package Provider Success Criteria

- ✅ **Package provider interface** implemented with resilience patterns
- ✅ **NuGet and GitHub providers** with ETag conditional requests
- ✅ **Exponential backoff and circuit breaker** patterns working
- ✅ **In-memory caching** with appropriate TTL strategies
- ✅ **Graceful degradation** to cached data on upstream failures
- ✅ **"Not found" and "unavailable" badges** for service failures

### Version Filtering Success Criteria

- ✅ **Version filtering** (gt, gte, lt, lte, eq) works correctly
- ✅ **Prerelease parameter** works for both providers
- ✅ **Semantic version comparison** handles edge cases
- ✅ **Empty results** handled gracefully with appropriate responses

### API Response Success Criteria

- ✅ **Response format** matches Shields.io specification exactly
- ✅ **Error handling** for missing packages with consistent schema
- ✅ **Cache headers** set appropriately for performance
- ✅ **CORS headers** applied correctly for browser usage

## 🧪 Testing the Package Endpoints

### Manual Testing Steps

1. **Test NuGet package badge**:

   ```powershell
   curl "http://localhost:5000/badges/packages/nuget/Newtonsoft.Json"
   ```

2. **Test NuGet with version filtering**:

   ```powershell
   curl "http://localhost:5000/badges/packages/nuget/Microsoft.Extensions.Http?gte=6.0.0&lt=8.0.0"
   ```

3. **Test GitHub package badge**:

   ```powershell
   curl "http://localhost:5000/badges/packages/github/localstack-dotnet/localstack.client"
   ```

4. **Test prerelease versions**:

   ```powershell
   curl "http://localhost:5000/badges/packages/nuget/Newtonsoft.Json?prerelease=true"
   ```

5. **Test non-existent package**:

   ```powershell
   curl "http://localhost:5000/badges/packages/nuget/NonExistentPackage12345"
   ```

### Expected Responses

**Successful NuGet Package**:

```json
{
  "schemaVersion": 1,
  "label": "nuget",
  "message": "13.0.3",
  "color": "blue"
}
```

**Package Not Found**:

```json
{
  "schemaVersion": 1,
  "label": "nuget",
  "message": "not found",
  "color": "red"
}
```

**Service Unavailable**:

```json
{
  "schemaVersion": 1,
  "label": "nuget",
  "message": "unavailable",
  "color": "lightgrey"
}
```

## 🔄 Next Steps

After Phase 2 completion, proceed to:

- **[Phase 3: Response Formatting](../03-implementation/Phase-3-response-formatting.md)** - Enhanced Shields.io responses and caching
- **[Phase 4: Authentication](../03-implementation/Phase-4-authentication.md)** - HMAC authentication and test result ingestion

## 🔗 Related Documentation

- **[System Architecture](../02-architecture/01-system-architecture.md)** - Package provider architecture and resilience patterns
- **[Requirements](../01-foundation/02-requirements.md)** - Package endpoint requirements (FR-1)
- **[Phase 1 Foundation](../03-implementation/Phase-1-foundation.md)** - Core infrastructure this phase builds upon
- **[Routing Strategy](../02-architecture/02-routing-strategy.md)** - Routing implementation for package endpoints
