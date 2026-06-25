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
/// The MOTD payload is just an absolute <c>http(s)</c> URL — CS2's InfoPanel only navigates URLs
/// (no inline HTML) and uses the page's own <c>&lt;title&gt;</c>.
///
/// Example consumers: a <c>!discord</c> or <c>!rules</c> chat command would call
/// <see cref="ShowMotd"/> with the relevant page URL.
/// </summary>
public interface IMotdShared
{
    /// <summary>Identity string for <c>RegisterSharpModuleInterface</c> / <c>GetOptionalSharpModuleInterface</c>.</summary>
    const string Identity = nameof(IMotdShared);

    /// <summary>
    /// Show the given MOTD URL to a single player right now. No-op if the client is not fully
    /// in-game. Safe to call off the game thread.
    /// </summary>
    /// <param name="client">The target player.</param>
    /// <param name="url">An absolute <c>http://</c> / <c>https://</c> URL.</param>
    void ShowMotd(IGameClient client, string url);

    /// <summary>
    /// Show the given MOTD URL to every in-game player right now. Safe to call off the game thread.
    /// </summary>
    /// <param name="url">An absolute <c>http://</c> / <c>https://</c> URL.</param>
    void ShowMotdAll(string url);

    /// <summary>
    /// Override, at runtime, the default MOTD URL shown on the configured connect/spawn trigger.
    /// Pass <c>null</c> to revert to the value defined in the plugin config. The override is
    /// applied to the engine stringtable on the next map activation (and immediately if a
    /// server is active). Safe to call off the game thread.
    /// </summary>
    /// <param name="url">New default URL, or <c>null</c> to restore the config default.</param>
    void SetDefaultMotd(string? url);
}
