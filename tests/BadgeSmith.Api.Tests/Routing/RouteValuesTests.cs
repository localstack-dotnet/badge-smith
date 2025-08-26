using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Tests.TestHelpers;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing;

/// <summary>
/// Tests for RouteValues ref struct for parameter extraction and manipulation.
/// </summary>
public sealed class RouteValuesTests
{
    [Fact]
    public void Constructor_Should_InitializeCorrectly()
    {
        // Arrange
        const string path = "/test/path";
        var buffer = new (string, int, int)[8];

        // Act
        var values = new RouteValues(path.AsSpan(), buffer.AsSpan());

        // Assert - We can't directly access private fields, but we can test behavior
        var parameters = values.ToImmutableDictionary();
        Assert.Empty(parameters);
    }

    [Fact]
    public void Set_Should_StoreParameterCorrectly()
    {
        // Arrange
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        values.Set("provider", 17, 5); // "nuget" starts at position 17, length 5

        // Assert
        Assert.True(values.TryGetString("provider", out var result));
        Assert.Equal("nuget", result);
    }

    [Fact]
    public void Set_Should_StoreMultipleParametersCorrectly()
    {
        // Arrange
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        values.Set("provider", 17, 5); // "nuget"
        values.Set("package", 23, 15); // "Newtonsoft.Json"

        // Assert
        Assert.True(values.TryGetString("provider", out var provider));
        Assert.True(values.TryGetString("package", out var package));
        Assert.Equal("nuget", provider);
        Assert.Equal("Newtonsoft.Json", package);
    }

    [Fact]
    public void Set_Should_ThrowWhenBufferIsFull()
    {
        // Arrange
        const string path = "/test/path";

        // Act & Assert - Test overflow scenario
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
        // Arrange
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        // Act
        var found = values.TryGetString(paramName, out var actualValue);

        // Assert
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
        // Arrange
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        // Act
        var found = values.TryGetSpan(paramName, out var span);

        // Assert
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
        // Arrange
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        // Act
        var providerFound = values.TryGetSpan("provider", out var providerSpan);
        var packageFound = values.TryGetSpan("package", out var packageSpan);

        // Assert
        Assert.True(providerFound);
        Assert.True(packageFound);
        Assert.Equal("nuget", providerSpan.ToString());
        Assert.Equal("Newtonsoft.Json", packageSpan.ToString());
    }

    [Fact]
    public void ToImmutableDictionary_Should_ReturnEmptyForNoParameters()
    {
        // Arrange
        const string path = "/health";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act
        var dictionary = values.ToImmutableDictionary();

        // Assert
        Assert.Empty(dictionary);
    }

    [Fact]
    public void ToImmutableDictionary_Should_ReturnAllParameters()
    {
        // Arrange
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        // Act
        var dictionary = values.ToImmutableDictionary();

        // Assert
        Assert.Equal(2, dictionary.Count);
        Assert.Equal("nuget", dictionary["provider"]);
        Assert.Equal("Newtonsoft.Json", dictionary["package"]);
    }

    [Fact]
    public void ToImmutableDictionary_Should_UseCaseInsensitiveKeys()
    {
        // Arrange
        const string path = "/badges/packages/nuget/Newtonsoft.Json";
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path,
            ("provider", 17, 5), // "nuget"
            ("package", 23, 15)); // "Newtonsoft.Json"

        // Act
        var dictionary = values.ToImmutableDictionary();

        // Assert
        Assert.True(dictionary.ContainsKey("provider"));
        Assert.True(dictionary.ContainsKey("PROVIDER")); // Case insensitive
        Assert.True(dictionary.ContainsKey("Provider")); // Case insensitive
        Assert.True(dictionary.ContainsKey("package"));
        Assert.True(dictionary.ContainsKey("PACKAGE")); // Case insensitive
    }

    [Fact]
    public void ToImmutableDictionary_Should_OverwriteDuplicateKeys()
    {
        // Arrange
        const string path = "/test/value1/value2";
        var values = RouteTestBuilder.CreateRouteValues(path);

        // Act - Set the same key twice
        values.Set("key", 6, 6); // "value1"
        values.Set("key", 13, 6); // "value2" (should overwrite)

        var dictionary = values.ToImmutableDictionary();

        // Assert
        Assert.Single(dictionary);
        Assert.Equal("value2", dictionary["key"]);
    }

    [Theory]
    [InlineData("/badges/packages/nuget/Package.With.Dots", "package", "Package.With.Dots")]
    [InlineData("/badges/packages/nuget/Package-With-Dashes", "package", "Package-With-Dashes")]
    [InlineData("/badges/packages/nuget/Package_With_Underscores", "package", "Package_With_Underscores")]
    [InlineData("/badges/tests/linux/owner/repo/feature%2Fawesome", "branch", "feature%2Fawesome")]
    [InlineData("/badges/tests/linux/owner/repo/release%2F8.0", "branch", "release%2F8.0")]
    public void Parameters_Should_HandleSpecialCharacters(string path, string paramName, string expectedValue)
    {
        // Arrange - We need to calculate positions dynamically for these tests
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
            values.Set("branch", branchStart, expectedValue.Length);
        }

        // Act
        var found = values.TryGetString(paramName, out var actualValue);

        // Assert
        Assert.True(found);
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void Parameters_Should_HandleEmptyValues()
    {
        // Arrange
        const string path = "/badges/packages//package"; // Empty provider
        var values = RouteTestBuilder.CreateRouteValues(path);
        values.Set("provider", 17, 0); // Empty string
        values.Set("package", 18, 7); // "package"

        // Act
        var providerFound = values.TryGetString("provider", out var provider);
        var packageFound = values.TryGetString("package", out var package);

        // Assert
        Assert.True(providerFound);
        Assert.True(packageFound);
        Assert.Equal("", provider);
        Assert.Equal("package", package);
    }

    [Theory]
    [MemberData(nameof(GetRealWorldParameterData))]
    public void Parameters_Should_HandleRealWorldScenarios(string path, (string key, int start, int length)[] parameters, IDictionary<string, string> expectedValues)
    {
        // Arrange
        var values = RouteTestBuilder.CreateRouteValuesWithParameters(path, parameters);

        // Act & Assert
        foreach (var (expectedKey, expectedValue) in expectedValues)
        {
            Assert.True(values.TryGetString(expectedKey, out var actualValue), $"Parameter '{expectedKey}' not found");
            Assert.Equal(expectedValue, actualValue);
        }

        var dictionary = values.ToImmutableDictionary();
        Assert.Equal(expectedValues.Count, dictionary.Count);
    }

    public static IEnumerable<object[]> GetRealWorldParameterData()
    {
        // NuGet package scenario
        yield return
        [
            "/badges/packages/nuget/Newtonsoft.Json", new[] { ("provider", 17, 5), ("package", 23, 15) },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "Newtonsoft.Json",
            },
        ];

        // GitHub package scenario
        yield return
        [
            "/badges/packages/github/localstack-dotnet/localstack.client", new[] { ("provider", 17, 6), ("org", 24, 18), ("package", 43, 16) },
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
            "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main",
            new[] { ("platform", 15, 5), ("owner", 21, 18), ("repo", 40, 30), ("branch", 71, 4) }, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
            "/badges/tests/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge",
            new[] { ("platform", 15, 5), ("owner", 21, 18), ("repo", 40, 16), ("branch", 57, 23) }, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "localstack.client",
                ["branch"] = "feature%2Fawesome-badge",
            },
        ];

        // Single parameter scenario
        yield return ["/health", Array.Empty<(string, int, int)>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)];

        // Complex package name scenario
        yield return
        [
            "/badges/packages/nuget/Microsoft.Extensions.DependencyInjection.Abstractions", new[] { ("provider", 17, 5), ("package", 23, 52) },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "Microsoft.Extensions.DependencyInjection.Abstractions",
            },
        ];
    }
}
