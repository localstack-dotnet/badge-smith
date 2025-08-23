using System.Text.RegularExpressions;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace BadgeSmith.Api.Routing;

/// <summary>
/// Immutable context object containing all request information and route parameters passed to route handlers.
/// Provides convenient access to the original API Gateway request and extracted route parameters
/// from the matched URL pattern for request processing.
/// </summary>
/// <param name="Request">The original API Gateway HTTP API v2 proxy request containing headers, body, query parameters, path, and HTTP method.</param>
/// <param name="LambdaContext"> The Lambda context containing logger, request ID, and execution environment.</param>
/// <param name="RouteMatch">Regex match result containing captured route parameters and named groups extracted from the matched URL pattern.</param>
internal record RouteContext(APIGatewayHttpApiV2ProxyRequest Request, ILambdaContext LambdaContext, Match RouteMatch)
{
    /// <summary>
    /// Attempts to retrieve a route parameter value by name from the matched URL pattern.
    /// </summary>
    /// <param name="name">The name of the route parameter to retrieve (e.g., "provider", "package", "owner").</param>
    /// <param name="value">When this method returns, contains the parameter value if found; otherwise, an empty string.</param>
    /// <returns><see langword="true"/> if the parameter exists and has a non-empty value; otherwise, <see langword="false"/>.</returns>
    /// <example>
    /// <code>
    /// if (context.TryGetRouteValue("provider", out var provider))
    /// {
    ///     // Use the provider value (e.g., "nuget", "github")
    /// }
    /// </code>
    /// </example>
    public bool TryGetRouteValue(string name, out string value)
    {
        var routeGroup = RouteMatch.Groups[name];
        if (routeGroup is { Success: true, Value.Length: > 0 })
        {
            value = routeGroup.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
