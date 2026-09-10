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
`ValheimServerGUI/Game/ValheimServer.cs:222-250`) can be lost entirely.

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
`ValheimServer.cs:222-250` and `ValheimServerGUI/Tools/Logging/ValheimServerLogger.cs`.

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

## 8. [open] [low] Stale docs

**Problem.** `AGENTS.md` (roadmap) still lists "BepInEx install/version" as unimplemented,
but BepInEx + Valheim Plus support shipped in commits `d447161` / `caf9fc3`. `README.md`
roadmap has the same gap.

**Fix.** Update `AGENTS.md` and `README.md` to reflect shipped BepInEx support; remaining
roadmap is mod list / mod config (on/off) / mod presets.