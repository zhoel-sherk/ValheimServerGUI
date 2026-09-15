# AGENTS.md

Context for AI agents working in this repository. Read this before making changes.

## Project

Avalonia (.NET 10) desktop app that manages a Valheim dedicated server on Windows:
starts/stops `valheim_server.exe`, parses its stdout for status & player events,
manages server profiles, worlds, difficulty settings and mods.

This is a **community fork** of [runeberry/ValheimServerGUI](https://github.com/runeberry/ValheimServerGUI)
(upstream is dormant since 2024). Licensed **GNU GPLv3** — keep the `LICENSE` file and the
"© Runeberry Software, LLC / Licensed under GNU GPLv3" notice in the About dialog intact.
Upstream promo links (Discord, donate, email) were deliberately removed; support links
must point to this fork's GitHub Issues (`AppSettings.UrlIssues`).

The original WinForms client has been **retired** (see tag `legacy-winforms`); the Avalonia
client is the only UI. `AVALONIA.md` records the migration plan/history.

## Solution layout

| Project | TFM | Purpose |
|---|---|---|
| `ValheimServerGUI.Core` | net10.0 | Platform-neutral domain: options & validation, player models, log parsing, world-gen data, process/platform/port-forwarding contracts |
| `ValheimServerGUI.Tools` | net10.0 | Process runner, JSON storage, loggers, HTTP |
| `ValheimServerGUI.Infrastructure` | net10.0 | Shared services: preferences, player data, mods/backups, Steam Cloud, Discord, UPnP, updates, logging pipeline, shared DI composition root |
| `ValheimServerGUI.Avalonia` | net10.0 | Avalonia client (views, ViewModels, composition root) |
| `ValheimServerGUI.Core.Tests` | net10.0 | Cross-platform xUnit tests for Core (log parsing, options, world settings, port helpers) |
| `ValheimServerGUI.Infrastructure.Tests` | net10.0 | xUnit tests for the server lifecycle, mods/backups and preferences |

Key files:
- `ValheimServerGUI.Core/Game/ServerLogParser.cs` + `ServerLogPatterns.cs` — server stdout regexes & dispatch
- `ValheimServerGUI.Core/Game/ValheimServer.cs` — server lifecycle + state transitions + log handler wiring
- `ValheimServerGUI.Core/Game/ValheimServerOptions.cs` — options model & validation
- `ValheimServerGUI.Core/Game/WorldSettingsOptions.cs` — difficulty presets + preset→modifier/key rules
- `ValheimServerGUI.Core/Game/ValheimPathExtensions.cs` — world discovery (see World layouts below)
- `ValheimServerGUI.Core/Processes/ServerProcess.cs` — `IServerProcess`/`IServerProcessFactory`
- `ValheimServerGUI.Core/Platform/IPlatformIntegration.cs` + `IUserInteraction.cs` — host contracts
- `ValheimServerGUI.Core/Network/IPortForwarder.cs` + `Infrastructure/Network/UpnpPortForwarder.cs` — UPnP/NAT-PMP (Mono.Nat); `ValheimPorts` knows the 3 adjacent UDP ports
- `ValheimServerGUI.Infrastructure/DependencyInjection/ValheimServerServices.cs` — shared DI composition root
- `ValheimServerGUI.Infrastructure/AppSettings.cs` — paths/URLs/defaults/version-less constants
- `ValheimServerGUI.Infrastructure/Tools/AssemblyHelper.cs` — version/build-date + pure `CompareVersions`
- `ValheimServerGUI.Infrastructure/Tools/GitHubClient.cs` — release lookup + pure `SelectLatestRelease`
- `ValheimServerGUI.Infrastructure/Tools/Logging/ValheimServerLogger.cs` + `ServerLogStream.cs` — server log filter/stream
- `ValheimServerGUI.Infrastructure/Tools/StartupHelper.cs` — Windows "Run" registry helper
- `ValheimServerGUI.Infrastructure/Game/Mods/BepInExManager.cs` — BepInEx install/status + paths/config listing
- `ValheimServerGUI.Infrastructure/Game/SteamCloudWorldProvider.cs` — Steam Cloud world import
- `ValheimServerGUI.Avalonia/App.axaml.cs` — composition root + exception boundary + tray
- `ValheimServerGUI.Avalonia/App.axaml` — Valheim "Mistlands Tech" dark theme
- `ValheimServerGUI.Avalonia/ViewModels/ShellViewModel.cs` — shell (profiles, status, dialogs)
- `ValheimServerGUI.Avalonia/Views/MainWindow.axaml` — menu, tabs, status bar (compiled bindings)

## Build / test / run

Requires **.NET SDK 10** (`dotnet --list-sdks`).

```pwsh
dotnet build ValheimServerGUI.sln -c Debug
dotnet test ValheimServerGUI.sln --nologo            # 161 tests, must be green
dotnet run --project ValheimServerGUI.Avalonia
```

Gotchas:
- A running client **locks** `bin\Debug` DLLs — stop `ValheimServerGUI.Avalonia`
  (and `valheim_server`) before rebuilding.
- Release builds need `/p:SignAssembly=false` (upstream `.snk` is not committed).
- Tests are hermetic: `ValheimServerTests` creates a dummy exe + temp save folder — do not
  reintroduce machine-specific paths there.

## Release / publish (vX.Y.Z)

1. Bump `<Version>` in `ValheimServerGUI.Avalonia/ValheimServerGUI.Avalonia.csproj`.
2. **Clean first** — `dotnet clean ValheimServerGUI.sln -c Release`.
3. Publish:
   ```pwsh
   dotnet publish ValheimServerGUI.Avalonia\ValheimServerGUI.Avalonia.csproj -c Release `
     /p:PublishProfile=small-x64-release /p:SignAssembly=false `
     /p:DebugType=none /p:DebugSymbols=false
   ```
   Output: `publish\avalonia-small-x64\` (single-file exe; native Skia/HarfBuzz bundled).
4. **Scan the artifact** for secrets and machine data: passwords, server/world names, local IPs,
   SteamIDs, `C:\Users\<name>` paths, upstream promo URLs. Decode as ASCII and search the bytes.
5. Zip the exe + `LICENSE.txt`, commit, tag `vX.Y.Z`, push, `gh release create`.
   For an alpha/RC, mark the GitHub release as pre-release (no update notification).

## Valheim server integration (verified against 1.0.7, network version 39)

The app launches `valheim_server.exe` with `-nographics -batchmode -name -port -world -public
-savedir -saveinterval -backups -backupshort -backuplong -password -crossplay [-preset|-modifier|-setkey]`.
All of these still work on modern builds.

Log parsing (status transitions & player events) is regex-based over the server's stdout, with a
`[HH:mm:ss.fff] ` timestamp prefix added by the logger **before** matching — patterns must not
anchor with `^` unless they account for the prefix. When server builds change log formats:

1. Capture real stdout: launch the exe with the same args via `Start-Process
   -RedirectStandardOutput` and play through the cycle (start → running → join → leave → shutdown).
2. Cross-check with decompilation: `ilspycmd -t <TypeName>` on
   `valheim_server_Data/Managed/assembly_valheim.dll` (literals are UTF-16 in the binary — naive
   string search there is unreliable, decompile instead).
3. Update `ServerLogPatterns` + tests in `ValheimServerGUI.Core.Tests` (message constants at the top).

Known formats (1.0.7): `Game server connected` (Running; do not match `Game server connected
failed`), `World save (5/5) done. Total time [Xms]` (may contain digit group separators),
`Session "..." registered with join code X` / `Session "..." with join code X and IP ... is active`,
`Got connection SteamID X`, `PlayFab socket with remote ID playfab/X received local Platform ID
Steam_<id>`, `Got character ZDOID from <name> : <user>:<obj>` (name is UTF-8),
`Got character ZDOID from <name> : 0:0` (character death; must not be treated as a login),
`Peer <id> has wrong password`, `Peer <id> has incompatible version`, `Closing socket <id>`,
`Destroying abandoned non persistent zdo <user>:<obj> owner <uid>`.

**World storage (1.0.7+)**: worlds live in `worlds_local/<WorldName>/_main.<n>.fwl2|db2`
(a directory per world). Legacy `worlds*/<Name>.fwl` is still supported —
`ValheimPathExtensions` (Core) scans both.

**Difficulty data sources (authoritative, in priority order):** game enums
(`WorldPresets`/`WorldModifiers`/`WorldModifierOption` — decompile `assembly_valheim.dll`),
game assets (`valheim_server_Data/resources.assets`, key strings like `nobuildcost|playerevents|
passivemobs|nomap|fire`), the shipped `Valheim Dedicated Server Manual.pdf` (often **lags**
behind — e.g. it omits `fire`). `WorldSettingsOptions` (Core) matches 1.0.7; presets override
modifiers/keys, same as in-game.

Server stdout is read as **UTF-8** (`LocalServerProcess`) — required for
non-ASCII character names. Do not remove those encoding settings.

## Local environment notes

- Valheim server install (this machine): `E:\SteamLibrary\steamapps\common\Valheim dedicated server`
- App data (plaintext, may contain the real server password — **never commit, never print**):
  `%USERPROFILE%\AppData\LocalLow\Runeberry\ValheimServerGUI\` (`userprefs.json`,
  `players-cache.json`, `logs\`)
- Ports for LAN/Internet play: UDP 2456–2458 must be forwarded; loopback connections to the
  public IP usually fail without NAT hairpin (use `localhost`).

## Conventions

- Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`), tag releases `vX.Y.Z`.
- Version lives in `ValheimServerGUI.Avalonia.csproj` `<Version>`.
- The repo uses XML-doc comments in code — keep existing style; do not strip comments.
- Roadmap (do not implement without asking): mod list, mod config (on/off), mod presets — see README
  "Roadmap". BepInEx + Valheim Plus install/update, Steam Cloud world import, Discord webhook
  notifications, UPnP port forwarding and mod folder/config actions have shipped.
- `ValheimServerGUI.Infrastructure` targets `net10.0` (platform-neutral); `SteamCloudWorldProvider`
  and `StartupHelper` touch the Windows registry — keep such calls behind `OperatingSystem.IsWindows()`.
- Closing the window hides the client to the system tray (protects a running server); the tray Exit
  stops the server first. `IPlatformIntegration` open folder/file/URL calls are best-effort and must
  never throw on a missing path or shell handler.
- The client uses a Valheim "Mistlands Tech" dark theme (`App.axaml`: Fluent resource overrides +
  `Vsg*` palette brushes; `Services/WindowsTheme.cs`: dark DWM title bar). Keep new UI on the `Vsg*`
  brushes rather than hard-coded colors.
- Port forwarding is manual only (a "Ports" dialog, like UPnP Wizard): UPnP/NAT-PMP via Mono.Nat
  (`IPortForwarder`), mapping UDP `base..base+2`. Nothing is mapped automatically; only the router is
  touched (no Windows Firewall changes). CGNAT/double NAT is detected via `ValheimPorts.IsPrivateAddress`.
