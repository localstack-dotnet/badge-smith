using Amazon.CDK;
using BadgeSmith.CDK.Shared;
using static BadgeSmith.Constants;

var app = new App();

var env = CreateEnvironment(app);

_ = new ProductionStack(app, ProductionStackId, new StackProps
{
    Env = env,
    Description = "BadgeSmith production infrastructure with CloudFront and SSL certificate",
});

app.Synth();

static Amazon.CDK.Environment CreateEnvironment(App app)
{
    return new Amazon.CDK.Environment
    {
        Account = GetRequiredEnvironmentValue(app, "account", "CDK_DEFAULT_ACCOUNT"),
        Region = GetRequiredEnvironmentValue(app, "region", "CDK_DEFAULT_REGION"),
    };
}

static string GetRequiredEnvironmentValue(App app, string contextKey, string environmentVariable)
{
    var value = app.Node.TryGetContext(contextKey) as string
                ?? System.Environment.GetEnvironmentVariable(environmentVariable);

    return !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException(
            $"CDK environment value '{contextKey}' is required. Set CDK context '{contextKey}' or {environmentVariable}.");
}
