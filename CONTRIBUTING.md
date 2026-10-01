# Contributing

Here's the recommended workflow for contributing code back to ValheimServerGUI:

1. Pick an open [issue](https://github.com/zhoel-sherk/ValheimServerGUI/issues) that you'd like to make changes for. If there isn't an open issue for your change, [create a new one](https://github.com/zhoel-sherk/ValheimServerGUI/issues/new).
1. Read the **Developer's Guide** below to learn how to run the application locally.
2. Fork this repository & make your code changes.
3. Create a [pull request](https://github.com/zhoel-sherk/ValheimServerGUI/pulls) from your fork. Link your issue to the pull request - you can do this by simply putting "Resolves #{your_issue_number}" in the PR description.

# Developer's Guide

The client targets **.NET 10** and **Avalonia 12.1.2**, and builds from the command line as well as
Visual Studio. `AGENTS.md` is the working reference for day-to-day changes; the highlights worth
knowing before you start are:

* A **running client locks `bin\Debug`**. Stop `ValheimServerGUI.Avalonia` (and any
  `valheim_server` it auto-started) before rebuilding.
* `dotnet test ValheimServerGUI.sln --nologo` runs everything; it must be green (216 tests).
* Release builds need `/p:SignAssembly=false` — the upstream `.snk` is not committed.

## Solution Projects

* **ValheimServerGUI.Avalonia** - The Avalonia desktop client (the only UI)
* **ValheimServerGUI.Core** - Platform-neutral domain: options & validation, player models, log parsing, world-gen data, contracts
* **ValheimServerGUI.Infrastructure** - Shared services: preferences, player data, mods/backups, Steam Cloud, Discord, UPnP, updates, logging, DI composition root
* **ValheimServerGUI.Tools** - Common utilities used by the clients. These contain no Valheim-specific code.
* **ValheimServerGUI.Core.Tests** / **ValheimServerGUI.Infrastructure.Tests** - Cross-platform xUnit tests

The original WinForms client and the legacy Serverless REST API have been retired; see tag
`legacy-winforms` and commit `1fe06b` for their source. There is no AWS deployment any more, and
the update check queries **this fork's** GitHub releases.

## Solution Resources (Secrets)

The **SolutionResources** folder contains code, configuration, and/or assets that are used in multiple projects in the Solution. Some files are considered "secret" and are not committed to source control. However, the solution is set up so that you **should not need any of these secret files** in order to do local development - only to publish the app.

In some cases, however, you may want to supply your own mock secret values for testing. Read more about specific SolutionResources files [here](/SolutionResources/README.md).

## ValheimServerGUI.Avalonia - Desktop Application

Running the desktop client locally is fairly straightforward. From the command line:

```pwsh
dotnet run --project ValheimServerGUI.Avalonia
```

Or in Visual Studio: select the **ValheimServerGUI.Avalonia** project and press **F5**.

### Publishing the app

The client publishes through the **small-x64-release** publish profile, producing a single-file,
framework-dependent `win-x64` .exe with the native Skia/HarfBuzz libraries bundled:

```pwsh
dotnet clean ValheimServerGUI.sln -c Release
dotnet publish ValheimServerGUI.Avalonia\ValheimServerGUI.Avalonia.csproj -c Release `
  /p:PublishProfile=small-x64-release /p:SignAssembly=false `
  /p:DebugType=none /p:DebugSymbols=false
```

The .exe lands in **/publish/avalonia-small-x64/**. Delete the `*.pdb` files there — the native
packages drag in ~100 MB of debug symbols that are not part of the client.

Publishing fails with an opaque `File.Delete` / `GenerateBundle` MSB4018 error if the client is
already running, because a launched .exe holds the output file.

### Publishing a signed version of the app

_For project maintainers only._

The fork does not ship a strong-name key or a code-signing certificate, so releases are built
with `/p:SignAssembly=false` and are not Authenticode-signed. If signing is reintroduced you will
need your own `.snk` and certificate; nothing about the build requires them.

### Creating a new release

_For project maintainers only._

1. Bump `<Version>` in `ValheimServerGUI.Avalonia/ValheimServerGUI.Avalonia.csproj`.
2. Publish as above, then scan the .exe for secrets and machine data before committing it.
3. Zip the .exe plus `LICENSE.txt` into **/release-artifacts/** (not `release/` or `artifacts/` —
   `.gitignore` already claims those for build output) and commit.
4. Tag `vX.Y.Z`, push, then create the release:

   ```pwsh
   gh release create vX.Y.Z release-artifacts\ValheimServerGUI-X.Y.Z.zip `
     --repo zhoel-sherk/ValheimServerGUI --title "vX.Y.Z" `
     --notes-file <notes.md> [--prerelease]
   ```

   Pass `--repo` explicitly — `gh` otherwise resolves the upstream repository and fails.

5. The release must contain an asset in order to trigger an update notification, and the semver on
   GitHub must be greater than the client's current version. **Pre-releases do not trigger an
   update notification**, which is what you want for alpha/RC builds.

The desktop client checks GitHub at most once every 24 hours, against `AppSettings.UrlUpdates`,
which already points at this fork's releases.