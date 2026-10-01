using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Anonymizer.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the service and the built-in anonymizers. Add more with <see cref="AddAnonymizer{T}"/>.</summary>
    public static IServiceCollection AddAnonymizer(this IServiceCollection services)
    {
        services.AddAnonymizer<LinuxPathAnonymizer>();
        services.AddAnonymizer<WindowsPathAnonymizer>();
        services.AddAnonymizer<SecretAnonymizer>();
        services.TryAddSingleton(Random.Shared);
        services.TryAddSingleton<IAnonymizationService, AnonymizationService>();
        return services;
    }

    public static IServiceCollection AddAnonymizer<T>(this IServiceCollection services) where T : class, IAnonymizer
        => services.AddSingleton<IAnonymizer, T>();
}
