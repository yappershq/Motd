using System;
using Microsoft.Extensions.Logging;
using Motd.Shared;
using Sharp.Shared.Objects;

namespace Motd.Core.Configuration;

internal interface IMotdConfig
{
    /// <summary>Plugin master switch.</summary>
    bool Enabled { get; }

    /// <summary>Default MOTD kind: "url" or "html".</summary>
    MotdKind DefaultKind { get; }

    /// <summary>Default MOTD value (URL for url kind; HTML body for html kind).</summary>
    string DefaultValue { get; }

    /// <summary>Optional default MOTD title (advisory; CS2 uses the page's own title).</summary>
    string DefaultTitle { get; }

    /// <summary>
    /// When to auto-show the default MOTD per player:
    /// 0 = never, 1 = on connect (PutInServer), 2 = on first spawn.
    /// </summary>
    int ShowTrigger { get; }

    /// <summary>Seconds to delay the auto-show after the trigger fires. 0 = immediate.</summary>
    float ShowDelay { get; }
}

internal sealed class MotdConfig : IMotdConfig
{
    private readonly IConVar? _cvEnabled;
    private readonly IConVar? _cvKind;
    private readonly IConVar? _cvValue;
    private readonly IConVar? _cvTitle;
    private readonly IConVar? _cvTrigger;
    private readonly IConVar? _cvDelay;

    public MotdConfig(InterfaceBridge bridge)
    {
        var cv = bridge.ConVarManager;

        _cvEnabled = cv.CreateConVar("motd_enabled", true, "Enable Motd plugin [0=off, 1=on]");
        _cvKind    = cv.CreateConVar("motd_kind", "url",
            "Default MOTD kind: 'url' (only mode CS2 renders) or 'html' (NOT rendered by CS2 — host it and use a url instead)");
        _cvValue   = cv.CreateConVar("motd_value", "https://example.com/motd",
            "Default MOTD value. For kind=url an absolute http(s):// URL.");
        _cvTitle   = cv.CreateConVar("motd_title", "",
            "Optional default MOTD title (advisory; CS2 uses the page's own <title>)");
        _cvTrigger = cv.CreateConVar("motd_show_trigger", 1,
            "When to auto-show the default MOTD: 0=never, 1=on connect, 2=on first spawn");
        _cvDelay   = cv.CreateConVar("motd_show_delay", 1.0f,
            "Seconds to delay the auto-show after the trigger fires (0=immediate)");

        var logger = bridge.LoggerFactory.CreateLogger("Motd.Config");
        IConVar?[] all = [_cvEnabled, _cvKind, _cvValue, _cvTitle, _cvTrigger, _cvDelay];
        ConVarConfigFile.Sync(bridge.SharpPath, "motd.cfg", "Motd", logger,
            Array.FindAll(all, c => c is not null)!);
    }

    public bool     Enabled      => _cvEnabled?.GetBool() ?? true;
    public MotdKind DefaultKind  => ParseKind(_cvKind?.GetString());
    public string   DefaultValue => _cvValue?.GetString() ?? string.Empty;
    public string   DefaultTitle => _cvTitle?.GetString() ?? string.Empty;
    public int      ShowTrigger  => _cvTrigger?.GetInt32() ?? 1;
    public float    ShowDelay    => _cvDelay?.GetFloat() ?? 1.0f;

    private static MotdKind ParseKind(string? raw)
        => string.Equals(raw?.Trim(), "html", StringComparison.OrdinalIgnoreCase)
            ? MotdKind.Html
            : MotdKind.Url;
}
