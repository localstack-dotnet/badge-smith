#pragma warning disable CA2252 // Using 'AddAWSLambdaFunction' requires opting into preview features.

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

var dynamoDbSeeder = builder.AddProject<Projects.BadgeSmith_DynamoDb_Seeders>(name: "BadgeSmithDynamoDbSeeders")
    .WithReference(badgeSmithStack)
    .WithEnvironment("AWS_RESOURCE_ORG_SECRETS_TABLE", badgeSmithStack.GetOutput(OrgSecretsOutputTableName))
    .WithEnvironment("WORKER_TIMEOUT_IN_SECONDS", "300");

var badgeSmithApi = builder
    .AddAWSLambdaFunction<Projects.BadgeSmith_Api>(name: "BadgeSmithApi", lambdaHandler: "bootstrap")
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithEnvironment("AWS_RESOURCE_TEST_RESULTS_TABLE", badgeSmithStack.GetOutput(TestResultsOutputTableName))
    .WithEnvironment("AWS_RESOURCE_NONCE_TABLE", badgeSmithStack.GetOutput(NonceTableOutputTableName))
    .WithEnvironment("AWS_RESOURCE_ORG_SECRETS_TABLE", badgeSmithStack.GetOutput(OrgSecretsOutputTableName))
    .WithReference(badgeSmithStack)
    .WaitFor(dynamoDbSeeder);

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(badgeSmithApi, Method.Any, "/{proxy+}");

builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);
