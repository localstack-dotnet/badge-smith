using BadgeSmith.Tools.Configuration;

namespace BadgeSmith.Tools.Services;

internal interface IAwsOptionsResolver
{
    internal EffectiveAwsOptions Resolve(IAwsCommandSettings settings);
}
