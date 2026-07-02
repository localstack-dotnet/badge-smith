using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.SecretsManager;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Net;
using System.Text;
using Testcontainers.LocalStack;
using Xunit;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

[CollectionDefinition("contract", DisableParallelization = true)]
public sealed class ContractFixtureRegistration : ICollectionFixture<BadgeSmithStackFixture>;

public sealed class BadgeSmithStackFixture : IAsyncLifetime
{
    private const string Region = "eu-central-1";

    public const string HmacSecret = "contract-test-secret";
    public const string Org = "test-org";

    private const string LambdaImageDefault = "badge-smith:local";
    private const string LambdaBuildHint =
        "docker build -f src/BadgeSmith.Api/Dockerfile --target lambda-image -t badge-smith:local .";
    private const string LambdaHealthProbeEventJson =
        """
        {
          "version": "2.0",
          "routeKey": "$default",
          "rawPath": "/health",
          "headers": {},
          "requestContext": {
            "http": {
              "method": "GET",
              "path": "/health"
            },
            "stage": "$default",
            "requestId": "contract-warmup"
          },
          "body": null,
          "isBase64Encoded": false
        }
        """;
    private static readonly TimeSpan LambdaStartupInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LambdaStartupTimeout = TimeSpan.FromSeconds(80);

    private INetwork? _network;
    private LocalStackContainer? _localstack;
    private IContainer? _wiremock;
    private IContainer? _lambda;

    public LambdaRieClient Lambda { get; private set; } = null!;
    public IAmazonDynamoDB DynamoDb { get; private set; } = null!;
    public IAmazonSecretsManager Secrets { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var image = Environment.GetEnvironmentVariable("BADGESMITH_TEST_IMAGE") ?? LambdaImageDefault;
        var wiremockDir = Path.Combine(AppContext.BaseDirectory, "Testing", "Infrastructure", "wiremock");

        _network = new NetworkBuilder().Build();

        _localstack = new LocalStackBuilder("localstack/localstack:4.6")
            .WithNetwork(_network)
            .WithNetworkAliases("localstack")
            .Build();

        _wiremock = new ContainerBuilder("wiremock/wiremock:3.9.1")
            .WithNetwork(_network)
            .WithNetworkAliases("wiremock")
            .WithBindMount(wiremockDir, "/home/wiremock", AccessMode.ReadOnly)
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/__admin/health")))
            .Build();

        await Task.WhenAll(_localstack.StartAsync(), _wiremock.StartAsync());

        var creds = new BasicAWSCredentials("test", "test");
        DynamoDb = new AmazonDynamoDBClient(creds, new AmazonDynamoDBConfig
        {
            ServiceURL = _localstack.GetConnectionString(),
            AuthenticationRegion = Region,
        });
        Secrets = new AmazonSecretsManagerClient(creds, new AmazonSecretsManagerConfig
        {
            ServiceURL = _localstack.GetConnectionString(),
            AuthenticationRegion = Region,
        });

        await AwsTestSeeder.CreateTablesAndSecretsAsync(DynamoDb, Secrets);

        _lambda = new ContainerBuilder(image)
            .WithNetwork(_network)
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithEnvironment("DOTNET_ENVIRONMENT", "Production")
            .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
            .WithEnvironment("AWS_REGION", Region)
            .WithEnvironment("AWS_DEFAULT_REGION", Region)
            .WithEnvironment("AWS_ENDPOINT_URL_DYNAMODB", "http://localstack:4566")
            .WithEnvironment("AWS_ENDPOINT_URL_SECRETS_MANAGER", "http://localstack:4566")
            .WithEnvironment("AWS_RESOURCE_TEST_RESULTS_TABLE", "badge-smith-test-result")
            .WithEnvironment("AWS_RESOURCE_NONCE_TABLE", "badge-smith-hmac-nonce")
            .WithEnvironment("AWS_RESOURCE_ORG_SECRETS_TABLE", "badge-smith-github-org-secrets")
            .WithEnvironment("HTTP_NUGET_BASE_URL", "http://wiremock:8080/nuget/")
            .WithEnvironment("HTTP_GITHUB_BASE_URL", "http://wiremock:8080/github/")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(8080)
                    .ForPath("/2015-03-31/functions/function/invocations")
                    .WithMethod(HttpMethod.Post)
                    .WithContent(() => new StringContent(LambdaHealthProbeEventJson, Encoding.UTF8, "application/json"))
                    .ForStatusCode(HttpStatusCode.OK)
                    .ForResponseMessageMatching(IsHealthyLambdaResponseAsync),
                wait => wait.WithInterval(LambdaStartupInterval).WithTimeout(LambdaStartupTimeout)))
            .Build();

        try
        {
            await _lambda.StartAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start image '{image}'. Build it first with: {LambdaBuildHint}", ex);
        }

        Lambda = new LambdaRieClient(new Uri($"http://{_lambda.Hostname}:{_lambda.GetMappedPublicPort(8080)}"));
    }

    private static async Task<bool> IsHealthyLambdaResponseAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return body.Contains("\"statusCode\":200", StringComparison.Ordinal)
               && body.Contains("Healthy", StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        if (_lambda is not null)
        {
            await _lambda.DisposeAsync();
        }

        if (_wiremock is not null)
        {
            await _wiremock.DisposeAsync();
        }

        if (_localstack is not null)
        {
            await _localstack.DisposeAsync();
        }

        if (_network is not null)
        {
            await _network.DisposeAsync();
        }

        DynamoDb?.Dispose();
        Secrets?.Dispose();
    }
}
