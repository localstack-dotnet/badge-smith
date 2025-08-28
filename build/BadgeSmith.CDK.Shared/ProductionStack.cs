#pragma warning disable CA1711, MA0051

using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.Route53;
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

        // SSL Certificate for custom domain
        Certificate = CreateSslCertificate();

        // CloudFront distribution for global-edge caching with security
        CloudFrontDistribution = CreateCloudFrontDistribution();

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
                ["AWS_RESOURCE_TEST_RESULTS_TABLE"] = SharedInfrastructure.TestResultsTable.TableName,
                ["AWS_RESOURCE_NONCE_TABLE"] = SharedInfrastructure.NonceTable.TableName,
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

    private Certificate CreateSslCertificate()
    {
        // Look up the existing hosted zone for localstackfor.net
        var hostedZone = HostedZone.FromLookup(this, LocalStackForNetZoneHostedZoneId, new HostedZoneProviderProps
        {
            DomainName = "localstackfor.net",
        });

        // Create ACM certificate for api.localstackfor.net
        // Must be in us-east-1 for CloudFront usage
        return new Certificate(this, ApiCertificateId, new CertificateProps
        {
            DomainName = "api.localstackfor.net",
            Validation = CertificateValidation.FromDns(hostedZone),
            CertificateName = "BadgeSmith API Certificate",
        });
    }

    private Distribution CreateCloudFrontDistribution()
    {
        // Generate a secure random secret for CloudFront -> API Gateway authentication
        var cloudFrontSecret = Guid.NewGuid().ToString("N")[..16]; // 16 char secret

        // Extract domain from API Gateway URL (remove https://)
        var apiGatewayDomain = ApiGateway.ApiEndpoint.Replace("https://", "", StringComparison.OrdinalIgnoreCase);

        // Create origin with a security header
        var apiOrigin = new HttpOrigin(apiGatewayDomain, new HttpOriginProps
        {
            HttpPort = 443,
            ProtocolPolicy = OriginProtocolPolicy.HTTPS_ONLY,
            CustomHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // 🔐 Security: CloudFront adds this secret header to all origin requests
                ["X-CloudFront-Secret"] = cloudFrontSecret,
            },
        });

        // Create a custom cache policy for origin-controlled caching
        var originControlledCachePolicy = new CachePolicy(this, CloudFrontCachePolicyId, new CachePolicyProps
        {
            CachePolicyName = CloudFrontCachePolicyName,
            Comment = "Forwards all headers and respects origin Cache-Control headers completely",

            // 🎯 Origin controls ALL caching decisions
            DefaultTtl = Duration.Seconds(0), // Trust origin headers completely
            MinTtl = Duration.Seconds(0), // Allow immediate expiration
            MaxTtl = Duration.Hours(24), // Cap at 24-hour max

            // 🚀 Forward ALL headers - maximum flexibility
            HeaderBehavior = CacheHeaderBehavior.AllowList("*"),

            // Forward all query strings
            QueryStringBehavior = CacheQueryStringBehavior.All(),

            // No cookies needed for badge API
            CookieBehavior = CacheCookieBehavior.None(),

            // Enable compression
            EnableAcceptEncodingGzip = true,
            EnableAcceptEncodingBrotli = true,
        });

        return new Distribution(this, CloudFrontDistributionId, new DistributionProps
        {
            Comment = "BadgeSmith API with origin-controlled caching and security",

            // 🌍 Custom domain with SSL certificate
            DomainNames = ["api.localstackfor.net"],
            Certificate = Certificate,

            // Default behavior - forward everything to origin
            DefaultBehavior = new BehaviorOptions
            {
                Origin = apiOrigin,
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                CachePolicy = originControlledCachePolicy,
                AllowedMethods = AllowedMethods.ALLOW_ALL,
                CachedMethods = CachedMethods.CACHE_GET_HEAD_OPTIONS,
                Compress = true,
            },

            // Optimize for cost and performance
            PriceClass = PriceClass.PRICE_CLASS_100, // US, Canada, Europe
            EnableIpv6 = true,
            HttpVersion = HttpVersion.HTTP2_AND_3,

            // Security settings
            MinimumProtocolVersion = SecurityPolicyProtocol.TLS_V1_2_2021,
        });
    }

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

        _ = new CfnOutput(this, CloudFrontDistributionOutputUrl, new CfnOutputProps
        {
            Value = $"https://{CloudFrontDistribution.DistributionDomainName}",
            Description = "CloudFront distribution URL for global access",
        });

        _ = new CfnOutput(this, CloudFrontDistributionOutputDomainUrl, new CfnOutputProps
        {
            Value = "https://api.localstackfor.net",
            Description = "Custom domain URL with SSL certificate",
        });

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

    /// <summary>
    /// SSL Certificate for api.localstackfor.net
    /// </summary>
    public ICertificate Certificate { get; }

    /// <summary>
    /// CloudFront distribution for global edge caching
    /// </summary>
    public Distribution CloudFrontDistribution { get; }
}
