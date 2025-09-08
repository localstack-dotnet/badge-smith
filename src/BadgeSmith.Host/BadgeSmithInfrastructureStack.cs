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
/// Uses the same shared SharedInfrastructureConstruct construct as production to ensure parity.
/// This avoids nested stack complexity that can cause issues with AWS Aspire's CDK provisioner.
/// </summary>
#pragma warning disable CA1812
internal sealed class BadgeSmithInfrastructureStack : Stack
{
    public BadgeSmithInfrastructureStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        var sharedInfrastructureConstruct = new SharedInfrastructureConstruct(this, SharedInfrastructureConstructId);

        TestResultsTable = sharedInfrastructureConstruct.TestResultsTable;
        NonceTable = sharedInfrastructureConstruct.NonceTable;
        LambdaExecutionRole = sharedInfrastructureConstruct.LambdaExecutionRole;
        OrgSecretsTable = sharedInfrastructureConstruct.OrgSecretsTable;

        Tags.SetTag("environment", "Local");
        Tags.SetTag("stack", "badge-smith-local-aspire");
        Tags.SetTag("managed-by", "aspire-orchestration");
    }

    public Table TestResultsTable { get; }

    public Table NonceTable { get; }

    public Table OrgSecretsTable { get; }

    public Role LambdaExecutionRole { get; }
}
