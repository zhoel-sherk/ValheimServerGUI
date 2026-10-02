# AGENTS.md

Context for AI agents working in this repository. Read this before making changes.

## Project

Avalonia (.NET 10) desktop app that manages a Valheim dedicated server on Windows:
starts/stops `valheim_server.exe`, parses its stdout for status & player events,
manages server profiles, worlds, difficulty settings and mods.

This is a **community fork** of [runeberry/ValheimServerGUI](https://github.com/runeberry/ValheimServerGUI).
Licensed **GNU GPLv3** — keep the `LICENSE` file and the
"© Runeberry Software, LLC / Licensed under GNU GPLv3" notice in the About dialog intact.
Upstream promo links (Discord, donate, email) were deliberately removed; support links
must point to this fork's GitHub Issues (`AppSettings.UrlIssues`).

**Upstream is not dormant** — it is an active WinForms project (branches `main`, `v2.4`,
`v3.0`, commits through 2026-09). It is registered as the `upstream` remote, so
`git log HEAD..upstream/main` and `git log HEAD..upstream/v3.0` show what we have not taken.
Check it when touching Core/Infrastructure: their WinForms UI work does not apply, but their
domain fixes do. Our merge-base with `upstream/main` is from 2024, so plenty of upstream
commits are already implemented here independently — verify before porting, and never
cherry-pick blindly. Ported so far: literal log substitution rendering, the
PlayStation/Nintendo platform tokens, Information-level HTTP success logging, and treating
`OperationCanceledException` as benign rather than a fault.

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
- `ValheimServerGUI.Core/Game/WorldSettingsOptions.cs` — difficulty presets + preset→modifier/key rules + `AreEquivalent`
- `ValheimServerGUI.Core/Game/LogEntry.cs` + `LogSeverity.cs` — a log line plus its UI severity
- `ValheimServerGUI.Core/Game/ValheimPathExtensions.cs` — world discovery (see World layouts below)
- `ValheimServerGUI.Core/Models/PlayerPlatforms.cs` — Steam/Xbox/PlayStation/Nintendo tokens (see below)
- `ValheimServerGUI.Core/Processes/ServerProcess.cs` — `IServerProcess`/`IServerProcessFactory`
- `ValheimServerGUI.Core/Platform/IPlatformIntegration.cs` + `IUserInteraction.cs` — host contracts
- `ValheimServerGUI.Core/Network/IPortForwarder.cs` + `Infrastructure/Network/UpnpPortForwarder.cs` — UPnP/NAT-PMP (Mono.Nat); `ValheimPorts` knows the 3 adjacent UDP ports
- `ValheimServerGUI.Infrastructure/DependencyInjection/ValheimServerServices.cs` — shared DI composition root
- `ValheimServerGUI.Infrastructure/AppSettings.cs` — paths/URLs/defaults/version-less constants
- `ValheimServerGUI.Infrastructure/Tools/AssemblyHelper.cs` — version/build-date + pure `CompareVersions`
- `ValheimServerGUI.Infrastructure/Tools/GitHubClient.cs` — release lookup + pure `SelectLatestRelease`
- `ValheimServerGUI.Infrastructure/Tools/Logging/ValheimServerLogger.cs` + `ServerLogStream.cs` — server log filter/stream
- `ValheimServerGUI.Infrastructure/Tools/Logging/LogSeverityClassifier.cs` — maps a rendered line to `LogSeverity` for the Logs tab
- `ValheimServerGUI.Infrastructure/Tools/Logging/BaseLogger.cs` — rule pipeline; renders substitutions literally (see Logging gotchas)
- `ValheimServerGUI.Infrastructure/Tools/StartupHelper.cs` — Windows "Run" registry helper
- `ValheimServerGUI.Infrastructure/Game/Mods/BepInExManager.cs` — BepInEx install/status + paths/config/plugin listing
- `ValheimServerGUI.Infrastructure/Game/Mods/BepInExLogReader.cs` — versions from the server's own `BepInEx/LogOutput.log`
- `ValheimServerGUI.Infrastructure/Game/Mods/BepInExConfig.cs` — lossless `BepInEx.cfg` text edit
- `ValheimServerGUI.Infrastructure/Game/SteamCloudWorldProvider.cs` — Steam Cloud world import
- `ValheimServerGUI.Avalonia/App.axaml.cs` — composition root + exception boundary + tray
- `ValheimServerGUI.Avalonia/App.axaml` — Valheim "Mistlands Tech" dark theme
- `ValheimServerGUI.Avalonia/ViewModels/ShellViewModel.cs` — shell (profiles, status, dialogs)
- `ValheimServerGUI.Avalonia/Views/MainWindow.axaml` — menu, tabs, status bar (compiled bindings)

## Build / test / run

Requires **.NET SDK 10** (`dotnet --list-sdks`).

```pwsh
dotnet build ValheimServerGUI.sln -c Debug
dotnet test ValheimServerGUI.sln --nologo            # 231 tests, must be green
dotnet run --project ValheimServerGUI.Avalonia
```

Gotchas:
- A running client **locks** `bin\Debug` DLLs — stop `ValheimServerGUI.Avalonia`
  (and `valheim_server`) before rebuilding.
- Release builds need `/p:SignAssembly=false` (upstream `.snk` is not committed).
- Tests are hermetic: `ValheimServerTests` creates a dummy exe + temp save folder — do not
  reintroduce machine-specific paths there.
- Publishing fails with an opaque `File.Delete` / `GenerateBundle` MSB4018 if the client is
  running: a launched `publish\...\ValheimServerGUI.Avalonia.exe` holds the output. Stop it
  first. If you kill it with `Stop-Process -Force` rather than the tray Exit, the auto-started
  `valheim_server` is **orphaned** and keeps running — stop that too.

### Avalonia 12 API changes (this project targets 12.1.2)

These differ from Avalonia 11 and will bite on copy-pasted snippets:

- `Window.DialogResult` **does not exist**. `ShowDialog` is `Task` / `Task<TResult>`, and the
  result is passed to `Close(result)`. `ShellViewModel.ShowDialog` wraps the generic form and
  the Windows close with `Close(true)` on accept.
- `ListBox` has no `HorizontalContentAlignment`; use `ListBox.Styles` to retarget item styles
  (e.g. flatten the Fluent `ListBoxItem` padding for the dense log list).
- `ListBox` exposes no `ScrollChanged` or `Viewport` — find the `ScrollViewer` with
  `GetVisualDescendants().OfType<ScrollViewer>()`, and only after the template is applied.
  Poll from `LayoutUpdated` **behind a bool flag**: re-subscribing from inside the handler adds
  a fresh delegate every layout pass and the handlers multiply exponentially (this leaked ~4 GB).
- `Application` has `TryGetResource(key, themeVariant, out value)`, not `TryFindResource`.

### Logging gotchas

- `BaseLogger` renders message-template substitutions **literally**. Serilog's default
  `RenderMessage()` wraps string scalars in quotes, so a template that already quotes its token
  would log as `""Test Server""` with inner quotes escaped. Quotes belong in the template.
- `LogEventLevel` does not survive to the UI — `LogReceived` carries a `string`. The Logs tab
  re-derives a severity via `LogSeverityClassifier`; the Application view recovers the level from
  the textual prefix in `LogLevelTransformer` (whose prefixes are public constants for this).
- `LogSeverityClassifier` must test `PlayerDied` **before** `PlayerConnected`: the connected
  pattern also matches the `0:0` line the game emits on death.

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
   Note: decoding as UTF-16 must be tried at **both** byte offsets 0 and 1 — .NET string literals
   are not 2-byte aligned in the file, and offset 1 alone produces false "MISSING" results.
5. Delete `publish\avalonia-small-x64\*.pdb` — the Skia/HarfBuzz native packages drag in ~100 MB
   of debug symbols that are not part of the client.
6. Zip the exe + `LICENSE.txt` into **`release-artifacts/`** and commit. Do **not** use `release/`
   or `artifacts/`: `.gitignore` already claims both for Visual Studio / .NET build output and the
   paths would be silently ignored.
7. Tag `vX.Y.Z`, push, then `gh release create ... --repo zhoel-sherk/ValheimServerGUI --prerelease`
   (pass `--repo` explicitly — `gh` resolves the wrong repo otherwise, and a failed first attempt
   can leave an empty draft release behind that must be deleted).
   For an alpha/RC, mark the GitHub release as pre-release (no update notification).
8. Check the tag is free before tagging: `git fetch origin --tags` **without `--force`** (a forced
   fetch overwrites a local tag of the same name) and `git ls-remote origin refs/tags/<tag>`.

## Valheim server integration (verified against 1.0.12, network version 40)

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

**World difficulty only reaches the server at launch.** `-preset`/`-modifier`/`-setkey` are baked
into the argv once by `ValheimServer.GenerateArgs`; the game exposes no runtime difficulty API.
So the World Settings dialog has to restart the server to apply anything, and the modifiers are
themselves baked in when a *world is generated* — raising difficulty on an existing world mostly
only affects newly generated areas. The dialog says both things out loud; do not "simplify" the
warnings away.

**Player platforms.** Crossplay logs a `Platform ID <token>_<id>` pair. The tokens the binary
reports are `Steam`, `Xbox`, `PlayStation` and `Switch` (which normalizes to our `Nintendo`).
There is no `Epic` token. `PlayerPlatforms.TryGetValidPlatform` deliberately accepts *unknown*
tokens too: rejecting them made `OnPlayerConnectingCrossplay` return early, so such a player
silently vanished from the list with nothing in the log. Keep it accepting, and keep the
once-per-platform warning.

Server stdout is read as **UTF-8** (`LocalServerProcess`) — required for
non-ASCII character names. Do not remove those encoding settings.

## BepInEx invariants

Two things the app depends on, both of which fail quietly if broken:

- **`[Logging.Console] Enabled = false` in `BepInEx/config/BepInEx.cfg`.** An enabled BepInEx
  console calls `AllocConsole`/`SetStdHandle` and can divert the server's stdout away from the
  redirected pipe that `ServerLogParser` reads — the status then never reaches `Running` and
  player events are lost. Thunderstore ships it *enabled*, and `CreateNoWindow` does not prevent
  it. `IBepInExManager.EnsureConsoleLoggingDisabled` repairs it; it is idempotent and is called on
  every server start from `StartAsync`, so an install made outside the app is fixed too. Never
  remove that call — it is the only thing that catches a hand-edited or externally-updated config.
- **Version sources, in order.** Pack version: the `.vsg-bepinex-pack-version` marker (written
  only when *this app* installed it) → `BepInEx/LogOutput.log` → the Valheim **client**
  `Player.log`. Only the first two are reliable on a dedicated host; `PlayerLogReader` reads
  `%LOCALAPPDATA%\IronGate\Valheim\Player.log`, which a machine that never runs the game client
  does not have. If that log is absent, `PackageVersion` is null and `ModVersion.IsNewer(x, null)`
  is false, so "check for updates" silently reports nothing to do — which is exactly how this bug
  presented. BepInEx runs with `AppendLog = false`, so `LogOutput.log` is the latest session only;
  `LogOutput.log.1` is the rotated older one and must not be parsed.

The plugins folder is **listed read-only**. BepInEx has no per-plugin on/off switch: the only way
to unload a mod is to move its `.dll` out of `BepInEx/plugins`. Enabling/disabling (via a
`vsg-disabled` folder) and Valheim Plus's ~60 per-feature `enabled` toggles are open work in
`TODO.md` — do not assume they exist.

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
  brushes rather than hard-coded colors. The platform badge colours are the one deliberate
  exception and live in the theme as `VsgPlatform*Brush` (brand stand-ins, not real logos).
- The Players list is a `ListBox` whose rows are replaced wholesale on every update
  (`PlayersViewModel.UpsertRow`) — per-field `INotifyPropertyChanged` on rows is pointless there.
- `PlayerInfo.PlayerStatus` is `[JsonIgnore]`: a player restored from the cache reads as Offline
  until they actually connect. That is what makes the offline rows dim immediately.
- Port forwarding is manual only (a "Ports" dialog, like UPnP Wizard): UPnP/NAT-PMP via Mono.Nat
  (`IPortForwarder`), mapping UDP `base..base+2`. Nothing is mapped automatically; only the router is
  touched (no Windows Firewall changes). CGNAT/double NAT is detected via `ValheimPorts.IsPrivateAddress`.
