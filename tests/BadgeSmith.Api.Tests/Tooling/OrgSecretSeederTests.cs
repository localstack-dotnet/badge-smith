using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Tools.Infrastructure;
using Moq;
using Spectre.Console.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Trait("Category", TestCategories.Unit)]
public sealed class OrgSecretSeederTests
{
    public static TheoryData<string> InvalidKeyNames =>
    [
        "invalid name",
        new string('a', 512),
    ];

    [Theory]
    [MemberData(nameof(InvalidKeyNames))]
    public async Task SeedAsync_Should_Validate_All_Entries_Before_Aws_Mutation_When_Config_Contains_Invalid_Name(string invalidKeyName)
    {
        var configPath = await WriteConfigAsync($$"""
                                                  {
                                                    "secrets": [
                                                      {
                                                        "org_name": "valid-org",
                                                        "name": "package",
                                                        "secret": "valid-secret",
                                                        "type": "Package",
                                                        "description": "Valid entry"
                                                      },
                                                      {
                                                        "org_name": "invalid-org",
                                                        "name": "{{invalidKeyName}}",
                                                        "secret": "valid-secret",
                                                        "type": "TestData",
                                                        "description": "Invalid entry"
                                                      }
                                                    ]
                                                  }
                                                  """);
        var dynamoDb = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        var secretsManager = new Mock<IAmazonSecretsManager>(MockBehavior.Strict);
        using var console = new TestConsole();
        var sut = new OrgSecretSeeder(console);

        try
        {
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SeedAsync(
                configPath,
                "org-secrets",
                dynamoDb.Object,
                secretsManager.Object,
                dryRun: false,
                TestContext.Current.CancellationToken));

            dynamoDb.VerifyNoOtherCalls();
            secretsManager.VerifyNoOtherCalls();
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task SeedAsync_Should_Trim_Identity_Fields_Before_Aws_Mutation_When_Config_Has_Surrounding_Whitespace()
    {
        var configPath = await WriteConfigAsync("""
                                                {
                                                  "secrets": [
                                                    {
                                                      "org_name": " LocalStack-DotNet ",
                                                      "name": " TestData ",
                                                      "secret": " secret-with-spaces-preserved ",
                                                      "type": " TestData ",
                                                      "description": "Test data secret"
                                                    }
                                                  ]
                                                }
                                                """);
        CreateSecretRequest? secretRequest = null;
        PutItemRequest? putRequest = null;
        var dynamoDb = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        dynamoDb
            .Setup(client => client.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((request, _) => putRequest = request)
            .ReturnsAsync(new PutItemResponse());
        var secretsManager = new Mock<IAmazonSecretsManager>(MockBehavior.Strict);
        secretsManager
            .Setup(client => client.CreateSecretAsync(It.IsAny<CreateSecretRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateSecretRequest, CancellationToken>((request, _) => secretRequest = request)
            .ReturnsAsync(new CreateSecretResponse());
        using var console = new TestConsole();
        var sut = new OrgSecretSeeder(console);

        try
        {
            var exitCode = await sut.SeedAsync(
                configPath,
                "org-secrets",
                dynamoDb.Object,
                secretsManager.Object,
                dryRun: false,
                TestContext.Current.CancellationToken);

            Assert.Equal(ToolExitCodes.Success, exitCode);
            Assert.NotNull(secretRequest);
            Assert.Equal("badgesmith/github/localstack-dotnet/testdata", secretRequest.Name);
            Assert.Equal(" secret-with-spaces-preserved ", secretRequest.SecretString);
            Assert.NotNull(putRequest);
            Assert.Equal("ORG#localstack-dotnet", putRequest.Item["PK"].S);
            Assert.Equal("CONST#GITHUB#testdata", putRequest.Item["SK"].S);
            Assert.Equal("badgesmith/github/localstack-dotnet/testdata", putRequest.Item["SecretName"].S);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    private static async Task<string> WriteConfigAsync(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await File.WriteAllTextAsync(path, json, TestContext.Current.CancellationToken);
        return path;
    }
}
