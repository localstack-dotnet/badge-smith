using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;
using static BadgeSmith.Constants;

namespace BadgeSmith.CDK.Shared.Constructs;

/// <summary>
/// HTTP API Gateway configured with BadgeSmith's Lambda proxy integration.
/// </summary>
public sealed class BadgeSmithHttpApiConstruct : HttpApi
{
    public BadgeSmithHttpApiConstruct(Construct scope, string id, IFunction badgeSmithFunction)
        : base(scope, id, new HttpApiProps
        {
            ApiName = ApiGatewayName,
            Description = "BadgeSmith API Gateway for badge endpoints",
            DefaultIntegration = CreateLambdaIntegration(badgeSmithFunction),
        })
    {
    }

    private static HttpLambdaIntegration CreateLambdaIntegration(IFunction badgeSmithFunction)
    {
        ArgumentNullException.ThrowIfNull(badgeSmithFunction);
        return new HttpLambdaIntegration(HttpLambdaIntegrationId, badgeSmithFunction);
    }

    public HttpApi ApiGateway => this;
}
