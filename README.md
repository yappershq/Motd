# Motd

CS2 ModSharp plugin that fixes the broken legacy MOTD by re-populating the engine's `InfoPanel`
string table (`motd` key) with a target URL. The engine's HTML panel is wired to that entry, so
writing it makes the MOTD open client-side on join.

## Projects

- **Motd.Shared** — pure-contract public API (`IMotdShared`, `MotdContent`, `MotdKind`). No native
  types, only primitives, so any plugin can reference it.
- **Motd.Core** — the plugin: stringtable write, config, auto-show on connect/spawn, publishes
  `IMotdShared`.

## Config (`sharp/configs/motd.cfg`, auto-generated)

| ConVar | Default | Meaning |
|---|---|---|
| `motd_enabled` | `1` | Master switch |
| `motd_kind` | `url` | `url` (only mode CS2 renders) or `html` (not rendered — skipped) |
| `motd_value` | `https://example.com/motd` | The URL (must be absolute http/https) |
| `motd_title` | `` | Advisory title |
| `motd_show_trigger` | `1` | 0=never, 1=on connect, 2=on first spawn/post-admin-check |
| `motd_show_delay` | `1.0` | Seconds to delay the auto-show |

## Public API (for other plugins)

Resolve in your `OnAllModulesLoaded`:

```csharp
var motd = sharpModuleManager
    .GetOptionalSharpModuleInterface<IMotdShared>(IMotdShared.Identity)?.Instance;

motd?.ShowMotd(client.Slot, MotdContent.ForUrl("https://discord.gg/xxxx")); // e.g. a !discord cmd
motd?.ShowMotdAll(MotdContent.ForUrl("https://site/rules"));
motd?.SetDefaultMotd(MotdContent.ForUrl("https://site/event")); // null reverts to config
```

All API methods are thread-safe (marshalled onto the game thread) and validate `IsInGame`.

## Mechanism & limitations

- **URL MOTD: supported.** Inline **HTML: not supported by CS2** (only URLs navigate) — host HTML
  yourself and pass its URL.
- **One global stringtable slot** — no per-recipient MOTD. Per-player content must encode identity
  in a shared URL (`?steamid=...`).
- Players with `cl_disablehtmlmotd 1` see nothing (client-side, unavoidable).
- The table resets per map; the plugin re-writes it on `OnServerActivate`.

## Build

```bash
cd Motd && dotnet build Motd.slnx -c Release
```
