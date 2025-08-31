using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Domain.Services.Package;
using BadgeSmith.Api.Infrastructure;
using BadgeSmith.Api.Observability;

namespace BadgeSmith.Api.Domain.Services.Nuget;

internal class NuGetPackageServiceFactory : INugetPackageServiceFactory
{
    private static readonly Lazy<INuGetPackageService> NuGetPackageServiceLazy = new(CreateNuGetPackageService);

    public INuGetPackageService NuGetPackageService => NuGetPackageServiceLazy.Value;

    private static NuGetPackageService CreateNuGetPackageService()
    {
        var logger = LoggerFactory.CreateLogger<NuGetPackageService>();
        var httpClient = HttpStack.CreateNuGetClient();
        var nuGetVersionService = new NuGetVersionService();

        return new NuGetPackageService(nuGetVersionService, logger, httpClient);
    }
}
