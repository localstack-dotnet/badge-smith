using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing;

[Trait("Category", TestCategories.Unit)]
public sealed class RouteTableTests
{
    [Fact]
    public void Routes_Should_Expose_Expected_Descriptors_When_RouteTable_Is_Loaded()
    {
        (string Name, string Method)[] expected =
        [
            ("Health", "GET"),
            ("NugetPackageBadge", "GET"),
            ("GithubPackagesBadge", "GET"),
            ("TestsBadge", "GET"),
            ("TestIngestion", "POST"),
            ("BadgeRedirect", "GET"),
        ];

        var actual = RouteTable.Routes
            .Select(static route => (route.Name, route.Method))
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
