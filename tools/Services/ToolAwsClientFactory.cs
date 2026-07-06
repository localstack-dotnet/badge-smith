using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SecretsManager;
using BadgeSmith.Tools.Configuration;
using LocalStack.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BadgeSmith.Tools.Services;

internal sealed class ToolAwsClientFactory : IToolAwsClientFactory
{
    public ToolAwsClientScope Create(EffectiveAwsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuration = BuildConfiguration(options);
        var services = new ServiceCollection();
        services.AddLocalStack(configuration);
        services.AddDefaultAWSOptions(BuildAwsOptions(options));
        services.AddAwsService<IAmazonDynamoDB>();
        services.AddAwsService<IAmazonSecretsManager>();

        var serviceProvider = services.BuildServiceProvider();
        return new ToolAwsClientScope(
            serviceProvider,
            serviceProvider.GetRequiredService<IAmazonDynamoDB>(),
            serviceProvider.GetRequiredService<IAmazonSecretsManager>());
    }

    private static IConfiguration BuildConfiguration(EffectiveAwsOptions options)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["LocalStack:UseLocalStack"] = options.UseLocalStack.ToString(),
            ["LocalStack:Config:LocalStackHost"] = options.LocalStackHost,
            ["LocalStack:Config:EdgePort"] = options.LocalStackEdgePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["LocalStack:Config:UseSsl"] = options.LocalStackUseSsl.ToString(),
            ["LocalStack:Config:UseLegacyPorts"] = options.LocalStackUseLegacyPorts.ToString(),
            ["LocalStack:Session:AwsAccessKeyId"] = options.LocalStackAccessKeyId,
            ["LocalStack:Session:AwsAccessKey"] = options.LocalStackSecretAccessKey,
            ["LocalStack:Session:AwsSessionToken"] = options.LocalStackSessionToken,
            ["LocalStack:Session:RegionName"] = options.Region,
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static AWSOptions BuildAwsOptions(EffectiveAwsOptions options)
    {
        return new AWSOptions
        {
            Profile = options.Profile,
            Region = RegionEndpoint.GetBySystemName(options.Region),
        };
    }
}
