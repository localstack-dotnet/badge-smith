namespace BadgeSmith.Tools.Infrastructure;

internal static class ToolExitCodes
{
    public const int Success = 0;
    public const int GeneralFailure = 1;
    public const int ValidationFailure = 2;
    public const int ExternalProcessFailure = 3;
    public const int NetworkFailure = 4;
    public const int Canceled = 130;
}
