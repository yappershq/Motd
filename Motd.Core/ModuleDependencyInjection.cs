using Microsoft.Extensions.DependencyInjection;
using Motd.Core.Configuration;
using Motd.Core.Modules;

namespace Motd.Core;

internal static class ModuleDependencyInjection
{
    internal static IServiceCollection AddModules(this IServiceCollection services)
    {
        // Config (constructs ConVars on instantiation — not an IModule)
        services.AddSingleton<IMotdConfig, MotdConfig>();

        // Core MOTD logic + public-API implementation
        services.AddSingleton<MotdModule>();
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<MotdModule>());

        return services;
    }
}
