# Avalonia Migration Roadmap

This document is the implementation plan for moving ValheimServerGUI from WinForms to Avalonia.
It is intentionally based on the repository as it exists today, not on an assumed future project
layout.

## Product priorities

1. Cross-platform desktop client
2. Management of a server on another machine over SSH
3. A modern, maintainable UI
4. Native AOT as an experiment after the application is functionally stable

The migration is incremental. The existing WinForms application remains the supported Windows
client until the Avalonia client reaches feature parity and has passed the same real-server checks.

## Repository reality

### Projects today

| Project | TFM | Role | Migration relevance |
|---|---|---|---|
| `ValheimServerGUI.Core` | `net10.0` | Platform-neutral domain: server options & validation, player models, log parsing (regexes), world-gen data, primary-key contract | New shared project; must stay free of WinForms, System.Drawing, registry and local-process APIs |
| `ValheimServerGUI` | `net10.0-windows` | WinForms executable, game logic, logging and platform helpers | Must be split or referenced by a new client only after dependencies are isolated |
| `ValheimServerGUI.Tools` | `net10.0` | Process, JSON, HTTP, logging and data helpers | Good starting point for shared code, but JSON and file APIs still need AOT/remote review |
| `ValheimServerGUI.Controls` | `net10.0-windows` | Reusable WinForms controls | WinForms-only; do not carry into Avalonia |
| `ValheimServerGUI.Tests` | `net10.0-windows` | xUnit tests for game logic and UI | Logic tests should become cross-platform; UI tests should be replaced |
| `ValheimServerGUI.Serverless` | `net10.0` | Legacy Lambda backend | Not part of the desktop UI migration |

There is no Avalonia project or SSH project in the solution yet. The solution currently contains seven
projects: the WinForms executable, `ValheimServerGUI.Core`, `ValheimServerGUI.Tools`, the WinForms
controls library, the two test projects and the legacy Serverless backend.

### Measured code size

The current C# line counts are approximate and include generated WinForms designer code:

| Area | Lines | Notes |
|---|---:|---|
| `ValheimServerGUI/Forms` | 6,339 | WinForms forms and designer files; rewrite |
| `ValheimServerGUI/Controls` | 759 | Additional controls inside the executable; rewrite |
| `ValheimServerGUI.Controls` project | 2,617 | WinForms custom-control library; rewrite or retire |
| `ValheimServerGUI/Game` | 3,237 | Valuable domain logic, but not yet UI/platform independent |
| `ValheimServerGUI/Tools` | 1,684 | Mixed: logging/network helpers plus WinForms/platform code |
| `ValheimServerGUI.Tools` project | 1,027 | TFM-neutral, but uses Newtonsoft.Json and local process/data assumptions |
| `ValheimServerGUI` total | 12,850 | Includes resources, forms, game and tools |

The old estimate of "~4,300 lines reusable as-is" is too optimistic. A better estimate is:

- **Reusable after seams are introduced:** most of `Game` and `ValheimServerGUI.Tools`, roughly
  3,000-4,000 lines of behavior, plus tests.
- **Reusable without meaningful changes:** only the parts that do not depend on WinForms, Windows
  paths, local `System.IO`, local `Process`, or Newtonsoft reflection behavior.
- **UI rewrite:** all forms and WinForms controls, including designer code.

### Existing UI and service boundaries

`Program.ConfigureServices` is a useful composition root, but it currently registers UI-specific
services (`IFormProvider`, WinForms forms) alongside domain services. `ValheimServer` depends on the
Core `IServerProcessFactory` abstraction, whose local implementation wraps
`System.Diagnostics.Process`; the SSH implementation will implement the same interface.

The current server lifecycle also:

- creates a process from a validated `ServerProcessSpec`;
- attaches `OutputDataReceived`, `ErrorDataReceived` and `Exited` handlers;
- starts asynchronous local stdout/stderr reading;
- terminates the process through `IServerProcess.Stop()`;
- parses logs through `ValheimServerLogger` and `ServerLogParser`.

The current mod managers are not remote-ready. `BepInExManager`, `ValheimPlusManager`,
`BackupService`, `PlayerLogReader` and `ZipHelper` directly use `File`, `Directory`, `Path`,
`FileVersionInfo` and `ZipFile`. `ModSourceClient` downloads to a local temporary directory.
Remote support therefore needs both a process transport and a filesystem/archive strategy.

Other platform-specific points found in the code:

- `Program.cs` uses `Application.Run`, WinForms DPI setup and WinForms forms.
- `FormProvider`, `ExceptionHandler`, `StartupHelper` and `WinFormsExtensions` are WinForms-bound.
- `StartupHelper` writes the Windows Run registry key.
- `OpenHelper` always invokes `explorer.exe` for directories and URLs.
- `Properties/Resources.Designer.cs` exposes `System.Drawing.Bitmap` and `Icon` resources.
- `Resources.resx` is a WinForms/System.Drawing-oriented resource file.
- `UserPreferencesProvider` expands Windows-style environment variables and persists local paths.
- JSON persistence and HTTP models use Newtonsoft.Json throughout both the app and Tools project.
- The server stdout path is UTF-8-sensitive and must remain UTF-8 for non-ASCII character names.

### Tests today

There are 108 `[Fact]`/`[Theory]` tests across three test projects (the exact executed count can
differ because theories expand at runtime):

- `ValheimServerGUI.Core.Tests` (`net10.0`): 53 tests — log parsing (`ServerLogParser`), the full
  server state machine (`ValheimServerCoreTests`), `ValheimServerOptions` validation, player models
  and world discovery. These run without a Windows desktop and are the primary cross-platform
  safety net.
- `ValheimServerGUI.Tests` (`net10.0-windows`): 54 tests — server integration (`ValheimServerTests`),
  mods/backups, and WinForms UI tests (`MainWindowTests`, `SplashFormTests`).
- `ValheimServerGUI.Serverless.Tests` (`net10.0`): 1 test.

Before changing architecture, add tests around process events, path resolution, JSON migration,
archive extraction, and log parsing. These tests should target a TFM-neutral project where possible.

### Phase 1 progress (2026)

Completed so far, with the WinForms app still building and all tests green:

- Added `ValheimServerGUI.Core` (`net10.0`) and `ValheimServerGUI.Core.Tests` (`net10.0`).
- Moved into Core (namespaces preserved for a smooth transition):
  - `ServerStatus`, `PlayerStatus`, `PlayerInfo`, `PlayerDataQuery`, `PlayerPlatforms`,
    `IPrimaryKeyEntity`.
  - `WorldGenPresets`, `WorldGenModifiers`, `WorldGenKeys`.
  - `ValheimServerOptions` + `IValheimServerOptions` with validation, and the
    `GetValidatedServerExe`/`GetValidatedSaveDataFolder` path helpers.
  - Log parsing: `ServerLogParser` + `ServerLogPatterns` (the regex table that used to live inline in
    `ValheimServer`). `ValheimServer` now registers its handlers against these patterns.
  - World discovery: `ValheimPathExtensions` (`GetWorldNames`/`IsWorldNameAvailable`) now lives in
    Core and scans both the legacy `worlds/*.fwl` and modern `worlds_local/<World>/` layouts.
- `ValheimServerGUI.Tools` now references Core (for `IPrimaryKeyEntity`); the app references Core and
  Tools.
- Introduced the process seam: `IServerProcess` / `IServerProcessFactory` / `ServerProcessSpec` in
  Core (`ValheimServerGUI.Core/Processes/ServerProcess.cs`), with a local implementation
  (`LocalServerProcess` / `LocalServerProcessFactory` in Tools) that preserves the UTF-8 stdout and
  working-directory behavior. `ValheimServer` now depends on `IServerProcessFactory` instead of
  `IProcessProvider`, and the old `IProcessProvider`/`ProcessProvider`/`ProcessExtensions` were
  removed. `MockServerProcessFactory` replaces the old mock in tests.
- Moved the server **state machine** into Core: `ValheimServer` now lives in
  `ValheimServerGUI.Core/Game/ValheimServer.cs`. It depends only on Core contracts
  (`IServerProcessFactory`, `IApplicationLog`, `IServerLoggerFactory`, `IPlayerDataRepository`,
  `ValheimConstants.SteamAppId`) plus the Core models/parser. The app-side `ValheimServerLogger`
  implements Core `IServerLogger`, and `ValheimServerLoggerFactory` implements
  `IServerLoggerFactory`; `ApplicationLogger` implements `IApplicationLog`. `IPlayerDataRepository`
  (interface) and `IDataRepository` moved to Core; the concrete `PlayerDataRepository` stays in the
  app. Core now contains cross-platform tests for the full server lifecycle
  (`ValheimServerCoreTests`).
- Added the platform contract `IPlatformIntegration` (open directory/URL) in Core, with
  `WindowsPlatformIntegration` as the local implementation (registered in DI). The legacy static
  `OpenHelper` now delegates to it, so existing call sites are unaffected while the Avalonia client
  can depend on the contract directly.
- Fixed a latent null-reference in `ValheimServerOptions.Validate()` (`AdditionalArgs` guard).

Phase 1 exit criteria are met: the WinForms app builds and runs, Core tests run without a Windows
desktop (53 tests), and no Core source references `System.Windows.Forms`, `System.Drawing`, registry
APIs or a concrete local process. Remaining niceties (filesystem/archive contracts, resource
separation, `IUserInteraction`) can be introduced as Phase 2 needs them; Core namespaces are still
preserved for a smooth move.

### Phase 2 progress (2026)

- Extracted `ValheimServerGUI.Infrastructure` (`net10.0`): all platform-neutral services previously
  inside the WinForms app moved out, preserving namespaces (`ValheimServerGUI.Game`,
  `ValheimServerGUI.Tools`, `ValheimServerGUI.Tools.Logging`). This includes preferences providers,
  `PlayerDataRepository`, mods/backups, software-update/IP/HTTP clients, path/time helpers, the
  platform integration and the Serilog logging pipeline. `Resources.*` access was replaced by
  `AppSettings` (paths/URLs/defaults) so `Resources.resx` stays WinForms-only; `ClientSecrets`
  compile-include was carried over for Release. `IExceptionHandler` contract moved to
  `ValheimServerGUI.Infrastructure.Diagnostics` (the WinForms `ExceptionHandler` implementation
  stays in the app). The WinForms app now references Infrastructure.
- Created `ValheimServerGUI.Avalonia` (`net10.0`) with Avalonia 12.1.2 (Avalonia, Avalonia.Desktop,
  Avalonia.Themes.Fluent, Avalonia.Controls.DataGrid), `Microsoft.Extensions.DependencyInjection`
  and `CommunityToolkit.Mvvm`. It references Core + Infrastructure + Tools only (no WinForms).
- Implemented Phase 2 step 1: app startup (`Program`/`AppBuilder`), DI composition root
  (`AppServices`, mirroring the WinForms registrations minus forms), exception boundary
  (AppDomain/TaskScheduler/Dispatcher handlers routed to `AvaloniaExceptionHandler`), `ViewLocator`,
  and a compiled-binding smoke test (`ShellViewModel` + `MainWindow.axaml` with profile selection
  and server status). The window renders and stays stable.
- Fixed a regression from moving `AssemblyHelper` to Infrastructure: `GetExecutingAssembly()`
  returned the Infrastructure assembly (informational version without the `+build` suffix), so
  `GetApplicationVersion()`/`GetApplicationBuildDate()` threw `ArgumentOutOfRangeException`. Now
  they read from `Assembly.GetEntryAssembly()` and tolerate a missing `+build` prefix.
- Implemented Phase 2 steps 2-4 (main shell + server controls): `ServerControlsViewModel` (server
  options, world selection, start/stop/restart with validation and port checks), a shell status bar
  (status, uptime, external/internal IP, invite code), and the `IUserInteraction` contract
  (`AvaloniaUserInteraction` with code-built dialogs). The `MainWindow` now renders the full control
  surface with compiled bindings; options are editable only while the server is stopped.
- Implemented Phase 2 steps 5-8: Players tab (`PlayersViewModel` with live status updates from
  `IPlayerDataRepository` events and remove/refresh), Logs tab (`LogsViewModel` streaming
  `IApplicationLogger` output with a bounded buffer), Mods &amp; Backups tab (`ModsViewModel` for
  BepInEx/Valheim Plus install and backup health/summary), and Preferences/About dialogs
  (`PreferencesViewModel`, `AboutViewModel` with version/build-date). The window is now a
  `TabControl` shell (Server / Players / Mods / Logs) with top-bar profile/status/actions and a
  status bar.
- Final audit fixes (4 parallel review agents, cross-checked against Avalonia 12.1.2 docs/source):
  - `ValheimServer` registered as **singleton** (was transient -> two independent instances meant
    the shell status/uptime/invite code never updated).
  - `MainWindow` now takes `ShellViewModel` via DI and sets `DataContext` (the shell previously
    never resolved a ViewModel, so the whole UI was inert).
  - Top-bar grid buttons moved onto `Auto` columns (they sat on fixed-width spacer columns and
    would clip).
  - `AvaloniaUserInteraction` now `await`s `ShowDialog` (Avalonia's `ShowDialog` is async; the
    previous code read the result before the dialog closed, so `ConfirmAsync` always returned
    false).
  - `Start/Stop/Restart` commands wired to `CanExecute`; Preferences OK now closes the window;
    Logs DataTemplate annotated with `x:DataType`; Mods install writes to the correct status field
    and only refreshes when server paths change; player cache is loaded on startup and cleared
    before reload; `IPlatformIntegration` registered in the Avalonia container; About links to the
    fork's GitHub Issues; Avalonia csproj gains `SourceRevisionId` for a truthful build date.
- Windows real-server validation (start/stop/restart, player join/leave, mods/backups) is the
  remaining Phase 2 exit-criteria gate before WSL/Linux (Phase 3).

### Post-audit feature work (2026)

- Cherry-picked upstream features onto the fork's structure: `Directory.Build.props`
  (`EnableWindowsTargeting`), pure `AssemblyHelper.CompareVersions` + `GitHubClient.SelectLatestRelease`
  with tests, and the case-correct `DiscordLogo` resource path (fixes Linux/CI builds).
- **Steam Cloud world import** (`Infrastructure/Game/SteamCloudWorldProvider`): cloud worlds appear
  in the Avalonia world dropdown with a ` (cloud)` suffix; starting one prompts Move/Copy/Cancel and
  imports the folder into `worlds_local` before launch. Registry access is Windows-guarded.
- **Discord webhook notifications** (ported from upstream PR #83 to Core/Infrastructure/Avalonia):
  `Server.PlayerDied` + the `0:0` death log pattern in Core, `DiscordWebhookClient` and Discord
  preferences in Infrastructure, and a `DiscordStatusService` + `DiscordSettingsWindow` in Avalonia.
- **Mods open-folder actions**: `IPlatformIntegration.OpenFile`, BepInEx plugins/config/log paths and
  config-file listing, surfaced in the Avalonia Mods tab (open server/plugins/config folders, open the
  BepInEx log, and open a selected `*.cfg`).
- **`LastStatusServer`**: player records now carry the last server a status change was seen on.
- **Robustness + tray**: platform open-folder/file/URL calls are best-effort (a missing path or
  shell handler never throws), and the Avalonia client adds a system tray icon. Closing the window
  now hides it to the tray so a running server is never stopped by accident; the tray menu offers
  Show / Start / Stop / Exit (Exit stops the server first).
- **Dark theme**: a Valheim "Mistlands Tech" palette (deep charcoal `#12131A`, panel `#1C1D26`,
  text `#E2E8F0`, purple accent `#9333EA`, cyan success `#06B6D4`) applied as Fluent resource
  overrides plus a Windows dark DWM title bar. Server status uses the palette (cyan Running,
  amber transitional, muted Stopped).
- **UPnP port forwarding** (`IPortForwarder` in Core, `UpnpPortForwarder` in Infrastructure using
  Mono.Nat 3.0.4): a manual "Ports" dialog discovers the gateway and maps/removes the three
  adjacent UDP ports the server needs. Manual only (nothing auto-mapped), router-only (no firewall
  changes), with CGNAT/double-NAT detection via `ValheimPorts.IsPrivateAddress`.

## Target architecture

Do not begin by copying `Game/` into a new project. First introduce seams that can be implemented by
both local and SSH backends.

### Proposed projects

Names are provisional and should be chosen when Phase 1 starts:

- `ValheimServerGUI.Core` (`net10.0`): server options, status, log parsing, player state, world
  preferences, mod/backup contracts and platform-neutral models.
- `ValheimServerGUI.Infrastructure` (`net10.0`): local process, local filesystem, JSON storage,
  HTTP, logging and platform adapters.
- `ValheimServerGUI.WinForms` (temporary compatibility client): current UI while the new client is
  developed. It may remain named `ValheimServerGUI` to reduce release/build churn.
- `ValheimServerGUI.Avalonia` (`net10.0`): Avalonia application, ViewModels and desktop adapters.
- `ValheimServerGUI.Remote` (`net10.0`): SSH/SFTP implementation, only after the local seams work.

Do not move `Resources.resx` into Core. Replace resource access from domain code with neutral
options, path providers and localized UI resources. The current generated resource class returns
System.Drawing types and would keep the shared project Windows-bound.

### Required interfaces

The first refactor should introduce narrow interfaces rather than one large `IRemoteServer`:

- `IServerProcess` or an equivalent event/stream abstraction: stdout, stderr, exit and stop.
- `IServerProcessFactory`: start a process from a validated command specification.
- `IFileSystem`: existence, read/write streams, directory enumeration, copy/move/delete and temp
  files. Keep `System.IO` behind this interface only where remote behavior is required.
- `IArchiveService`: extract/upload archives without assuming a local destination.
- `IPlatformIntegration`: open URL/folder, startup registration, native notifications and tray.
- `IClock` or .NET `TimeProvider`: uptime, delays and deterministic tests.
- `IUserInteraction`: error dialogs, file/folder pickers and confirmation prompts. The core must
  never call `MessageBox`.

The local implementations should preserve current behavior first. Only then add SSH implementations.
Avoid an abstraction for every `Path.Join` call; paths are cheap values, while process, filesystem,
UI and platform side effects are the important boundaries.

### Local and remote process model

The local implementation wraps `System.Diagnostics.Process`. The remote implementation cannot
simply return a `Process` object. For the first SSH version, target a Linux host and use an explicit
remote session model:

1. Validate the remote working directory and server binary.
2. Start a managed command through SSH, redirecting stdout/stderr to a remote log file and writing
   a PID or process-group identifier.
3. Stream or poll the remote log and feed the same parser used locally.
4. Stop the process group through a controlled remote command, not by guessing a process name.
5. Detect exit and stale PID files after reconnecting.

`SshClient.RunCommand` is suitable for short commands, not for the complete lifetime of a server.
SSH.NET `ShellStream` or a small remote helper script/service may be needed for long-lived sessions.
SFTP is suitable for files, but it does not extract archives or start processes. Uploading an archive
and invoking `tar`/`unzip` remotely should be explicit and validated.

The first supported remote target should be Linux. Remote Windows over OpenSSH is a later scenario
because shell quoting, process groups, path syntax and service behavior differ substantially.

## Avalonia decisions

Avalonia 12 supports .NET 8 and later; .NET 10 is the recommended target. Pin all Avalonia packages
to one tested version in one place. At the time this document was updated, the working baseline is
Avalonia 12.1.x; verify the exact patch version with NuGet before creating the project.

Initial package set:

```xml
<PackageReference Include="Avalonia" Version="12.1.x" />
<PackageReference Include="Avalonia.Desktop" Version="12.1.x" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.x" />
<PackageReference Include="Avalonia.Controls.DataGrid" Version="12.1.x" />
```

Use compiled bindings (`x:CompileBindings="True"`) from the first screen. Use CommunityToolkit.Mvvm
only if it is actually useful for the ViewModels; do not add ReactiveUI just because an old template
does. `Avalonia.Diagnostics` should not be listed: Avalonia 12 removed that package. DevTools need
the current Avalonia diagnostics package/product documented for the chosen 12.x release.

Control mapping:

| WinForms | Avalonia plan |
|---|---|
| Forms and designer files | XAML views plus ViewModels; no generated designer equivalent |
| `DataListView` | `DataGrid` for players/backups, `ItemsControl`/`ListBox` where rows are custom |
| RichTextBox log viewer | Start with virtualized log rows; evaluate AvaloniaEdit only if needed |
| MessageBox | An injected dialog service; choose an Avalonia-compatible dialog implementation |
| Open/folder dialogs | Avalonia `StorageProvider`, behind `IUserInteraction` |
| NotifyIcon | Avalonia tray support plus per-desktop packaging/testing; tray is not a core contract |
| ImageList/System.Drawing | Avalonia resources, SVG/PNG assets and platform-neutral icon keys |
| WinForms timers | `PeriodicTimer`, dispatcher scheduling or ViewModel lifecycle services |

Do not promise identical tray behavior on every Linux desktop. GNOME, KDE, Wayland, X11 and desktop
extensions differ. A functional window without a tray is an acceptable first Linux milestone.

## Phased implementation plan

### Phase 0: stabilize the Windows release first

Current repository version is `2.4.1`; `v2.5.0` is a future milestone, not a shipped fact.

Tasks:

- Build and test the current solution using the repository's documented .NET 10 commands.
- Exercise server start, running, player join/leave, world save, stop and restart with a real server.
- Exercise BepInEx and Valheim Plus install-from-file and update flows.
- Verify backups, new `worlds_local/<WorldName>/_main.*` discovery and legacy world layouts.
- Verify UTF-8 player names and password redaction in logs.
- Keep the current WinForms release path stable while migration branches are developed.
- Tag the stabilized release only after CI/build artifacts and release scanning pass.

Exit criteria: a reproducible Windows release with no known regression in current features and a
green test run. Do not start a broad UI rewrite before this point.

### Phase 1: make the domain portable without changing the UI

Tasks:

- Add Core/Infrastructure projects only when a real dependency is moved, not as empty shells.
- Extract process, file, archive, clock and platform contracts.
- Keep a local implementation that behaves exactly like the current WinForms path.
- Move log parsing and server state transitions into Core while preserving existing regex tests.
- Move `ValheimServerOptions` validation into Core, including path validation through an injected
  environment/path policy.
- Separate user-facing strings/resources from domain classes.
- Replace `IFormProvider` and direct MessageBox calls with UI-facing interfaces in the WinForms host.
- Add cancellation tokens and explicit lifecycle disposal to long-running operations.
- Add cross-platform tests for the extracted code and keep the existing WinForms tests until parity.

Exit criteria: the existing WinForms app still builds and runs; Core tests run without a Windows
desktop; no Core source references `System.Windows.Forms`, `System.Drawing`, registry APIs or a
concrete local process.

### Phase 2: Avalonia Windows parity

Create `ValheimServerGUI.Avalonia` as a second client. Do not replace the release executable yet.

Implement in this order:

1. App startup, DI composition root, exception boundary and compiled-binding smoke test.
2. Main shell, profile selection and server status.
3. Server options, validation, world preferences and start/stop/restart.
4. Server details, uptime, invite code, IP status and world-save status.
5. Players and player details.
6. Logs and log-folder actions.
7. Mods and backups.
8. Preferences, update checks, About and remaining dialogs.

Use ViewModels that expose observable state and commands. Keep server operations off the UI thread,
marshal state changes through the UI dispatcher, and make every async command report failure through
the injected interaction service.

Exit criteria: feature parity with the stabilized WinForms client on Windows, including real-server
start/stop and mod/backup checks. The WinForms client remains the fallback release during this phase.

### Phase 3: WSL validation

WSL is a validation lane, not automatically a production distribution target.

Tasks:

- Test the Avalonia client under WSLg on a WSL2 distribution.
- Test both local Linux server execution (`valheim_server.x86_64`) and a Windows server boundary.
- Use `OperatingSystem.IsWindows()`, `IsLinux()` and `IsMacOS()` in platform adapters, not scattered
  string checks.
- Replace Windows-only startup registration with a no-op or a documented Linux user-service option.
- Replace `explorer.exe` with platform adapters (`open`, `xdg-open` or equivalent) and test failure
  reporting.
- Verify UTF-8, executable permissions, case-sensitive paths, signal-based shutdown and save paths.
- Verify X11/Wayland behavior supplied by WSLg; do not require a tray for the first WSL milestone.

Exit criteria: GUI launches under WSLg and a Linux server can be started, observed, stopped and
restarted locally. Any unsupported WSL scenario is documented rather than silently claimed supported.

### Phase 4: Fedora, Ubuntu and macOS

Start with runtime-dependent builds. Add Native AOT only after each platform has a working baseline.

Linux tasks:

- Test at least one GNOME/Wayland and one X11-compatible environment.
- Validate `XDG_CONFIG_HOME`, `XDG_DATA_HOME` and `XDG_STATE_HOME` with sensible fallbacks.
- Test executable permissions, `SIGTERM`/process groups, case-sensitive world names and file locks.
- Decide packaging per release: AppImage, Flatpak, `.deb` and/or `.rpm`; do not promise all formats.
- Treat tray integration and systemd user services as optional platform adapters.

macOS tasks:

- Test `osx-arm64` and `osx-x64` only if hardware/CI is available.
- Use `~/Library/Application Support` and macOS cache/log conventions through a path provider.
- Test app bundle, quarantine/Gatekeeper, code signing and optional notarization separately.
- Verify the Valheim server distribution actually available on macOS before promising local hosting.

Exit criteria: a documented install/run/test matrix for Fedora, Ubuntu and macOS, including known
limitations. A platform is supported only when server lifecycle and persistent data tests pass.

### Phase 5: SSH remote management

Implement SSH after the local abstraction seams and WSL/Linux process behavior are proven. The first
vertical slice should be: local Avalonia GUI on Windows or Linux → SSH → Linux Valheim server.

Add:

- connection profile: host, port, username, authentication method and remote server directory;
- key-based authentication as the default; password authentication only as an explicit fallback;
- host-key verification and a clear trust/change workflow; never silently accept any host key;
- connection test, reconnect and operation cancellation;
- remote command executor, SFTP file access and remote archive operation;
- remote PID/process-group tracking and log streaming/polling;
- separate local GUI logs from remote server logs;
- no password/private-key material in ordinary preferences or crash reports.

The likely library is the NuGet package `SSH.NET` (`Renci.SshNet` namespace). Confirm its current
version, license and Native AOT behavior before adding it. A dependency being compatible with .NET
10 does not make it reflection/trimming/AOT-safe.

Remote acceptance tests:

- connect with a test key and reject an untrusted/changed host key;
- inspect remote server/world paths without local path assumptions;
- start and observe stdout/stderr; preserve non-ASCII names;
- stop/restart after a GUI reconnect;
- install a test archive and verify remote extraction permissions;
- list/download backups without exposing credentials;
- recover cleanly from network loss and stale remote processes.

## Native AOT track

Native AOT is deliberately after the Windows parity milestone and should not control the architecture
of the first Avalonia screen.

Avalonia 12's Native AOT guidance requires, at minimum, AOT compatibility across referenced projects,
compiled XAML bindings and avoiding runtime-dynamic XAML/resource behavior. Start with a normal
self-contained publish, then enable trimming warnings, then AOT. Keep separate publish profiles per
RID (`win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`).

Known blockers in this repository:

- Newtonsoft.Json is used in `JsonFileProvider`, HTTP models, preferences, player data, mod metadata
  and crash reports.
- generic `LoadAsync<TFile>`/`SaveAsync<TFile>` and untyped REST deserialization use runtime type
  information;
- generated `Resources.Designer.cs` and WinForms dependencies must not enter the AOT client;
- SSH.NET and third-party Avalonia controls need independent trimming/AOT verification;
- reflection in logging, assembly metadata and resource loading needs an actual publish test.

Preferred sequence:

1. Keep Newtonsoft for the WinForms compatibility client.
2. Introduce a source-generated `System.Text.Json` serializer context for new Core models.
3. Migrate persisted JSON with golden files and backward-compatible property names.
4. Remove untyped deserialization from the AOT client boundary.
5. Set `IsAotCompatible` on eligible projects and fix warnings instead of suppressing them globally.
6. Publish and compare startup time, private memory, binary size and behavior against the regular
   self-contained build.

Do not set a hard binary-size or startup-time exit target before measuring an actual application build.
Native AOT does not guarantee a sub-100 ms startup or a specific file size for a desktop app with
Skia, fonts, assets and networking.

## .NET 10 and C# 14 usage policy

The solution already targets `net10.0`, but it does not currently opt into C# 14-specific patterns
systematically. Use new language features where they reduce bugs or boilerplate, not as a mechanical
rewrite during the UI migration.

### `field` backed properties

C# 14's contextual `field` keyword allows validation or normalization without declaring a manual
backing field:

```csharp
public int Port
{
    get;
    set => field = value is >= 1 and <= 65535
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value));
}
```

Good candidates after nullability and model ownership are clarified:

- `ValheimServerOptions` scalar validation;
- ViewModel properties where validation does not need to raise a separate notification;
- small immutable-ish configuration models.

Important limitations:

- `field` does not raise `INotifyPropertyChanged`; a ViewModel setter still needs notification
  machinery or a source generator.
- `field` is contextual. Existing members literally named `field` can be ambiguous; use `@field`,
  `this.field` or rename the member.
- Do not use it to hide cross-property validation such as password/world/server-name rules. Keep
  aggregate validation in a validator/service.
- Validate serialization compatibility before changing persisted property behavior.

### Other useful C# 14 features

- **Extension blocks:** group path/platform helpers by receiver, for example `OperatingSystem` or
  URI/path value types. They are compile-time syntax, not runtime plugins, so they do not solve
  remote dispatch or AOT reflection by themselves.
- **Null-conditional assignment:** useful for optional UI adapters, but do not use it to hide lost
  state changes or failed remote operations.
- **`nameof` with unbound generic types:** useful in generic storage/test diagnostics without magic
  strings.
- **Partial constructors/events:** consider only when a source generator or ViewModel framework
  needs them; they are not a reason to restructure the domain.
- **Span conversions:** reserve for measured hot paths such as log parsing or archive metadata. The
  UI and SSH work are I/O-bound, so Span-based rewrites should not come first.

### .NET 10 APIs and practices to adopt

- Enable nullable reference types and warnings incrementally in extracted Core projects.
- Use `TimeProvider` for uptime, delays and restart tests instead of hard-coded `Task.Delay` where
  deterministic time matters.
- Use `PeriodicTimer` or cancellable async loops for refresh work instead of UI timer event plumbing.
- Prefer `CancellationToken` on process, HTTP, archive and SSH operations.
- Use `OperatingSystem.IsWindows/IsLinux/IsMacOS` only at platform boundaries.
- Use `System.Text.Json` source generation for the AOT path, while preserving Newtonsoft-backed
  compatibility in the current Windows client until migration is proven.
- Treat `required`, `init`, records and primary constructors as modeling tools; do not apply them to
  persisted classes until the JSON migration format is covered by golden-file tests.

## Verification gates

Every phase must leave a working build:

1. `dotnet build ValheimServerGUI.sln -c Debug`
2. `dotnet test ValheimServerGUI.sln --nologo`
3. Real-server smoke test for start/running/join/leave/save/stop/restart
4. No credentials, local paths or machine-specific data in logs/artifacts
5. For non-Windows clients: path, encoding, process-signal and permissions tests
6. For AOT: publish per RID, inspect warnings, then run the published artifact

The WinForms release must stay green until the Avalonia client has passed the same gates. Do not
delete the WinForms project merely because the Avalonia window renders.

## References

- Avalonia documentation: <https://docs.avaloniaui.net/>
- Avalonia 12 breaking changes: <https://docs.avaloniaui.net/docs/avalonia12-breaking-changes>
- Avalonia Native AOT: <https://docs.avaloniaui.net/docs/deployment/native-aot>
- C# 14 overview: <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14>
- C# 14 `field` keyword: <https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/field>
- .NET 10 overview: <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview>
- SSH.NET: <https://github.com/sshnet/SSH.NET>
