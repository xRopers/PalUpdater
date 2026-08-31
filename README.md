# PalUpdater

A small Windows tray app that watches the [RE-UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) GitHub
repo for new releases and installs them into your Palworld folder, without wiping your installed
mods or `UE4SS-settings.ini`.

No third-party NuGet packages are used — everything is built-in .NET (HttpClient, System.Text.Json,
System.IO.Compression, WinForms), so there's nothing to restore, no dependency chain to trust.

## Memory footprint

A .NET WinForms app's baseline is roughly 25-40MB just from the CLR + WinForms assemblies being
loaded - there's a floor here you can't get below without dropping .NET entirely for something
like native Win32/C++. That said, this project has a few settings in `PalUpdater.csproj` aimed at
staying near the low end of that range:

- `InvariantGlobalization` - skips loading full ICU globalization data (worth several MB; safe
  here since the app doesn't do culture-specific text formatting)
- `ServerGarbageCollection=false` / `ConcurrentGarbageCollection=false` - workstation GC without a
  background GC thread, tuned for a small idle app rather than server throughput
- A single shared `HttpClient` reused for the app's lifetime instead of a new one (and its own
  connection pool) allocated on every periodic check

If you rebuild after pulling these changes and Task Manager still shows a similar number, that's
expected - most of it is the runtime floor, not something specific to this app's code.

## Build

You'll need the .NET 8 SDK (https://dotnet.microsoft.com/download) — I couldn't compile this myself in
the sandbox I built it in (no dotnet SDK / no network access to installers there), so build it on your
own machine:

```
cd PalUpdater
dotnet build
```

To get a standalone `.exe` you can drop anywhere (no .NET runtime install required on the target machine):

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The exe will land in `bin\Release\net8.0-windows\win-x64\publish\PalUpdater.exe`.

## First run

1. Launch `PalUpdater.exe`. It sits in your system tray (bottom-right, may be in the hidden icons
   overflow arrow the first time).
2. Since no game path is set yet, Settings opens automatically. Either click **Auto-detect via Steam**
   or **Browse** to your Palworld install folder — the folder that directly contains a `Pal` subfolder
   (e.g. `...\steamapps\common\Palworld`).
3. Set your check interval (default 6 hours) and whether you want updates installed automatically or
   just notified via a tray balloon.
4. Click **Save**, then **Check Now** to do an initial install.

## Notes specific to your setup

- This targets your **local** Palworld client install. For the GameHostBros dedicated server, UE4SS
  needs to go into the server's own `Pal\Binaries\Win64` — that's a separate file tree you'd update via
  your host's file manager or FTP, not this app, unless you point `GameRootPath` at a locally synced
  copy of the server files.
- On update, PalUpdater backs up your `Mods\` folder (including `mods.txt`) and `UE4SS-settings.ini`
  before extracting the new build, then copies them back on top afterward — so mods like your
  `WeaponStatsCustomizer` survive a UE4SS core update. If extraction fails partway through, it still
  restores your mods/config from the backup rather than leaving the folder half-updated.
- GitHub's unauthenticated API rate limit is 60 requests/hour per IP, which is more than enough for the
  default 6-hour check interval. If you drop the interval very low, you can generate a
  [personal access token](https://github.com/settings/tokens) (no scopes needed, just used for the
  higher rate limit) and paste it into the Settings token field — it's stored only in your local
  `%AppData%\PalUpdater\config.json`.
- The asset picker skips anything prefixed `zDEV` (UE4SS's experimental/dev builds) and anything with
  `pdb` in the name, then picks the largest remaining `.zip` — that's reliably the full release package
  rather than a partial one. Check **Include prerelease / dev builds** in Settings if you actually want
  the dev builds.

## Run on startup

To have it launch automatically when you log in:

1. Press `Win+R`, type `shell:startup`, hit Enter.
2. Drop a shortcut to `PalUpdater.exe` in that folder.

## Files

- `Program.cs` — entry point
- `TrayAppContext.cs` — tray icon, menu, background timer, update orchestration
- `SettingsForm.cs` — settings UI + log viewer
- `UpdateChecker.cs` — GitHub releases API calls, asset selection
- `Installer.cs` — backup / extract / restore logic
- `SteamLocator.cs` — best-effort Steam library scan to find Palworld
- `Config.cs` — settings persistence (`%AppData%\PalUpdater\config.json`)
- `Models/GitHubModels.cs` — JSON models for the GitHub API response
