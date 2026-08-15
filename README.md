# Friendly Grid Access

Friendly Grid Access is a **Torch server plugin for Space Engineers Dedicated Server**. It is intended to let a grid remain owned by Faction A while explicitly approved friendly factions can pass the server-side block access check when the current faction relationship meets the configured reputation threshold.

> **Prototype status:** this plugin patches an internal Space Engineers access method. Test on a staging copy of your world after Space Engineers or Torch updates. Some specialized block interactions may perform additional checks and can require additional patches.

## Default rule

- Grid ownership stays unchanged.
- Friendly access must be explicitly granted per grid.
- Default minimum faction reputation: **+1500**.
- Reputation is checked again at access time.
- Vanilla access is never revoked by this plugin; it only upgrades a vanilla denial when an FGA grant is valid.

## Player commands

Stand near a grid owned by your faction and use:

```text
!fga grant TAG
!fga revoke TAG
!fga list
!fga status
```

`TAG` is the target faction tag. Founder/leader and proximity checks are performed by the plugin.

## Recommended: build entirely on GitHub

You **do not need access to your hosted game server's Windows desktop** to compile this repository.

The included GitHub Actions workflow runs on a Windows runner and:

1. downloads SteamCMD;
2. installs the Space Engineers Dedicated Server (`app 298740`) to obtain matching game assemblies;
3. downloads the current successful Torch server build from TorchAPI;
4. restores NuGet dependencies;
5. compiles `FriendlyGridAccess.dll`;
6. creates a Torch-ready ZIP containing `manifest.xml` and the plugin DLL;
7. uploads the files as a GitHub Actions artifact;
8. when triggered by a version tag, attaches them to a GitHub Release.

### First test build

After uploading this repository to GitHub:

1. Open your repository.
2. Open **Actions**.
3. Select **Build FriendlyGridAccess**.
4. Click **Run workflow**.
5. Wait for the job to finish.
6. Download the `FriendlyGridAccess-...` artifact from the workflow run.

This lets you see whether the source still compiles against the current Torch and Space Engineers versions before publishing a release.

### Create a release

Create and push a Git tag such as:

```text
v0.4.0
```

You can create the tag using GitHub's Releases UI as well. The workflow detects the tag, builds the plugin, and publishes files similar to:

```text
FriendlyGridAccess-0.4.0.zip
FriendlyGridAccess.dll
manifest.xml
```

The **ZIP** is the preferred Torch plugin package.

## Third-party hosted servers

Do not put the source repository URL ending in `.git` into a field that expects a runnable plugin. A Git repository contains source code, not the compiled assembly.

For a host that supports manual plugin uploads, use the generated:

```text
FriendlyGridAccess-0.4.0.zip
```

The ZIP contains `manifest.xml` next to `FriendlyGridAccess.dll`, which is the format expected by Torch plugin distribution.

If your host only accepts a **plugin URL**, use a direct URL to the generated release ZIP if the host documents support for direct ZIP URLs. If the host only accepts TorchAPI catalog URLs, the plugin must be published through the TorchAPI plugin catalog instead.

## Local developer build

A local Windows build is still supported for developers who have Torch installed:

```powershell
.\build.ps1 -TorchDir "C:\Torch"
```

This creates `dist\FriendlyGridAccess.zip`.

## Plugin identity

Permanent plugin GUID:

```text
26f55d62-7b65-4e78-a347-dabf640d66d1
```

Keep this GUID unchanged between releases so Torch recognizes updates as the same plugin.

## Safety / compatibility

Back up the world before installing or updating server plugins. Friendly Grid Access uses Harmony to patch a Space Engineers internal access method. A game update can change the method signature; the plugin intentionally fails clearly if the expected method can no longer be found rather than silently patching an unrelated method.

## License

MIT. See `LICENSE`.
