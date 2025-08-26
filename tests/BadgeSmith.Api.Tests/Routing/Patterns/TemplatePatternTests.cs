using BadgeSmith.Api.Routing.Patterns;
using BadgeSmith.Api.Tests.TestHelpers;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing.Patterns;

/// <summary>
/// Tests for TemplatePattern route matching with realistic badge URLs.
/// </summary>
public sealed class TemplatePatternTests
{
    [Fact]
    public void Constructor_Should_ParseTemplateCorrectly()
    {
        // Arrange & Act
        var pattern = new TemplatePattern("/badges/packages/{provider}/{package}");

        // Assert
        Assert.NotNull(pattern);
    }

    [Theory]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget/Newtonsoft.Json", true)]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget/Microsoft.Extensions.Http", true)]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/github/AutoMapper", true)]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages", false)]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget", false)]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget/package/extra", false)]
    [InlineData("/badges/packages/{provider}/{package}", "/different/path/nuget/package", false)]
    public void TryMatch_Should_HandleBasicTemplateMatching(string template, string path, bool expectedMatch)
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.Equal(expectedMatch, result);
    }

    [Theory]
    [InlineData("/badges/packages/nuget/Newtonsoft.Json", "provider", "nuget")]
    [InlineData("/badges/packages/nuget/Newtonsoft.Json", "package", "Newtonsoft.Json")]
    [InlineData("/badges/packages/github/localstack-dotnet", "provider", "github")]
    [InlineData("/badges/packages/github/localstack-dotnet", "package", "localstack-dotnet")]
    public void TryMatch_Should_ExtractParametersCorrectly(string path, string parameterName, string expectedValue)
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedValue, values.GetParameterValue(parameterName));
    }

    [Fact]
    public void TryMatch_Should_ExtractAllParametersFromNuGetUrl()
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("nuget", parameters["provider"]);
        Assert.Equal("Newtonsoft.Json", parameters["package"]);
    }

    [Fact]
    public void TryMatch_Should_ExtractAllParametersFromGitHubUrl()
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{org}/{package}");
        const string path = "/badges/packages/github/localstack-dotnet/localstack.client";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Equal(3, parameters.Count);
        Assert.Equal("github", parameters["provider"]);
        Assert.Equal("localstack-dotnet", parameters["org"]);
        Assert.Equal("localstack.client", parameters["package"]);
    }

    [Fact]
    public void TryMatch_Should_ExtractAllParametersFromTestUrl()
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}");
        const string path = "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Equal(4, parameters.Count);
        Assert.Equal("linux", parameters["platform"]);
        Assert.Equal("localstack-dotnet", parameters["owner"]);
        Assert.Equal("dotnet-aspire-for-localstack", parameters["repo"]);
        Assert.Equal("main", parameters["branch"]);
    }

    [Theory]
    [InlineData("/badges/tests/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge", "feature%2Fawesome-badge")]
    [InlineData("/badges/tests/linux/dotnet/aspnetcore/release%2F8.0", "release%2F8.0")]
    [InlineData("/badges/tests/windows/myorg/myrepo/hotfix%2Fbug-123", "hotfix%2Fbug-123")]
    public void TryMatch_Should_HandleUrlEncodedBranches(string path, string expectedBranch)
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}");
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedBranch, values.GetParameterValue("branch"));
    }

    [Theory]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/NUGET/PACKAGE", "NUGET", "PACKAGE")]
    [InlineData("/badges/tests/{platform}/{owner}/{repo}/{branch}", "/badges/tests/LINUX/OWNER/REPO/BRANCH", "LINUX", "OWNER")]
    public void TryMatch_Should_PreserveCaseInParameterValues(string template, string path, string expectedParam1, string expectedParam2)
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Contains(expectedParam1, parameters.Values);
        Assert.Contains(expectedParam2, parameters.Values);
    }

    [Theory]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages//package")]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/provider/")]
    [InlineData("/badges/tests/{platform}/{owner}/{repo}/{branch}", "/badges/tests///repo/branch")]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget/Package.With.Dots")]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget/Package-With-Dashes")]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget/Package_With_Underscores")]
    [InlineData("/badges/tests/{platform}/{owner}/{repo}/{branch}", "/badges/tests/linux/owner-with-dashes/repo.with.dots/branch_with_underscores")]
    public void TryMatch_Should_HandleEmptySegmentsAndSpecialCharacters(string template, string path)
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        // Note: This tests current behavior - we may want to return false for empty segments
        // depending on requirements
        Assert.True(result);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/badges")]
    [InlineData("/badges/packages")]
    [InlineData("/badges/packages/provider")]
    [InlineData("/badges/packages/provider/package/extra/segments")]
    [InlineData("/different/structure/entirely")]
    [InlineData("")]
    public void TryMatch_Should_ReturnFalseForNonMatchingPaths(string path)
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryMatch_Should_HandleLeadingSlashCorrectly()
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        const string pathWithSlash = "/badges/packages/nuget/Newtonsoft.Json";
        const string pathWithoutSlash = "badges/packages/nuget/Newtonsoft.Json";

        var valuesWithSlash = RouteTestBuilder.CreateRouteValues(pathWithSlash);
        var valuesWithoutSlash = RouteTestBuilder.CreateRouteValues(pathWithoutSlash);

        // Act
        var resultWithSlash = pattern.TryMatch(pathWithSlash.AsSpan(), ref valuesWithSlash);
        var resultWithoutSlash = pattern.TryMatch(pathWithoutSlash.AsSpan(), ref valuesWithoutSlash);

        // Assert
        Assert.True(resultWithSlash);
        Assert.True(resultWithoutSlash);

        // Both should extract the same parameters
        Assert.Equal("nuget", valuesWithSlash.GetParameterValue("provider"));
        Assert.Equal("nuget", valuesWithoutSlash.GetParameterValue("provider"));
        Assert.Equal("Newtonsoft.Json", valuesWithSlash.GetParameterValue("package"));
        Assert.Equal("Newtonsoft.Json", valuesWithoutSlash.GetParameterValue("package"));
    }

    [Theory]
    [MemberData(nameof(GetRealWorldTestData))]
    public void TryMatch_Should_HandleRealWorldUrls(string template, string url, bool shouldMatch, IDictionary<string, string> expectedParameters)
    {
        // Arrange
        var pattern = RouteTestBuilder.CreateTemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(url);

        // Act
        var result = pattern.TryMatch(url.AsSpan(), ref values);

        // Assert
        Assert.Equal(shouldMatch, result);

        if (shouldMatch && expectedParameters.Count > 0)
        {
            var actualParameters = values.ToDictionary();
            Assert.Equal(expectedParameters.Count, actualParameters.Count);

            foreach (var (key, expectedValue) in expectedParameters)
            {
                Assert.True(actualParameters.ContainsKey(key), $"Parameter '{key}' not found");
                Assert.Equal(expectedValue, actualParameters[key]);
            }
        }
    }

    public static IEnumerable<object[]> GetRealWorldTestData()
    {
        // NuGet package tests
        yield return
        [
            "/badges/packages/{provider}/{package}",
            "/badges/packages/nuget/Newtonsoft.Json",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["provider"] = "nuget", ["package"] = "Newtonsoft.Json" },
        ];

        yield return
        [
            "/badges/packages/{provider}/{package}",
            "/badges/packages/nuget/Microsoft.Extensions.Http",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["provider"] = "nuget", ["package"] = "Microsoft.Extensions.Http" },
        ];

        // GitHub package tests
        yield return
        [
            "/badges/packages/{provider}/{org}/{package}",
            "/badges/packages/github/localstack-dotnet/localstack.client",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["provider"] = "github", ["org"] = "localstack-dotnet", ["package"] = "localstack.client" },
        ];

        yield return
        [
            "/badges/packages/{provider}/{org}/{package}",
            "/badges/packages/github/microsoft/vscode",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["provider"] = "github", ["org"] = "microsoft", ["package"] = "vscode" },
        ];

        // Test badge tests
        yield return
        [
            "/badges/tests/{platform}/{owner}/{repo}/{branch}",
            "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main",
            true,
            new Dictionary<string, string>
(StringComparer.OrdinalIgnoreCase) {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "dotnet-aspire-for-localstack",
                ["branch"] = "main",
            },
        ];

        yield return
        [
            "/badges/tests/{platform}/{owner}/{repo}/{branch}",
            "/badges/tests/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge",
            true,
            new Dictionary<string, string>
(StringComparer.OrdinalIgnoreCase) {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "localstack.client",
                ["branch"] = "feature%2Fawesome-badge",
            },
        ];

        // Non-matching tests
        yield return
        [
            "/badges/packages/{provider}/{package}",
            "/badges/packages/nuget",
            false,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ];

        yield return
        [
            "/badges/packages/{provider}/{package}",
            "/different/path",
            false,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ];
    }
}
