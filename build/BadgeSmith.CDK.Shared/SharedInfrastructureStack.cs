#pragma warning disable CA1711, MA0051, MA0056

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using BadgeSmith.CDK.Shared.Constructs;
using Constructs;

namespace BadgeSmith.CDK.Shared;

/// <summary>
/// Shared infrastructure that should exist in both local (LocalStack) and production environments.
/// This includes IAM roles, DynamoDB tables, and other foundational resources.
/// </summary>
public class SharedInfrastructureStack : Stack
{
    public SharedInfrastructureStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        // Create the shared infrastructure using the reusable construct
        // This ensures identical configuration between local and production environments
        Infrastructure = new BadgeSmithInfrastructure(this, "Infrastructure");

        // Expose the resources through properties for easy access
        TestResultsTable = Infrastructure.TestResultsTable;
        NonceTable = Infrastructure.NonceTable;
        LambdaExecutionRole = Infrastructure.LambdaExecutionRole;

        _ = new CfnOutput(this, "TestResultsTableArn", new CfnOutputProps
        {
            Value = TestResultsTable.TableArn,
            Description = "ARN of the test results DynamoDB table",
        });

        _ = new CfnOutput(this, "NonceTableArn", new CfnOutputProps
        {
            Value = NonceTable.TableArn,
            Description = "ARN of the nonce DynamoDB table",
        });

        _ = new CfnOutput(this, "LambdaExecutionRoleArn", new CfnOutputProps
        {
            Value = LambdaExecutionRole.RoleArn,
            Description = "ARN of the Lambda execution role",
        });
    }

    /// <summary>
    /// The shared infrastructure construct containing all resources
    /// </summary>
    public BadgeSmithInfrastructure Infrastructure { get; }

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
    /// </summary>
    public Role LambdaExecutionRole { get; }
}
