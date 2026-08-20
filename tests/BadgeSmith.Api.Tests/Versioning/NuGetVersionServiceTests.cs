using BadgeSmith.Api.Core.Versioning;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Versioning;

[Trait("Category", TestCategories.Unit)]
public sealed class NuGetVersionServiceTests
{
    [Fact]
    public void ParseAndFilterVersions_Should_Select_Maximum_Version_When_Multiple_Valid_Versions_Are_Provided()
    {
        var sut = new NuGetVersionService();
        string[] versions = ["1.9.0", "1.10.0", "1.2.0"];

        var result = sut.ParseAndFilterVersions(versions, versionRange: null, includePrerelease: false);

        AssertSelectedVersion("1.10.0", result);
    }

    [Fact]
    public void ParseAndFilterVersions_Should_Apply_Valid_Range_When_Versions_Are_Inside_And_Outside_Range()
    {
        var sut = new NuGetVersionService();
        string[] versions = ["0.9.0", "1.0.0", "1.9.0", "2.0.0", "10.0.0"];

        var result = sut.ParseAndFilterVersions(versions, "[1.0.0,2.0.0)", includePrerelease: false);

        AssertSelectedVersion("1.9.0", result);
    }

    [Fact]
    public void ParseAndFilterVersions_Should_Return_InvalidVersionRange_When_Version_Range_Cannot_Be_Parsed()
    {
        var sut = new NuGetVersionService();
        string[] versions = ["1.0.0"];

        var result = sut.ParseAndFilterVersions(versions, "not-a-range", includePrerelease: false);

        Assert.True(result.IsFailure);
        Assert.True(result.Failure.IsT0);
        var failure = result.Failure.AsT0;
        Assert.Equal("PACKAGE_RANGE_INVALID", failure.Code);
        Assert.Equal("versionRange", failure.PropertyName);
        Assert.Contains("not-a-range", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseAndFilterVersions_Should_Exclude_Prerelease_Versions_When_Prerelease_Is_Not_Included()
    {
        var sut = new NuGetVersionService();
        string[] versions = ["1.9.0", "2.0.0-beta.1"];

        var result = sut.ParseAndFilterVersions(versions, versionRange: null, includePrerelease: false);

        AssertSelectedVersion("1.9.0", result);
    }

    [Fact]
    public void ParseAndFilterVersions_Should_Include_Prerelease_Versions_When_Prerelease_Is_Included()
    {
        var sut = new NuGetVersionService();
        string[] versions = ["1.9.0", "2.0.0-alpha.1", "2.0.0-beta.1"];

        var result = sut.ParseAndFilterVersions(versions, versionRange: null, includePrerelease: true);

        AssertSelectedVersion("2.0.0-beta.1", result);
    }

    [Fact]
    public void ParseAndFilterVersions_Should_Ignore_Unparseable_Versions_When_Valid_Version_Remains()
    {
        var sut = new NuGetVersionService();
        string[] versions = ["not-a-version", "1.2.3", "also-not-a-version"];

        var result = sut.ParseAndFilterVersions(versions, versionRange: null, includePrerelease: false);

        AssertSelectedVersion("1.2.3", result);
    }

    [Fact]
    public void ParseAndFilterVersions_Should_Return_LastVersionNotFound_When_No_Version_Matches_Criteria()
    {
        var sut = new NuGetVersionService();
        string[] versions = ["not-a-version", "0.9.0", "1.5.0-beta.1", "2.0.0"];

        var result = sut.ParseAndFilterVersions(versions, "[1.0.0,2.0.0)", includePrerelease: false);

        Assert.True(result.IsFailure);
        Assert.True(result.Failure.IsT1);
        Assert.Equal("The latest version of the package could not be found", result.Failure.AsT1.Reason);
    }

    private static void AssertSelectedVersion(string expected, NuGetVersionResult result)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.NuGetVersion?.ToNormalizedString());
    }
}
