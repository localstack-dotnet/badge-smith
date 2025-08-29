#pragma warning disable CA1711, MA0051, MA0056

using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Constructs;
using static BadgeSmith.Constants;

namespace BadgeSmith.CDK.Shared.Constructs;

/// <summary>
/// Shared infrastructure construct that provides foundational AWS resources for both local (LocalStack) and production environments.
/// Includes Lambda execution role, DynamoDB tables for test results and nonce storage, and the Lambda function itself.
/// Ensures identical resource configuration across all deployment environments.
/// </summary>
public class SharedInfrastructureConstruct : Construct
{
    public SharedInfrastructureConstruct(Construct scope, string id) : base(scope, id)
    {
        LambdaExecutionRoleConstruct = new LambdaExecutionRoleConstruct(this, LambdaExecutionRoleConstructId);
        DynamoDbTablesConstruct = new DynamoDbTablesConstruct(this, LambdaExecutionRoleConstruct.Role, DynamoDbTablesConstructId);

        // Expose the resources through properties for easy access
        TestResultsTable = DynamoDbTablesConstruct.TestResultsTable;
        NonceTable = DynamoDbTablesConstruct.NonceTable;
        LambdaExecutionRole = LambdaExecutionRoleConstruct.Role;
    }

    /// <summary>
    /// Lambda execution role construct that provides IAM permissions for the BadgeSmith function.
    /// </summary>
    public LambdaExecutionRoleConstruct LambdaExecutionRoleConstruct { get; }

    /// <summary>
    /// DynamoDB tables construct containing test results and nonce tables with proper IAM permissions.
    /// </summary>
    public DynamoDbTablesConstruct DynamoDbTablesConstruct { get; }

    /// <summary>
    /// DynamoDB table for storing test results with TTL and GSI for latest lookup
    /// </summary>
    public Table TestResultsTable { get; }

    /// <summary>
    /// DynamoDB table for HMAC nonce storage to prevent replay attacks
    /// </summary>
    public Table NonceTable { get; }

    /// <summary>
    /// IAM role used by the Lambda function with least-privilege permissions for DynamoDB and Secrets Manager access.
    /// </summary>
    public Role LambdaExecutionRole { get; }
}
