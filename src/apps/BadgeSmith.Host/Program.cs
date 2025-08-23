#pragma warning disable CA2252 // Using 'AddAWSLambdaFunction' requires opting into preview features.

using Amazon;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.LocalStack.Container;

var builder = DistributedApplication.CreateBuilder(args);

var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);

// Bootstrap the localstack container with enhanced configuration
var localstack = builder
    .AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
    });

var badgeSmithApi = builder
    .AddAWSLambdaFunction<Projects.BadgeSmith_Api>(
        name: "BadgeSmithApi",
        lambdaHandler: "BadgeSmith.Api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithEnvironment("ObservabilityOptions:EnableOtel", "true");

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(badgeSmithApi, Method.Any, "/{proxy+}");

builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);
