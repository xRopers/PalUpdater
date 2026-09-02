using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using PalUpdater.Models;

namespace PalUpdater;

public class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private AppConfig _config;
    private SettingsForm? _settingsForm;
    private bool _checkInProgress;

    public TrayAppContext()
    {
        _config = AppConfig.Load();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Check Now", null, async (_, _) => await CheckAndMaybeInstallAsync(manual: true));
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _trayIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "PalUpdater - UE4SS auto-updater",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        _timer = new System.Windows.Forms.Timer();
        UpdateTimerInterval();
        _timer.Tick += async (_, _) => await CheckAndMaybeInstallAsync(manual: false);
        _timer.Start();

        Logger.Log("PalUpdater started.");

        // Always show the window on launch so it's obvious the app actually started -
        // launching straight to a silent tray icon was confusing with no feedback.
        ShowSettings();

        if (!string.IsNullOrWhiteSpace(_config.GameRootPath))
        {
            _ = CheckAndMaybeInstallAsync(manual: false);
        }
    }

    public void UpdateTimerInterval()
    {
        _timer.Interval = Math.Max(1, _config.CheckIntervalHours) * 60 * 60 * 1000;
    }

    public void ReloadConfig() => _config.CopyFrom(AppConfig.Load());

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }
        _settingsForm = new SettingsForm(_config, this);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
    }

    public async Task CheckAndMaybeInstallAsync(bool manual, bool force = false)
    {
        if (_checkInProgress) return;
        if (string.IsNullOrWhiteSpace(_config.GameRootPath))
        {
            if (manual) MessageBox.Show("Set your Palworld install folder in Settings first.", "PalUpdater");
            return;
        }

        _checkInProgress = true;
        try
        {
            Logger.Log(manual ? "Manual check triggered." : "Scheduled check triggered.");
            var checker = new UpdateChecker(_config.GitHubToken);
            var release = await checker.GetLatestReleaseAsync(_config.IncludePrerelease);

            if (release == null)
            {
                Logger.Log("Could not retrieve release info (see above for error).");
                if (manual) MessageBox.Show("Couldn't reach GitHub or parse the release info. Check the log.", "PalUpdater");
                return;
            }

            var asset = UpdateChecker.PickAsset(release);
            if (asset == null)
            {
                Logger.Log($"New release {release.TagName} found, but no matching .zip asset was found to install.");
                return;
            }

            // Compare by asset filename, not just the release tag. Rolling releases (e.g. UE4SS's
            // "experimental-latest") reuse the same tag name forever while swapping out the actual
            // file each build - the filename is what actually changes (it embeds a commit hash), so
            // that's the real "is this a new build" signal. Tag name alone would report "up to date"
            // forever after the first install from a rolling release, even as new builds ship.
            var sameAsLastInstalled = asset.Name == _config.LastInstalledAssetName;

            if (!force && sameAsLastInstalled && FilesActuallyInstalled())
            {
                Logger.Log($"Already up to date ({asset.Name}).");
                if (manual) MessageBox.Show($"Already up to date: {asset.Name}", "PalUpdater");
                return;
            }

            if (!force && sameAsLastInstalled && !FilesActuallyInstalled())
            {
                Logger.Log($"Config says {asset.Name} is installed, but the UE4SS files are missing from " +
                           $"{_config.ResolvedInstallPath} - reinstalling.");
            }

            Logger.Log($"New version available: {asset.Name} (currently: {(_config.LastInstalledAssetName == "" ? "none" : _config.LastInstalledAssetName)})");

            var gameRunning = IsGameRunning();

            if (_config.AutoInstall)
            {
                if (gameRunning)
                {
                    // The DLL is likely locked by the running process - overwriting it now would
                    // probably fail partway through. Defer and let the next scheduled check retry
                    // once the game has closed, instead of risking a half-applied update.
                    Logger.Log($"{release.TagName} is available, but Palworld is currently running - " +
                               "deferring install until the game is closed.");
                }
                else
                {
                    await DownloadAndInstallAsync(checker, release, asset);
                }
            }
            else if (manual)
            {
                // A manual "Check Now" click deserves a clear, hard-to-miss result -
                // not a balloon tip that can auto-dismiss or get suppressed by Windows.
                var promptText = $"UE4SS {release.TagName} is available (asset: {asset.Name}).\n\nInstall it now?";
                if (gameRunning)
                {
                    promptText += "\n\nNote: Palworld appears to be running. The update files may be locked " +
                                  "and installing now could fail - consider closing the game first.";
                }

                var result = MessageBox.Show(promptText, "PalUpdater - update available", MessageBoxButtons.YesNo,
                    gameRunning ? MessageBoxIcon.Warning : MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                    await DownloadAndInstallAsync(checker, release, asset);
                else
                    Logger.Log($"User declined install of {release.TagName}.");
            }
            else
            {
                _trayIcon.BalloonTipTitle = "UE4SS update available";
                _trayIcon.BalloonTipText = $"{release.TagName} is available. Click here or open Settings to install.";
                _trayIcon.BalloonTipClicked += OnBalloonClicked;
                _trayIcon.ShowBalloonTip(10000);

                void OnBalloonClicked(object? s, EventArgs e)
                {
                    _trayIcon.BalloonTipClicked -= OnBalloonClicked;
                    _ = DownloadAndInstallAsync(checker, release, asset);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Unexpected error during check: {ex.Message}");
        }
        finally
        {
            _checkInProgress = false;
        }
    }

    private async Task DownloadAndInstallAsync(UpdateChecker checker, GitHubRelease release, GitHubAsset asset)
    {
        try
        {
            var tempZip = Path.Combine(Path.GetTempPath(), asset.Name);
            Logger.Log($"Downloading {asset.Name}...");
            await checker.DownloadAssetAsync(asset, tempZip);

            Logger.Log($"Installing to {_config.ResolvedInstallPath}...");
            Installer.Install(tempZip, _config.ResolvedInstallPath);

            try { File.Delete(tempZip); } catch { /* ignore */ }

            _config.LastInstalledTag = release.TagName;
            _config.LastInstalledAssetName = asset.Name;
            _config.Save();

            Logger.Log($"Installed UE4SS {release.TagName} successfully.");

            if (IsGameRunning())
            {
                Logger.Log("Palworld is currently running - the update won't take effect until you restart the game.");
                _trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
                _trayIcon.BalloonTipTitle = "UE4SS updated - restart Palworld";
                _trayIcon.BalloonTipText =
                    $"Installed {release.TagName}, but Palworld is currently running. Restart the game for the update to take effect.";
                _trayIcon.ShowBalloonTip(15000);

                // A balloon tip alone is easy to miss (auto-dismisses, can be suppressed by Windows
                // notification settings), so also surface a dialog the user has to acknowledge.
                MessageBox.Show(
                    $"UE4SS {release.TagName} was installed, but Palworld is currently running.\n\n" +
                    "The update won't take effect until you restart the game.",
                    "PalUpdater - restart recommended",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else
            {
                _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                _trayIcon.BalloonTipTitle = "UE4SS updated";
                _trayIcon.BalloonTipText = $"Installed {release.TagName}.";
                _trayIcon.ShowBalloonTip(8000);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Install failed: {ex.Message}");
            MessageBox.Show($"Update failed: {ex.Message}\n\nYour existing UE4SS install and mods were not touched " +
                             $"unless the log says otherwise.", "PalUpdater - error");
        }
    }

    // Palworld's actual game process is Palworld-Win64-Shipping.exe; the Steam launcher stub can
    // show up briefly as Palworld.exe too, so check both to be safe.
    private static bool IsGameRunning()
    {
        return Process.GetProcessesByName("Palworld-Win64-Shipping").Length > 0
               || Process.GetProcessesByName("Palworld").Length > 0;
    }

    // UE4SS drops this DLL directly into Pal/Binaries/Win64 - its presence is a reliable
    // signal that an install actually exists there, independent of what config.json claims.
    private bool FilesActuallyInstalled()
    {
        if (string.IsNullOrWhiteSpace(_config.ResolvedInstallPath)) return false;
        return File.Exists(Path.Combine(_config.ResolvedInstallPath, "dwmapi.dll"));
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        Application.Exit();
    }
}
