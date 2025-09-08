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

        TestResultsTable = DynamoDbTablesConstruct.TestResultsTable;
        NonceTable = DynamoDbTablesConstruct.NonceTable;
        OrgSecretsTable = DynamoDbTablesConstruct.OrgSecretsTable;
        LambdaExecutionRole = LambdaExecutionRoleConstruct.Role;
    }

    public LambdaExecutionRoleConstruct LambdaExecutionRoleConstruct { get; }

    public DynamoDbTablesConstruct DynamoDbTablesConstruct { get; }

    public Table TestResultsTable { get; }

    public Table NonceTable { get; }

    public Table OrgSecretsTable { get; }

    public Role LambdaExecutionRole { get; }
}
