using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Patterns;

namespace BadgeSmith.Api.Performance.Tests.TestHelpers;

/// <summary>
/// Builder pattern for creating route-related test objects with fluent API.
/// </summary>
internal static class RouteTestBuilder
{
    /// <summary>
    /// Creates a RouteValues instance for testing with the given path and buffer size.
    /// </summary>
    /// <param name="path">The path to parse.</param>
    /// <param name="bufferSize">Size of the parameter buffer (default: 8).</param>
    /// <returns>A RouteValues instance ready for testing.</returns>
    public static RouteValues CreateRouteValues(string path, int bufferSize = 8)
    {
        var buffer = new (string, int, int)[bufferSize];
        return new RouteValues(path.AsSpan(), buffer.AsSpan());
    }

    /// <summary>
    /// Creates a RouteValues instance with pre-populated parameters.
    /// </summary>
    /// <param name="path">The original path.</param>
    /// <param name="parameters">Parameters to set in the RouteValues.</param>
    /// <returns>A RouteValues instance with the specified parameters.</returns>
    public static RouteValues CreateRouteValuesWithParameters(string path, params (string key, int start, int length)[] parameters)
    {
        var buffer = new (string, int, int)[Math.Max(8, parameters.Length)];
        var values = new RouteValues(path.AsSpan(), buffer.AsSpan());

        foreach (var (key, start, length) in parameters)
        {
            values.Set(key, start, length);
        }

        return values;
    }

    /// <summary>
    /// Creates an ExactPattern for testing.
    /// </summary>
    /// <param name="literal">The literal string to match.</param>
    /// <returns>An ExactPattern instance.</returns>
    public static ExactPattern CreateExactPattern(string literal) => new(literal);

    /// <summary>
    /// Creates a TemplatePattern for testing.
    /// </summary>
    /// <param name="template">The template string with {parameter} placeholders.</param>
    /// <returns>A TemplatePattern instance.</returns>
    public static TemplatePattern CreateTemplatePattern(string template) => new(template);

    /// <summary>
    /// Creates a mock RouteDescriptor for testing.
    /// </summary>
    /// <param name="name">The route name.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="requiresAuth">Whether the route requires authentication.</param>
    /// <returns>A RouteDescriptor instance.</returns>
    public static RouteDescriptor CreateRouteDescriptor(
        string name,
        string method,
        IRoutePattern pattern,
        bool requiresAuth = false)
    {
        return new RouteDescriptor(
            Name: name,
            Method: method,
            RequiresAuth: requiresAuth,
            HandlerFactory: _ => new MockRouteHandler(),
            Pattern: pattern);
    }

    /// <summary>
    /// Creates a RouteResolver with the specified routes for testing.
    /// </summary>
    /// <param name="routes">The routes to include in the resolver.</param>
    /// <returns>A RouteResolver instance.</returns>
    public static RouteResolver CreateRouteResolver(params RouteDescriptor[] routes) => new(routes);

    /// <summary>
    /// Mock route handler for testing purposes.
    /// </summary>
    private sealed class MockRouteHandler : IRouteHandler
    {
        public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext routeContext, CancellationToken ct = default)
        {
            var response = new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Body = "Mock response",
            };
            return Task.FromResult(response);
        }
    }
}

/// <summary>
/// Extension methods for easier testing of routing components.
/// </summary>
internal static class RouteTestExtensions
{
    /// <summary>
    /// Tries to extract a parameter value as a string from RouteValues.
    /// </summary>
    /// <param name="values">The RouteValues instance.</param>
    /// <param name="parameterName">The parameter name to extract.</param>
    /// <returns>The parameter value if found, null otherwise.</returns>
    public static string? GetParameterValue(this RouteValues values, string parameterName)
    {
        return values.TryGetString(parameterName, out var value) ? value : null;
    }

    /// <summary>
    /// Converts RouteValues to a dictionary for easy assertion in tests.
    /// </summary>
    /// <param name="values">The RouteValues instance.</param>
    /// <returns>A dictionary representation of the route values.</returns>
    public static Dictionary<string, string> ToDictionary(this RouteValues values)
    {
        var immutableDict = values.ToImmutableDictionary();
        return new Dictionary<string, string>(immutableDict, StringComparer.OrdinalIgnoreCase);
    }
}
