#pragma warning disable CA1711

using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
// using Amazon.CDK.AWS.CloudFront;
// using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;
using static BadgeSmith.Constants;
using Function = Amazon.CDK.AWS.Lambda.Function;
using FunctionProps = Amazon.CDK.AWS.Lambda.FunctionProps;

namespace BadgeSmith.CDK.Shared;

/// <summary>
/// Production stack that includes shared infrastructure plus Lambda and S3 deployment.
/// This stack is used for actual AWS deployments.
/// </summary>
public sealed class ProductionStack : Stack
{
    public ProductionStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        // Create a shared infrastructure first - this provides DynamoDB tables, IAM roles, etc.
        // Enable CDK outputs for production deployment
        SharedInfrastructure = new SharedInfrastructureStack(this, InfrastructureStackId, new StackProps
        {
            Description = "BadgeSmith shared infrastructure for production deployment",
        });

        // Lambda function with Native AOT runtime
        BadgeSmithFunction = CreateLambdaFunction();

        // API Gateway HTTP API v2 for optimal performance
        ApiGateway = CreateApiGateway();

        // CloudFront distribution for global edge caching
        // CloudFrontDistribution = CreateCloudFrontDistribution();

        // Outputs for CI/CD pipeline and monitoring
        CreateOutputs();

        // Production-specific tags
        Tags.SetTag("Environment", "Production");
        Tags.SetTag("Stack", "BadgeSmith-Production");
        Tags.SetTag("CostCenter", "Engineering");
    }

    private Function CreateLambdaFunction()
    {
        return new Function(this, LambdaId, new FunctionProps
        {
            FunctionName = LambdaName,
            Runtime = Runtime.PROVIDED_AL2023,
            Code = Code.FromAsset("../artifacts/badge-lambda-linux-arm64.zip"),
            Handler = "bootstrap", // Native AOT uses bootstrap handler
            Role = SharedInfrastructure.LambdaExecutionRole,
            Timeout = Duration.Seconds(30),
            MemorySize = 512,
            Architecture = Architecture.ARM_64,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOTNET_ENVIRONMENT"] = "Production",
                ["APP_NAME"] = LambdaName,
                ["APP_ENABLE_TELEMETRY_FACTORY_PERF_LOGS"] = "true",
                ["AWS_TEST_RESULTS_TABLE"] = SharedInfrastructure.TestResultsTable.TableName,
                ["AWS_NONCE_TABLE"] = SharedInfrastructure.NonceTable.TableName,
                // ["AWS_LAMBDA_EXEC_WRAPPER"] = "/opt/otel-instrument", // For future OpenTelemetry support
            },
            Description = "BadgeSmith Native AOT Lambda function for badge generation",
        });
    }

    private HttpApi CreateApiGateway()
    {
        var lambdaIntegration = new HttpLambdaIntegration(HttpLambdaIntegrationId, BadgeSmithFunction);

        return new HttpApi(this, ApiGatewayRoleId, new HttpApiProps
        {
            ApiName = ApiGatewayName,
            Description = "BadgeSmith API Gateway for badge endpoints",
            DefaultIntegration = lambdaIntegration,
        });
    }

    // private Distribution CreateCloudFrontDistribution()
    // {
    //     var apiOrigin = new HttpOrigin(ApiGateway.ApiEndpoint.Replace("https://", "", StringComparison.InvariantCultureIgnoreCase));
    //
    //     return new Distribution(this, "BadgeSmithCloudFront", new DistributionProps
    //     {
    //         Comment = "BadgeSmith CDN for global badge delivery",
    //         DefaultBehavior = new BehaviorOptions
    //         {
    //             Origin = apiOrigin,
    //             ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
    //             CachePolicy = CachePolicy.CACHING_OPTIMIZED,
    //             AllowedMethods = AllowedMethods.ALLOW_GET_HEAD_OPTIONS,
    //             CachedMethods = CachedMethods.CACHE_GET_HEAD_OPTIONS,
    //         },
    //         AdditionalBehaviors = new Dictionary<string, IBehaviorOptions>(StringComparer.Ordinal)
    //         {
    //             ["/tests/results"] = new BehaviorOptions
    //             {
    //                 Origin = apiOrigin,
    //                 ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
    //                 CachePolicy = CachePolicy.CACHING_DISABLED, // No caching for POST endpoints
    //                 AllowedMethods = AllowedMethods.ALLOW_ALL,
    //             },
    //         },
    //         PriceClass = PriceClass.PRICE_CLASS_100, // USA, Canada, Europe only
    //         EnableIpv6 = true,
    //     });
    // }

    private void CreateOutputs()
    {
        _ = new CfnOutput(this, LambdaOutputFunctionArn, new CfnOutputProps
        {
            Value = BadgeSmithFunction.FunctionArn,
            Description = "ARN of the BadgeSmith Lambda function",
        });

        _ = new CfnOutput(this, ApiGatewayOutputUrl, new CfnOutputProps
        {
            Value = ApiGateway.ApiEndpoint,
            Description = "API Gateway endpoint URL",
        });

        // _ = new CfnOutput(this, "CloudFrontUrl", new CfnOutputProps
        // {
        //     Value = $"https://{CloudFrontDistribution.DistributionDomainName}",
        //     Description = "CloudFront distribution URL for global access",
        // });

        _ = new CfnOutput(this, TestResultsOutputTableName, new CfnOutputProps
        {
            Value = SharedInfrastructure.TestResultsTable.TableName,
            Description = "DynamoDB table name for test results",
        });

        _ = new CfnOutput(this, NonceTableOutputTableName, new CfnOutputProps
        {
            Value = SharedInfrastructure.TestResultsTable.TableName,
            Description = "DynamoDB table name for nonce",
        });
    }

    /// <summary>
    /// Reference to the shared infrastructure containing DynamoDB tables, IAM roles, etc.
    /// </summary>
    public SharedInfrastructureStack SharedInfrastructure { get; }

    /// <summary>
    /// BadgeSmith Lambda function with Native AOT runtime
    /// </summary>
    public Function BadgeSmithFunction { get; }

    /// <summary>
    /// API Gateway HTTP API v2 for Lambda integration
    /// </summary>
    public HttpApi ApiGateway { get; }

    // /// <summary>
    // /// CloudFront distribution for global edge caching
    // /// </summary>
    // public Distribution CloudFrontDistribution { get; }
}
