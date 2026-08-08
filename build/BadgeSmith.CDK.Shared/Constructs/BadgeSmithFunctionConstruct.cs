using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Constructs;
using static BadgeSmith.Constants;

namespace BadgeSmith.CDK.Shared.Constructs;

/// <summary>
/// Construct that creates the BadgeSmith Lambda function with Native AOT runtime.
/// Configures the function with ARM64 architecture, environment variables for DynamoDB table names,
/// and proper IAM role assignment for secure resource access.
/// </summary>
public class BadgeSmithFunctionConstruct : Construct
{
    public BadgeSmithFunctionConstruct(
        Construct scope,
        ITable testResultsTable,
        ITable nonceTable,
        ITable orgSecretTable,
        IRole lambdaExecutionRole,
        string id)
        : this(
            scope,
            testResultsTable,
            nonceTable,
            orgSecretTable,
            lambdaExecutionRole,
            id,
            BadgeSmithFunctionConfiguration.Production)
    {
    }

    public BadgeSmithFunctionConstruct(
        Construct scope,
        ITable testResultsTable,
        ITable nonceTable,
        ITable orgSecretTable,
        IRole lambdaExecutionRole,
        string id,
        BadgeSmithFunctionConfiguration configuration) : base(scope, id)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(testResultsTable);
        ArgumentNullException.ThrowIfNull(nonceTable);
        ArgumentNullException.ThrowIfNull(orgSecretTable);
        ArgumentNullException.ThrowIfNull(lambdaExecutionRole);
        ArgumentNullException.ThrowIfNull(configuration);

        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_ENVIRONMENT"] = "Production",
            ["APP_NAME"] = LambdaName,
            ["APP_ENABLE_TELEMETRY_FACTORY_PERF_LOGS"] = "true",
            [UpstreamModeEnvironmentVariable] = UpstreamModeLive,
            ["AWS_RESOURCE_TEST_RESULTS_TABLE"] = testResultsTable.TableName,
            ["AWS_RESOURCE_NONCE_TABLE"] = nonceTable.TableName,
            ["AWS_RESOURCE_ORG_SECRETS_TABLE"] = orgSecretTable.TableName,
            // ["AWS_LAMBDA_EXEC_WRAPPER"] = "/opt/otel-instrument", // For future OpenTelemetry support
        };

        if (configuration.ExtraEnvironment is not null)
        {
            foreach (var (key, value) in configuration.ExtraEnvironment)
            {
                environment[key] = value;
            }
        }

        BadgeSmithFunction = new Function(this, LambdaId, new FunctionProps
        {
            FunctionName = LambdaName,
            Runtime = Runtime.PROVIDED_AL2023,
            Code = Code.FromAsset(configuration.AssetPath),
            Handler = "bootstrap",
            Role = lambdaExecutionRole,
            Timeout = Duration.Seconds(LambdaTimeoutInSeconds),
            MemorySize = 512,
            Architecture = configuration.Architecture,
            Environment = environment,
            Description = "BadgeSmith Native AOT Lambda function for badge generation",
        });

        _ = new CfnOutput(this, LambdaOutputFunctionArn, new CfnOutputProps
        {
            Value = BadgeSmithFunction.FunctionArn,
            Description = "ARN of the BadgeSmith Lambda function",
        });
    }

    public Function BadgeSmithFunction { get; }
}

public sealed record BadgeSmithFunctionConfiguration(
    string AssetPath,
    Architecture Architecture,
    IReadOnlyDictionary<string, string>? ExtraEnvironment = null)
{
    public static BadgeSmithFunctionConfiguration Production { get; } = new(
        "../artifacts/badge-lambda-linux-arm64.zip",
        Architecture.ARM_64);
}
