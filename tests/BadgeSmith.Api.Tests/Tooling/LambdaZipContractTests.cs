using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Trait("Category", TestCategories.Unit)]
public sealed class LambdaZipContractTests
{
    [Fact]
    public async Task LambdaZip_Should_Package_One_Executable_Bootstrap_With_Zlib_Deflate_Level_9()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var dockerfile = await File.ReadAllTextAsync(
            Path.Combine(directory.FullName, "src", "BadgeSmith.Api", "Dockerfile"),
            TestContext.Current.CancellationToken);
        var stage = dockerfile.Split("FROM debian:bookworm-slim AS lambda-zip", StringSplitOptions.None)[1]
            .Split("FROM scratch AS export-zip", StringSplitOptions.None)[0];

        Assert.Matches(@"--no-install-recommends\s+python3(?:\s|$)", stage);
        Assert.DoesNotContain("--no-install-recommends zip", stage, StringComparison.Ordinal);
        Assert.DoesNotContain("zip -9", stage, StringComparison.Ordinal);
        Assert.Contains("import zipfile", stage, StringComparison.Ordinal);
        Assert.Contains("ZIP_DEFLATED", stage, StringComparison.Ordinal);
        Assert.Matches(@"compresslevel\s*=\s*9\b", stage);
        Assert.Matches("namelist\\(\\)\\s*!=\\s*\\[\"bootstrap\"\\]", stage);
        Assert.Contains("0o100755", stage, StringComparison.Ordinal);
        Assert.Contains("testzip()", stage, StringComparison.Ordinal);
        Assert.Matches(@"\.compress_type\s*!=\s*zipfile\.ZIP_DEFLATED", stage);
        Assert.Matches(@"\.file_size\s*!=\s*os\.path\.getsize", stage);
    }
}
