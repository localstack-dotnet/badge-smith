#pragma warning disable ASPIRECSHARPAPPS001 // AddCSharpApp is experimental in Aspire 13.

using Amazon;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.LocalStack.Container;
using BadgeSmith.Host;
using static BadgeSmith.Constants;

var builder = DistributedApplication.CreateBuilder(args);
var upstreamMode = ResolveUpstreamMode();
var httpNuGetBaseUrl = Environment.GetEnvironmentVariable("HTTP_NUGET_BASE_URL");
var httpGitHubBaseUrl = Environment.GetEnvironmentVariable("HTTP_GITHUB_BASE_URL");
ValidateUpstreamConfiguration(upstreamMode, httpNuGetBaseUrl, httpGitHubBaseUrl);

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
    .WithEnvironment(UpstreamModeEnvironmentVariable, upstreamMode)
    .WithReference(badgeSmithStack);

if (!string.IsNullOrWhiteSpace(httpNuGetBaseUrl))
{
    badgeSmithApi.WithEnvironment("HTTP_NUGET_BASE_URL", httpNuGetBaseUrl);
}

if (!string.IsNullOrWhiteSpace(httpGitHubBaseUrl))
{
    badgeSmithApi.WithEnvironment("HTTP_GITHUB_BASE_URL", httpGitHubBaseUrl);
}

var secretMappingConfigPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "tools", "organization-pat-mapping.json"));
if (upstreamMode == UpstreamModeLive)
{
    if (!File.Exists(secretMappingConfigPath))
    {
        throw new FileNotFoundException(
            "Live upstream mode requires tools/organization-pat-mapping.json. Copy the tracked .dist template and add local secrets.",
            secretMappingConfigPath);
    }

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

static string ResolveUpstreamMode()
{
    var value = Environment.GetEnvironmentVariable(UpstreamModeEnvironmentVariable);
    if (string.IsNullOrWhiteSpace(value) || value.Equals(UpstreamModeLive, StringComparison.OrdinalIgnoreCase))
    {
        return UpstreamModeLive;
    }

    if (value.Equals(UpstreamModeMock, StringComparison.OrdinalIgnoreCase))
    {
        return UpstreamModeMock;
    }

    throw new InvalidOperationException(
        $"{UpstreamModeEnvironmentVariable} must be either {UpstreamModeLive} or {UpstreamModeMock}.");
}

static void ValidateUpstreamConfiguration(string upstreamMode, string? nuGetBaseUrl, string? gitHubBaseUrl)
{
    if (upstreamMode == UpstreamModeMock
        && (string.IsNullOrWhiteSpace(nuGetBaseUrl) || string.IsNullOrWhiteSpace(gitHubBaseUrl)))
    {
        throw new InvalidOperationException(
            $"{UpstreamModeMock} upstream mode requires both HTTP_NUGET_BASE_URL and HTTP_GITHUB_BASE_URL.");
    }

    ValidateUpstreamUrl("HTTP_NUGET_BASE_URL", nuGetBaseUrl, upstreamMode);
    ValidateUpstreamUrl("HTTP_GITHUB_BASE_URL", gitHubBaseUrl, upstreamMode);
}

static void ValidateUpstreamUrl(string variableName, string? value, string upstreamMode)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return;
    }

    var allowHttp = upstreamMode == UpstreamModeMock;
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
        || string.IsNullOrWhiteSpace(uri.Host)
        || !string.IsNullOrEmpty(uri.UserInfo)
        || !string.IsNullOrEmpty(uri.Query)
        || !string.IsNullOrEmpty(uri.Fragment)
        || (uri.Scheme != Uri.UriSchemeHttps && (!allowHttp || uri.Scheme != Uri.UriSchemeHttp)))
    {
        var allowedSchemes = allowHttp ? "HTTP or HTTPS" : "HTTPS";
        throw new InvalidOperationException(
            $"{variableName} must be an absolute {allowedSchemes} URL without credentials, query, or fragment in {upstreamMode} mode.");
    }
}
#pragma warning restore ASPIRECSHARPAPPS001
