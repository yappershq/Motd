using Sharp.Shared.Objects;

namespace Motd.Shared;

/// <summary>
/// Public, cross-plugin API for the Motd plugin. Other ModSharp plugins resolve this via:
/// <code>
/// var iface = sharpModuleManager
///     .GetOptionalSharpModuleInterface&lt;IMotdShared&gt;(IMotdShared.Identity);
/// var motd = iface?.Instance;
/// </code>
/// Resolve it in <c>OnAllModulesLoaded</c> (not Init/PostInit) — Motd publishes in PostInit.
///
/// All methods are safe to call from any thread: the implementation marshals the actual engine
/// write onto the game thread and validates <c>IsInGame</c> before showing anything.
///
/// Example consumers: a <c>!discord</c> or <c>!rules</c> chat command would call
/// <see cref="ShowMotd"/> with <see cref="MotdContent.ForUrl"/> pointing at the relevant page.
/// </summary>
public interface IMotdShared
{
    /// <summary>Identity string for <c>RegisterSharpModuleInterface</c> / <c>GetOptionalSharpModuleInterface</c>.</summary>
    const string Identity = nameof(IMotdShared);

    /// <summary>
    /// Show the given MOTD to a single player right now. No-op if the client is not fully
    /// in-game. Safe to call off the game thread.
    /// </summary>
    /// <param name="client">The target player.</param>
    /// <param name="content">The MOTD payload to display.</param>
    void ShowMotd(IGameClient client, MotdContent content);

    /// <summary>
    /// Show the given MOTD to every in-game player right now. Safe to call off the game thread.
    /// </summary>
    void ShowMotdAll(MotdContent content);

    /// <summary>
    /// Override, at runtime, the default MOTD shown on the configured connect/spawn trigger.
    /// Pass <c>null</c> to revert to the value defined in the plugin config. The override is
    /// applied to the engine stringtable on the next map activation (and immediately if a
    /// server is active). Safe to call off the game thread.
    /// </summary>
    /// <param name="content">New default, or <c>null</c> to restore the config default.</param>
    void SetDefaultMotd(MotdContent? content);
}
