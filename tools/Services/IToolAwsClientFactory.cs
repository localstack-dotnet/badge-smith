using BadgeSmith.Tools.Configuration;

namespace BadgeSmith.Tools.Services;

internal interface IToolAwsClientFactory
{
    internal ToolAwsClientScope Create(EffectiveAwsOptions options);
}
