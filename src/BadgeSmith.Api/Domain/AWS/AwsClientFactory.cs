using Amazon.DynamoDBv2;
using Amazon.SecretsManager;

namespace BadgeSmith.Api.Domain.AWS;

internal static class AwsClientFactory
{
    private static readonly Lazy<AmazonDynamoDBClient> DynamoDbClientLazy = new(AwsClientBuilder.CreateAwsClient<AmazonDynamoDBClient>);
    private static readonly Lazy<AmazonSecretsManagerClient> AmazonSecretsManagerClientLazy = new(AwsClientBuilder.CreateAwsClient<AmazonSecretsManagerClient>);

    public static AmazonDynamoDBClient AmazonDynamoDbClient => DynamoDbClientLazy.Value;
    public static AmazonSecretsManagerClient AmazonSecretsManagerClient => AmazonSecretsManagerClientLazy.Value;
}
