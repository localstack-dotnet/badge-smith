using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Tools.Configuration;
using BadgeSmith.Tools.Services;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Collection(ToolAwsClientFactoryTestGroup.Name)]
[Trait("Category", TestCategories.Unit)]
public sealed class ToolAwsClientFactoryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_Should_Construct_DynamoDb_And_SecretsManager_Clients_When_LocalStack_Mode_Is_Set(bool useLocalStack)
    {
        using var environment = AwsEnvironmentVariables.WithDummyCredentials();

        using var scope = new ToolAwsClientFactory().Create(CreateOptions(useLocalStack));

        Assert.NotNull(scope.DynamoDb);
        Assert.NotNull(scope.SecretsManager);
    }

    private static EffectiveAwsOptions CreateOptions(bool useLocalStack)
    {
        return new EffectiveAwsOptions(
            UseLocalStack: useLocalStack,
            Region: "us-east-1",
            Profile: null,
            LocalStackHost: "localhost",
            LocalStackEdgePort: 4566,
            LocalStackUseSsl: false,
            LocalStackUseLegacyPorts: false,
            LocalStackAccessKeyId: "test",
            LocalStackSecretAccessKey: "test",
            LocalStackSessionToken: "test");
    }

    private sealed class AwsEnvironmentVariables : IDisposable
    {
        private readonly string? _accessKeyId;
        private readonly string? _secretAccessKey;
        private readonly string? _sessionToken;
        private readonly string? _metadataDisabled;

        private AwsEnvironmentVariables()
        {
            _accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            _secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            _sessionToken = Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN");
            _metadataDisabled = Environment.GetEnvironmentVariable("AWS_EC2_METADATA_DISABLED");
        }

        public static AwsEnvironmentVariables WithDummyCredentials()
        {
            var variables = new AwsEnvironmentVariables();

            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", "test");
            Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "true");

            return variables;
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", _accessKeyId);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", _secretAccessKey);
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", _sessionToken);
            Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", _metadataDisabled);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public static class ToolAwsClientFactoryTestGroup
{
    public const string Name = "tool-aws-client-factory";
}
