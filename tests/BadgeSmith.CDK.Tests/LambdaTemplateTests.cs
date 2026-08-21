using Amazon.CDK;
using Amazon.CDK.Assertions;
using Amazon.CDK.AWS.Lambda;
using BadgeSmith.CDK.Shared.Constructs;
using Xunit;

namespace BadgeSmith.CDK.Tests;

public sealed class LambdaTemplateTests
{
    private static readonly string[] Arm64Architecture = ["arm64"];

    [Fact]
    public void BadgeSmithFunctionConstruct_Should_Configure_Production_Runtime_Contract_When_Synthesized()
    {
        var assetDirectory = Directory.CreateTempSubdirectory("badgesmith-cdk-tests-");

        try
        {
            File.WriteAllText(Path.Combine(assetDirectory.FullName, "bootstrap"), "test asset");
            var template = CreateTemplate(assetDirectory.FullName);

            _ = Assert.Single(template.FindResources("AWS::Lambda::Function"));
            template.HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["Architectures"] = Arm64Architecture,
                ["Runtime"] = "provided.al2023",
                ["Timeout"] = 20,
                ["MemorySize"] = 512,
                ["Handler"] = "bootstrap",
                ["Environment"] = Match.ObjectLike(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["Variables"] = Match.ObjectLike(new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["BADGESMITH_UPSTREAM_MODE"] = "Live",
                        ["AWS_RESOURCE_TEST_RESULTS_TABLE"] = Match.AnyValue(),
                        ["AWS_RESOURCE_NONCE_TABLE"] = Match.AnyValue(),
                        ["AWS_RESOURCE_ORG_SECRETS_TABLE"] = Match.AnyValue(),
                    }),
                }),
            });
        }
        finally
        {
            assetDirectory.Delete(recursive: true);
        }
    }

    private static Template CreateTemplate(string assetPath)
    {
        var app = new App();
        var stack = new Stack(app, "LambdaTestStack");
        var sharedInfrastructure = new SharedInfrastructureConstruct(stack, "TestSharedInfrastructure");

        _ = new BadgeSmithFunctionConstruct(
            stack,
            sharedInfrastructure.TestResultsTable,
            sharedInfrastructure.NonceTable,
            sharedInfrastructure.OrgSecretsTable,
            sharedInfrastructure.LambdaExecutionRole,
            "TestBadgeSmithFunction",
            new BadgeSmithFunctionConfiguration(assetPath, Architecture.ARM_64));

        return Template.FromStack(stack);
    }
}
