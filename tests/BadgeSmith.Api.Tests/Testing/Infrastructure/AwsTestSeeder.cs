using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

public static class AwsTestSeeder
{
    public const string HmacSecret = "contract-test-secret";
    public const string Org = "test-org";

    private const string TestResultsTableName = "badge-smith-test-result";
    private const string NonceTableName = "badge-smith-hmac-nonce";
    private const string OrgSecretsTableName = "badge-smith-github-org-secrets";

    public static async Task CreateTablesAndSecretsAsync(IAmazonDynamoDB dynamo, IAmazonSecretsManager secrets)
    {
        await CreatePkSkTableAsync(dynamo, NonceTableName, withGsi: false);
        await CreatePkSkTableAsync(dynamo, OrgSecretsTableName, withGsi: false);
        await CreatePkSkTableAsync(dynamo, TestResultsTableName, withGsi: true);

        await CreateSecretAsync(secrets, "badgesmith/github/test-org/testdata", HmacSecret);
        await CreateSecretAsync(secrets, "badgesmith/github/test-org/package", "dummy-github-pat");
        await CreateSecretAsync(secrets, "badgesmith/github/unauthorized-org/package", "dummy-github-pat");
        await CreateSecretAsync(secrets, "badgesmith/github/forbidden-org/package", "dummy-github-pat");

        await PutSecretMappingAsync(dynamo, "testdata", "badgesmith/github/test-org/testdata");
        await PutSecretMappingAsync(dynamo, "package", "badgesmith/github/test-org/package");
        await PutSecretMappingAsync(dynamo, "unauthorized-org", "package", "badgesmith/github/unauthorized-org/package");
        await PutSecretMappingAsync(dynamo, "forbidden-org", "package", "badgesmith/github/forbidden-org/package");
    }

    private static async Task PutSecretMappingAsync(IAmazonDynamoDB dynamo, string tokenTypeLower, string secretName)
    {
        await PutSecretMappingAsync(dynamo, Org, tokenTypeLower, secretName);
    }

    private static async Task PutSecretMappingAsync(IAmazonDynamoDB dynamo, string orgLower, string tokenTypeLower, string secretName)
    {
        await dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = OrgSecretsTableName,
            Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new($"ORG#{orgLower}"),
                ["SK"] = new($"CONST#GITHUB#{tokenTypeLower}"),
                ["SecretName"] = new(secretName),
            },
        });
    }

    private static async Task CreateSecretAsync(IAmazonSecretsManager secrets, string name, string secretString)
    {
        if (await SecretExistsAsync(secrets, name))
        {
            return;
        }

        await secrets.CreateSecretAsync(new CreateSecretRequest
        {
            Name = name,
            SecretString = secretString,
        });
    }

    private static async Task<bool> SecretExistsAsync(IAmazonSecretsManager secrets, string name)
    {
        try
        {
            await secrets.DescribeSecretAsync(new DescribeSecretRequest { SecretId = name });
            return true;
        }
        catch (Amazon.SecretsManager.Model.ResourceNotFoundException)
        {
            return false;
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

        if (await TableExistsAsync(dynamo, name))
        {
            return;
        }

        await dynamo.CreateTableAsync(request);
    }

    private static async Task<bool> TableExistsAsync(IAmazonDynamoDB dynamo, string name)
    {
        try
        {
            await dynamo.DescribeTableAsync(name);
            return true;
        }
        catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException)
        {
            return false;
        }
    }
}
