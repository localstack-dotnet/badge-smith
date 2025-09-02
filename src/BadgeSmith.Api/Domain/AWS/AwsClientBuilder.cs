#if !ENABLE_LOCALSTACK
using Amazon.Runtime;

namespace BadgeSmith.Api.Domain.AWS;

internal static class AwsClientBuilder
{
    public static TClient CreateAwsClient<TClient>() where TClient : AmazonServiceClient, IAmazonService, new()
    {
        return new TClient();
    }
}
#endif

#if ENABLE_LOCALSTACK

using System.Globalization;
using Amazon.Runtime;
using LocalStack.Client;
using LocalStack.Client.Contracts;
using LocalStack.Client.Options;

namespace BadgeSmith.Api.Domain.AWS;

internal static class AwsClientBuilder
{
    private static readonly ISession Session = SessionStandalone
        .Init()
        .WithConfigurationOptions(BuildConfigOptions())
        .WithSessionOptions(BuildSessionOptions())
        .Create();

    public static TClient CreateAwsClient<TClient>() where TClient : AmazonServiceClient, IAmazonService, new()
    {
        return Settings.UseLocalStack ? Session.CreateClientByImplementation<TClient>() : new TClient();
    }

    public static ConfigOptions BuildConfigOptions()
    {
        var localStackHost = Environment.GetEnvironmentVariable("LocalStack__Config__LocalStackHost");
        var localStackPort = Environment.GetEnvironmentVariable("LocalStack__Config__EdgePort");
        var useLegacyPorts = Environment.GetEnvironmentVariable("LocalStack__Config__UseLegacyPorts");
        var useSsl = Environment.GetEnvironmentVariable("LocalStack__Config__UseSsl");

        if (string.IsNullOrEmpty(localStackHost) || string.IsNullOrEmpty(localStackPort))
        {
            var localStackConnection = Environment.GetEnvironmentVariable("ConnectionStrings__localstack");
            if (!string.IsNullOrEmpty(localStackConnection) && Uri.TryCreate(localStackConnection, UriKind.Absolute, out var uri))
            {
                localStackHost ??= uri.Host;
                localStackPort ??= uri.Port.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (string.IsNullOrEmpty(localStackHost) && string.IsNullOrEmpty(localStackPort) && string.IsNullOrEmpty(useLegacyPorts) && string.IsNullOrEmpty(useSsl))
        {
            return new ConfigOptions();
        }

        int? edgePort;
        if (string.IsNullOrEmpty(localStackPort))
        {
            edgePort = null;
        }
        else if (int.TryParse(localStackPort, CultureInfo.InvariantCulture, out var port))
        {
            edgePort = port;
        }
        else
        {
            edgePort = null;
        }

        bool? legacyPorts;
        if (string.IsNullOrEmpty(useLegacyPorts))
        {
            legacyPorts = null;
        }
        else if (bool.TryParse(useLegacyPorts, out var legacy))
        {
            legacyPorts = legacy;
        }
        else
        {
            legacyPorts = null;
        }

        bool? ssl;
        if (string.IsNullOrEmpty(useSsl))
        {
            ssl = null;
        }
        else if (bool.TryParse(useSsl, out var sslValue))
        {
            ssl = sslValue;
        }
        else
        {
            ssl = null;
        }

        return new ConfigOptions(
            localStackHost ?? LocalStack.Client.Models.Constants.LocalStackHost,
            legacyPorts ?? LocalStack.Client.Models.Constants.UseLegacyPorts,
            ssl ?? LocalStack.Client.Models.Constants.UseSsl,
            edgePort ?? LocalStack.Client.Models.Constants.EdgePort
        );
    }

    public static SessionOptions BuildSessionOptions()
    {
        var awsAccessKey = Environment.GetEnvironmentVariable("LocalStack__Session__AwsAccessKey");
        var awsAccessKeyId = Environment.GetEnvironmentVariable("LocalStack__Session__AwsAccessKeyId");
        var awsSessionToken = Environment.GetEnvironmentVariable("LocalStack__Session__AwsSessionToken");
        var regionName = Environment.GetEnvironmentVariable("LocalStack__Session__RegionName");

        if (string.IsNullOrEmpty(awsAccessKey) && string.IsNullOrEmpty(awsAccessKeyId) && string.IsNullOrEmpty(awsSessionToken) && string.IsNullOrEmpty(regionName))
        {
            return new SessionOptions();
        }

        return new SessionOptions(
            awsAccessKeyId: string.IsNullOrEmpty(awsAccessKeyId) ? LocalStack.Client.Models.Constants.AwsAccessKeyId : awsAccessKeyId,
            awsAccessKey: string.IsNullOrEmpty(awsAccessKey) ? LocalStack.Client.Models.Constants.AwsAccessKey : awsAccessKey,
            awsSessionToken: string.IsNullOrEmpty(awsSessionToken) ? LocalStack.Client.Models.Constants.AwsSessionToken : awsSessionToken,
            regionName: string.IsNullOrEmpty(regionName) ? LocalStack.Client.Models.Constants.RegionName : regionName
        );
    }
}
#endif
