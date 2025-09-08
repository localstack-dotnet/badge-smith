#pragma warning disable CA1711, MA0051

using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.Route53.Targets;
using Amazon.CDK.AwsApigatewayv2Integrations;
using BadgeSmith.CDK.Shared.Constructs;
using Constructs;
using Function = Amazon.CDK.AWS.Lambda.Function;
using static BadgeSmith.Constants;
using Distribution = Amazon.CDK.AWS.CloudFront.Distribution;

namespace BadgeSmith.CDK.Shared;

/// <summary>
/// Production stack that includes shared infrastructure, Lambda function, API Gateway, CloudFront CDN, and SSL certificate.
/// This stack is used for actual AWS production deployments with global edge caching and custom domain support.
/// </summary>
public sealed class ProductionStack : Stack
{
    public ProductionStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        SharedInfrastructureConstruct = new SharedInfrastructureConstruct(this, SharedInfrastructureConstructId);

        TestResultsTable = SharedInfrastructureConstruct.TestResultsTable;
        NonceTable = SharedInfrastructureConstruct.NonceTable;
        OrgSecretsTable = SharedInfrastructureConstruct.OrgSecretsTable;

        BadgeSmithFunctionConstruct = new BadgeSmithFunctionConstruct(
            this,
            SharedInfrastructureConstruct.TestResultsTable,
            SharedInfrastructureConstruct.NonceTable,
            SharedInfrastructureConstruct.OrgSecretsTable,
            SharedInfrastructureConstruct.LambdaExecutionRole,
            LambdaConstructId);

        BadgeSmithFunction = BadgeSmithFunctionConstruct.BadgeSmithFunction;

        ApiGateway = CreateApiGateway();

        var logGroup = new LogGroup(this, "HttpApiAccessLogs", new LogGroupProps
        {
            Retention = RetentionDays.ONE_WEEK,
            RemovalPolicy = RemovalPolicy.DESTROY,
        });

        var cfnStage = (CfnStage)ApiGateway.DefaultStage!.Node.DefaultChild!;

        cfnStage.AccessLogSettings = new CfnStage.AccessLogSettingsProperty
        {
            DestinationArn = logGroup.LogGroupArn,
            Format = "{ \"requestId\":\"$context.requestId\","
                     + " \"routeKey\":\"$context.routeKey\","
                     + " \"status\":\"$context.status\","
                     + " \"error\":\"$context.error.message\","
                     + " \"path\":\"$context.path\","
                     + " \"method\":\"$context.httpMethod\","
                     + " \"host\":\"$context.domainName\" }",
        };

        CloudFrontDistribution = CreateCloudFrontDistribution();

        CreateCustomDomainRecord();

        CreateOutputs();

        Tags.SetTag("environment", "Production");
        Tags.SetTag("stack", "badge-smith-production");
    }

    private IHostedZone LocalStackDotnetHostedZone => HostedZone.FromLookup(this, LocalStackForNetZoneHostedZoneId, new HostedZoneProviderProps
    {
        DomainName = "localstackfor.net",
    });

    /// <summary>
    /// AWS Certificate Manager (ACM) SSL certificate for api.localstackfor.net domain.
    /// Provides HTTPS encryption for the custom domain with DNS validation.
    /// </summary>
    private ICertificate ApiLocalStackCertificate =>
        Certificate.FromCertificateArn(this, ApiCertificateId, "arn:aws:acm:us-east-1:377140207735:certificate/227f14fe-92b1-442c-bb80-ae4032e742fe");

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

    private Distribution CreateCloudFrontDistribution()
    {
        var apiGatewayDomain = Fn.Select(2, Fn.Split("/", ApiGateway.ApiEndpoint));

        var apiOrigin = new HttpOrigin(apiGatewayDomain, new HttpOriginProps
        {
            ProtocolPolicy = OriginProtocolPolicy.HTTPS_ONLY,
        });

        // Create origin-controlled cache policy - Lambda controls TTL via Cache-Control headers
        var originControlledCachePolicy = new CachePolicy(this, CloudFrontCachePolicyId, new CachePolicyProps
        {
            CachePolicyName = CloudFrontCachePolicyName,
            Comment = "Origin-controlled caching with Lambda-driven TTL decisions",

            DefaultTtl = Duration.Seconds(0), // No cache if origin doesn't specify
            MinTtl = Duration.Seconds(0), // Allow immediate expiration (no-cache)
            MaxTtl = Duration.Hours(24), // Cap runaway TTLs at 24 hours

            HeaderBehavior = CacheHeaderBehavior.None(),

            // Badge URLs vary by query parameters (e.g., version filters)
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

            DomainNames = [ApiLocalStackForNetDomain],
            Certificate = ApiLocalStackCertificate,

            DefaultBehavior = new BehaviorOptions
            {
                Origin = apiOrigin,
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                CachePolicy = originControlledCachePolicy,
                OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER,
                AllowedMethods = AllowedMethods.ALLOW_ALL,
                CachedMethods = CachedMethods.CACHE_GET_HEAD_OPTIONS,
                Compress = true,
            },

            PriceClass = PriceClass.PRICE_CLASS_100,
            EnableIpv6 = true,
            HttpVersion = HttpVersion.HTTP2_AND_3,

            MinimumProtocolVersion = SecurityPolicyProtocol.TLS_V1_2_2021,
        });
    }

    public ARecord CreateCustomDomainRecord()
    {
        return new ARecord(this, ApiLocalStackARecordId, new ARecordProps
        {
            Zone = LocalStackDotnetHostedZone,
            RecordName = "api", // => api.localstackfor.net
            Target = RecordTarget.FromAlias(new CloudFrontTarget(CloudFrontDistribution)),
        });
    }

    private void CreateOutputs()
    {
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
            Value = SharedInfrastructureConstruct.TestResultsTable.TableName,
            Description = "DynamoDB table name for test results",
        });

        _ = new CfnOutput(this, NonceTableOutputTableName, new CfnOutputProps
        {
            Value = SharedInfrastructureConstruct.NonceTable.TableName,
            Description = "DynamoDB table name for nonce",
        });
    }

    public SharedInfrastructureConstruct SharedInfrastructureConstruct { get; }

    public BadgeSmithFunctionConstruct BadgeSmithFunctionConstruct { get; }

    public Function BadgeSmithFunction { get; }

    public Table TestResultsTable { get; }

    public Table NonceTable { get; }

    public Table OrgSecretsTable { get; }

    public HttpApi ApiGateway { get; }

    public Distribution CloudFrontDistribution { get; }
}
