# Solution Resources

This folder is not a build input. Nothing in the solution compiles anything from here — it exists
only to hold files that are git-ignored, so that Release builds can reference them by path without
them ever entering git.

You **do not need anything in this folder for local development**. The only thing the solution
expects is the strong-name key, and only for Release builds.

### ValheimServerGUI.snk

Referenced by the `Release` `PropertyGroup` of four csproj files as
`AssemblyOriginatorKeyFile`. The file is git-ignored and is deliberately **not** committed — a
strong-name key in a public repository is not a secret anyone wants leaked.

Consequences:

* `Debug` builds work with no key at all.
* Release builds need `/p:SignAssembly=false`, which is what the release steps in `AGENTS.md`
  and `CONTRIBUTING.md` pass. That produces an unsigned build, which is what this fork ships.

If you want strong-name signing for your own fork, drop your own `.snk` here.

### Removed

`ClientSecrets.Values.cs` / `ServerSecrets.Values.cs` and `appsettings.local.json` were documented
here for the retired Serverless REST backend and its compile-time secrets. Both were deleted in
`1fe06b` (the backend itself went with them), and the leftover `*.Values.cs` files were halves of
partial classes whose other half no longer existed — they would not have compiled. They have been
removed.

The app has no compile-time secrets: `Infrastructure/AppSettings.cs` holds plain paths, URLs and
defaults, and the only API it talks to is the public GitHub releases endpoint for this fork.