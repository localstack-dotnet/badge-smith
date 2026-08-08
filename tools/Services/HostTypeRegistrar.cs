using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace BadgeSmith.Tools.Services;

internal sealed class HostTypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;
    private IServiceProvider? _serviceProvider;

    public HostTypeRegistrar(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public void UseServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public ITypeResolver Build()
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("The host service provider must be assigned before running the command app.");
        }

        return new HostTypeResolver(_serviceProvider);
    }

    public void Register(Type service, Type implementation)
    {
        if (!_services.IsReadOnly)
        {
            _services.AddTransient(service, implementation);
        }
    }

    public void RegisterInstance(Type service, object implementation)
    {
        if (!_services.IsReadOnly)
        {
            _services.AddSingleton(service, implementation);
        }
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        if (!_services.IsReadOnly)
        {
            _services.AddSingleton(service, _ => factory());
        }
    }

    private sealed class HostTypeResolver : ITypeResolver, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;

        public HostTypeResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object? Resolve(Type? type)
        {
            return type is null ? null : ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, type);
        }

        public void Dispose()
        {
        }
    }
}
