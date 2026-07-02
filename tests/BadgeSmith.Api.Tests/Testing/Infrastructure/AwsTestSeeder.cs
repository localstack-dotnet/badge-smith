using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

public static class AwsTestSeeder
{
    public static async Task CreateTablesAndSecretsAsync(IAmazonDynamoDB dynamo, IAmazonSecretsManager secrets)
    {
        await CreatePkSkTableAsync(dynamo, "badge-smith-hmac-nonce", withGsi: false);
        await CreatePkSkTableAsync(dynamo, "badge-smith-github-org-secrets", withGsi: false);
        await CreatePkSkTableAsync(dynamo, "badge-smith-test-result", withGsi: true);

        await CreateSecretAsync(secrets, "badgesmith/github/test-org/testdata", BadgeSmithStackFixture.HmacSecret);
        await CreateSecretAsync(secrets, "badgesmith/github/test-org/package", "dummy-github-pat");

        await PutSecretMappingAsync(dynamo, "testdata", "badgesmith/github/test-org/testdata");
        await PutSecretMappingAsync(dynamo, "package", "badgesmith/github/test-org/package");
    }

    private static async Task PutSecretMappingAsync(IAmazonDynamoDB dynamo, string tokenTypeLower, string secretName)
    {
        await dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = "badge-smith-github-org-secrets",
            Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new($"ORG#{BadgeSmithStackFixture.Org}"),
                ["SK"] = new($"CONST#GITHUB#{tokenTypeLower}"),
                ["SecretName"] = new(secretName),
            },
        });
    }

    private static async Task CreateSecretAsync(IAmazonSecretsManager secrets, string name, string secretString)
    {
        try
        {
            await secrets.CreateSecretAsync(new CreateSecretRequest
            {
                Name = name,
                SecretString = secretString,
            });
        }
        catch (ResourceExistsException)
        {
            // Tolerated: fixture reuse or partial prior run left the secret in place.
        }
    }

    private static async Task CreatePkSkTableAsync(IAmazonDynamoDB dynamo, string name, bool withGsi)
    {
        var request = new CreateTableRequest
        {
            TableName = name,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions =
            [
                new AttributeDefinition("PK", ScalarAttributeType.S),
                new AttributeDefinition("SK", ScalarAttributeType.S),
            ],
            KeySchema =
            [
                new KeySchemaElement("PK", KeyType.HASH),
                new KeySchemaElement("SK", KeyType.RANGE),
            ],
        };

        if (withGsi)
        {
            request.AttributeDefinitions.Add(new AttributeDefinition("GSI1PK", ScalarAttributeType.S));
            request.AttributeDefinitions.Add(new AttributeDefinition("GSI1SK", ScalarAttributeType.S));
            request.GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = "GSI1",
                    KeySchema =
                    [
                        new KeySchemaElement("GSI1PK", KeyType.HASH),
                        new KeySchemaElement("GSI1SK", KeyType.RANGE),
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                },
            ];
        }

        try
        {
            await dynamo.CreateTableAsync(request);
        }
        catch (ResourceInUseException)
        {
            // Tolerated: fixture reuse or partial prior run left the table in place.
        }
    }
}
