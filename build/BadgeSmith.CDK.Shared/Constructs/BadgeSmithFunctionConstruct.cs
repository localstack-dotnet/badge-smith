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
    public BadgeSmithFunctionConstruct(Construct scope, ITable testResultsTable, ITable nonceTable, IRole lambdaExecutionRole, string id) : base(scope, id)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(testResultsTable);
        ArgumentNullException.ThrowIfNull(nonceTable);
        ArgumentNullException.ThrowIfNull(lambdaExecutionRole);

        BadgeSmithFunction = new Function(this, LambdaId, new FunctionProps
        {
            FunctionName = LambdaName,
            Runtime = Runtime.PROVIDED_AL2023,
            Code = Code.FromAsset("../artifacts/badge-lambda-linux-arm64.zip"),
            Handler = "bootstrap", // Native AOT uses bootstrap handler
            Role = lambdaExecutionRole,
            Timeout = Duration.Seconds(15),
            MemorySize = 512,
            Architecture = Architecture.ARM_64,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOTNET_ENVIRONMENT"] = "Production",
                ["APP_NAME"] = LambdaName,
                ["APP_ENABLE_TELEMETRY_FACTORY_PERF_LOGS"] = "true",
                ["AWS_RESOURCE_TEST_RESULTS_TABLE"] = testResultsTable.TableName,
                ["AWS_RESOURCE_NONCE_TABLE"] = nonceTable.TableName,
                // ["AWS_LAMBDA_EXEC_WRAPPER"] = "/opt/otel-instrument", // For future OpenTelemetry support
            },
            Description = "BadgeSmith Native AOT Lambda function for badge generation",
        });

        _ = new CfnOutput(this, LambdaOutputFunctionArn, new CfnOutputProps
        {
            Value = BadgeSmithFunction.FunctionArn,
            Description = "ARN of the BadgeSmith Lambda function",
        });
    }

    /// <summary>
    /// BadgeSmith Lambda function with Native AOT runtime for high-performance badge generation.
    /// Deployed with ARM64 architecture and configured with environment variables for DynamoDB access.
    /// </summary>
    public Function BadgeSmithFunction { get; }
}
