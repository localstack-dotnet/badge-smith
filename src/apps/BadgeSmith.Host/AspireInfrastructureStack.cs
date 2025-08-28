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
        // Create the shared infrastructure using the same construct as production
        // This ensures 100% identical configuration between local and production environments
        Infrastructure = new BadgeSmithInfrastructure(this, InfrastructureConstructId);

        // Expose the resources through properties for easy access by Aspire
        TestResultsTable = Infrastructure.TestResultsTable;
        NonceTable = Infrastructure.NonceTable;
        LambdaExecutionRole = Infrastructure.LambdaExecutionRole;

        // Add LocalStack-specific tags for easier identification
        Tags.SetTag("Environment", "LocalDevelopment");
        Tags.SetTag("Stack", "BadgeSmith-Aspire");
        Tags.SetTag("ManagedBy", "AspireOrchestration");
    }

    /// <summary>
    /// The shared infrastructure construct containing all resources
    /// </summary>
    public BadgeSmithInfrastructure Infrastructure { get; }

    /// <summary>
    /// DynamoDB table for storing test results with TTL and GSI for the latest lookup
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
