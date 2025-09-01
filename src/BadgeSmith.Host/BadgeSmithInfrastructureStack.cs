#pragma warning disable CA1711

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using BadgeSmith.CDK.Shared.Constructs;
using Constructs;
using static BadgeSmith.Constants;

namespace BadgeSmith.Host;

/// <summary>
/// Flattened infrastructure stack specifically for Aspire + LocalStack integration.
/// Uses the same shared BadgeSmithInfrastructure construct as production to ensure parity.
/// This avoids nested stack complexity that can cause issues with AWS Aspire's CDK provisioner.
/// </summary>
#pragma warning disable CA1812
internal sealed class BadgeSmithInfrastructureStack : Stack
{
    public BadgeSmithInfrastructureStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        var sharedInfrastructureConstruct = new SharedInfrastructureConstruct(this, SharedInfrastructureConstructId);

        // Expose the resources through properties for easy access by Aspire
        TestResultsTable = sharedInfrastructureConstruct.TestResultsTable;
        NonceTable = sharedInfrastructureConstruct.NonceTable;
        LambdaExecutionRole = sharedInfrastructureConstruct.LambdaExecutionRole;
        OrgSecretsTable = sharedInfrastructureConstruct.OrgSecretsTable;

        // Add LocalStack-specific tags for easier identification
        Tags.SetTag("environment", "Local");
        Tags.SetTag("stack", "badge-smith-local-aspire");
        Tags.SetTag("managed-by", "aspire-orchestration");
    }

    /// <summary>
    /// DynamoDB table for storing test results with TTL and GSI for the latest lookup
    /// </summary>
    public Table TestResultsTable { get; }

    /// <summary>
    /// DynamoDB table for HMAC nonce storage to prevent replay attacks
    /// </summary>
    public Table NonceTable { get; }

    /// <summary>
    /// DynamoDB table mapping GitHub org to Secrets Manager secret name
    /// </summary>
    public Table OrgSecretsTable { get; }

    /// <summary>
    /// IAM role that the Lambda function would assume in production
    /// Used for testing IAM permission scenarios locally
    /// </summary>
    public Role LambdaExecutionRole { get; }
}
