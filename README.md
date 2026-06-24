<div align="center">
  <h1><strong>Motd</strong></h1>
  <p>Restore the legacy MOTD panel in CS2 — show a URL on connect, override it at runtime from other plugins.</p>
</div>

<p align="center">
  <a href="https://github.com/Kxnrl/modsharp-public"><img src="https://img.shields.io/badge/framework-ModSharp-5865F2?logo=github" alt="ModSharp"></a>
  <img src="https://img.shields.io/badge/game-CS2-orange" alt="CS2">
  <img src="https://img.shields.io/github/stars/yappershq/Motd?style=flat&logo=github" alt="Stars">
</p>

---

CS2 ships with the engine's HTML/MOTD panel wired up but never populated, so it never opens. **Motd** repopulates it by writing your target URL into the engine's `InfoPanel` networked string table (`motd` key), which makes the panel open client-side on join. It auto-shows a configurable default on connect or first spawn, and exposes a `Motd.Shared` API so other plugins can push a different page to one player or everyone at runtime (e.g. for a `!rules` or `!discord` command).

> **CS2 limitation:** only a **URL** renders. Inline HTML is not displayed by CS2 — host your page and pass its URL. Players with `cl_disablehtmlmotd 1` opt out client-side.

## 🚀 Install

Copy the build output into your ModSharp install (`<sharp>` = your `sharp` directory):

| From | To |
|------|----|
| `.build/modules/Motd/` | `<sharp>/modules/Motd/` |
| `.build/shared/Motd.Shared/` | `<sharp>/shared/Motd.Shared/` |
| `.build/locales/motd.json` | `<sharp>/locales/motd.json` |

Restart the server (or change map) to load. Requires LocalizerManager (ships with ModSharp). The config file is generated automatically on first run (see below).

## ⚙️ Configuration

ConVars are generated into `<sharp>/configs/motd.cfg` on first run:

| ConVar | Default | Meaning |
|--------|---------|---------|
| `motd_enabled` | `1` | Master switch (`0` = off, `1` = on). |
| `motd_kind` | `url` | Default MOTD kind: `url` (only mode CS2 renders) or `html` (not rendered by CS2 — host it and use a URL). |
| `motd_value` | `https://example.com/motd` | Default MOTD value. For `url` kind, an absolute `http(s)://` URL. |
| `motd_title` | _(empty)_ | Optional title (advisory; CS2 uses the page's own `<title>`). |
| `motd_show_trigger` | `1` | When to auto-show the default: `0` = never, `1` = on connect, `2` = on first spawn. |
| `motd_show_delay` | `1.0` | Seconds to delay the auto-show after the trigger fires (`0` = immediate). |

## 🔧 How it works

The engine's HTML MOTD panel is bound to the `InfoPanel` string table, key `motd`, whose user-data is the target URL as a null-terminated UTF-8 blob. Motd writes that entry via ModSharp's `FindStringTable` + `AddString`/`SetStringUserData` — no native interop, managed `byte[]`, no unsafe code. The table is rebuilt per map, so it re-populates on every `OnServerActivate`. The global slot holds one value; for true per-player content, `ShowMotd` sends a crafted `svc_UpdateStringTable` that overrides only that client's view. All engine writes are marshalled onto the game thread.

## 🧩 Public API

Other plugins consume `IMotdShared` (resolve in `OnAllModulesLoaded` — Motd publishes it in `PostInit`):

```csharp
var motd = sharpModuleManager
    .GetOptionalSharpModuleInterface<IMotdShared>(IMotdShared.Identity)?.Instance;

// Show a page to one player (slot 0..63), to everyone, or change the default:
motd?.ShowMotd(slot, MotdContent.ForUrl("https://example.com/rules", "Rules"));
motd?.ShowMotdAll(MotdContent.ForUrl("https://example.com/event"));
motd?.SetDefaultMotd(MotdContent.ForUrl("https://example.com/welcome")); // null = revert to config
```

All methods take plain `int` slots and are safe to call from any thread.

## 📦 Build

```bash
dotnet build Motd.slnx -c Release
```

Outputs `.build/modules/Motd/Motd.Core.dll` (the plugin), `.build/shared/Motd.Shared/Motd.Shared.dll` (the public contract), and `.build/locales/motd.json`.

## 🙏 Credits

MOTD-restore mechanism inspired by the `FixLoadMotd` approach and [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)'s string-table handling.

---

<div align="center">
  <p>Made with ❤️ by <a href="https://github.com/yappershq">yappershq</a></p>
  <p>⭐ Star this repo if you find it useful!</p>
</div>
