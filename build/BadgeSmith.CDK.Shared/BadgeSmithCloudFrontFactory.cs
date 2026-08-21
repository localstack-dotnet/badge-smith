using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Constructs;
using static BadgeSmith.Constants;

namespace BadgeSmith.CDK.Shared;

public static class BadgeSmithCloudFrontFactory
{
    public static Distribution Create(Construct scope, string originHostname, ICertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(originHostname);
        ArgumentNullException.ThrowIfNull(certificate);

        var apiOrigin = new HttpOrigin(originHostname, new HttpOriginProps
        {
            ProtocolPolicy = OriginProtocolPolicy.HTTPS_ONLY,
        });

        // Lambda response headers select freshness within these deployment bounds.
        var originControlledCachePolicy = new CachePolicy(scope, CloudFrontCachePolicyId, new CachePolicyProps
        {
            CachePolicyName = CloudFrontCachePolicyName,
            Comment = "Origin-controlled caching with Lambda-driven TTL decisions",

            DefaultTtl = Duration.Seconds(0),
            MinTtl = Duration.Seconds(0),
            MaxTtl = Duration.Hours(24),

            HeaderBehavior = CacheHeaderBehavior.None(),
            QueryStringBehavior = CacheQueryStringBehavior.All(),
            CookieBehavior = CacheCookieBehavior.None(),

            EnableAcceptEncodingGzip = true,
            EnableAcceptEncodingBrotli = true,
        });

        return new Distribution(scope, CloudFrontDistributionId, new DistributionProps
        {
            Comment = "BadgeSmith API with origin-controlled caching and security",

            DomainNames = [ApiLocalStackForNetDomain],
            Certificate = certificate,

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
}
