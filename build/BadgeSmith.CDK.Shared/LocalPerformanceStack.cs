#pragma warning disable CA1711 // AWS CDK stack types intentionally use the Stack suffix.

using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using BadgeSmith.CDK.Shared.Constructs;
using Constructs;
using static BadgeSmith.Constants;
using Function = Amazon.CDK.AWS.Lambda.Function;

namespace BadgeSmith.CDK.Shared;

/// <summary>
/// LocalStack-only stack for running local performance baselines without production edge resources.
/// </summary>
public sealed class LocalPerformanceStack : Stack
{
    public LocalPerformanceStack(
        Construct scope,
        string id,
        LocalPerformanceStackSettings settings,
        IStackProps? props = null) : base(scope, id, props)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SharedInfrastructureConstruct = new SharedInfrastructureConstruct(this, SharedInfrastructureConstructId);

        TestResultsTable = SharedInfrastructureConstruct.TestResultsTable;
        NonceTable = SharedInfrastructureConstruct.NonceTable;
        OrgSecretsTable = SharedInfrastructureConstruct.OrgSecretsTable;

        BadgeSmithFunctionConstruct = new BadgeSmithFunctionConstruct(
            this,
            TestResultsTable,
            NonceTable,
            OrgSecretsTable,
            SharedInfrastructureConstruct.LambdaExecutionRole,
            LambdaConstructId,
            new BadgeSmithFunctionConfiguration(
                settings.LambdaAssetPath,
                settings.LambdaArchitecture,
                settings.LambdaEnvironment));

        BadgeSmithFunction = BadgeSmithFunctionConstruct.BadgeSmithFunction;
        BadgeSmithFunctionUrl = BadgeSmithFunction.AddFunctionUrl(new FunctionUrlOptions
        {
            AuthType = FunctionUrlAuthType.NONE,
        });

        var httpApiConstruct = new BadgeSmithHttpApiConstruct(this, ApiGatewayRoleId, BadgeSmithFunction);
        ApiGateway = httpApiConstruct.ApiGateway;
        Amazon.CDK.Tags.Of(httpApiConstruct).Add("_custom_id_", ApiGatewayName);

        CreateOutputs();

        Tags.SetTag("environment", "LocalPerformance");
        Tags.SetTag("stack", "badge-smith-local-performance");
        Tags.SetTag("managed-by", "perf-baseline");
    }

    private void CreateOutputs()
    {
        _ = new CfnOutput(this, ApiGatewayOutputUrl, new CfnOutputProps
        {
            Value = ApiGateway.ApiEndpoint,
            Description = "API Gateway endpoint URL",
        });

        _ = new CfnOutput(this, LambdaOutputFunctionUrl, new CfnOutputProps
        {
            Value = BadgeSmithFunctionUrl.Url,
            Description = "Lambda Function URL for local performance fallback",
        });

        _ = new CfnOutput(this, TestResultsOutputTableName, new CfnOutputProps
        {
            Value = TestResultsTable.TableName,
            Description = "DynamoDB table name for test results",
        });

        _ = new CfnOutput(this, NonceTableOutputTableName, new CfnOutputProps
        {
            Value = NonceTable.TableName,
            Description = "DynamoDB table name for nonce",
        });

        _ = new CfnOutput(this, OrgSecretsOutputTableName, new CfnOutputProps
        {
            Value = OrgSecretsTable.TableName,
            Description = "DynamoDB table name for GitHub org secrets",
        });
    }

    public SharedInfrastructureConstruct SharedInfrastructureConstruct { get; }

    public BadgeSmithFunctionConstruct BadgeSmithFunctionConstruct { get; }

    public Function BadgeSmithFunction { get; }

    public IFunctionUrl BadgeSmithFunctionUrl { get; }

    public Table TestResultsTable { get; }

    public Table NonceTable { get; }

    public Table OrgSecretsTable { get; }

    public HttpApi ApiGateway { get; }
}

#pragma warning restore CA1711

public sealed record LocalPerformanceStackSettings(
    string LambdaAssetPath,
    Architecture LambdaArchitecture,
    IReadOnlyDictionary<string, string> LambdaEnvironment);
