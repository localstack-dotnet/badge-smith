namespace BadgeSmith;

internal static class Constants
{
    public const string DynamoDbTablesConstructId = "DynamoDbTablesConstruct";

    public const string TestResultsTableId = "TestResultsTable";

    public const string TestResultsTableName = "badge-smith-test-result";

    public const string TestResultsOutputTableArn = "TestResultsTableArn";

    public const string TestResultsOutputTableName = "TestResultsTableTableName";

    public const string NonceTableId = "NonceTable";

    public const string NonceTableName = "badge-smith-hmac-nonce";

    public const string NonceTableOutputArn = "NonceTableArn";

    public const string NonceTableOutputTableName = "NonceTableTableName";

    public const string OrgSecretsTableId = "OrgSecretsTable";

    public const string OrgSecretsTableName = "badge-smith-github-org-secrets";

    public const string OrgSecretsTableOutputArn = "OrgSecretsTableArn";

    public const string OrgSecretsOutputTableName = "OrgSecretsTableTableName";

    public const string LambdaId = "BadgeSmithFunction";

    public const string LambdaName = "badge-smith-function";

    public const string LambdaConstructId = "BadgeSmithFunctionConstruct";

    public const string LambdaExecutionRoleConstructId = "BadgeSmithLambdaExecutionRoleConstruct";

    public const string LambdaExecutionRoleId = "BadgeSmithLambdaExecutionRole";

    public const string LambdaExecutionRoleName = "badge-smith-lambda-execution-role";

    public const string LambdaExecutionOutputRoleArn = "LambdaExecutionRoleArn";

    public const string LambdaOutputFunctionArn = "BadgeSmithLambdaFunctionArn";

    public const string HttpLambdaIntegrationId = "BadgeSmithLambdaIntegration";

    public const string ApiGatewayRoleId = "BadgeSmithApi";

    public const string ApiGatewayName = "badge-smith-api";

    public const string ApiGatewayOutputUrl = "BadgeSmithApiUrl";

    public const string LocalStackForNetZoneHostedZoneId = "LocalStackForNetZone";

    public const string ApiCertificateId = "ApiCertificate";

    public const string CloudFrontCachePolicyId = "OriginControlledCachePolicy";

    public const string CloudFrontCachePolicyName = "badge-smith-origin-controlled";

    public const string CloudFrontDistributionId = "BadgeSmithCloudFront";

    public const string CloudFrontDistributionOutputUrl = "CloudFrontUrl";

    public const string CloudFrontDistributionOutputDomainUrl = "CustomDomainUrl";

    public const string ApiLocalStackARecordId = "ApiLocalStackARecord";

    public const string ProductionStackId = "BadgeSmithStack";

    public const string SharedInfrastructureConstructId = "BadgeSmithSharedInfrastructureConstruct";

    public const string ApiLocalStackForNetDomain = "api.localstackfor.net";

    public const int LambdaTimeoutInSeconds = 20;
}
