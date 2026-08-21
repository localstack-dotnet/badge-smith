namespace BadgeSmith.Api.Core.Routing.Helpers;

/// <summary>
/// Typed Location-driven redirect status. <c>default(RedirectStatus)</c> has code 0 and is rejected
/// by every redirect API so an uninitialized status cannot reach the wire.
/// </summary>
internal readonly record struct RedirectStatus
{
    private RedirectStatus(int code)
    {
        Code = code;
    }

    public static RedirectStatus MovedPermanently { get; } = new(301);

    public static RedirectStatus Found { get; } = new(302);

    public static RedirectStatus SeeOther { get; } = new(303);

    public static RedirectStatus TemporaryRedirect { get; } = new(307);

    public static RedirectStatus PermanentRedirect { get; } = new(308);

    public int Code { get; }
}
