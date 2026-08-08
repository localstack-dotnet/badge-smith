using Amazon.CDK;
using BadgeSmith.CDK.Shared;
using static BadgeSmith.Constants;

var app = new App();

var env = CreateLocalPerformanceEnvironment(app);
var localPerformanceSettings = CreateLocalPerformanceSettings(app);

_ = new LocalPerformanceStack(app, LocalPerformanceStackId, localPerformanceSettings, new StackProps
{
    Env = env,
    Description = "BadgeSmith local performance infrastructure for LocalStack benchmarking",
});

app.Synth();

static Amazon.CDK.Environment CreateLocalPerformanceEnvironment(App app)
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

static LocalPerformanceStackSettings CreateLocalPerformanceSettings(App app)
{
    var lambdaAssetPath = GetContextValue(app, "lambdaZipPath", "../../artifacts/badge-lambda-linux-x64.zip");
    var lambdaArchitecture = GetLambdaArchitecture(GetContextValue(app, "lambdaArchitecture", "x86_64"));
    var httpNuGetBaseUrl = GetLiveUpstreamUrl(app, "httpNuGetBaseUrl", "https://api.nuget.org/");
    var httpGitHubBaseUrl = GetLiveUpstreamUrl(app, "httpGitHubBaseUrl", "https://api.github.com/");
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

static string GetLiveUpstreamUrl(App app, string key, string defaultValue)
{
    var value = GetContextValue(app, key, defaultValue);
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
        || string.IsNullOrWhiteSpace(uri.Host)
        || !string.IsNullOrEmpty(uri.UserInfo)
        || !string.IsNullOrEmpty(uri.Query)
        || !string.IsNullOrEmpty(uri.Fragment)
        || uri.Scheme != Uri.UriSchemeHttps)
    {
        throw new InvalidOperationException(
            $"CDK context '{key}' must be an absolute HTTPS URL without credentials, query, or fragment in Live mode.");
    }

    return value;
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
