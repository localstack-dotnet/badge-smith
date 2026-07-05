using BadgeSmith.Api.Core.Routing.Cors;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing.CorsHandler;

[Trait("Category", TestCategories.Unit)]
public class CorsOptionsTests
{
    [Fact]
    public void CorsOptions_Should_Have_Correct_Values_When_Default()
    {
        var options = CorsOptions.Default;

        Assert.False(options.AllowCredentials);
        Assert.Equal(3600, options.MaxAgeSeconds);
        Assert.True(options.UseWildcardWhenNoCredentials);
        Assert.Null(options.OriginAllowed);
        Assert.Null(options.AllowedOrigins);
        Assert.Contains("content-type", options.AllowedRequestHeaders);
        Assert.Null(options.ExposeHeaders);
    }

    [Fact]
    public void CorsOptions_Should_Support_Custom_Configuration()
    {
        var customOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "https://app.example.com",
        };
        var customHeaders = new HashSet<string>
            (StringComparer.OrdinalIgnoreCase)
            {
                "x-api-key",
            };
        var options = new CorsOptions
        {
            AllowCredentials = true,
            MaxAgeSeconds = 7200,
            UseWildcardWhenNoCredentials = false,
            AllowedOrigins = customOrigins,
            ExposeHeaders = customHeaders,
        };

        Assert.True(options.AllowCredentials);
        Assert.Equal(7200, options.MaxAgeSeconds);
        Assert.False(options.UseWildcardWhenNoCredentials);
        Assert.Equal(customOrigins, options.AllowedOrigins);
        Assert.Equal(customHeaders, options.ExposeHeaders);
    }
}
