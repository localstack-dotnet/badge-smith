namespace BadgeSmith.Api.Routing.Contracts;

internal interface IRouteResolver
{
    /// <summary>
    /// Resolves a route from the incoming HTTP request by matching the path and method against registered routes.
    /// Uses optimized linear search with early HTTP method filtering and source-generated regex matching.
    /// </summary>
    /// <param name="path">The request path to match against route patterns (e.g., "/badges/packages/nuget/MyPackage")</param>
    /// <param name="method">The HTTP method to match (GET, POST, PUT, DELETE, etc.)</param>
    /// <returns>A RouteEntry with populated Match data if found, or null if no route matches the request</returns>
    public RouteEntry? ResolveRoute(string path, string method);

    /// <summary>
    /// Returns the set of HTTP methods supported by routes that match this path.
    /// Useful for CORS preflight (OPTIONS).
    /// </summary>
    /// <param name="path">The request path to match against route patterns (e.g., "/badges/packages/nuget/MyPackage")</param>
    public IReadOnlyList<string> GetAllowedMethods(string path);
}
