using BadgeSmith.Tools.Configuration;
using Microsoft.Extensions.Configuration;
using LocalStackConstants = LocalStack.Client.Models.Constants;

namespace BadgeSmith.Tools.Services;

internal sealed class AwsOptionsResolver : IAwsOptionsResolver
{
    private readonly IConfiguration _configuration;

    public AwsOptionsResolver(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public EffectiveAwsOptions Resolve(IAwsCommandSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var configuredLocalStack = _configuration.GetValue("LocalStack:UseLocalStack", false);
        var useLocalStack = !settings.NoLocalStack && (settings.LocalStack || configuredLocalStack);
        var liveRegion = FirstNonBlank(settings.AwsRegion, _configuration["AWS:Region"], _configuration["AWS_REGION"], _configuration["AWS_DEFAULT_REGION"], LocalStackConstants.RegionName);
        var region = useLocalStack
            ? FirstNonBlank(settings.AwsRegion, _configuration["LocalStack:Session:RegionName"], liveRegion, LocalStackConstants.RegionName)
            : liveRegion;
        var profile = useLocalStack
            ? null
            : FirstNonBlankOrNull(settings.AwsProfile, _configuration["AWS:Profile"], _configuration["AWS_PROFILE"]);

        return new EffectiveAwsOptions(
            useLocalStack,
            region,
            profile,
            FirstNonBlank(_configuration["LocalStack:Config:LocalStackHost"], LocalStackConstants.LocalStackHost),
            GetInt("LocalStack:Config:EdgePort", LocalStackConstants.EdgePort),
            GetBool("LocalStack:Config:UseSsl", LocalStackConstants.UseSsl),
            GetBool("LocalStack:Config:UseLegacyPorts", LocalStackConstants.UseLegacyPorts),
            FirstNonBlank(_configuration["LocalStack:Session:AwsAccessKeyId"], LocalStackConstants.AwsAccessKeyId),
            FirstNonBlank(_configuration["LocalStack:Session:AwsAccessKey"], _configuration["LocalStack:Session:AwsSecretAccessKey"], LocalStackConstants.AwsAccessKey),
            FirstNonBlank(_configuration["LocalStack:Session:AwsSessionToken"], LocalStackConstants.AwsSessionToken));
    }

    private bool GetBool(string key, bool fallback)
    {
        return _configuration.GetValue(key, fallback);
    }

    private int GetInt(string key, int fallback)
    {
        return _configuration.GetValue(key, fallback);
    }

    private static string FirstNonBlank(params string?[] values)
    {
        return FirstNonBlankOrNull(values) ?? string.Empty;
    }

    private static string? FirstNonBlankOrNull(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
    }
}
