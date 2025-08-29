using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;
using static BadgeSmith.Constants;

namespace BadgeSmith.CDK.Shared.Constructs;

/// <summary>
/// Construct that creates an IAM execution role for the BadgeSmith Lambda function.
/// Provides least-privilege access to DynamoDB tables and Secrets Manager for HMAC keys and API tokens.
/// Follows AWS security best practices with specific resource ARN patterns.
/// </summary>
public class LambdaExecutionRoleConstruct : Construct
{
    public LambdaExecutionRoleConstruct(Construct scope, string id) : base(scope, id)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // Lambda Execution Role with minimal required permissions
        Role = new Role(this, LambdaExecutionRoleId, new RoleProps
        {
            RoleName = LambdaExecutionRoleName,
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            ManagedPolicies = [ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")],
            Description = "Execution role for BadgeSmith Lambda function with least privilege access",
        });

        // Grant Secrets Manager permissions for HMAC keys and provider tokens
        Role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = ["secretsmanager:GetSecretValue"],
            Resources =
            [
                $"arn:aws:secretsmanager:{Stack.Of(this).Region}:{Stack.Of(this).Account}:secret:badge/repo/*",
                $"arn:aws:secretsmanager:{Stack.Of(this).Region}:{Stack.Of(this).Account}:secret:badge/github/*",
                $"arn:aws:secretsmanager:{Stack.Of(this).Region}:{Stack.Of(this).Account}:secret:badge/nuget/*",
            ],
        }));

        _ = new CfnOutput(this, LambdaExecutionOutputRoleArn, new CfnOutputProps
        {
            Value = Role.RoleArn,
            Description = "ARN of the Lambda execution role",
        });
    }

    /// <summary>
    /// IAM execution role for the BadgeSmith Lambda function with least-privilege permissions.
    /// Includes CloudWatch Logs access, DynamoDB read/write permissions, and Secrets Manager access for authentication tokens.
    /// </summary>
    public Role Role { get; }
}
