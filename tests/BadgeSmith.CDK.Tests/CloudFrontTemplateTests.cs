using Amazon.CDK;
using Amazon.CDK.Assertions;
using Amazon.CDK.AWS.CertificateManager;
using BadgeSmith.CDK.Shared;
using Xunit;
using CdkEnvironment = Amazon.CDK.Environment;

namespace BadgeSmith.CDK.Tests;

public sealed class CloudFrontTemplateTests
{
    private const string CachePolicyResourceType = "AWS::CloudFront::CachePolicy";
    private const string DistributionResourceType = "AWS::CloudFront::Distribution";

    private static readonly string[] DomainAliases = ["api.localstackfor.net"];
    private static readonly string[] AllowedMethods = ["GET", "HEAD", "OPTIONS", "PUT", "PATCH", "POST", "DELETE"];
    private static readonly string[] CachedMethods = ["GET", "HEAD", "OPTIONS"];

    private static readonly object[] HttpsOrigins =
    [
        Match.ObjectLike(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["CustomOriginConfig"] = Match.ObjectLike(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["OriginProtocolPolicy"] = "https-only",
            }),
        }),
    ];

    [Fact]
    public void Create_Should_Configure_OriginControlled_Cache_Policy_When_Synthesized()
    {
        var template = CreateTemplate();

        _ = Assert.Single(template.FindResources(CachePolicyResourceType));
        template.HasResourceProperties(CachePolicyResourceType, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["CachePolicyConfig"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["DefaultTTL"] = 0,
                ["MinTTL"] = 0,
                ["MaxTTL"] = 86400,
                ["ParametersInCacheKeyAndForwardedToOrigin"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["CookiesConfig"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["CookieBehavior"] = "none"
                    },
                    ["HeadersConfig"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["HeaderBehavior"] = "none"
                    },
                    ["QueryStringsConfig"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["QueryStringBehavior"] = "all"
                    },
                    ["EnableAcceptEncodingGzip"] = true,
                    ["EnableAcceptEncodingBrotli"] = true,
                },
            },
        });
    }

    [Fact]
    public void Create_Should_Configure_Distribution_Transport_And_Cache_Behavior_When_Synthesized()
    {
        var template = CreateTemplate();
        var cachePolicyLogicalId = template.GetResourceId(CachePolicyResourceType);

        _ = Assert.Single(template.FindResources(DistributionResourceType));
        template.HasResourceProperties(DistributionResourceType, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["DistributionConfig"] = Match.ObjectLike(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["Aliases"] = DomainAliases,
                ["DefaultCacheBehavior"] = Match.ObjectLike(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["AllowedMethods"] = AllowedMethods,
                    ["CachedMethods"] = CachedMethods,
                    ["CachePolicyId"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["Ref"] = cachePolicyLogicalId
                    },
                    ["OriginRequestPolicyId"] = "b689b0a8-53d0-40ab-baf2-68738e2966ac",
                    ["ViewerProtocolPolicy"] = "redirect-to-https",
                    ["Compress"] = true,
                }),
                ["Origins"] = Match.ArrayWith(HttpsOrigins),
                ["IPV6Enabled"] = true,
                ["PriceClass"] = "PriceClass_100",
                ["HttpVersion"] = "http2and3",
                ["ViewerCertificate"] = Match.ObjectLike(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["MinimumProtocolVersion"] = "TLSv1.2_2021",
                }),
            }),
        });
    }

    [Fact]
    public void Create_Should_Leave_Custom_Error_Responses_Absent_When_Synthesized()
    {
        var template = CreateTemplate();

        _ = Assert.Single(template.FindResources(DistributionResourceType));
        template.HasResourceProperties(DistributionResourceType, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["DistributionConfig"] = Match.ObjectLike(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["CustomErrorResponses"] = Match.Absent(),
            }),
        });
    }

    private static Template CreateTemplate()
    {
        var app = new App();
        var stack = new Stack(app, "CloudFrontTestStack", new StackProps
        {
            Env = new CdkEnvironment
            {
                Account = "123456789012",
                Region = "eu-central-1",
            },
        });
        var certificate = Certificate.FromCertificateArn(
            stack,
            "TestCertificate",
            "arn:aws:acm:us-east-1:123456789012:certificate/00000000-0000-0000-0000-000000000000");

        _ = BadgeSmithCloudFrontFactory.Create(
            stack,
            "example.execute-api.eu-central-1.amazonaws.com",
            certificate);

        return Template.FromStack(stack);
    }
}
