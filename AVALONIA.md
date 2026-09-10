# AVALONIA.md

Roadmap and architectural plan for migrating ValheimServerGUI from WinForms to Avalonia UI.

## Goals

1. **Cross-platform GUI** — run on Windows, Linux (Fedora, Ubuntu) and macOS
2. **Remote server management** — connect to a Valheim server on another machine via SSH
3. **Modern UI** — Avalonia Fluent theme, dark/light modes, proper DPI scaling
4. **Native AOT** — single-file binary with no .NET runtime required (experimental, for fun)

## Current state (v2.4.1)

| Layer | Lines | WinForms-free? | Avalonia port effort |
|---|---|---|---|
| `ValheimServerGUI.Tools` (net10) | 1 027 | ✅ Fully reusable | — |
| `ValheimServerGUI/Game` + `Game/Mods` | 3 237 | ✅ Fully reusable | — |
| `ValheimServerGUI/Forms` (WinForms + designer) | 6 339 | 🔴 Rewrite | ~4 000 lines XAML + code-behind |
| `ValheimServerGUI.Controls` (12 custom controls) | 2 617 | 🔴 Rewrite | ~2 000 lines Avalonia UserControls/Styles |
| `Program.cs`, `FormProvider`, `WinFormsExtensions` | ~150 | 🔴 Adapt | Replace with Avalonia lifecycle |

**Reusable**: Tools + Game = **~4 300 lines** (log parsing, server lifecycle, mods, backups, prefs, data) — moved to a shared project or kept as-is.

**Rewrite**: ~9 000 lines WinForms (of which ~3 500 is throwaway Designer boilerplate).

Key components to rewrite, by complexity:

| Component | Current | Avalonia replacement | Effort |
|---|---|---|---|
| MainWindow (10 tabs, tray, menu, status bar) | WinForms partials + Designer | XAML + ViewModel(s) | High |
| 12 FormField controls (Label, Text, Checkbox, Dropdown, Numeric, Radio, Filename) | WinForms UserControls + Designer | Avalonia UserControls + Styles | Medium (mechanical) |
| DataListView (players table, backups list) | Custom-drawn ListView (463 lines) | Avalonia DataGrid or ListBox + DataTemplates | High |
| LogViewer | RichTextBox (126 lines) | AvaloniaEdit or SelectableTextBlock + colorization | Medium |
| Tray icon | NotifyIcon (13 usages) | Avalonia.Controls.TrayIcon | Low |
| MessageBox / dialogs | WinForms MessageBox (60), OpenFileDialog, FolderBrowser | MsBox.Avalonia + StorageProvider | Low |
| Icons / ImageList | System.Drawing + 39 resources | Avalonia IImage + SVG/ico | Low |
| SplashForm / AsyncPopout | WinForms forms | Avalonia Windows | Low |

## Phased plan

### Phase 0 — Stabilize current WinForms release (done, pending polish)

**Status**: v2.4.1 shipped. Mods & Backups tabs working. All 45 tests green.

Remaining polish before declaring WinForms "done":
- Verify local install flow (Install from file) with real BepInEx/V+ archives
- Run full mod lifecycle test (install → configure → server loads BepInEx/V+ → Player.log shows versions)
- Collect final round of user feedback on Mods/Backups UX
- Release **v2.5.0** with accumulated fixes
- Archive this as the "legacy" WinForms build

**Exit criteria**: v2.5.0 tagged, no known bugs in daily use.

---

### Phase 1 — Avalonia skeleton + MVVM foundation

**Goal**: Get an empty Avalonia window rendering with the same server status functionality as a proof of concept.

Tasks:
- Create `ValheimServerGUI.Avalonia` project (net10.0, Avalonia 12.1.2)
- Avalonia.ReactiveUI for MVVM, Avalonia.Themes.Fluent for styling
- Port `ConfigureServices()` from `Program.cs` (DI container is framework-agnostic)
- Implement `App.axaml` + `App.axaml.cs` (Avalonia app lifecycle, replaces `Application.Run`)
- Implement `MainWindow.axaml` with a `TabControl` shell (tabs empty initially)
- Move `Game/`, `Tools/`, `Game/Mods/` projects as-is (they are net10.0 / net10.0-windows-free)
  - Tools project: already net10.0, zero changes
  - Game/Mods projects: currently in `ValheimServerGUI` (net10.0-windows) — extract into a new
    `ValheimServerGUI.Game` (net10.0) project so it is shared between WinForms and Avalonia builds
- Implement `IShellViewModel` with basic server path + status properties
- Wire ValheimServer lifecycle to the ViewModel (start/stop/status)
- Wire splash/startup flow

**Exit criteria**: Empty Avalonia window with tabs, ViewModel binding to server status, console log output from server lifecycle events.

---

### Phase 2 — Core server controls (Windows parity)

**Goal**: Start/stop a server from Avalonia UI, matching current WinForms capabilities.

Tasks:
- Server Controls tab: server name, password, port, world select (radio + text/dropdown), crossplay toggle, community toggle
- Advanced Controls tab: save interval, backup settings, additional args, exe path, save folder
- Start / Stop / Restart buttons, status indicator
- Server Details tab: IP addresses (internal/external), invite code, uptime timer, world save stats
- Window chrome: title bar shows profile name + server status icon

**Key considerations**:
- Data binding: bind TextBox/CheckBox/Slider to ViewModel properties, no code-behind impurities
- World preferences form → dialog with the same fields
- File path selectors → Avalonia `StorageProvider.OpenFilePickerAsync()`

**Exit criteria**: Can configure and start a Valheim server from the Avalonia UI on Windows, same feature set as WinForms v2.5.0 Server Controls.

---

### Phase 3 — Players, Logs & Tray

**Goal**: Port the server detail tabs and system tray.

Tasks:
- Players tab: Avalonia DataGrid with columns (Name, Status, Last Update), selection → Player Details dialog
- Player Details dialog: show character list, edit player name
- Logs tab: log viewer with Application/Server views, folder open, clear/save buttons
- Tray icon: minimize to tray, tray context menu (Start/Stop/Restart/Close)
- Status bar with server status, update-check link
- Timer-based refresh (Players table, Server Details, uptime)

**DataListView → DataGrid migration notes**:
- Current `DataListView` supports custom icon rendering, row sorting, column resizing
- Avalonia DataGrid supports sorting, columns, custom CellTemplate — covers all needs
- For icon/status rendering: use `DataGridTemplateColumn` with `Image` + text
- Column sorting: Avalonia DataGrid has built-in sorting support

**LogViewer migration notes**:
- Current `LogViewer` uses WinForms RichTextBox (styled line items)
- Avalonia option 1: `TextBox` with `AcceptsReturn=true, IsReadOnly=true` — simple but no syntax coloring
- Avalonia option 2: AvaloniaEdit (third-party, MIT) — full-featured, but heavier
- Avalonia option 3: `ItemsControl` with virtualized `TextBlock` per line — native, lightweight, supports coloring via `Span` in `TextBlock`
- Recommendation: Option 3 for the MVP (native, matches current behavior), Option 2 later if needed

**Exit criteria**: Players, Logs, Tray fully functional on Windows. Feature parity with WinForms v2.5.0.

---

### Phase 4 — Mods & Backups

**Goal**: Port the install UI and backup viewer.

Tasks:
- Mods tab: status labels, install/update buttons, "Install from file" with StorageProvider, check-for-updates
- BepInEx / Valheim Plus manager wiring (already UI-free in Game/Mods — just bind to ViewModel)
- Backups tab: health status, ListView/DataGrid of backups, Refresh/Open folder
- Open folder → cross-platform: `Process.Start("explorer.exe")` on Windows; `xdg-open` on Linux; `open` on macOS — use `RuntimeInformation.IsOSPlatform()` or Avalonia's `DesktopInterop.OpenFolder()` if available

**Exit criteria**: Full Mods + Backups tabs working on Windows.

---

### Phase 5 — UI polish

**Goal**: Professional look, theming, DPI, icons.

Tasks:
- Fluent theme: dark/light mode toggle (persist in user prefs)
- Icons: convert 39 bitmap resources to SVG or use Avalonia's built-in icon pack
- DPI scaling: Avalonia handles HiDPI natively — verify layout at 125%/150%/200%
- Window state persistence: save/restore size, position, maximized state
- Splash screen: custom Avalonia Window with branding
- About dialog: modern layout, version display
- Preferences dialog: all current settings + theme toggle
- About prompt at first launch

**Exit criteria**: UI looks polished at 100% and 200% DPI, dark/light themes work.

---

### Phase 6 — Native AOT (experimental)

**Goal**: Build a single-file AOT binary to explore startup time and binary size.

Tasks:
- Enable `<PublishAot>true</PublishAot>` in the Avalonia project
- Switch from Newtonsoft.Json to `System.Text.Json` with source generators
  - All `[JsonProperty]` → `[JsonPropertyName]`
  - `JsonConvert.SerializeObject/DeserializeObject` → `JsonSerializer.Serialize/Deserialize`
  - Register source generators for all model types
  - Impact: `PlayerDataRepository`, `UserPreferences`, `WorldPreferences`, `ModSourceClient` JSON models, `CrashReport`
- Enable trimming hints: `<TrimMode>full</TrimMode>`, Avalonia trimming annotations
- Test publish: `dotnet publish -r win-x64 /p:PublishAot=true`
- Compare: binary size, startup time, memory vs the current ReadyToRun single-file exe

**Risks**:
- AOT compilation time is longer (~5-10x)
- Some reflection-heavy code (Serilog sinks, if any) may need AOT annotations
- System.Text.Json migration is a meaningful refactor but pays off in binary size + startup

**Exit criteria**: AOT build produces a single `ValheimServerGUI` binary (< 30 MB), starts in < 100ms, no runtime errors on Windows.

---

### Phase 7 — WSL

**Goal**: Verify the Avalonia build works under Windows Subsystem for Linux.

Tasks:
- Build `linux-x64` target in WSL
- Verify Avalonia rendering (needs X11 or Wayland server — WSLg provides this)
- Verify file paths (`~/.config/...` instead of `%APPDATA%`)
- Disable Windows-only features: StartWithWindows (registry-based), tray icon (if WSLg doesn't support StatusNotifierItem)
- Test server lifecycle (launching valheim_server.exe or the Linux binary `valheim_server.x86_64`)
- Note: on WSL the Valheim dedicated server uses the Linux binary; need to detect server binary name (`valheim_server.x86_64` vs `valheim_server.exe`)

**Exit criteria**: Avalonia GUI launches in WSL with WSLg, can start/stop the Linux server binary.

---

### Phase 8 — Cross-platform: Fedora, Ubuntu, macOS + SSH

**Goal**: Full cross-platform support, including remote server management via SSH.

#### 8a. Linux (Fedora / Ubuntu)

Tasks:
- Build `linux-x64` AOT binary (or runtime-dependent if AOT issues)
- Packaging: Flatpak (primary, Sandboxed) or AppImage; .deb/.rpm secondary
- Tray: Avalonia TrayIcon uses StatusNotifierItem (works on GNOME/KDE via D-Bus)
- Paths: `~/.config/ValheimServerGUI/` for prefs, `~/...IronGate/Valheim/` for game data
- Systemd integration: optional "run server on login" via systemd user service
- Server binary: detect `valheim_server.x86_64` (Linux) vs `.exe` (Windows) automatically

**Exit criteria**: App installs, launches, manages server on Fedora/Ubuntu.

#### 8b. macOS

Tasks:
- Build `osx-arm64` + `osx-x64`
- Packaging: .dmg or .app bundle
- Tray: NSStatusItem via Avalonia TrayIcon (macOS-native)
- Paths: `~/Library/Application Support/IronGate/Valheim/`
- Consider: macOS lacks native SteamCMD; user must install server via Steam manually
- Gatekeeper: code signing or manual allow (open-source app)

**Exit criteria**: App runs on macOS, manages the server binary.

#### 8c. SSH remote management (new feature)

Architecture:
```
┌─────────────────────────────────────────────────┐
│  Avalonia GUI (local, any platform)             │
│  ┌─────────────┐  ┌─────────────┐               │
│  │ IRemoteExec │  │ ISftpClient │               │
│  │ (commands)  │  │ (files)     │               │
│  └──────┬──────┘  └──────┬──────┘               │
│         │                │                       │
└─────────┼────────────────┼───────────────────────┘
          │                │
     ┌────┴────────────┐   │
     │ SSH transport    │   │
     │ (SSH.NET lib)    │   │
     └────┬────────────┘   │
          │                │
   ───────┼────────────────┼────── network ───────
          │                │
┌─────────┴────────────────┴───────────────────────┐
│  Remote Linux/Windows server                      │
│  valheim_server (x86_64 or .exe)                 │
│  BepInEx + V+ installed via SSH                  │
└─────────────────────────────────────────────────┘
```

Core abstractions to introduce:
- `IRemoteCommandExecutor` — execute a shell command, stream stdout/stderr
  - Local: wraps `System.Diagnostics.Process`
  - Remote: wraps `SshClient.RunCommand()` from SSH.NET
- `IFileAccess` — read/write/delete files, list directories
  - Local: `System.IO.File`
  - Remote: `SftpClient` (SSH.NET)
- `IProcessManager` — replace `IProcessProvider` with a platform-agnostic interface
  - Local: current implementation
  - Remote: SSH command + channel read + kill via signal

Server connection model:
- New setting: `ServerConnection` (Local | SSH)
- SSH config: host, port, username, key path (or password), remote server folder
- Stored in `userprefs.json` under a new `connection` object
- SSH.NET NuGet package: `SSH.Net` (MIT license, stable, widely used)

Tasks:
- Define `IRemoteCommandExecutor` + `IFileAccess` interfaces
- Implement `LocalExecutor` (wraps current Process/System.IO logic)
- Implement `SSHExecutor` + `SFTPFileAccess` (SSH.NET)
- Update `ValheimServer` to use `IRemoteCommandExecutor` instead of `IProcessProvider`
- Update `BepInExManager` / `ValheimPlusManager` / `BackupService` to use `IFileAccess`
- Connection settings UI: host/port/user/key + test connection button
- Server executable name detection: `.exe` (Windows) vs `.x86_64` (Linux)
- SSH key management: file picker, key generation hint

SSH.NET package: `SSH.Net` v2024.x (latest), MIT license, supports .NET 8/10, AOT-compatible.

**Exit criteria**: Can connect to a remote Linux server via SSH, install BepInEx/V+, start/stop the server, view logs, manage backups — all from the Avalonia GUI running locally on Windows.

---

## Order of work

```
Phase 0  ▸  v2.5.0 release (WinForms polished)
Phase 1  ▸  Avalonia skeleton + shared Game/Tools project
Phase 2  ▸  Core server controls (Windows)
Phase 3  ▸  Players, Logs, Tray (Windows)
Phase 4  ▸  Mods & Backups (Windows)
Phase 5  ▸  UI polish (Fluent, icons, DPI)
Phase 6  ▸  Native AOT experimental build
Phase 7  ▸  WSL verification
Phase 8a ▸  Linux (Fedora/Ubuntu)
Phase 8b ▸  macOS
Phase 8c ▸  SSH remote management
```

Phases 1-5 form the MVP for a cross-platform desktop app. Phases 6-8 are progressive enhancements. Each phase produces a working build.

## Effort estimate (rough)

| Phase | Scope | Estimate |
|---|---|---|
| 0 | Polish + release | 1-2 days (testing) |
| 1 | Skeleton + shared libs | 2-3 days |
| 2 | Server controls parity | 3-5 days |
| 3 | Players, Logs, Tray | 3-4 days |
| 4 | Mods & Backups | 2-3 days |
| 5 | UI polish | 2-3 days |
| 6 | Native AOT | 2-3 days |
| 7 | WSL | 1-2 days |
| 8a | Linux | 2-3 days |
| 8b | macOS | 2-3 days |
| 8c | SSH remote management | 5-8 days |
| **Total** | | **~20-35 days** |

These are rough estimates for focused work. Phases 1-5 (MVP) ≈ 11-18 days.

## Technical notes

### Avalonia packages

```
Avalonia                           12.1.2
Avalonia.Desktop                   12.1.2
Avalonia.Themes.Fluent             12.1.2
Avalonia.Fonts.Inter               12.1.2
Avalonia.Diagnostics               12.1.2  (dev only)
Avalonia.Controls.DataGrid         12.1.2
Avalonia.Svg.Skia                  (if using SVG icons)
```

### TFM

- Target: `net10.0` (Avalonia 12.1.2 supports net10; verify at build time)
- If Avalonia 12 TFM is net8.0-only, fall back to `net8.0` and run on .NET 10 runtime

### Native AOT prerequisites

- Replace `Newtonsoft.Json` with `System.Text.Json` + source generators throughout
  - Impact: `PlayerDataRepository`, `UserPreferencesFile`, `WorldPreferencesFile`, `CrashReport`, `ModSourceClient` models
  - Annotation: `[JsonPropertyName("x")]` on all properties, register `JsonSerializerContext` for each model type
- Enable `<PublishAot>true</PublishAot>`, `<InvariantGlobalization>true</InvariantGlobalization>`
- Avalonia AOT: supported since 11.x; trimming hints in Avalonia packages handle most cases
- Windows AOT: straightforward; Linux/macOS: needs native Skia libs (included in Avalonia.Desktop)

### SSH remote management

- Library: `SSH.Net` (MIT, .NET 8/10 compatible)
- Key concepts:
  - `SshClient` for command execution, `SftpClient` for file transfer
  - Key-based auth preferred (password auth supported as fallback)
  - Remote server path must be absolute (e.g. `/home/user/ValheimServer`)
  - Server binary: `valheim_server.x86_64` (Linux) or `.exe` (Windows)
  - Log streaming: SSH channel stdout → parse with same `LogBasedActions` regexes
  - Mod install: upload zip via SFTP, extract on remote via `tar -xf` or unzip
  - Backup download: SFTP get from remote save folder

### Split `ValheimServerGUI` into shared + UI projects

Current monolith `ValheimServerGUI` (net10.0-windows) bundles UI + Game logic. To share Game/Tools across WinForms and Avalonia:

- **New project**: `ValheimServerGUI.Game` (net10.0)
  - Move: `Game/`, `Game/Mods/`, `Properties/Resources.resx`, `Properties/Resources.Designer.cs`, `Properties/PublishProfiles/`
  - These contain no WinForms dependencies (verified)
- **Existing**: `ValheimServerGUI` (net10.0-windows) — keeps Forms, Controls, Program.cs, WinFormsExtensions
- **New**: `ValheimServerGUI.Avalonia` (net10.0) — Avalonia app, shares `ValheimServerGUI.Game`

This split can be done in Phase 1 before any Avalonia UI code is written.
