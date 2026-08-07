using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit;
using static BadgeSmith.Constants;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

/// <summary>
/// The collection name is intentionally "aspire-contract" so functional contract
/// tests serialize against this single fixture (DisableParallelization = true).
/// </summary>
[CollectionDefinition("aspire-contract", DisableParallelization = true)]
public sealed class AspireContractFixtureRegistration : ICollectionFixture<AspireContractFixture>;

public sealed class AspireContractFixture : IAsyncLifetime
{
    private const string Region = "eu-central-1";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication? _app;
    private IContainer? _wiremock;

    public ContractHttpClient Api { get; private set; } = null!;
    public IAmazonDynamoDB DynamoDb { get; private set; } = null!;
    public IAmazonSecretsManager Secrets { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
        Environment.SetEnvironmentVariable(UpstreamModeEnvironmentVariable, UpstreamModeMock);

        var wiremockDir = Path.Combine(AppContext.BaseDirectory, "Testing", "Infrastructure", "wiremock");
        _wiremock = new ContainerBuilder("wiremock/wiremock:3.9.1")
            .WithBindMount(wiremockDir, "/home/wiremock", AccessMode.ReadOnly)
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPort(8080).ForPath("/__admin/health")))
            .Build();

        await _wiremock.StartAsync().ConfigureAwait(false);
        var wiremockBaseUrl = $"http://{_wiremock.Hostname}:{_wiremock.GetMappedPublicPort(8080)}";

        // Test-owned WireMock upstreams override the production NuGet/GitHub base URLs.
        // These must be set before the Aspire AppHost builder is created so that the
        // Lambda emulator receives the resolved values via the AppHost's env pass-through.
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", wiremockBaseUrl + "/nuget/");
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", wiremockBaseUrl + "/github/");

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.BadgeSmith_Host>()
            .ConfigureAwait(false);

        _app = await builder.BuildAsync().ConfigureAwait(false);

        using var startupCts = new CancellationTokenSource(StartupTimeout);
        await _app.StartAsync(startupCts.Token).ConfigureAwait(false);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("APIGatewayEmulator", startupCts.Token)
            .ConfigureAwait(false);

        var apiEndpoint = _app.GetEndpoint("APIGatewayEmulator", "http");
        Api = new ContractHttpClient(apiEndpoint);

        var localStackEndpoint = _app.GetEndpoint("localstack", "http");
        var credentials = new BasicAWSCredentials("test", "test");
        DynamoDb = new AmazonDynamoDBClient(credentials, new AmazonDynamoDBConfig
        {
            ServiceURL = localStackEndpoint.ToString().TrimEnd('/'),
            AuthenticationRegion = Region,
        });
        Secrets = new AmazonSecretsManagerClient(credentials, new AmazonSecretsManagerConfig
        {
            ServiceURL = localStackEndpoint.ToString().TrimEnd('/'),
            AuthenticationRegion = Region,
        });

        await AwsTestSeeder.CreateTablesAndSecretsAsync(DynamoDb, Secrets).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", null);
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", null);
        Environment.SetEnvironmentVariable(UpstreamModeEnvironmentVariable, null);

        DynamoDb?.Dispose();
        Secrets?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_wiremock is not null)
        {
            await _wiremock.DisposeAsync().ConfigureAwait(false);
        }
    }
}
