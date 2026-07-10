using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Tools;
using BadgeSmith.Tools.Commands;
using BadgeSmith.Tools.Configuration;
using BadgeSmith.Tools.Infrastructure;
using BadgeSmith.Tools.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Trait("Category", TestCategories.Unit)]
public sealed class BadgeSmithToolInProcessTests
{
    [Fact]
    public void Linked_Tool_Source_Should_Expose_Tool_Exit_Codes()
    {
        Assert.Equal(0, ToolExitCodes.Success);
        Assert.Equal(2, ToolExitCodes.ValidationFailure);
    }

    [Fact]
    public void SpectreConsoleLogger_Should_Write_To_Injected_TestConsole()
    {
        using var console = new TestConsole();
        console.Width(200);
        var logger = new SpectreConsoleLogger(console, TimeProvider.System);

        logger.Info("hello [tool]");

        Assert.Contains("INFO", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hello", console.Output, StringComparison.Ordinal);
        Assert.Contains("[tool]", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BadgeSmithTool_Should_Run_Help_In_Process_When_Using_TestConsole()
    {
        using var console = new TestConsole();
        console.Width(200);

        var exitCode = await BadgeSmithTool.RunAsync(["--help"], console: console);

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.Contains("USAGE", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("badgesmith", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BadgeUpdate_Should_Dry_Run_In_Process_Without_Printing_Hmac_Secret()
    {
        using var console = new TestConsole();
        console.Width(240);

        var exitCode = await BadgeSmithTool.RunAsync([
            "badge", "update",
            "--platform", "Linux",
            "--test-passed", "2",
            "--test-failed", "0",
            "--test-skipped", "1",
            "--test-url-html", "https://example.com/tests",
            "--commit-sha", "abc123",
            "--run-id", "42",
            "--repository", "localstack-dotnet/badge-smith",
            "--server-url", "https://github.com",
            "--base-url", "https://api.example.com/prefix/",
            "--branch", "feature/tools",
            "--dry-run",
        ], builder => builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("BADGESMITH_HMAC_SECRET", "test-secret"),
        ]), console);

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.Contains("DRY RUN", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://api.example.com/prefix/tests/results/linux/localstack-dotnet/badge-smith/feature%2Ftools", console.Output, StringComparison.Ordinal);
        Assert.Contains("https://api.example.com/prefix/badges/tests/linux/localstack-dotnet/badge-smith/feature%2Ftools", console.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestsIngest_Should_Dry_Run_In_Process_Without_Printing_Secret()
    {
        using var console = new TestConsole();
        console.Width(240);
        const string payload = "{\"platform\":\"Linux\",\"passed\":1,\"failed\":0,\"skipped\":0,\"total\":1}";

        var exitCode = await BadgeSmithTool.RunAsync([
            "tests", "ingest",
            "--base-url", "https://example.com",
            "--owner", "LocalStack-DotNet",
            "--repo", "BadgeSmith",
            "--platform", "Linux",
            "--branch", "feature/tools",
            "--secret", "test-secret",
            "--payload", payload,
            "--dry-run",
        ], console: console);

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.Contains("DRY RUN", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.com/tests/results/linux/localstack-dotnet/badgesmith/feature%2Ftools", console.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecretsSeed_Should_Not_Create_Aws_Clients_When_Dry_Run_Is_Set()
    {
        using var console = new TestConsole();
        console.Width(240);
        var configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await File.WriteAllTextAsync(configPath, """
                                                 {
                                                   "secrets": [
                                                     {
                                                       "org_name": "LocalStack-DotNet",
                                                       "name": "package",
                                                       "secret": "ghp_testtoken",
                                                       "type": "Package",
                                                       "description": "Package token"
                                                     }
                                                   ]
                                                 }
                                                 """, TestContext.Current.CancellationToken);

        try
        {
            var exitCode = await BadgeSmithTool.RunAsync([
                "secrets", "seed",
                "--config", configPath,
                "--dry-run",
            ], builder => builder.Services.AddSingleton<IToolAwsClientFactory, ThrowingAwsClientFactory>(), console);

            Assert.Equal(ToolExitCodes.Success, exitCode);
            Assert.Contains("DRY RUN", console.Output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ghp_testtoken", console.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task SecretsSeed_Should_Print_Aws_Options_In_Help()
    {
        using var console = new TestConsole();
        console.Width(240);

        var exitCode = await BadgeSmithTool.RunAsync(["secrets", "seed", "--help"], console: console);

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.Contains("--localstack", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--no-localstack", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--aws-profile", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--aws-region", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--config", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--table-name", console.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--dry-run", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestsIngestSettings_Should_Reject_Each_Required_Value_When_Value_Is_Empty()
    {
        var invalidSettings = new[]
        {
            CreateTestIngestSettings(baseUrl: ""), CreateTestIngestSettings(owner: ""), CreateTestIngestSettings(repo: ""), CreateTestIngestSettings(platform: ""),
            CreateTestIngestSettings(branch: ""), CreateTestIngestSettings(secret: ""),
        };

        foreach (var settings in invalidSettings)
        {
            Assert.False(settings.Validate().Successful);
        }
    }

    [Fact]
    public void TestsIngestSettings_Should_Reject_Payload_And_Payload_File_When_Both_Are_Supplied()
    {
        var settings = CreateTestIngestSettings(payloadFile: "payload.json");

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.NotNull(result.Message);
        Assert.Contains("exactly one", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestsIngestSettings_Should_Reject_Payload_File_When_File_Does_Not_Exist()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var settings = CreateTestIngestSettings(payload: null, payloadFile: missingPath);

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.NotNull(result.Message);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BadgeUpdateSettings_Should_Reject_Each_Required_Value_When_Value_Is_Empty()
    {
        var invalidSettings = new[]
        {
            CreateBadgeUpdateSettings(baseUrl: ""), CreateBadgeUpdateSettings(platform: ""), CreateBadgeUpdateSettings(commitSha: ""), CreateBadgeUpdateSettings(runId: ""),
            CreateBadgeUpdateSettings(repository: ""), CreateBadgeUpdateSettings(serverUrl: ""),
        };

        foreach (var settings in invalidSettings)
        {
            Assert.False(settings.Validate().Successful);
        }
    }

    [Fact]
    public void AwsOptionsResolver_Should_Enable_LocalStack_When_Environment_Config_Is_True()
    {
        var configuration = BuildConfiguration([
            new("LocalStack:UseLocalStack", "true"),
            new("LocalStack:Session:RegionName", "us-west-2"),
            new("AWS:Profile", "live-profile"),
        ]);
        var resolver = new AwsOptionsResolver(configuration);

        var options = resolver.Resolve(new TestAwsSettings());

        Assert.True(options.UseLocalStack);
        Assert.Equal("us-west-2", options.Region);
        Assert.Null(options.Profile);
    }

    [Fact]
    public void AwsOptionsResolver_Should_Let_LocalStack_Command_Option_Win_Over_Profile()
    {
        var configuration = BuildConfiguration([
            new("AWS:Profile", "live-profile"),
            new("AWS:Region", "eu-central-1"),
        ]);
        var resolver = new AwsOptionsResolver(configuration);

        var options = resolver.Resolve(new TestAwsSettings
        {
            AwsProfile = "command-profile",
            AwsRegion = "ap-southeast-2",
            LocalStack = true,
        });

        Assert.True(options.UseLocalStack);
        Assert.Equal("ap-southeast-2", options.Region);
        Assert.Null(options.Profile);
    }

    [Fact]
    public void AwsOptionsResolver_Should_Use_Profile_When_LocalStack_Is_Disabled()
    {
        var configuration = BuildConfiguration([
            new("LocalStack:UseLocalStack", "true"),
            new("LocalStack:Session:RegionName", "us-west-2"),
            new("AWS:Profile", "configured-profile"),
            new("AWS:Region", "eu-central-1"),
        ]);
        var resolver = new AwsOptionsResolver(configuration);

        var options = resolver.Resolve(new TestAwsSettings
        {
            AwsProfile = "command-profile",
            AwsRegion = "ap-southeast-2",
            NoLocalStack = true,
        });

        Assert.False(options.UseLocalStack);
        Assert.Equal("ap-southeast-2", options.Region);
        Assert.Equal("command-profile", options.Profile);
    }

    private static BadgeUpdateSettings CreateBadgeUpdateSettings(
        string baseUrl = "https://api.example.com",
        string platform = "linux",
        string commitSha = "abc123",
        string runId = "42",
        string repository = "localstack-dotnet/badge-smith",
        string serverUrl = "https://github.com")
    {
        return new BadgeUpdateSettings
        {
            BaseUrl = baseUrl,
            Platform = platform,
            CommitSha = commitSha,
            RunId = runId,
            Repository = repository,
            ServerUrl = serverUrl,
        };
    }

    private static TestIngestSettings CreateTestIngestSettings(
        string baseUrl = "https://api.example.com",
        string owner = "localstack-dotnet",
        string repo = "badge-smith",
        string platform = "linux",
        string branch = "main",
        string secret = "test-secret",
        string? payload = "{}",
        string? payloadFile = null)
    {
        return new TestIngestSettings
        {
            BaseUrl = baseUrl,
            Owner = owner,
            Repo = repo,
            Platform = platform,
            Branch = branch,
            Secret = secret,
            Payload = payload,
            PayloadFile = payloadFile,
        };
    }

    private static IConfiguration BuildConfiguration(KeyValuePair<string, string?>[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestAwsSettings : IAwsCommandSettings
    {
        public string? AwsProfile { get; init; }

        public string? AwsRegion { get; init; }

        public bool LocalStack { get; init; }

        public bool NoLocalStack { get; init; }
    }

    private sealed class ThrowingAwsClientFactory : IToolAwsClientFactory
    {
        public ToolAwsClientScope Create(EffectiveAwsOptions options)
        {
            throw new InvalidOperationException("AWS clients should not be created during dry-run.");
        }
    }
}
