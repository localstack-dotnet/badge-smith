#pragma warning disable CA2252 // Using 'AddAWSLambdaFunction' requires opting into preview features.
#pragma warning disable ASPIRECSHARPAPPS001 // AddCSharpApp is experimental in Aspire 13.

using Amazon;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.LocalStack.Container;
using BadgeSmith.Host;
using static BadgeSmith.Constants;

var builder = DistributedApplication.CreateBuilder(args);

var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);

var localstack = builder
    .AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
    });

var badgeSmithStack = builder
    .AddAWSCDKStack("BadgeSmithStackResource", scope => new BadgeSmithInfrastructureStack(scope, "badge-smith-stack"))
    .WithReference(awsConfig);

badgeSmithStack.AddOutput(TestResultsOutputTableName, stack => stack.TestResultsTable.TableName);
badgeSmithStack.AddOutput(NonceTableOutputTableName, stack => stack.NonceTable.TableName);
badgeSmithStack.AddOutput(OrgSecretsOutputTableName, stack => stack.OrgSecretsTable.TableName);

var badgeSmithApi = builder
    .AddAWSLambdaFunction<Projects.BadgeSmith_Api>(name: "BadgeSmithApi", lambdaHandler: "bootstrap")
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithEnvironment("AWS_RESOURCE_TEST_RESULTS_TABLE", badgeSmithStack.GetOutput(TestResultsOutputTableName))
    .WithEnvironment("AWS_RESOURCE_NONCE_TABLE", badgeSmithStack.GetOutput(NonceTableOutputTableName))
    .WithEnvironment("AWS_RESOURCE_ORG_SECRETS_TABLE", badgeSmithStack.GetOutput(OrgSecretsOutputTableName))
    .WithReference(badgeSmithStack);

var httpNuGetBaseUrl = Environment.GetEnvironmentVariable("HTTP_NUGET_BASE_URL");
if (!string.IsNullOrWhiteSpace(httpNuGetBaseUrl))
{
    badgeSmithApi.WithEnvironment("HTTP_NUGET_BASE_URL", httpNuGetBaseUrl);
}

var httpGitHubBaseUrl = Environment.GetEnvironmentVariable("HTTP_GITHUB_BASE_URL");
if (!string.IsNullOrWhiteSpace(httpGitHubBaseUrl))
{
    badgeSmithApi.WithEnvironment("HTTP_GITHUB_BASE_URL", httpGitHubBaseUrl);
}

var secretMappingConfigPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "tools", "organization-pat-mapping.json"));
if (File.Exists(secretMappingConfigPath))
{
    var dynamoDbSeeder = builder.AddCSharpApp("BadgeSmithDynamoDbSeeders", "../../tools/badgesmith.cs")
        .WithArgs("secrets", "seed", "--config", secretMappingConfigPath, "--timeout-seconds", "300")
        .WithReference(awsConfig)
        .WithReference(badgeSmithStack)
        .WithEnvironment("AWS_RESOURCE_ORG_SECRETS_TABLE", badgeSmithStack.GetOutput(OrgSecretsOutputTableName))
        .ExcludeFromManifest();

    badgeSmithApi.WaitFor(dynamoDbSeeder);
}

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithEnvironment("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1")
    .WithEnvironment("LANG", "C")
    .WithEnvironment("LC_ALL", "C")
    .WithReference(badgeSmithApi, Method.Any, "/{proxy+}");

builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);
#pragma warning restore ASPIRECSHARPAPPS001
