# 🚦 Routing Strategy

*Last Updated: August 20, 2025*

## 🎯 Design Philosophy

BadgeSmith uses a **hybrid routing table + compiled regex approach** that combines the maintainability of centralized route definitions with the maximum performance of source-generated regex patterns.

## 🔍 Current vs Proposed Approach

### ❌ Pure Regex Approach (Current)

```csharp
private static readonly Regex PackageBadge = new(@"^/badges/packages/(?<provider>nuget|github)/(?:(?<org>[^/]+)/)?(?<package>[^/]+)$");
```

**Issues:**

- Scattered route definitions
- Hard to maintain and extend
- No centralized route metadata
- Difficult route precedence management

### ✅ Hybrid Table + Compiled Regex (Proposed)

```csharp
public static partial class RouteTable
{
    [GeneratedRegex(@"^/badges/packages/(?<provider>nuget|github)/(?<package>[^/]+)$")]
    private static partial Regex PackageBadgeRegex();

    private static readonly RouteEntry[] Routes = [
        new("/badges/packages/{provider}/{package}", PackageBadgeRegex(), typeof(PackageBadgeHandler), "GET", RequiresAuth: false),
        // ... more routes
    ];
}
```

## 🏗️ Route Architecture

### Route Entry Structure

```csharp
public record RouteEntry(
    string Template,        // Human-readable route template
    Regex CompiledRegex,   // Source-generated regex for performance
    Type Handler,          // Handler class type
    string Method,         // HTTP method
    bool RequiresAuth,     // Authentication requirement
    Match? Match = null    // Populated during resolution
);
```

### Route Resolution Process

1. **Linear search** through route array (6 routes max = negligible overhead)
2. **Source-generated regex matching** for optimal performance
3. **Handler instantiation** with caching for reuse
4. **Route context creation** with extracted parameters

## 📋 Route Definitions

### Package Badges

```
Template: /badges/packages/{provider}/{package}
Regex: ^/badges/packages/(?<provider>nuget|github)/(?<package>[^/]+)$
Handler: PackageBadgeHandler
Method: GET
Auth: None

Template: /badges/packages/{provider}/{org}/{package}
Regex: ^/badges/packages/(?<provider>nuget|github)/(?<org>[^/]+)/(?<package>[^/]+)$
Handler: PackageBadgeHandler
Method: GET
Auth: None
```

### Test Badges & Ingestion

```
Template: /badges/tests/{platform}/{owner}/{repo}/{branch}
Regex: ^/badges/tests/(?<platform>linux|windows|macos)/(?<owner>[^/]+)/(?<repo>[^/]+)/(?<branch>.+)$
Handler: TestBadgeHandler
Method: GET
Auth: None

Template: /tests/results
Regex: ^/tests/results$
Handler: TestIngestionHandler
Method: POST
Auth: Required
```

### Utility Routes

```
Template: /health
Regex: ^/health$
Handler: HealthHandler
Method: GET
Auth: None

Template: /redirect/test-results/{platform}/{owner}/{repo}/{branch}
Regex: ^/redirect/test-results/(?<platform>linux|windows|macos)/(?<owner>[^/]+)/(?<repo>[^/]+)/(?<branch>.+)$
Handler: TestRedirectHandler
Method: GET
Auth: None
```

## ⚡ Performance Characteristics

### Source-Generated Regex Benefits

- **Compile-time optimization** - no runtime regex compilation
- **Native AOT friendly** - no reflection or dynamic compilation
- **Maximum throughput** - optimized IL generation
- **Predictable performance** - no JIT warmup required

### Route Resolution Performance

```
Operation: Route lookup for 6 routes
Time Complexity: O(n) where n=6
Expected Time: < 1μs for typical badge request
Memory: Zero allocations during lookup
```

## 🛠️ Handler Interface Design

### Base Handler Contract

```csharp
public interface IRouteHandler
{
    Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        RouteContext context,
        CancellationToken cancellationToken = default);
}

public record RouteContext(
    APIGatewayHttpApiV2ProxyRequest Request,
    ILambdaContext LambdaContext,
    Match RouteMatch,
    IServiceProvider Services
);
```

### Handler Implementation Pattern

```csharp
public class PackageBadgeHandler : IRouteHandler
{
    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        RouteContext context, CancellationToken cancellationToken = default)
    {
        // Extract route parameters
        var provider = context.RouteMatch.Groups["provider"].Value;
        var package = context.RouteMatch.Groups["package"].Value;

        // Business logic implementation
        // ...

        return CreateBadgeResponse(result);
    }
}
```

## 🎯 Key Design Decisions

### 1. Route Ordering Strategy

**Decision**: Array-based route definitions with explicit ordering
**Rationale**: More specific routes placed first, ensures correct precedence
**Example**: `/badges/packages/{provider}/{org}/{package}` before `/badges/packages/{provider}/{package}`

### 2. Source Generation Over Runtime Compilation

**Decision**: Use `[GeneratedRegex]` attributes for all route patterns
**Rationale**: Maximum performance, AOT compatibility, compile-time validation
**Trade-off**: Slightly more code, but significantly better runtime characteristics

### 3. Handler Caching Strategy

**Decision**: Simple dictionary-based handler cache with lazy instantiation
**Rationale**: Minimize cold start impact while reusing handlers for warm invocations
**Memory Impact**: ~6 handler instances cached, negligible memory overhead

### 4. Route Context Design

**Decision**: Immutable record with all necessary request context
**Rationale**: Clean separation of concerns, testable handler interfaces
**Benefits**: Easy mocking, clear dependencies, functional programming patterns

## 🚀 Implementation Benefits

### Development Experience

- **Centralized route management** - all routes defined in one place
- **Type-safe handlers** - compile-time handler validation
- **Clear route templates** - human-readable route documentation
- **Easy testing** - mockable route context and handlers

### Runtime Performance

- **Sub-microsecond route resolution** for typical requests
- **Zero-allocation lookups** during steady state
- **Optimal regex performance** with source generation
- **Minimal cold start overhead** with cached handlers

### Maintainability

- **Simple route addition** - add entry to route array
- **Clear route precedence** - explicit ordering in array
- **Handler isolation** - each route type has dedicated handler
- **Testable components** - easy unit testing of individual handlers

## 🔗 Related Documentation

- **[System Architecture](01-system-architecture.md)** - Overall system design context
- **[Phase 1 Foundation](../03-implementation/Phase-1-foundation.md)** - Implementation details
- **[Security Design](03-security-design.md)** - Authentication integration with routing
