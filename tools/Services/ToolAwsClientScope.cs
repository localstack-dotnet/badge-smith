using Amazon.DynamoDBv2;
using Amazon.SecretsManager;
using Microsoft.Extensions.DependencyInjection;

namespace BadgeSmith.Tools.Services;

internal sealed class ToolAwsClientScope : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public ToolAwsClientScope(ServiceProvider serviceProvider, IAmazonDynamoDB dynamoDb, IAmazonSecretsManager secretsManager)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        DynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        SecretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
    }

    public IAmazonDynamoDB DynamoDb { get; }

    public IAmazonSecretsManager SecretsManager { get; }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
