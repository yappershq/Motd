using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Motd.Core.Configuration;
using Motd.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace Motd.Core.Modules;

/// <summary>
/// Core MOTD module + <see cref="IMotdShared"/> implementation.
///
/// MECHANISM (ported from FixLoadMotd / CounterStrikeSharp): the engine's HTML MOTD panel is wired
/// to the <c>InfoPanel</c> networked string table, key <c>motd</c>, whose user-data is the target
/// URL as a null-terminated UTF-8 blob. CS2 never populates it (legacy MOTD "broken"); writing it
/// makes the panel open client-side on join. We do this via ModSharp's <c>FindStringTable</c> +
/// <c>AddString</c>/<c>SetStringUserData</c> — no native interop, managed <c>byte[]</c>, no unsafe.
///
/// LIMITATIONS (see docs/FINDINGS.md):
///  - Only a URL renders. Inline HTML ("html" kind) is NOT shown by CS2 — logged + skipped.
///  - The stringtable slot is a single GLOBAL value; there is no per-recipient variant, so
///    "show X only to player P" sets the global slot to X. True per-player content must encode
///    identity in a shared URL (e.g. ?steamid=...). We do set the global slot to that player's URL.
///  - Players with cl_disablehtmlmotd 1 see nothing — client-side, unavoidable.
///  - The table is rebuilt per map, so we re-populate it on every OnServerActivate.
///
/// THREAD SAFETY: every public API marshals slot resolution and the engine write onto the game
/// thread via IModSharp.InvokeFrameAction, and validates IsInGame before showing.
/// </summary>
internal sealed class MotdModule : IModule, IClientListener, IGameListener, IMotdShared
{
    private const string TableName     = "InfoPanel";
    private const string StringKeyName = "motd";

    private readonly InterfaceBridge    _bridge;
    private readonly ILogger<MotdModule> _logger;
    private readonly IMotdConfig        _config;

    private readonly object _lock = new();

    // Runtime override of the default MOTD (null = use config). Guarded by _lock.
    private MotdContent? _runtimeDefault;

    int IClientListener.ListenerVersion  => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 0;

    int IGameListener.ListenerVersion  => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority => 0;

    public MotdModule(InterfaceBridge bridge, ILogger<MotdModule> logger, IMotdConfig config)
    {
        _bridge = bridge;
        _logger = logger;
        _config = config;
    }

    // ===== IModule =====

    public bool Init()
    {
        _bridge.ClientManager.InstallClientListener(this);
        _bridge.ModSharp.InstallGameListener(this);
        return true;
    }

    public void OnAllSharpModulesLoaded()
    {
        if (!_config.Enabled)
            _logger.LogInformation("[Motd] Disabled via motd_enabled 0");
    }

    public void Shutdown()
    {
        // Listeners are torn down by the host on plugin unload.
    }

    // ===== Default MOTD resolution =====

    private MotdContent EffectiveDefault()
    {
        lock (_lock)
        {
            if (_runtimeDefault is { } rt)
                return rt;
        }

        var title = _config.DefaultTitle;
        return new MotdContent(_config.DefaultKind, _config.DefaultValue,
            string.IsNullOrWhiteSpace(title) ? null : title);
    }

    // ===== IGameListener: repopulate per map =====

    void IGameListener.OnServerActivate()
    {
        if (!_config.Enabled)
            return;

        // The InfoPanel table is rebuilt on every map load — re-write the default here.
        WriteMotdToTable(EffectiveDefault());
    }

    // ===== IClientListener: auto-show on connect / spawn =====

    void IClientListener.OnClientPutInServer(IGameClient client)
    {
        if (_config.Enabled && _config.ShowTrigger == 1)
            ScheduleShowDefault(client.Slot);
    }

    void IClientListener.OnClientPostAdminCheck(IGameClient client)
    {
        // PostAdminCheck is the closest "first spawn"-ish point for trigger 2 without a spawn hook;
        // it fires once per join after the player is fully recognized. The panel auto-opens once
        // the stringtable holds a URL, so writing it here covers the on-spawn case.
        if (_config.Enabled && _config.ShowTrigger == 2)
            ScheduleShowDefault(client.Slot);
    }

    private void ScheduleShowDefault(PlayerSlot slot)
    {
        var content = EffectiveDefault();
        var delay   = _config.ShowDelay;

        if (delay <= 0f)
        {
            ShowToSlotOnGameThread(slot.AsPrimitive(), content);
            return;
        }

        var captured = slot.AsPrimitive();
        _bridge.ModSharp.PushTimer(() => ShowToSlotOnGameThread(captured, content),
            delay, GameTimerFlags.StopOnMapEnd);
    }

    // ===== IMotdShared (public API) — safe from any thread =====

    public void ShowMotd(int slot, MotdContent content)
    {
        if (slot is < 0 or > 63)
            return;

        _bridge.ModSharp.InvokeFrameAction(() => ShowToSlotOnGameThread((byte) slot, content));
    }

    public void ShowMotdAll(MotdContent content)
    {
        _bridge.ModSharp.InvokeFrameAction(() =>
        {
            // Single global stringtable slot: write once; engine shows it to whoever opens next.
            // We also (re)write so any subsequent panel-open picks up this content.
            WriteMotdToTable(content);
        });
    }

    public void SetDefaultMotd(MotdContent? content)
    {
        lock (_lock)
        {
            _runtimeDefault = content;
        }

        // Apply immediately if a server is active; otherwise OnServerActivate will pick it up.
        var effective = EffectiveDefault();
        _bridge.ModSharp.InvokeFrameAction(() => WriteMotdToTable(effective));
    }

    // ===== Game-thread implementations =====

    private void ShowToSlotOnGameThread(byte slot, MotdContent content)
    {
        var client = _bridge.ClientManager.GetGameClient(new PlayerSlot(slot));
        if (client is null || !client.IsValid || !client.IsInGame || client.IsFakeClient || client.IsHltv)
            return;

        // No per-recipient stringtable entry exists. Set the global slot to this player's URL so
        // the next panel-open (engine auto-open on join) renders it. For true per-player content,
        // encode identity into the URL (e.g. ?steamid=...).
        WriteMotdToTable(content);
    }

    /// <summary>
    /// Write the MOTD URL into InfoPanel/motd. Must run on the game thread.
    /// HTML kind is unsupported by CS2 — logged and skipped.
    /// </summary>
    private void WriteMotdToTable(MotdContent content)
    {
        if (content.Kind == MotdKind.Html)
        {
            _logger.LogWarning(
                "[Motd] HTML MOTD is not rendered by CS2; host the HTML and pass a URL instead. Skipping.");
            return;
        }

        var url = content.Value;
        if (string.IsNullOrWhiteSpace(url)
            || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("[Motd] MOTD value is not an absolute http(s) URL: '{Url}'. Skipping.", url);
            return;
        }

        var table = _bridge.ModSharp.FindStringTable(TableName);
        if (table is null)
        {
            _logger.LogWarning("[Motd] String table '{Table}' not found (no active map?). Skipping.", TableName);
            return;
        }

        // URL as null-terminated UTF-8 — exactly what the engine MOTD panel expects.
        var data = Encoding.UTF8.GetBytes(url + "\0");

        var idx = table.FindStringIndex(StringKeyName);
        if (idx >= 0)
            table.SetStringUserData(idx, data);
        else
            table.AddString(true, StringKeyName, data);

        _logger.LogInformation("[Motd] Set {Table}/{Key} = {Url}", TableName, StringKeyName, url);
    }
}
