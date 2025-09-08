#pragma warning disable CA1711, MA0051

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Constructs;
using static BadgeSmith.Constants;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace BadgeSmith.CDK.Shared.Constructs;

/// <summary>
/// Construct that creates DynamoDB tables for BadgeSmith application data storage.
/// Includes test results table with GSI for efficient latest-result queries and nonce table for HMAC replay protection.
/// Automatically grants read/write permissions to the provided Lambda execution role.
/// </summary>
public class DynamoDbTablesConstruct : Construct
{
    public DynamoDbTablesConstruct(Construct scope, IRole lambdaExecutionRole, string id) : base(scope, id)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(lambdaExecutionRole);

        // Test Results Table - stores badge test results with TTL
        TestResultsTable = new Table(this, TestResultsTableId, new TableProps
        {
            TableName = TestResultsTableName,
            PartitionKey = new Attribute
            {
                Name = "PK",
                Type = AttributeType.STRING,
            },
            SortKey = new Attribute
            {
                Name = "SK",
                Type = AttributeType.STRING,
            },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            TimeToLiveAttribute = "TTL",
            RemovalPolicy = RemovalPolicy.DESTROY,
        });

        // GSI for latest results lookup
        TestResultsTable.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
        {
            IndexName = "GSI1",
            PartitionKey = new Attribute
            {
                Name = "GSI1PK",
                Type = AttributeType.STRING,
            },
            SortKey = new Attribute
            {
                Name = "GSI1SK",
                Type = AttributeType.STRING,
            },
            ProjectionType = ProjectionType.ALL,
        });

        // HMAC Nonce Table - prevents replay attacks
        NonceTable = new Table(this, NonceTableId, new TableProps
        {
            TableName = NonceTableName,
            PartitionKey = new Attribute
            {
                Name = "PK",
                Type = AttributeType.STRING,
            },
            SortKey = new Attribute
            {
                Name = "SK",
                Type = AttributeType.STRING,
            },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            TimeToLiveAttribute = "TTL",
            RemovalPolicy = RemovalPolicy.DESTROY,
        });

        OrgSecretsTable = new Table(this, OrgSecretsTableId, new TableProps
        {
            TableName = OrgSecretsTableName,
            PartitionKey = new Attribute
            {
                Name = "PK", // ORG#{org}
                Type = AttributeType.STRING,
            },
            SortKey = new Attribute
            {
                Name = "SK", // CONST#GITHUB#{secret_type}
                Type = AttributeType.STRING,
            },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY,
        });

        // Grant DynamoDB permissions to Lambda role
        TestResultsTable.GrantReadWriteData(lambdaExecutionRole);
        NonceTable.GrantReadWriteData(lambdaExecutionRole);
        OrgSecretsTable.GrantReadData(lambdaExecutionRole);

        _ = new CfnOutput(this, TestResultsOutputTableArn, new CfnOutputProps
        {
            Value = TestResultsTable.TableArn,
            Description = "ARN of the test results DynamoDB table",
        });

        _ = new CfnOutput(this, NonceTableOutputArn, new CfnOutputProps
        {
            Value = NonceTable.TableArn,
            Description = "ARN of the nonce DynamoDB table",
        });

        _ = new CfnOutput(this, OrgSecretsTableOutputArn, new CfnOutputProps
        {
            Value = OrgSecretsTable.TableArn,
            Description = "ARN of the GitHub org secrets mapping table",
        });
    }

    public Table TestResultsTable { get; }

    public Table NonceTable { get; }

    public Table OrgSecretsTable { get; }
}
