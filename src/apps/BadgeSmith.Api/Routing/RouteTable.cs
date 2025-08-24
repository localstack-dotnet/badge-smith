using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Routing.Patterns;

namespace BadgeSmith.Api.Routing;

internal static class RouteTable
{
    public static RouteDescriptor[] Routes { get; set; } =
    [
        new RouteDescriptor(
            Name: "Health",
            Method: "GET",
            RequiresAuth: false,
            HandlerType: typeof(IHealthCheckHandler),
            Pattern: new ExactPattern("/health")),

        new RouteDescriptor(
            Name: "NugetPackageBadge",
            Method: "GET",
            RequiresAuth: false,
            HandlerType: typeof(INugetPackageBadgeHandler),
            Pattern: new TemplatePattern("/badges/packages/{provider}/{package}")),

        new RouteDescriptor(
            Name: "GithubPackagesBadge",
            Method: "GET",
            RequiresAuth: false,
            HandlerType: typeof(IGithubPackagesBadgeHandler),
            Pattern: new TemplatePattern("/badges/packages/{provider}/{org}/{package}")),

        new RouteDescriptor(
            Name: "TestsBadge",
            Method: "GET",
            RequiresAuth: false,
            HandlerType: typeof(ITestResultsBadgeHandler),
            Pattern: new TemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}")),

        new RouteDescriptor(
            Name: "TestIngestion",
            Method: "POST",
            RequiresAuth: true,
            HandlerType: typeof(ITestResultIngestionHandler),
            Pattern: new ExactPattern("/tests/results")),

        new RouteDescriptor(
            Name: "BadgeRedirect",
            Method: "GET",
            RequiresAuth: false,
            HandlerType: typeof(ITestResultRedirectionHandler),
            Pattern: new TemplatePattern("/redirect/test-results/{platform}/{owner}/{repo}/{branch}")),
    ];
}
