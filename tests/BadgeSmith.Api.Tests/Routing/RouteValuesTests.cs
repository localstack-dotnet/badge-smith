using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Tests.TestHelpers;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing;

public sealed class RouteValuesTests
{
    [Fact]
    public void Constructor_Should_InitializeCorrectly()
    {
        const string path = "/test/path";
        var buffer = new (string, int, int)[8];

        var values = new RouteValues(path.AsSpan(), buffer.AsSpan());

        var parameters = values.ToImmutableDictionary();
        Assert.Empty(parameters);
    }

    [Fact]
    public void Set_Should_StoreParameterCorrectly()
    {
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValues(path);

        values.Set("provider", 17, 5); // "nuget" starts at position 17, length 5

        Assert.True(values.TryGetString("provider", out var result));
        Assert.Equal("nuget", result);
    }

    [Fact]
    public void Set_Should_StoreMultipleParametersCorrectly()
    {
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValues(path);

        values.Set("provider", 17, 5); // "nuget"
        values.Set("package", 23, 15); // "Newtonsoft.Json"

        Assert.True(values.TryGetString("provider", out var provider));
        Assert.True(values.TryGetString("package", out var package));
        Assert.Equal("nuget", provider);
        Assert.Equal("Newtonsoft.Json", package);
    }

    [Fact]
    public void Set_Should_ThrowWhenBufferIsFull()
    {
        const string path = "/test/path";

        var exception = Assert.Throws<InvalidOperationException>(() => CreateFullBufferAndOverflow(path));
        Assert.Equal("RouteValues buffer is full.", exception.Message);
    }

    private static void CreateFullBufferAndOverflow(string path)
    {
        var buffer = new (string, int, int)[2]; // Small buffer
        var values = new RouteValues(path.AsSpan(), buffer.AsSpan());
        values.Set("param1", 0, 4);
        values.Set("param2", 5, 4);
        values.Set("param3", 0, 1); // This should throw
    }

    [Theory]
    [InlineData("provider", true, "nuget")]
    [InlineData("PROVIDER", true, "nuget")] // Case insensitive
    [InlineData("Provider", true, "nuget")] // Case insensitive
    [InlineData("package", true, "Newtonsoft.Json")]
    [InlineData("PACKAGE", true, "Newtonsoft.Json")] // Case insensitive
    [InlineData("nonexistent", false, null)]
    [InlineData("", false, null)]
    public void TryGetString_Should_HandleCaseInsensitiveLookup(string paramName, bool expectedFound, string? expectedValue)
    {
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        var found = values.TryGetString(paramName, out var actualValue);

        Assert.Equal(expectedFound, found);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData("provider", true)]
    [InlineData("PROVIDER", true)] // Case insensitive
    [InlineData("package", true)]
    [InlineData("nonexistent", false)]
    [InlineData("", false)]
    public void TryGetSpan_Should_HandleCaseInsensitiveLookup(string paramName, bool expectedFound)
    {
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        var found = values.TryGetSpan(paramName, out var span);

        Assert.Equal(expectedFound, found);

        if (expectedFound)
        {
            Assert.True(span.Length > 0);
        }
        else
        {
            Assert.True(span.IsEmpty);
        }
    }

    [Fact]
    public void TryGetSpan_Should_ReturnCorrectSpanData()
    {
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        var providerFound = values.TryGetSpan("provider", out var providerSpan);
        var packageFound = values.TryGetSpan("package", out var packageSpan);

        Assert.True(providerFound);
        Assert.True(packageFound);
        Assert.Equal("nuget", providerSpan.ToString());
        Assert.Equal("Newtonsoft.Json", packageSpan.ToString());
    }

    [Fact]
    public void ToImmutableDictionary_Should_ReturnEmptyForNoParameters()
    {
        const string path = "/health";
        var values = RouteTestBuilder.CreateRouteValues(path);

        var dictionary = values.ToImmutableDictionary();

        Assert.Empty(dictionary);
    }

    [Fact]
    public void ToImmutableDictionary_Should_ReturnAllParameters()
    {
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        var dictionary = values.ToImmutableDictionary();

        Assert.Equal(2, dictionary.Count);
        Assert.Equal("nuget", dictionary["provider"]);
        Assert.Equal("Newtonsoft.Json", dictionary["package"]);
    }

    [Fact]
    public void ToImmutableDictionary_Should_UseCaseInsensitiveKeys()
    {
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        var dictionary = values.ToImmutableDictionary();

        Assert.True(dictionary.ContainsKey("provider"));
        Assert.True(dictionary.ContainsKey("PROVIDER")); // Case insensitive
        Assert.True(dictionary.ContainsKey("Provider")); // Case insensitive
        Assert.True(dictionary.ContainsKey("package"));
        Assert.True(dictionary.ContainsKey("PACKAGE")); // Case insensitive
    }

    [Fact]
    public void ToImmutableDictionary_Should_OverwriteDuplicateKeys()
    {
        const string path = "/test/value1/value2";
        var values = RouteTestBuilder.CreateRouteValues(path);

        values.Set("key", 6, 6); // "value1"
        values.Set("key", 13, 6); // "value2" (should overwrite)

        var dictionary = values.ToImmutableDictionary();

        Assert.Single(dictionary);
        Assert.Equal("value2", dictionary["key"]);
    }

    [Theory]
    [InlineData("/badges/packages/nuget/Package.With.Dots", "package", "Package.With.Dots")]
    [InlineData("/badges/packages/nuget/Package-With-Dashes", "package", "Package-With-Dashes")]
    [InlineData("/badges/packages/nuget/Package_With_Underscores", "package", "Package_With_Underscores")]
    [InlineData("/badges/tests/linux/owner/repo/feature%2Fawesome", "branch", "feature/awesome")]
    [InlineData("/badges/tests/linux/owner/repo/release%2F8.0", "branch", "release/8.0")]
    public void Parameters_Should_HandleSpecialCharacters(string path, string paramName, string expectedValue)
    {
        const string provider = "nuget";
        var package = expectedValue;
        var fullPath = paramName == "package"
            ? $"/badges/packages/{provider}/{package}"
            : path;

        var values = RouteTestBuilder.CreateRouteValues(fullPath);

        if (paramName == "package")
        {
            var packageStart = fullPath.LastIndexOf('/') + 1;
            values.Set("provider", 17, provider.Length);
            values.Set("package", packageStart, package.Length);
        }
        else if (paramName == "branch")
        {
            // For branch tests, manually set up the parameters
            values.Set("platform", 15, 5); // "linux"
            values.Set("owner", 21, 5); // "owner"
            values.Set("repo", 27, 4); // "repo"
            var branchStart = fullPath.LastIndexOf('/') + 1;
            var branchLength = fullPath.Length - branchStart; // Use actual URL-encoded length
            values.Set("branch", branchStart, branchLength);
        }

        var found = values.TryGetString(paramName, out var actualValue);

        Assert.True(found);
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void Parameters_Should_HandleEmptyValues()
    {
        const string path = "/badges/packages//package"; // Empty provider
        var values = RouteTestBuilder.CreateRouteValues(path);
        values.Set("provider", 17, 0); // Empty string
        values.Set("package", 18, 7); // "package"

        var providerFound = values.TryGetString("provider", out var provider);
        var packageFound = values.TryGetString("package", out var package);

        Assert.True(providerFound);
        Assert.True(packageFound);
        Assert.Equal("", provider);
        Assert.Equal("package", package);
    }

    [Theory]
    [MemberData(nameof(GetRealWorldTemplateScenarios))]
    public void Parameters_Should_HandleRealWorldScenarios(string template, string path, IDictionary<string, string> expectedValues)
    {
        var pattern = RouteTestBuilder.CreateTemplatePattern(template);
        var values = RouteTestBuilder.CreateRouteValues(path);

        var matched = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(matched, $"Pattern '{template}' should match path '{path}'");

        foreach (var (expectedKey, expectedValue) in expectedValues)
        {
            Assert.True(values.TryGetString(expectedKey, out var actualValue), $"Parameter '{expectedKey}' not found");
            Assert.Equal(expectedValue, actualValue);
        }

        var dictionary = values.ToImmutableDictionary();
        Assert.Equal(expectedValues.Count, dictionary.Count);
    }

    public static IEnumerable<object[]> GetRealWorldTemplateScenarios()
    {
        // NuGet package scenario
        yield return
        [
            "/badges/packages/{provider}/{package}",
            "/badges/packages/nuget/Newtonsoft.Json",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "Newtonsoft.Json",
            },
        ];

        // GitHub package scenario
        yield return
        [
            "/badges/packages/{provider}/{org}/{package}",
            "/badges/packages/github/localstack-dotnet/localstack.client",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "github",
                ["org"] = "localstack-dotnet",
                ["package"] = "localstack.client",
            },
        ];

        // Test badge scenario
        yield return
        [
            "/badges/tests/{platform}/{owner}/{repo}/{branch}",
            "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "dotnet-aspire-for-localstack",
                ["branch"] = "main",
            },
        ];

        // URL encoded branch scenario
        yield return
        [
            "/badges/tests/{platform}/{owner}/{repo}/{branch}",
            "/badges/tests/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "localstack.client",
                ["branch"] = "feature/awesome-badge",
            },
        ];

        // Long NuGet package name scenario
        yield return
        [
            "/badges/packages/{provider}/{package}",
            "/badges/packages/nuget/Microsoft.Extensions.DependencyInjection.Abstractions",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "Microsoft.Extensions.DependencyInjection.Abstractions",
            },
        ];
    }
}
