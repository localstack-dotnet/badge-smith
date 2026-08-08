namespace BadgeSmith.Tools.Configuration;

internal sealed record EffectiveAwsOptions(
    bool UseLocalStack,
    string Region,
    string? Profile,
    string LocalStackHost,
    int LocalStackEdgePort,
    bool LocalStackUseSsl,
    bool LocalStackUseLegacyPorts,
    string LocalStackAccessKeyId,
    string LocalStackSecretAccessKey,
    string LocalStackSessionToken);
