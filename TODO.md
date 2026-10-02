# TODO

Known issues and improvements for running the Valheim dedicated server with BepInEx
(mods). Priorities: high / medium / low.

Status legend: `[done]` implemented, `[open]` not yet implemented.

---

## 1. [done] [high] BepInEx console hijacks stdout

**Problem.** The `BepInExPack_Valheim` pack defaults `[Logging.Console] Enabled = true`
in `BepInEx/config/BepInEx.cfg`. With it enabled, BepInEx calls `AllocConsole()` and
`SetStdHandle()`, which diverts Unity/game output away from the GUI's stdout pipe into
an extra console window and into `BepInEx/LogOutput.log`. `CreateNoWindow = true`
(`ValheimServerGUI.Tools/Processes/LocalServerProcess.cs`) does **not** prevent
`AllocConsole()`.

**Consequence.** The GUI may never see the `Game server connected` line, so the status
stays `Starting` forever; player join/leave events (parsed from stdout in
`ValheimServerGUI.Core/Game/ValheimServer.cs:222-250`) can be lost entirely.

**Fix.** After install/update, write `[Logging.Console] Enabled = false` into
`BepInEx/config/BepInEx.cfg` as a merge (do not clobber other settings) inside
`BepInExManager.InstallFromZip` (`ValheimServerGUI/Game/Mods/BepInExManager.cs:109-119`).

**Status.** Implemented. `BepInExConfig.DisableConsoleLogging`
(`ValheimServerGUI/Game/Mods/BepInExConfig.cs`) merges the `Enabled = false` value into
the existing file (preserving comments/other settings and the original line endings) or
appends the section when missing. Called from `BepInExManager.ApplyServerLoggingDefaults`
after every install/update. Covered by `BepInExConfigTests` and the updated
`BepInExInstallPreservesExistingConfigAndDisablesConsole` test.

---

## 2. [open] [medium] Fallback log source missing

**Problem.** Even after disabling the BepInEx console, BepInEx may still split output
between stdout and `BepInEx/LogOutput.log` (flushed every ~2 s, UTF-8 no BOM). There is
currently no secondary source of server events.

**Fix (optional).** Tail/read `BepInEx/LogOutput.log` as a supplementary event source for
status transitions and player events when stdout is incomplete. Relates to
`ValheimServerGUI.Core/Game/ValheimServer.cs:222-250` and `ValheimServerGUI/Tools/Logging/ValheimServerLogger.cs`.

**Avalonia note.** Keep the parser shared; only the file-watch source is platform-facing.

---

## 3. [done] [medium] WorkingDirectory not set

**Problem.** The old `ProcessExtensions.AddBackgroundProcess`
(`ValheimServerGUI.Tools/Processes/ProcessExtensions.cs:11-35`) never set
`StartInfo.WorkingDirectory`, so the server inherited the GUI process's CWD. Doorstop's
`fix_cwd()` makes injection work regardless, but any mod reading
`Environment.CurrentDirectory` gets the wrong path.

**Fix.** Set `WorkingDirectory = Path.GetDirectoryName(exePath)`; requires passing the
exe path through `AddBackgroundProcess` (called from `ValheimServer.cs:155`).

**Status.** Implemented. `LocalServerProcess` sets the working directory from the
`ServerProcessSpec` (derived from the executable path when not supplied). The old
`ProcessExtensions` no longer exists; the logic lives in
`ValheimServerGUI.Tools/Processes/LocalServerProcess.cs`. This is a local-process concern
and stays inside the local process implementation for the Avalonia/SSH split.

---

## 4. [done] [medium] GetStatus ignores doorstop_config.ini

**Problem.** `BepInExManager.GetStatus` (`ValheimServerGUI/Game/Mods/BepInExManager.cs:43-45`)
only checks `winhttp.dll` and `BepInEx/core/BepInEx.dll`. If `doorstop_config.ini` is
deleted or corrupt, the GUI reports "installed" but injection silently fails.

**Fix.** Include `doorstop_config.ini` existence in the installed-status check.

**Status.** Implemented. `GetStatus` now requires `winhttp.dll`,
`doorstop_config.ini` and `BepInEx/core/BepInEx.dll`. Covered by the
`GetStatusRequiresDoorstopConfig` test.

---

## 5. [done] [medium] Crossplay + BepInEx conflict

**Problem.** Community consensus says BepInEx mods and `-crossplay` do not mix, but the
GUI allows enabling both without any warning.

**Fix.** Warn or block when BepInEx is installed and the active profile has crossplay
enabled (see the `-crossplay` handling in `ValheimServer.cs:376`).

**Status.** Implemented as a warning (non-blocking). `ValheimServer.Start` logs a warning
via `WarnIfModsAndCrossplay` when `-crossplay` is requested and `winhttp.dll` exists next
to the server exe. Kept in the domain layer so the same check survives the Avalonia/remote
migration.

---

## 6. [done] [low] InstallFromFileAsync accepts any archive

**Problem.** `BepInExManager.InstallFromFileAsync` (`BepInExManager.cs:77-84`) performs no
version check. A pack older than 5.4.2333 (pre-Unity 6) can be installed and crash the
server on startup for Valheim 1.0.x.

**Fix.** Validate/reject or warn on BepInExPack versions below 5.4.2333.

**Status.** Implemented as a warning (non-blocking, best-effort version detection).
`BepInExManager.WarnIfPackPredatesUnity6` reads the installed `BepInEx.dll` file version
and logs a warning when it predates `5.4.23.3`. Detection is skipped for non-assembly
files.

---

## 7. [open] [low] Hard kill truncates BepInEx log / risks world save

**Problem.** `IServerProcess.Stop()` (`ValheimServerGUI.Tools/Processes/LocalServerProcess.cs`)
uses `taskkill /pid <id>` with no `/T` and no `/F`. BepInEx spawns no child
processes, so there are no orphans, but `LogOutput.log` flushes only every ~2 s and a
hard kill loses the tail and can skip a clean world save. Not BepInEx-specific, but
relevant when running modded.

**Fix.** Consider a graceful stop (CTRL+C / close) before falling back to a forced kill.

---

## 8. [done] [low] Stale docs

**Problem.** `AGENTS.md` (roadmap) still lists "BepInEx install/version" as unimplemented,
but BepInEx + Valheim Plus support shipped in commits `d447161` / `caf9fc3`. `README.md`
roadmap has the same gap.

**Fix.** Update `AGENTS.md` and `README.md` to reflect shipped BepInEx support; remaining
roadmap is mod list / mod config (on/off) / mod presets.

**Status.** Implemented. `AGENTS.md` and `README.md` now list BepInEx/Valheim Plus,
Steam Cloud world import, Discord webhook notifications, UPnP port forwarding and mod
folder/config actions as shipped.

**Updated 2.4.5.** A read-only mod list shipped (item 11 covers the rest). The README roadmap
now reads: mod enable/disable (item 11), Valheim Plus feature toggles (item 12), mod presets,
and installing arbitrary mods (item 15).

---

## 9. [open] [medium] NAT-PMP mapping read spams the log

**Problem.** `UpnpPortForwarder` tries to enumerate existing mappings, but the NAT-PMP protocol
has no "list all" call — Mono.Nat raises `UnsupportedOperation: The NAT-PMP protocol does not
support listing all mappings`. Every discovery attempt logs
`[WRN] UPnP: could not read the existing mappings: ...`, which in practice repeats every few
seconds while the Ports dialog / gateway check runs. The Logs tab now colours warnings, which
makes the repetition more visible than it was.

**Fix.** Treat "cannot enumerate" as a normal outcome rather than a fault: log once per session
at Debug, and fall back to reporting the port state from the mapping we ourselves created.

---

## 10. [open] [low] README screenshots predate the 2.4.4 UI

**Problem.** `img/Screenshot-Players.png` and `img/Screenshot-Logs.png` still show the UI from
before the Players/Logs rework — no column headers, no platform badges, no log highlighting and
the gaps between log lines that 2.4.4 removed.

**Fix.** Re-capture both screenshots against a 2.4.4+ build (they are the first thing a new
visitor sees). Not done automatically: capturing needs a live window and a running server.

**Status.** Done in 2.4.4 — all four screenshots recaptured against a live 1.0.12 server.

---

## 11. [open] [high] No way to enable or disable a mod

**Problem.** BepInEx has no per-plugin on/off switch. The only way to stop a mod loading is to
move its `.dll` out of `BepInEx/plugins`, and the app cannot do that. 2.4.5 lists the plugins
read-only, which is half the story: an admin can see that seven mods are installed but not
disable one without opening Explorer while the server is stopped.

**Decided mechanism: move the DLL.** Toggle = move `BepInEx/plugins/<Mod>.dll` to
`BepInEx/vsg-disabled/<Mod>.dll` and back. Chosen over writing an `enabled` key into the mod's
config because most mods have no such key — on a real install only 2 of 7 do, so a config-based
toggle would silently do nothing for the rest.

**Notes for whoever implements it.** The server must be stopped (BepInEx loads plugins once at
startup, so a moved file needs a restart). Name collisions between the two folders need a
decision. The UI copy must make clear that this moves files, and `Remove Player`-style
destructive actions should confirm.

---

## 12. [open] [medium] Valheim Plus feature toggles are unreachable

**Problem.** `BepInEx/config/org.bepinex.plugins.valheim_plus.cfg` is 207 KB and holds 60
`[Section]` blocks each with an `enabled` key (14 on, 46 off on a real install). The app can open
the file in the OS editor and nothing else, so every Valheim Plus feature is a hand edit of
XML-ish text. The file is also pathological: Valheim Plus embeds the whole Unity `KeyCode` enum
in every keycode comment, which is why it is that large.

**Fix.** Parse the per-section `enabled` keys into a list of checkboxes, reusing the lossless
`BepInExConfig` edit style so only the touched lines change. Follow the `[Section]` conventions
`DisableConsoleLogging` already implements, and keep the write atomic.

---

## 13. [open] [medium] Backups are discovered but never created

**Problem.** `BackupService` only lists `<world>_backup_*` entries and rates them Healthy /
Warning. There is no create, restore, delete or prune, and the retention count shown
(`backups 4` / `backupshort` / `backuplong`) is a server argument the app has no control over.

**Fix.** Add copy-to-backup, restore-from-backup, delete, and prune-to-count. Restore is
destructive and needs a confirmation plus a fresh backup first.

---

## 14. [open] [high] No player access control (largest upstream gap)

**Problem.** Valheim reads `adminlist.txt`, `banlist.txt` and `permittedlist.txt` from the save
folder. Nothing in this app writes them, and a real install had none at all — so there is no way
to make someone an admin, ban a player, or run a permitted-only server.

Upstream (`runeberry/ValheimServerGUI` `v3.0`) has this as a profile-scoped role model:
`PlayerAccessListService` plus generation of the three files at server start. The commits are
`2bd4913` (domain layer; its PlayStation/Nintendo half is already here), `4987ed0` (generate at
start), `0b9edae`, `790a6a7`, `b49cd0d`, `a0919ee`.

**Porting note.** Do not cherry-pick — the WinForms role editor does not apply, only the Core
domain and the file generation. Two details from upstream worth preserving: Steam ids may be
written bare or `Steam_`-prefixed and must be de-duplicated, while non-Steam ids are matched
**case-sensitively** by `ZNet.ListContainsId`, so the raw log token has to be preserved; and
header lines plus unauthored lines must survive a rewrite.

---

## 15. [open] [medium] Only BepInEx and Valheim Plus can be installed or updated

**Problem.** `ValheimPlusManager` hardcodes its two plugin file names, and the Thunderstore
endpoint in `AppSettings.UrlBepInExPackApi` is the only one wired up. On a real install Jotunn,
Drop That!, Impactful Skills, Network Performance System, PlantEasily and AchievementEnabler are
installed and completely unmanageable — Jotunn in particular is a dependency host many mods need.

**Fix.** Generalise `ModSourceClient` to arbitrary Thunderstore packages: list, resolve the right
distribution for a dedicated server, install, update, and surface declared dependencies. Reading
plugin metadata needs assembly inspection beyond `FileVersionInfo` (GUID, author, dependencies);
Jotunn additionally ships a `Jotunn.xml` with its docs.

---

## 16. [open] [low] Plugin load failures are invisible

**Problem.** The app never reads `BepInEx/LogOutput.log`; it can only open it in an editor. A
plugin that throws while loading produces nothing in the GUI. The log does contain such lines
(alongside Unity shader noise, which is why a naive read is noisy).

**Fix.** Surface `[Error]` / `[Exception]` lines from the mod-loading section as warnings on the
Mods tab, and consider a "Mods" log view. Needs filtering so Unity's own errors do not drown it
out.