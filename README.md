# PalUpdater — Automatic UE4SS Updater for Palworld

![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6)
![Open Source](https://img.shields.io/badge/open%20source-yes-brightgreen)

**PalUpdater is a free, open-source Windows tray app that automatically checks for and installs
the latest [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) builds for [Palworld](https://www.palworldgame.com/),
so you never have to manually re-download and re-extract UE4SS every time it updates.**

It runs quietly in your system tray, checks GitHub on a schedule you control, and installs updates
automatically (or just notifies you first — your choice) — all while preserving your installed
UE4SS Lua mods and `UE4SS-settings.ini` across every update.

<!-- Add a screenshot of the Settings window here once you have one -->

## Table of contents

- [What is PalUpdater?](#what-is-palupdater)
- [Features](#features)
- [Why open source](#why-open-source)
- [Install](#install)
- [First run](#first-run)
- [How it works](#how-it-works)
- [FAQ](#faq)
- [PalUpdater vs. other ways to update UE4SS](#palupdater-vs-other-ways-to-update-ue4ss)
- [Building from source](#building-from-source)
- [Project structure](#project-structure)
- [Contributing](#contributing)
- [Credits](#credits)
- [License](#license)

## What is PalUpdater?

UE4SS ([RE-UE4SS](https://github.com/UE4SS-RE/RE-UE4SS)) is the scripting framework that most
Lua-based Palworld mods depend on. It updates frequently, and keeping it current normally means:
checking GitHub, downloading a zip, closing the game, extracting the zip over your install, and
hoping you didn't just overwrite your `Mods` folder. **PalUpdater automates that entire process**
for the standard local UE4SS install — the one that lives in `Pal\Binaries\Win64`, not the Steam
Workshop version.

## Features

- 🔄 **Automatic update checks** — polls the official UE4SS GitHub releases on a configurable
  interval (default every 6 hours)
- ⚡ **Auto-install or notify-only** — choose whether updates install themselves or you get asked
  first
- 🧩 **Mods and settings survive updates** — `Mods\` (including `mods.txt`) and
  `UE4SS-settings.ini` are backed up before an update and restored afterward, so a UE4SS core
  update never wipes your mod setup
- 🎮 **Running-game detection** — if Palworld is open when an update lands, PalUpdater warns you
  to restart it (an already-running game won't pick up new UE4SS files until relaunched), and
  defers auto-installs until the game is closed to avoid writing over locked files
- 🔍 **Steam auto-detect** — finds your Palworld install automatically by reading Steam's library
  folders, or you can browse manually
- 📝 **Built-in log viewer** — see exactly what was checked, downloaded, and installed, with a
  one-click clear button
- 🪶 **Lightweight** — ~19MB resident memory as a self-contained single-file exe; no installer, no
  external dependencies, nothing to configure beyond picking your game folder
- 🧬 **Zero third-party packages** — built entirely on stock .NET (HttpClient, System.Text.Json,
  System.IO.Compression, WinForms)

## Why open source

PalUpdater downloads files from the internet and writes an executable DLL into your game folder —
behavior that's worth being able to verify yourself rather than take on faith, especially since
that exact pattern (network download → extract → drop an executable) is also what a lot of actual
malware does. Every line of this project is public and auditable. There's also no dependency
chain to trust beyond .NET itself, since no third-party NuGet packages are used anywhere in the
project.

If your antivirus or a hosting site flags the compiled `.exe`, that's a known false-positive
pattern for small, unsigned, first-seen .NET utilities that do this kind of file operation — not a
sign that anything is actually wrong. Build it yourself from source here if you'd rather not trust
a prebuilt binary at all.

## Install

1. Grab the latest `PalUpdater.exe` from the [Releases](../../releases) page (or build it yourself
   — see [Building from source](#building-from-source)).
2. Put it anywhere on your PC. No installer, no admin rights needed.
3. Run it.

## First run

1. Launch `PalUpdater.exe`. It shows the Settings window on launch and also sits in your system
   tray (you may need to click the hidden-icons arrow the first time to see it).
2. Set your Palworld install folder — click **Auto-detect via Steam**, or **Browse** to it
   manually. This should be the folder that directly contains a `Pal` subfolder (e.g.
   `...\steamapps\common\Palworld`), not the `Win64` folder itself.
3. Set your check interval and whether you want updates installed automatically or just flagged
   for you to approve.
4. Click **Save**, then **Check Now** to run an initial check/install.

To have it launch automatically at login: press `Win+R`, type `shell:startup`, hit Enter, and drop
a shortcut to `PalUpdater.exe` in that folder.

## How it works

1. On its check interval, PalUpdater queries the GitHub Releases API for
   `UE4SS-RE/RE-UE4SS` — either the official `latest` release, or the newest release overall if
   you've opted into prerelease/dev builds in Settings.
2. It picks the correct `.zip` asset from that release automatically (skipping `zDEV`-prefixed
   dev/debug builds and `.pdb` symbol files by default), rather than relying on any hardcoded
   filename — release filenames change with every build (they include a commit hash), so matching
   by pattern against the live asset list is what makes this resilient to upstream changes.
3. Before installing, it checks whether the UE4SS loader DLL is actually present on disk — not
   just what's recorded in its own config — so a manually deleted or corrupted install gets
   correctly detected and reinstalled rather than skipped as "already up to date."
4. It backs up your `Mods\` folder and `UE4SS-settings.ini`, extracts the new release over your
   install folder, then restores your mods/config back on top. If extraction fails partway
   through, your mods/config are still restored rather than left in a half-updated state.
5. If Palworld is running at any point in this process, PalUpdater either warns you a restart is
   needed (manual/notify flow) or defers the install until the game is closed (auto-install flow).

## FAQ

**Is this safe? Why does my antivirus flag it?**
See [Why open source](#why-open-source) above — this is a known false-positive pattern for small,
unsigned .NET utilities that download and extract files, not an indication of anything malicious.
The full source is here for anyone to check.

**Does this work with the Steam Workshop version of UE4SS?**
No — Steam Workshop subscriptions already auto-update on their own through Steam. PalUpdater is
for the standard manual UE4SS install path (`Pal\Binaries\Win64`), for people who don't use or
don't want to use the Workshop version.

**Will this delete my installed mods when it updates UE4SS?**
No. Your `Mods` folder and `UE4SS-settings.ini` are backed up before the update and restored
afterward automatically — see [How it works](#how-it-works).

**Does this work for dedicated servers?**
Not directly. It manages your local client's UE4SS install. A dedicated server's files are a
separate tree you'd typically update through your host's control panel or file manager (some
hosts, like GameHostBros, have their own built-in "auto update UE4SS" option for exactly this).
You could point PalUpdater at a locally synced copy of your server files if your workflow involves
one, but that's outside its default scope.

**Do I need to keep Palworld closed while it checks for updates?**
No, background checks are harmless either way. It only matters at the moment of actually
installing — see the running-game detection in [Features](#features).

**Does this modify any of my actual mod files, or just UE4SS itself?**
Just UE4SS core files. It never touches individual `.pak` or Lua mod files beyond backing up and
restoring the `Mods` folder as a whole during a UE4SS update.

## PalUpdater vs. other ways to update UE4SS

| | Auto-updates? | Covers manual/local install? | Standalone (no other mod manager needed)? |
|---|---|---|---|
| **PalUpdater** | ✅ | ✅ | ✅ |
| Steam Workshop UE4SS | ✅ (via Steam) | ❌ (different mods path) | ✅ |
| Nexus Mods + Vortex | ⚠️ notifies, manual re-download | ✅ | ❌ (requires Vortex) |
| Manual GitHub download | ❌ | ✅ | ✅ |

If you're already happy subscribing to the Steam Workshop UE4SS build, you don't need this — it
already auto-updates for you. PalUpdater exists for people on the standard manual install path who
want that same "set it and forget it" convenience without switching to Workshop or installing a
full general-purpose mod manager just for one framework.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
cd PalUpdater
dotnet build
```

To produce a standalone `.exe` (no .NET runtime install required on the machine that runs it):

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output lands in `bin\Release\net10.0-windows\win-x64\publish\PalUpdater.exe`.

### A note on optional custom icon

If `app.ico` isn't present in the project folder, either drop one in (see the `.csproj`'s
`ApplicationIcon` setting) or clear that setting before building — a missing referenced icon file
will fail the build.

### Memory footprint notes

A .NET WinForms app has a baseline of roughly 20-40MB just from the CLR and WinForms assemblies —
there's a floor here that can't be gotten under without dropping .NET for native Win32/C++
entirely. `PalUpdater.csproj` includes a few settings to stay near the low end of that range:
`InvariantGlobalization` (skips loading ICU globalization data), workstation GC without a
background thread (`ServerGarbageCollection=false` / `ConcurrentGarbageCollection=false`), and a
single `HttpClient` shared for the app's lifetime instead of one allocated per check.

## Project structure

- `Program.cs` — entry point
- `TrayAppContext.cs` — tray icon, menu, background timer, update orchestration, running-game
  detection
- `SettingsForm.cs` — settings UI and log viewer
- `UpdateChecker.cs` — GitHub Releases API calls, asset selection logic
- `Installer.cs` — backup / extract / restore logic
- `SteamLocator.cs` — best-effort Steam library scan to auto-find Palworld
- `Config.cs` — settings persistence (`%AppData%\PalUpdater\config.json`)
- `Models/GitHubModels.cs` — JSON models for the GitHub API response

## Contributing

Issues and pull requests are welcome. If you're reporting a bug, include the relevant lines from
the in-app log (Settings window → Log panel, or `%AppData%\PalUpdater\log.txt`).

## Credits

- [UE4SS-RE](https://github.com/UE4SS-RE/RE-UE4SS) for building and maintaining UE4SS itself —
  this project only automates keeping it up to date, all credit for the framework goes to them.

## License

[MIT](LICENSE) — free to use, modify, and redistribute.
