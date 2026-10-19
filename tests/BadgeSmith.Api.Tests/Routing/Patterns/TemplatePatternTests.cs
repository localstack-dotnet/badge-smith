using System.Diagnostics.CodeAnalysis;
using BadgeSmith.Api.Core.Routing.Patterns;
using BadgeSmith.Api.Tests.TestHelpers;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing.Patterns;

[Trait("Category", TestCategories.Unit)]
public sealed class TemplatePatternTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData(null, "/x")]
    [InlineData("", "")]
    [InlineData("", "/x")]
    [InlineData("/", "/")]
    [InlineData("/", "x")]
    [InlineData("///", "")]
    [InlineData("///", "/x")]
    public void TryMatch_Should_Preserve_Null_Key_When_Template_Is_Null_Empty_Or_Slashes(string? template, string path)
    {
        // Null is an intentional runtime witness for the constructor's existing AsSpan behavior.
        var pattern = new TemplatePattern(template!);
        var values = RouteTestBuilder.CreateRouteValues(path);

        Assert.True(pattern.TryMatch(path.AsSpan(), ref values));
        Assert.False(values.TryGetString("unrelated", out _));
        Assert.True(values.TryGetSpan(null!, out var raw));
        Assert.Equal(path.TrimStart('/'), raw.ToString());
        Assert.Throws<ArgumentNullException>(() => MatchAndMaterialize(template, path));
    }

    [Theory]
    [InlineData("/a/", "/a/", "")]
    [InlineData("/a/", "/a/x", "x")]
    [InlineData("/a/", "/a/x/", "x")]
    [InlineData("/a//b", "/a//b", "")]
    [InlineData("/a//b", "/a/x/b", "x")]
    [InlineData("/a//{x}", "/a//v", "")]
    [InlineData("/a//{x}", "/a/anything/v", "anything")]
    public void TryMatch_Should_Preserve_Null_Key_When_Template_Has_Empty_Segments(string template, string path, string expectedRaw)
    {
        var pattern = new TemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        Assert.True(pattern.TryMatch(path.AsSpan(), ref values));
        Assert.True(values.TryGetSpan(null!, out var raw));
        Assert.Equal(expectedRaw, raw.ToString());
        if (template.EndsWith("{x}", StringComparison.Ordinal))
        {
            Assert.True(values.TryGetString("x", out var named));
            Assert.Equal("v", named);
        }

        Assert.Throws<ArgumentNullException>(() => MatchAndMaterialize(template, path));
    }

    [Theory]
    [InlineData("/a/", "/a")]
    [InlineData("/a/", "/a/x/y")]
    [InlineData("/a//b", "/a/b")]
    [InlineData("/a//{x}", "/a/v")]
    [InlineData("a/{x}", "//a/v")]
    [InlineData("/{branch}", "/feature/fix")]
    [InlineData("/a%2Fb/{x}", "/a/b/v")]
    public void TryMatch_Should_Reject_Different_Segment_Count_When_Template_Is_Constructed(string template, string path)
    {
        var pattern = new TemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        Assert.False(pattern.TryMatch(path.AsSpan(), ref values));
    }

    [Theory]
    [InlineData("a/{x}", "/a/v")]
    [InlineData("/a/{x}", "a/v")]
    [InlineData("///a/{x}", "/a/v")]
    [InlineData("////a/{x}", "a/v")]
    public void TryMatch_Should_Preserve_Named_Value_When_Template_Has_Leading_Slashes(string template, string path)
    {
        var parameters = MatchAndMaterialize(template, path);

        Assert.Single(parameters);
        Assert.Equal("v", parameters["x"]);
    }

    [Theory]
    [InlineData("/{")]
    [InlineData("/}")]
    [InlineData("/{x")]
    [InlineData("/x}")]
    [InlineData("/{x}tail")]
    [InlineData("/prefix{x}")]
    [InlineData("/a{b}c")]
    public void TryMatch_Should_Preserve_Literal_Braces_When_Template_Is_Not_Enclosed(string template)
    {
        var pattern = new TemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(template);
        var differentPath = template + "different";
        var differentValues = RouteTestBuilder.CreateRouteValues(differentPath);

        Assert.True(pattern.TryMatch(template.AsSpan(), ref values));
        Assert.Empty(values.ToImmutableDictionary());
        Assert.False(pattern.TryMatch(differentPath.AsSpan(), ref differentValues));
    }

    [Theory]
    [InlineData("/{}", "")]
    [InlineData("/{{x}}", "{x}")]
    [InlineData("/{{x}", "{x")]
    [InlineData("/{x}}", "x}")]
    [InlineData("/{a{b}c}", "a{b}c")]
    [InlineData("/{şube}", "şube")]
    public void TryMatch_Should_Strip_Only_Outer_Braces_When_Template_Segment_Is_Enclosed(string template, string expectedKey)
    {
        var parameters = MatchAndMaterialize(template, "/value");

        Assert.Single(parameters);
        Assert.Equal("value", parameters[expectedKey]);
    }

    [Theory]
    [InlineData("/a/{x}", "/A/Value", "x")]
    [InlineData("/İstanbul/雪/{名}", "/İstanbul/雪/Value", "名")]
    [InlineData("/a%2Fb/{x}", "/a%2fb/Value", "x")]
    public void TryMatch_Should_Preserve_Literal_Comparison_And_Value_Case_When_Template_Is_Constructed(string template, string path, string key)
    {
        var parameters = MatchAndMaterialize(template, path);

        Assert.Single(parameters);
        Assert.Equal("Value", parameters[key]);
    }

    [Theory]
    [InlineData("/{x}/{x}")]
    [InlineData("/{x}/{X}")]
    public void TryMatch_Should_Preserve_First_Lookup_And_Last_Dictionary_Value_When_Keys_Repeat(string template)
    {
        const string path = "/first%2Fvalue/last+value";
        var pattern = new TemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        Assert.True(pattern.TryMatch(path.AsSpan(), ref values));
        Assert.True(values.TryGetSpan("X", out var raw));
        Assert.Equal("first%2Fvalue", raw.ToString());
        Assert.True(values.TryGetString("x", out var first));
        Assert.Equal("first/value", first);
        var parameters = values.ToImmutableDictionary();
        Assert.Single(parameters);
        Assert.Equal("last value", parameters["x"]);
    }

    [Theory]
    [InlineData("/feature%2Ffix", "feature%2Ffix", "feature/fix")]
    [InlineData("/a%20b", "a%20b", "a b")]
    [InlineData("/a+b", "a+b", "a b")]
    [InlineData("/a%2Bb", "a%2Bb", "a+b")]
    [InlineData("/a%252Fb", "a%252Fb", "a%2Fb")]
    public void TryMatch_Should_Decode_Only_Materialized_Values_When_Template_Is_Constructed(string path, string expectedRaw, string expectedDecoded)
    {
        var pattern = new TemplatePattern("/{branch}");
        var values = RouteTestBuilder.CreateRouteValues(path);

        Assert.True(pattern.TryMatch(path.AsSpan(), ref values));
        Assert.True(values.TryGetSpan("branch", out var raw));
        Assert.Equal(expectedRaw, raw.ToString());
        Assert.True(values.TryGetString("branch", out var decoded));
        Assert.Equal(expectedDecoded, decoded);
        var parameters = values.ToImmutableDictionary();
        Assert.Single(parameters);
        Assert.Equal(expectedDecoded, parameters["branch"]);
    }

    private static IReadOnlyDictionary<string, string> MatchAndMaterialize(string? template, string path)
    {
        var pattern = new TemplatePattern(template!);
        var values = RouteTestBuilder.CreateRouteValues(path);
        Assert.True(pattern.TryMatch(path.AsSpan(), ref values));
        return values.ToImmutableDictionary();
    }

    [Fact]
    public void Constructor_Should_Parse_Template_Correctly()
    {
        var pattern = new TemplatePattern("/badges/packages/{provider}/{package}");

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
    public void TryMatch_Should_Handle_Basic_Template_Matching(string template, string path, bool expectedMatch)
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.Equal(expectedMatch, result);
    }

    [Theory]
    [InlineData("/badges/packages/nuget/Newtonsoft.Json", "provider", "nuget")]
    [InlineData("/badges/packages/nuget/Newtonsoft.Json", "package", "Newtonsoft.Json")]
    [InlineData("/badges/packages/github/localstack-dotnet", "provider", "github")]
    [InlineData("/badges/packages/github/localstack-dotnet", "package", "localstack-dotnet")]
    public void TryMatch_Should_Extract_Parameters_Correctly(string path, string parameterName, string expectedValue)
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);
        Assert.Equal(expectedValue, values.GetParameterValue(parameterName));
    }

    [Fact]
    public void TryMatch_Should_Extract_All_Parameters_When_Url_Is_NuGet()
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("nuget", parameters["provider"]);
        Assert.Equal("Newtonsoft.Json", parameters["package"]);
    }

    [Fact]
    public void TryMatch_Should_Extract_All_Parameters_When_Url_Is_GitHub()
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{org}/{package}");
        const string path = "/badges/packages/github/localstack-dotnet/localstack.client";
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Equal(3, parameters.Count);
        Assert.Equal("github", parameters["provider"]);
        Assert.Equal("localstack-dotnet", parameters["org"]);
        Assert.Equal("localstack.client", parameters["package"]);
    }

    [Fact]
    public void TryMatch_Should_Extract_All_Parameters_When_Url_Is_Test()
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}");
        const string path = "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main";
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Equal(4, parameters.Count);
        Assert.Equal("linux", parameters["platform"]);
        Assert.Equal("localstack-dotnet", parameters["owner"]);
        Assert.Equal("dotnet-aspire-for-localstack", parameters["repo"]);
        Assert.Equal("main", parameters["branch"]);
    }

    [Theory]
    [InlineData("/badges/tests/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge", "feature/awesome-badge")]
    [InlineData("/badges/tests/linux/dotnet/aspnetcore/release%2F8.0", "release/8.0")]
    [InlineData("/badges/tests/windows/myorg/myrepo/hotfix%2Fbug-123", "hotfix/bug-123")]
    [InlineData("/badges/tests/linux/org/repo/feature%2Fbug%2Dfix", "feature/bug-fix")]
    [InlineData("/badges/tests/windows/org/repo/branch%20with%20spaces", "branch with spaces")]
    [InlineData("/badges/tests/macos/org/repo/release%2F2024%2D01%2D15", "release/2024-01-15")]
    public void TryMatch_Should_Handle_Url_Encoded_Branches(string path, string expectedBranch)
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/tests/{platform}/{owner}/{repo}/{branch}");
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);
        Assert.Equal(expectedBranch, values.GetParameterValue("branch"));
    }

    [Theory]
    [InlineData("/badges/packages/nuget/Package%20With%20Spaces", "Package With Spaces")]
    [InlineData("/badges/packages/nuget/Package%2EWith%2EDots", "Package.With.Dots")]
    [InlineData("/badges/packages/nuget/Package%2DWith%2DDashes", "Package-With-Dashes")]
    [InlineData("/badges/packages/nuget/Package%5FWith%5FUnderscores", "Package_With_Underscores")]
    [InlineData("/badges/packages/nuget/Package%2BWith%2BPlus", "Package+With+Plus")]
    [InlineData("/badges/packages/nuget/Package%26With%26Ampersand", "Package&With&Ampersand")]
    [InlineData("/badges/packages/nuget/Package%3DWith%3DEquals", "Package=With=Equals")]
    [InlineData("/badges/packages/nuget/Package%3FWith%3FQuestion", "Package?With?Question")]
    [InlineData("/badges/packages/nuget/Package%23With%23Hash", "Package#With#Hash")]
    [InlineData("/badges/packages/nuget/Microsoft%2EExtensions%2EHttp", "Microsoft.Extensions.Http")]
    public void TryMatch_Should_Handle_Url_Encoded_Package_Names(string path, string expectedPackage)
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);
        Assert.Equal("nuget", values.GetParameterValue("provider"));
        Assert.Equal(expectedPackage, values.GetParameterValue("package"));
    }

    [Theory]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/NUGET/PACKAGE", "NUGET", "PACKAGE")]
    [InlineData("/badges/tests/{platform}/{owner}/{repo}/{branch}", "/badges/tests/LINUX/OWNER/REPO/BRANCH", "LINUX", "OWNER")]
    public void TryMatch_Should_Preserve_Case_When_Reading_Parameter_Values(string template, string path, string expectedParam1, string expectedParam2)
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);

        var parameters = values.ToDictionary();
        Assert.Contains(expectedParam1, parameters.Values, StringComparer.Ordinal);
        Assert.Contains(expectedParam2, parameters.Values, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages//package")]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/provider/")]
    [InlineData("/badges/tests/{platform}/{owner}/{repo}/{branch}", "/badges/tests///repo/branch")]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget/Package.With.Dots")]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget/Package-With-Dashes")]
    [InlineData("/badges/packages/{provider}/{package}", "/badges/packages/nuget/Package_With_Underscores")]
    [InlineData("/badges/tests/{platform}/{owner}/{repo}/{branch}", "/badges/tests/linux/owner-with-dashes/repo.with.dots/branch_with_underscores")]
    public void TryMatch_Should_Handle_Empty_Segments_And_Special_Characters(string template, string path)
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

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
    public void TryMatch_Should_Return_False_When_Path_Does_Not_Match(string path)
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.False(result);
    }

    [Fact]
    public void TryMatch_Should_Handle_Leading_Slash_Correctly()
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern("/badges/packages/{provider}/{package}");
        const string pathWithSlash = "/badges/packages/nuget/Newtonsoft.Json";
        const string pathWithoutSlash = "badges/packages/nuget/Newtonsoft.Json";

        var valuesWithSlash = RouteTestBuilder.CreateRouteValues(pathWithSlash);
        var valuesWithoutSlash = RouteTestBuilder.CreateRouteValues(pathWithoutSlash);

        var resultWithSlash = pattern.TryMatch(pathWithSlash.AsSpan(), ref valuesWithSlash);
        var resultWithoutSlash = pattern.TryMatch(pathWithoutSlash.AsSpan(), ref valuesWithoutSlash);

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
    public void TryMatch_Should_Handle_Real_World_Urls(string template, string url, bool shouldMatch, IDictionary<string, string> expectedParameters)
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(url);

        var result = pattern.TryMatch(url.AsSpan(), ref values);

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

    [SuppressMessage(
        "Design",
        "MA0051:Method is too long",
        Justification = "Keeping the template scenarios in one MemberData source makes the route matrix auditable.")]
    public static IEnumerable<object[]> GetRealWorldTestData()
    {
        yield return
        [
            "/badges/packages/{provider}/{package}",
            "/badges/packages/nuget/Newtonsoft.Json",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "Newtonsoft.Json"
            },
        ];

        yield return
        [
            "/badges/packages/{provider}/{package}",
            "/badges/packages/nuget/Microsoft.Extensions.Http",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "Microsoft.Extensions.Http"
            },
        ];

        // GitHub package tests
        yield return
        [
            "/badges/packages/{provider}/{org}/{package}",
            "/badges/packages/github/localstack-dotnet/localstack.client",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "github",
                ["org"] = "localstack-dotnet",
                ["package"] = "localstack.client"
            },
        ];

        yield return
        [
            "/badges/packages/{provider}/{org}/{package}",
            "/badges/packages/github/microsoft/vscode",
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "github",
                ["org"] = "microsoft",
                ["package"] = "vscode"
            },
        ];

        // Test badge tests
        yield return
        [
            "/badges/tests/{platform}/{owner}/{repo}/{branch}",
            "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main",
            true,
            new Dictionary<string, string>
                (StringComparer.OrdinalIgnoreCase)
                {
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
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "localstack.client",
                ["branch"] = "feature/awesome-badge",
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
