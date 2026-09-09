# AGENTS.md

Context for AI agents working in this repository. Read this before making changes.

## Project

WinForms (.NET 10) desktop app that manages a Valheim dedicated server on Windows:
starts/stops `valheim_server.exe`, parses its stdout for status & player events,
manages server profiles, worlds, and difficulty settings.

This is a **community fork** of [runeberry/ValheimServerGUI](https://github.com/runeberry/ValheimServerGUI)
(upstream is dormant since 2024). Licensed **GNU GPLv3** — keep the `LICENSE` file and the
"© Runeberry Software, LLC / Licensed under GNU GPLv3" notice in `AboutForm` intact.
Upstream promo links (Discord, donate, email) were deliberately removed; support links
must point to this fork's GitHub Issues (`Resources: UrlIssues`).

## Solution layout

| Project | TFM | Purpose |
|---|---|---|
| `ValheimServerGUI` | net10.0-windows | Main WinForms app (forms, game logic, logging) |
| `ValheimServerGUI.Controls` | net10.0-windows | Custom form-field controls (FormField family) |
| `ValheimServerGUI.Tools` | net10.0 | Process runner, JSON storage, loggers, HTTP |
| `ValheimServerGUI.Tests` | net10.0-windows | xUnit tests for the app |
| `ValheimServerGUI.Serverless` (+.Tests) | net10.0 | AWS Lambda backend (bug reports/player info) — legacy, mostly dead upstream API |

Key files:
- `ValheimServerGUI/Game/ValheimServer.cs` — server process lifecycle + **log-parsing regexes** (`LogBasedActions`)
- `ValheimServerGUI/Game/ValheimPathExtensions.cs` — world discovery (see World layouts below)
- `ValheimServerGUI/Game/WorldGen*.cs` — difficulty presets / modifiers / keys
- `ValheimServerGUI/Tools/Logging/ValheimServerLogger.cs` — server log noise filter
- `ValheimServerGUI/Properties/Resources.resx` (+ generated Designer.cs) — app strings & URLs

## Build / test / run

Requires **.NET SDK 10** (`dotnet --list-sdks`). GUI targets `net10.0-windows`.

```pwsh
dotnet build ValheimServerGUI.sln -c Debug
dotnet test ValheimServerGUI.sln --nologo            # 22 tests, must be green
dotnet run --project ValheimServerGUI                # or run bin\Debug\net10.0-windows\ValheimServerGUI.exe
```

Gotchas:
- A running GUI instance **locks** `bin\Debug` DLLs — stop `ValheimServerGUI` (and `valheim_server`)
  processes before rebuilding.
- `dotnet test -c Release` / Release builds of Tools/Controls need `/p:SignAssembly=false`
  (upstream `.snk` is not committed).
- Tests are hermetic: `ValheimServerTests` creates a dummy exe + temp save folder — do not
  reintroduce machine-specific paths there.

## Release / publish (vX.Y.Z)

1. Bump `<Version>` in `ValheimServerGUI/ValheimServerGUI.csproj`.
2. **Clean first** — `dotnet clean ValheimServerGUI.sln -c Release`. Skipping this reuses stale
   obj outputs whose PDB references embed build-machine usernames into the single-file exe.
3. Publish (the post-publish `signtool` failure is **cosmetic** — upstream cert is absent; outputs
   are already written):
   ```pwsh
   dotnet publish ValheimServerGUI\ValheimServerGUI.csproj -c Release `
     /p:PublishProfile=small-x64-release /p:SignAssembly=false `
     /p:TargetFramework=net10.0-windows /p:DebugType=none /p:DebugSymbols=false
   ```
   Output: `publish\small-x64\` (single-file exe, ReadyToRun).
4. **Scan the artifact** for secrets and machine data (this has caught real issues twice):
   passwords, server/world names, local IPs, SteamIDs, `C:\Users\<name>` paths, upstream promo
   URLs. Decode as ASCII and search the raw bytes.
5. Zip `ValheimServerGUI.exe` + `LICENSE.txt`, commit, tag `vX.Y.Z`, push, `gh release create`.
6. `SolutionResources/ClientSecrets.Values.cs` and `ServerSecrets.Values.cs` are **gitignored**
   local mocks required for Release builds — create empty-string mocks, never commit real values.

## Valheim server integration (verified against 1.0.7, network version 39)

The GUI launches `valheim_server.exe` with `-nographics -batchmode -name -port -world -public
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
3. Update `LogBasedActions` + tests in `ValheimServerTests` (message constants at the top).

Known formats (1.0.7): `Game server connected` (Running; do not match `Game server connected
failed`), `World save (5/5) done. Total time [Xms]` (may contain digit group separators),
`Session "..." registered with join code X` / `Session "..." with join code X and IP ... is active`,
`Got connection SteamID X`, `PlayFab socket with remote ID playfab/X received local Platform ID
Steam_<id>`, `Got character ZDOID from <name> : <user>:<obj>` (name is UTF-8),
`Peer <id> has wrong password`, `Peer <id> has incompatible version`, `Closing socket <id>`,
`Destroying abandoned non persistent zdo <user>:<obj> owner <uid>`.

**World storage (1.0.7+)**: worlds live in `worlds_local/<WorldName>/_main.<n>.fwl2|db2`
(a directory per world). Legacy `worlds*/<Name>.fwl` is still supported by the GUI —
`ValheimPathExtensions` scans both.

**Difficulty data sources (authoritative, in priority order):** game enums
(`WorldPresets`/`WorldModifiers`/`WorldModifierOption` — decompile `assembly_valheim.dll`),
game assets (`valheim_server_Data/resources.assets`, key strings like `nobuildcost|playerevents|
passivemobs|nomap|fire`), the shipped `Valheim Dedicated Server Manual.pdf` (often **lags**
behind — e.g. it omits `fire`). The GUI's gear-icon dialog (WorldPreferencesForm) currently
matches 1.0.7 exactly; presets override modifiers/keys, same as in-game.

Server stdout is read as **UTF-8** (`ProcessExtensions.AddBackgroundProcess`) — required for
non-ASCII character names. Do not remove those encoding settings.

## Local environment notes

- Valheim server install (this machine): `E:\SteamLibrary\steamapps\common\Valheim dedicated server`
- App data (plaintext, may contain the real server password — **never commit, never print**):
  `%USERPROFILE%\AppData\LocalLow\Runeberry\ValheimServerGUI\` (`userprefs.json`,
  `players-cache.json`, `logs\`)
- The app has **no single-instance guard**: two GUIs = two windows, and the second server start
  fails on port 2456/2457.
- Ports for LAN/Internet play: UDP 2456–2458 must be forwarded; loopback connections to the
  public IP usually fail without NAT hairpin (use `localhost`).

## Conventions

- Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`), tag releases `vX.Y.Z`.
- Version lives only in `ValheimServerGUI.csproj` `<Version>`.
- The repo uses XML-doc comments in code — keep existing style; do not strip comments.
- Roadmap (do not implement without asking): BepInEx install/version, mod list, mod config
  (on/off), mod presets — see README "Roadmap".
