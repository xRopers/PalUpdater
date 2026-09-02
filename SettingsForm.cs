using System.Windows.Forms;
using System.Drawing;

namespace PalUpdater;

public class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly TrayAppContext _tray;

    private TextBox _pathBox = new();
    private NumericUpDown _intervalBox = new();
    private CheckBox _autoInstallBox = new();
    private CheckBox _prereleaseBox = new();
    private TextBox _tokenBox = new();
    private TextBox _logBox = new();
    private Label _statusLabel = new();

    public SettingsForm(AppConfig config, TrayAppContext tray)
    {
        _config = config;
        _tray = tray;

        Text = "PalUpdater Settings";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        Width = 880;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += (_, _) => { }; // hitting X just hides to tray behavior handled by caller

        BuildUi();
        LoadFromConfig();

        Logger.OnLog += AppendLog;
        FormClosed += (_, _) => Logger.OnLog -= AppendLog;
    }

    private void BuildUi()
    {
        // AutoScale on the actual font so control sizing tracks the user's DPI/text-scale setting
        // instead of assuming 96 DPI, which is what caused clipped button text before.
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(880, 760);

        const int margin = 15;
        const int fieldHeight = 28;
        const int rowGap = 10;
        var contentWidth = ClientSize.Width - margin * 2;
        var y = margin;

        Label MakeLabel(string text, int width) => new()
        {
            Text = text, Left = margin, Top = y, Width = width, AutoSize = false, Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Button MakeButton(string text)
        {
            var b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            b.Padding = new Padding(8, 4, 8, 4);
            return b;
        }

        var pathLabel = MakeLabel("Palworld install folder:", contentWidth);
        Controls.Add(pathLabel);
        y += pathLabel.Height + 4;

        var browseBtn = MakeButton("Browse...");
        browseBtn.Click += (_, _) => BrowseForFolder();
        Controls.Add(browseBtn); // add first so AutoSize resolves before we read its real Width below

        _pathBox = new TextBox { Left = margin, Top = y + 3, Height = fieldHeight, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        browseBtn.Top = y;
        browseBtn.Left = ClientSize.Width - margin - browseBtn.Width;
        browseBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _pathBox.Width = browseBtn.Left - margin - 10;
        Controls.Add(_pathBox);
        y += Math.Max(_pathBox.Height, browseBtn.Height) + rowGap;

        var detectBtn = MakeButton("Auto-detect via Steam");
        detectBtn.Left = margin;
        detectBtn.Top = y;
        detectBtn.Click += (_, _) => AutoDetect();
        Controls.Add(detectBtn);
        y += detectBtn.Height + rowGap * 2;

        var intervalLabel = MakeLabel("Check every (hours):", 160);
        intervalLabel.AutoSize = true;
        Controls.Add(intervalLabel);
        _intervalBox = new NumericUpDown
        {
            Left = margin + intervalLabel.PreferredWidth + 8, Top = y - 2, Width = 60, Height = fieldHeight,
            Minimum = 1, Maximum = 168
        };
        Controls.Add(_intervalBox);
        y += Math.Max(intervalLabel.Height, _intervalBox.Height) + rowGap * 2;

        _autoInstallBox = new CheckBox
        {
            Text = "Install updates automatically (uncheck to be notified and install manually)",
            Left = margin, Top = y, Width = contentWidth, AutoSize = false, Height = 38,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_autoInstallBox);
        y += _autoInstallBox.Height + rowGap;

        _prereleaseBox = new CheckBox
        {
            Text = "Include prerelease / dev builds",
            Left = margin, Top = y, Width = contentWidth, AutoSize = false, Height = 38,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(_prereleaseBox);
        y += _prereleaseBox.Height + rowGap * 2;

        var tokenLabel = MakeLabel("GitHub token (optional, only needed for short check intervals):", contentWidth);
        Controls.Add(tokenLabel);
        y += tokenLabel.Height + 4;

        _tokenBox = new TextBox
        {
            Left = margin, Top = y, Width = contentWidth, Height = fieldHeight,
            UseSystemPasswordChar = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_tokenBox);
        y += _tokenBox.Height + rowGap * 2;

        var saveBtn = MakeButton("Save");
        saveBtn.Left = margin;
        saveBtn.Top = y;
        saveBtn.Click += (_, _) => SaveConfig();
        Controls.Add(saveBtn);

        var checkNowBtn = MakeButton("Check Now");
        checkNowBtn.Top = y;
        checkNowBtn.Left = saveBtn.Right + 10;
        checkNowBtn.Click += async (_, _) => await CheckNow();
        Controls.Add(checkNowBtn);

        var forceBtn = MakeButton("Force Reinstall");
        forceBtn.Top = y;
        forceBtn.Left = checkNowBtn.Right + 10;
        forceBtn.Click += async (_, _) => await ForceReinstall();
        Controls.Add(forceBtn);

        _statusLabel = new Label
        {
            Left = forceBtn.Right + 15, Top = y + 5, Width = ClientSize.Width - margin - (forceBtn.Right + 15),
            Text = "", AutoSize = false, Height = 38, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_statusLabel);
        y += Math.Max(saveBtn.Height, Math.Max(checkNowBtn.Height, forceBtn.Height)) + rowGap * 2;

        var logLabel = MakeLabel("Log:", 100);
        Controls.Add(logLabel);
        y += logLabel.Height + 4;

        _logBox = new TextBox
        {
            Left = margin, Top = y, Width = contentWidth, Height = 160,
            Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_logBox);
        y += _logBox.Height + rowGap;

        var clearLogBtn = MakeButton("Clear Log");
        clearLogBtn.Left = margin;
        clearLogBtn.Top = y;
        clearLogBtn.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        clearLogBtn.Click += (_, _) => ClearLog();
        Controls.Add(clearLogBtn);

        if (File.Exists(AppConfig.LogPath))
        {
            try
            {
                var lines = File.ReadAllLines(AppConfig.LogPath);
                _logBox.Text = string.Join(Environment.NewLine, lines.TakeLast(50));
                _logBox.SelectionStart = _logBox.Text.Length;
                _logBox.ScrollToCaret();
            }
            catch { /* ignore */ }
        }
    }

    private void LoadFromConfig()
    {
        _pathBox.Text = _config.GameRootPath;
        _intervalBox.Value = Math.Clamp(_config.CheckIntervalHours, 1, 168);
        _autoInstallBox.Checked = _config.AutoInstall;
        _prereleaseBox.Checked = _config.IncludePrerelease;
        _tokenBox.Text = _config.GitHubToken ?? "";
        _statusLabel.Text = string.IsNullOrEmpty(_config.LastInstalledAssetName)
            ? "No UE4SS version installed yet."
            : $"Currently installed: {_config.LastInstalledAssetName}";
    }

    private void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Select your Palworld install folder (contains the 'Pal' folder)" };
        if (dialog.ShowDialog() == DialogResult.OK)
            _pathBox.Text = dialog.SelectedPath;
    }

    private void AutoDetect()
    {
        var found = SteamLocator.TryFindPalworld();
        if (found != null)
        {
            _pathBox.Text = found;
            MessageBox.Show($"Found Palworld at:\n{found}", "PalUpdater");
        }
        else
        {
            MessageBox.Show("Couldn't auto-detect Palworld through Steam. Please browse for it manually.", "PalUpdater");
        }
    }

    private void SaveConfig()
    {
        if (string.IsNullOrWhiteSpace(_pathBox.Text) || !Directory.Exists(Path.Combine(_pathBox.Text, "Pal")))
        {
            var proceed = MessageBox.Show(
                "That folder doesn't look like a Palworld install (no 'Pal' subfolder found). Save anyway?",
                "PalUpdater", MessageBoxButtons.YesNo);
            if (proceed != DialogResult.Yes) return;
        }

        _config.GameRootPath = _pathBox.Text.Trim();
        _config.CheckIntervalHours = (int)_intervalBox.Value;
        _config.AutoInstall = _autoInstallBox.Checked;
        _config.IncludePrerelease = _prereleaseBox.Checked;
        _config.GitHubToken = string.IsNullOrWhiteSpace(_tokenBox.Text) ? null : _tokenBox.Text.Trim();
        _config.Save();

        _tray.ReloadConfig();
        _tray.UpdateTimerInterval();

        MessageBox.Show("Settings saved.", "PalUpdater");
    }

    private async Task CheckNow()
    {
        SaveConfig();
        await _tray.CheckAndMaybeInstallAsync(manual: true);
        LoadFromConfig();
    }

    private async Task ForceReinstall()
    {
        SaveConfig();
        await _tray.CheckAndMaybeInstallAsync(manual: true, force: true);
        LoadFromConfig();
    }

    private void ClearLog()
    {
        _logBox.Clear();
        try
        {
            File.WriteAllText(AppConfig.LogPath, "");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cleared the view, but couldn't clear the log file on disk: {ex.Message}", "PalUpdater");
        }
        Logger.Log("Log cleared.");
    }

    private void AppendLog(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(line)));
            return;
        }
        _logBox.AppendText(line + Environment.NewLine);
    }
}
