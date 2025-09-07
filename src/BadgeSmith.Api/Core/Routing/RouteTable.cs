using BadgeSmith.Api.Core.Routing.Patterns;

namespace BadgeSmith.Api.Core.Routing;

internal static class RouteTable
{
    public static RouteDescriptor[] Routes { get; set; } =
    [
        new(
            Name: "Health",
            Method: "GET",
            HandlerResolver: () => ApplicationRegistry.HealthCheckHandler,
            Pattern: new ExactPattern("/health")),

        new(
            Name: "NugetPackageBadge",
            Method: "GET",
            HandlerResolver: () => ApplicationRegistry.NugetPackageBadgeHandler,
            Pattern: new TemplatePattern("/badges/packages/{provider}/{package}")),

        new(
            Name: "GithubPackagesBadge",
            Method: "GET",
            HandlerResolver: () => ApplicationRegistry.GithubPackagesBadgeHandler,
            Pattern: new TemplatePattern("/badges/packages/{provider}/{org}/{package}")),

        new(
            Name: "TestsBadge",
            Method: "GET",
            HandlerResolver: () => ApplicationRegistry.TestResultsBadgeHandler,
            Pattern: new TemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}")),

        new(
            Name: "TestIngestion",
            Method: "POST",
            HandlerResolver: () => ApplicationRegistry.TestResultIngestionHandler,
            Pattern: new TemplatePattern("/tests/results/{platform}/{owner}/{repo}/{branch}")),

        new(
            Name: "BadgeRedirect",
            Method: "GET",
            HandlerResolver: () => ApplicationRegistry.TestResultRedirectionHandler,
            Pattern: new TemplatePattern("/redirect/test-results/{platform}/{owner}/{repo}/{branch}")),
    ];
}
