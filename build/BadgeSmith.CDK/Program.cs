using Amazon.CDK;
using BadgeSmith.CDK.Shared;
using static BadgeSmith.Constants;

var app = new App();

// Get environment from CDK context or CLI
var env = new Amazon.CDK.Environment
{
    Account = app.Node.TryGetContext("account") as string ?? System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
    Region = app.Node.TryGetContext("region") as string ?? System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION"),
};

_ = new ProductionStack(app, ProductionStackId, new StackProps
{
    Env = env,
    Description = "BadgeSmith production infrastructure with CloudFront and SSL certificate",
});

app.Synth();
