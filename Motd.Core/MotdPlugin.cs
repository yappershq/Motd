using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Motd.Core.Modules;
using Motd.Shared;
using Sharp.Shared;

namespace Motd.Core;

/// <summary>
/// Motd — CS2 ModSharp plugin that (re)populates the engine's broken legacy MOTD by writing the
/// target URL into the <c>InfoPanel</c> networked string table under the <c>motd</c> key.
///
/// Mechanism (see docs/FINDINGS.md): the engine's HTML/MOTD panel is wired to that stringtable
/// entry. CS2 ships with it never populated, so the panel never opens; writing it "fixes" the MOTD.
/// Only a URL renders (inline HTML is dead in CS2). Players with <c>cl_disablehtmlmotd 1</c> opt out.
/// </summary>
public sealed class MotdPlugin : IModSharpModule
{
    public string DisplayName   => "Motd";
    public string DisplayAuthor => "yappershq";

    private readonly ILogger<MotdPlugin> _logger;
    private readonly ServiceProvider     _serviceProvider;

    public MotdPlugin(
        ISharedSystem   sharedSystem,
        string?         dllPath,
        string?         sharpPath,
        Version?        version,
        IConfiguration? coreConfiguration,
        bool            hotReload)
    {
        ArgumentNullException.ThrowIfNull(sharedSystem);
        ArgumentNullException.ThrowIfNull(dllPath);
        ArgumentNullException.ThrowIfNull(sharpPath);

        var loggerFactory = sharedSystem.GetLoggerFactory();
        _logger = loggerFactory.CreateLogger<MotdPlugin>();

        _ = new InterfaceBridge(dllPath, sharpPath, sharedSystem, loggerFactory);

        var services = new ServiceCollection();
        services.AddSingleton(sharedSystem);
        services.AddSingleton(InterfaceBridge.Instance);
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(LoggerFactoryLogger<>));

        services.AddModules();

        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.Init(), "Init");

        return true;
    }

    public void PostInit()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.OnPostInit(), "PostInit");

        // Publish the public API in PostInit (consumers resolve it in their OnAllModulesLoaded).
        var service = _serviceProvider.GetRequiredService<MotdModule>();
        InterfaceBridge.Instance.SharpModuleManager
            .RegisterSharpModuleInterface<IMotdShared>(this, IMotdShared.Identity, service);

        _logger.LogInformation("[Motd] Published IMotdShared");
    }

    public void OnAllModulesLoaded()
    {
        // Resolve ILocalizerManager in OAM — all PostInits have finished, interfaces are live.
        InterfaceBridge.Instance.InitLocalizer();

        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.OnAllSharpModulesLoaded(), "OnAllModulesLoaded");

        _logger.LogInformation("[Motd] Plugin loaded successfully");
    }

    public void Shutdown()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.Shutdown(), "Shutdown");

        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    public void OnLibraryConnected(string name)  { }
    public void OnLibraryDisconnect(string name) { }

    private void CallSafe(IModule module, Action<IModule> action, string phase)
    {
        try
        {
            action(module);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Motd] Error in {Phase} for {Module}", phase, module.GetType().Name);
        }
    }
}

internal sealed class LoggerFactoryLogger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _inner = factory.CreateLogger(typeof(T).Name);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}
