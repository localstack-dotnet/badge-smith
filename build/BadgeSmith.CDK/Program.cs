using Amazon.CDK;
using BadgeSmith.CDK.Shared;
using static BadgeSmith.Constants;

var app = new App();

var stack = app.Node.TryGetContext("stack") as string;

// Get environment from CDK context or CLI
var env = stack is "local-performance"
    ? CreateLocalPerformanceEnvironment(app)
    : new Amazon.CDK.Environment
    {
        Account = app.Node.TryGetContext("account") as string ?? System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
        Region = app.Node.TryGetContext("region") as string ?? System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION"),
    };

if (stack is "local-performance")
{
    var localPerformanceSettings = CreateLocalPerformanceSettings(app);

    _ = new LocalPerformanceStack(app, LocalPerformanceStackId, localPerformanceSettings, new StackProps
    {
        Env = env,
        Description = "BadgeSmith local performance infrastructure for LocalStack benchmarking",
    });
}
else
{
    _ = new ProductionStack(app, ProductionStackId, new StackProps
    {
        Env = env,
        Description = "BadgeSmith production infrastructure with CloudFront and SSL certificate",
    });
}

app.Synth();

static Amazon.CDK.Environment CreateLocalPerformanceEnvironment(App app)
{
    return new Amazon.CDK.Environment
    {
        Account = app.Node.TryGetContext("account") as string
                  ?? "000000000000",
        Region = app.Node.TryGetContext("region") as string
                 ?? "us-east-1",
    };
}

static LocalPerformanceStackSettings CreateLocalPerformanceSettings(App app)
{
    var lambdaAssetPath = GetContextValue(app, "lambdaZipPath", "../artifacts/badge-lambda-linux-x64.zip");
    var lambdaArchitecture = GetLambdaArchitecture(GetContextValue(app, "lambdaArchitecture", "x86_64"));
    var httpNuGetBaseUrl = GetContextValue(app, "httpNuGetBaseUrl", "https://api.nuget.org/");
    var httpGitHubBaseUrl = GetContextValue(app, "httpGitHubBaseUrl", "https://api.github.com/");
#pragma warning disable S5332 // LocalStack container endpoint is HTTP-only inside the Docker network.
    var localStackEndpoint = GetContextValue(app, "localStackEndpoint", "http://localstack:4566");
#pragma warning restore S5332

    return new LocalPerformanceStackSettings(
        lambdaAssetPath,
        lambdaArchitecture,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AWS_ENDPOINT_URL"] = localStackEndpoint,
            ["AWS_ENDPOINT_URL_DYNAMODB"] = localStackEndpoint,
            ["AWS_ENDPOINT_URL_SECRETS_MANAGER"] = localStackEndpoint,
            ["AWS_ENDPOINT_URL_SECRETSMANAGER"] = localStackEndpoint,
            ["HTTP_NUGET_BASE_URL"] = httpNuGetBaseUrl,
            ["HTTP_GITHUB_BASE_URL"] = httpGitHubBaseUrl,
        });
}

static string GetContextValue(App app, string key, string defaultValue)
{
    return app.Node.TryGetContext(key) as string ?? defaultValue;
}

static Amazon.CDK.AWS.Lambda.Architecture GetLambdaArchitecture(string value)
{
    return value switch
    {
        "x86_64" => Amazon.CDK.AWS.Lambda.Architecture.X86_64,
        "arm64" => Amazon.CDK.AWS.Lambda.Architecture.ARM_64,
        _ => throw new ArgumentException("lambdaArchitecture must be either 'x86_64' or 'arm64'.", nameof(value)),
    };
}
