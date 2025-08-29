using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Infrastructure;
using BadgeSmith.Api.Observability;

namespace BadgeSmith.Api.Domain.Services.Nuget;

internal class NugetPackageServiceFactory : INugetPackageServiceFactory
{
    private static readonly Lazy<INuGetPackageService> NuGetPackageServiceLazy = new(CreateNuGetPackageService);

    public INuGetPackageService NuGetPackageService => NuGetPackageServiceLazy.Value;

    private static NuGetPackageService CreateNuGetPackageService()
    {
        var logger = LoggerFactory.CreateLogger<NuGetPackageService>();
        var httpClient = HttpStack.NuGet;

        return new NuGetPackageService(logger, httpClient);
    }
}
