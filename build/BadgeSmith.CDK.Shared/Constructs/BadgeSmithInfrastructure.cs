#pragma warning disable CA1711, MA0051

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace BadgeSmith.CDK.Shared.Constructs;

/// <summary>
/// Reusable construct that defines all BadgeSmith infrastructure resources.
/// This ensures identical resource configuration between local and production environments.
/// Can be used in both flat stacks (Aspire) and nested stacks (Production).
/// </summary>
public class BadgeSmithInfrastructure : Construct
{
    public BadgeSmithInfrastructure(Construct scope, string id) : base(scope, id)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // Test Results Table - stores badge test results with TTL
        TestResultsTable = new Table(this, "TestResultsTable", new TableProps
        {
            TableName = "badge-test-results",
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
            RemovalPolicy = RemovalPolicy.DESTROY, // For development/testing
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
        NonceTable = new Table(this, "NonceTable", new TableProps
        {
            TableName = "hmac-nonce",
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

        // Lambda Execution Role with minimal required permissions
        LambdaExecutionRole = new Role(this, "LambdaExecutionRole", new RoleProps
        {
            RoleName = "BadgeSmithLambdaExecutionRole",
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            ManagedPolicies = [ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")],
            Description = "Execution role for BadgeSmith Lambda function with least privilege access",
        });

        // Grant DynamoDB permissions to Lambda role
        TestResultsTable.GrantReadWriteData(LambdaExecutionRole);
        NonceTable.GrantReadWriteData(LambdaExecutionRole);

        // Grant Secrets Manager permissions for HMAC keys and provider tokens
        LambdaExecutionRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = ["secretsmanager:GetSecretValue"],
            Resources =
            [
                $"arn:aws:secretsmanager:{((Stack)scope).Region}:{((Stack)scope).Account}:secret:badge/repo/*",
                $"arn:aws:secretsmanager:{((Stack)scope).Region}:{((Stack)scope).Account}:secret:badge/github/*",
                $"arn:aws:secretsmanager:{((Stack)scope).Region}:{((Stack)scope).Account}:secret:badge/nuget/*",
            ],
        }));
    }

    /// <summary>
    /// DynamoDB table for storing test results with TTL and GSI for latest lookup
    /// </summary>
    public Table TestResultsTable { get; }

    /// <summary>
    /// DynamoDB table for HMAC nonce storage to prevent replay attacks
    /// </summary>
    public Table NonceTable { get; }

    /// <summary>
    /// IAM role that the Lambda function would assume in production
    /// Used for testing IAM permission scenarios locally
    /// </summary>
    public Role LambdaExecutionRole { get; }
}
