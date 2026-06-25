using System;
using Microsoft.Extensions.Logging;
using Motd.Shared;
using Sharp.Shared.Objects;

namespace Motd.Core.Configuration;

internal interface IMotdConfig
{
    /// <summary>Plugin master switch.</summary>
    bool Enabled { get; }

    /// <summary>Default MOTD URL (absolute http(s)://).</summary>
    string DefaultValue { get; }

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
    private readonly IConVar? _cvValue;
    private readonly IConVar? _cvTrigger;
    private readonly IConVar? _cvDelay;

    public MotdConfig(InterfaceBridge bridge)
    {
        var cv = bridge.ConVarManager;

        _cvEnabled = cv.CreateConVar("motd_enabled", true, "Enable Motd plugin [0=off, 1=on]");
        _cvValue   = cv.CreateConVar("motd_value", "https://example.com/motd",
            "Default MOTD URL — an absolute http(s):// address (CS2 only navigates URLs).");
        _cvTrigger = cv.CreateConVar("motd_show_trigger", 1,
            "When to auto-show the default MOTD: 0=never, 1=on connect, 2=on first spawn");
        _cvDelay   = cv.CreateConVar("motd_show_delay", 1.0f,
            "Seconds to delay the auto-show after the trigger fires (0=immediate)");

        var logger = bridge.LoggerFactory.CreateLogger("Motd.Config");
        IConVar?[] all = [_cvEnabled, _cvValue, _cvTrigger, _cvDelay];
        ConVarConfigFile.Sync(bridge.SharpPath, "motd.cfg", "Motd", logger,
            Array.FindAll(all, c => c is not null)!);
    }

    public bool   Enabled      => _cvEnabled?.GetBool() ?? true;
    public string DefaultValue => _cvValue?.GetString() ?? string.Empty;
    public int    ShowTrigger  => _cvTrigger?.GetInt32() ?? 1;
    public float  ShowDelay    => _cvDelay?.GetFloat() ?? 1.0f;
}
