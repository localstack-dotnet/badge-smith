using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Contracts;
using BadgeSmith.Api.Core.Routing.Patterns;

namespace BadgeSmith.Api.Tests.TestHelpers;

internal static class RouteTestBuilder
{
    public static RouteValues CreateRouteValues(string path, int bufferSize = 8)
    {
        var buffer = new (string, int, int)[bufferSize];
        return new RouteValues(path.AsSpan(), buffer.AsSpan());
    }

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

    public static ExactPattern CreateExactPattern(string literal) => new(literal);

    public static TemplatePattern CreateTemplatePattern(string template) => new(template);

    public static RouteDescriptor CreateRouteDescriptor(string name, string method, IRoutePattern pattern)
    {
        return new RouteDescriptor(
            Name: name,
            Method: method,
            HandlerResolver: () => new MockRouteHandler(),
            Pattern: pattern);
    }

    public static RouteResolver CreateRouteResolver(params RouteDescriptor[] routes) => new(routes);

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
