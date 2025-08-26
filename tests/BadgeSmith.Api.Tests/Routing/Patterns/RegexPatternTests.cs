using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using BadgeSmith.Api.Routing.Patterns;
using BadgeSmith.Api.Tests.TestHelpers;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing.Patterns;

/// <summary>
/// Tests for RegexPattern route matching with named groups.
/// </summary>
public sealed class RegexPatternTests
{
    [Fact]
    public void Constructor_Should_InitializeWithSimpleRegex()
    {
        // Arrange & Act
        var pattern = new RegexPattern(() => new Regex("^/health$", RegexOptions.IgnoreCase | RegexOptions.Compiled));

        // Assert
        Assert.NotNull(pattern);
    }

    [Fact]
    public void Constructor_Should_InitializeWithNamedGroups()
    {
        // Arrange & Act
        var pattern = new RegexPattern(() => new Regex(@"^/badges/packages/(?<provider>\w+)/(?<package>[\w.-]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled));

        // Assert
        Assert.NotNull(pattern);
    }

    [Theory]
    [InlineData("/health", true)]
    [InlineData("/Health", true)]  // Case insensitive
    [InlineData("/HEALTH", true)]  // Case insensitive
    [InlineData("/healthz", false)]
    [InlineData("/health/check", false)]
    [InlineData("health", false)]  // Missing slash
    [InlineData("", false)]
    public void TryMatch_Should_HandleSimpleRegexPatterns(string path, bool expectedMatch)
    {
        // Arrange
        var pattern = new RegexPattern(() => new Regex("^/health$", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.Equal(expectedMatch, result);
    }

    [Theory]
    [InlineData("/badges/packages/nuget/Newtonsoft.Json", true, "nuget", "Newtonsoft.Json")]
    [InlineData("/badges/packages/github/AutoMapper", true, "github", "AutoMapper")]
    [InlineData("/badges/packages/npm/lodash", true, "npm", "lodash")]
    [InlineData("/badges/packages/pypi/requests", true, "pypi", "requests")]
    [InlineData("/badges/packages/", false, null, null)]
    [InlineData("/badges/packages/nuget", false, null, null)]
    [InlineData("/different/path", false, null, null)]
    public void TryMatch_Should_ExtractNamedGroupsCorrectly(string path, bool expectedMatch, string? expectedProvider, string? expectedPackage)
    {
        // Arrange
        var pattern = new RegexPattern(() => new Regex(@"^/badges/packages/(?<provider>\w+)/(?<package>[\w.-]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.Equal(expectedMatch, result);

        if (expectedMatch)
        {
            Assert.Equal(expectedProvider, values.GetParameterValue("provider"));
            Assert.Equal(expectedPackage, values.GetParameterValue("package"));
        }
    }

    [Theory]
    [InlineData("/badges/tests/linux/owner/repo/main", "linux", "owner", "repo", "main")]
    [InlineData("/badges/tests/windows/microsoft/vscode/release-1.2", "windows", "microsoft", "vscode", "release-1.2")]
    [InlineData("/badges/tests/macos/facebook/react/feature_branch", "macos", "facebook", "react", "feature_branch")]
    public void TryMatch_Should_ExtractMultipleNamedGroups(string path, string expectedPlatform, string expectedOwner, string expectedRepo, string expectedBranch)
    {
        // Arrange
        var pattern = new RegexPattern(() => new Regex(
            @"^/badges/tests/(?<platform>\w+)/(?<owner>[\w-]+)/(?<repo>[\w.-]+)/(?<branch>[\w.-]+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled));
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Equal(4, parameters.Count);
        Assert.Equal(expectedPlatform, parameters["platform"]);
        Assert.Equal(expectedOwner, parameters["owner"]);
        Assert.Equal(expectedRepo, parameters["repo"]);
        Assert.Equal(expectedBranch, parameters["branch"]);
    }

    [Fact]
    public void TryMatch_Should_HandleOptionalGroups()
    {
        // Arrange - Pattern with optional org group for both nuget (no org) and github (has org)
        var pattern = new RegexPattern(() => new Regex(
            @"^/badges/packages/(?<provider>\w+)(?:/(?<org>[\w-]+))?/(?<package>[\w.-]+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled));

        // Test NuGet (no org)
        const string nugetPath = "/badges/packages/nuget/Newtonsoft.Json";
        var nugetValues = RouteTestBuilder.CreateRouteValues(nugetPath);

        // Test GitHub (has org)
        const string githubPath = "/badges/packages/github/localstack-dotnet/localstack.client";
        var githubValues = RouteTestBuilder.CreateRouteValues(githubPath);

        // Act
        var nugetResult = pattern.TryMatch(nugetPath.AsSpan(), ref nugetValues);
        var githubResult = pattern.TryMatch(githubPath.AsSpan(), ref githubValues);

        // Assert
        Assert.True(nugetResult);
        Assert.True(githubResult);

        // NuGet should have provider and package, but no org
        var nugetParams = nugetValues.ToDictionary();
        Assert.Equal("nuget", nugetParams["provider"]);
        Assert.Equal("Newtonsoft.Json", nugetParams["package"]);
        Assert.False(nugetParams.ContainsKey("org") && !string.IsNullOrEmpty(nugetParams["org"]));

        // GitHub should have provider, org, and package
        var githubParams = githubValues.ToDictionary();
        Assert.Equal("github", githubParams["provider"]);
        Assert.Equal("localstack-dotnet", githubParams["org"]);
        Assert.Equal("localstack.client", githubParams["package"]);
    }

    [Theory]
    [InlineData(@"^/badges/packages/(?<provider>\w+)/(?<package>[\w.-]+)$", "/badges/packages/nuget/Package.With.Dots")]
    [InlineData(@"^/badges/packages/(?<provider>\w+)/(?<package>[\w.-]+)$", "/badges/packages/nuget/Package-With-Dashes")]
    [InlineData(@"^/badges/tests/(?<platform>\w+)/(?<owner>[\w-]+)/(?<repo>[\w.-]+)/(?<branch>[\w.-_]+)$", "/badges/tests/linux/owner-name/repo.name/branch_name")]
    public void TryMatch_Should_HandleSpecialCharactersInGroups(string regexPattern, string path)
    {
        // Arrange
        var pattern = new RegexPattern(() => new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryMatch_Should_HandleComplexPackageNames()
    {
        // Arrange
        var pattern = new RegexPattern(() => new Regex(
            @"^/badges/packages/(?<provider>\w+)/(?<package>[\w.-]+(?:\.[\w.-]+)*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled));

        var complexPaths = new[]
        {
            "/badges/packages/nuget/Microsoft.Extensions.DependencyInjection",
            "/badges/packages/nuget/Microsoft.Extensions.DependencyInjection.Abstractions",
            "/badges/packages/nuget/System.Text.Json",
            "/badges/packages/github/some.complex.package-name",
        };

        foreach (var path in complexPaths)
        {
            var values = RouteTestBuilder.CreateRouteValues(path);

            // Act
            var result = pattern.TryMatch(path.AsSpan(), ref values);

            // Assert
            Assert.True(result, $"Path {path} should match");
            Assert.True(values.TryGetString("provider", out var provider));
            Assert.True(values.TryGetString("package", out var package));
            Assert.NotNull(provider);
            Assert.NotNull(package);
        }
    }

    [Theory]
    [InlineData("/badges/packages/nuget/Package With Spaces")]  // Spaces not allowed in \w
    [InlineData("/badges/packages/123invalid/package")]         // Provider starting with number
    [InlineData("/badges/packages//package")]                   // Empty provider
    [InlineData("/badges/packages/provider/")]                  // Empty package
    public void TryMatch_Should_RejectInvalidPatterns(string path)
    {
        // Arrange
        var pattern = new RegexPattern(() => new Regex(@"^/badges/packages/(?<provider>\w+)/(?<package>[\w.-]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryMatch_Should_NotSetParametersForUnsuccessfulGroups()
    {
        // Arrange - Pattern with a group that won't match
        var pattern = new RegexPattern(() => new Regex(
            @"^/badges/packages/(?<provider>\w+)/(?<package>[\w.-]+)(?:/(?<version>\d+\.\d+\.\d+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled));

        const string pathWithoutVersion = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValues(pathWithoutVersion);

        // Act
        var result = pattern.TryMatch(pathWithoutVersion.AsSpan(), ref values);

        // Assert
        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Equal("nuget", parameters["provider"]);
        Assert.Equal("Newtonsoft.Json", parameters["package"]);
        // Version group should not be present since it didn't match
        Assert.False(parameters.ContainsKey("version") && !string.IsNullOrEmpty(parameters["version"]));
    }

    [Theory]
    [MemberData(nameof(GetSourceGeneratedRegexData))]
    public void TryMatch_Should_WorkWithSourceGeneratedRegex(Func<Regex> regexFactory, string path, bool expectedMatch, IDictionary<string, string> expectedParameters)
    {
        ArgumentNullException.ThrowIfNull(regexFactory);
        ArgumentNullException.ThrowIfNull(expectedParameters);

        // Arrange
        var pattern = new RegexPattern(regexFactory);
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var result = pattern.TryMatch(path.AsSpan(), ref values);

        // Assert
        Assert.Equal(expectedMatch, result);

        if (expectedMatch && expectedParameters.Count > 0)
        {
            var actualParameters = values.ToDictionary();
            foreach (var (key, expectedValue) in expectedParameters)
            {
                Assert.True(actualParameters.ContainsKey(key), $"Parameter '{key}' not found");
                Assert.Equal(expectedValue, actualParameters[key]);
            }
        }
    }

    [SuppressMessage("Design", "CA1024:Use properties where appropriate")]
    public static IEnumerable<object[]> GetSourceGeneratedRegexData()
    {
        // Simulating source-generated regex patterns that could be used in the future
        yield return
        [
            // Health check pattern
            new Func<Regex>(() => new Regex("^/health$", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            "/health",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ];

        yield return
        [
            // Package pattern with named groups
            new Func<Regex>(() => new Regex(@"^/badges/packages/(?<provider>\w+)/(?<package>[\w.-]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            "/badges/packages/nuget/Newtonsoft.Json",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["provider"] = "nuget", ["package"] = "Newtonsoft.Json" },
        ];

        yield return
        [
            // Test pattern with all parameters
            new Func<Regex>(() => new Regex(@"^/badges/tests/(?<platform>\w+)/(?<owner>[\w-]+)/(?<repo>[\w.-]+)/(?<branch>[\w.-]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "dotnet-aspire-for-localstack",
                ["branch"] = "main",
            },
        ];

        yield return
        [
            // Non-matching pattern
            new Func<Regex>(() => new Regex(@"^/badges/packages/(?<provider>\w+)/(?<package>[\w.-]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            "/different/path",
            false,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ];
    }
}
