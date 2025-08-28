namespace BadgeSmith;

internal static class Constants
{
    public const string TestResultsTableId = "TestResultsTable";

    public const string TestResultsTableName = "badge-test-result";

    public const string TestResultsOutputTableArn = "TestResultsTableArn";

    public const string TestResultsOutputTableName = "TestResultsTableTableName";

    public const string NonceTableId = "NonceTable";

    public const string NonceTableName = "hmac-nonce";

    public const string NonceTableOutputArn = "NonceTableArn";

    public const string NonceTableOutputTableName = "NonceTableTableName";

    public const string LambdaId = "BadgeSmithFunction";

    public const string LambdaName = "badge-smith-function";

    public const string LambdaExecutionRoleId = "BadgeSmithLambdaExecutionRole";

    public const string LambdaExecutionRoleName = "badge-smith-lambda-execution-role";

    public const string LambdaExecutionOutputRoleArn = "LambdaExecutionRoleArn";

    public const string LambdaOutputFunctionArn = "NonceTableArn";

    public const string HttpLambdaIntegrationId = "BadgeSmithLambdaIntegration";

    public const string ApiGatewayRoleId = "BadgeSmithApi";

    public const string ApiGatewayName = "badge-smith-api";

    public const string ApiGatewayOutputUrl = "badge-smith-api";

    public const string LocalStackForNetZoneHostedZoneId = "LocalStackForNetZone";

    public const string ApiCertificateId = "ApiCertificate";

    public const string CloudFrontCachePolicyId = "OriginControlledCachePolicy";

    public const string CloudFrontCachePolicyName = "badge-smith-origin-controlled";

    public const string CloudFrontDistributionId = "BadgeSmithCloudFront";

    public const string CloudFrontDistributionOutputUrl = "CloudFrontUrl";

    public const string CloudFrontDistributionOutputDomainUrl = "CustomDomainUrl";

    public const string InfrastructureConstructId = "badge-smith-infra-construct";

    public const string ProductionStackId = "badge-smith-stack";

    public const string InfrastructureStackId = "badge-smith-infra-stack";
}
