using System.Text.Json;

namespace PalUpdater;

public class AppConfig
{
    // Root of the Palworld install (contains "Pal\Binaries\Win64")
    public string GameRootPath { get; set; } = "";

    public int CheckIntervalHours { get; set; } = 6;

    // Display only - not reliable for up-to-date checks on rolling releases, see LastInstalledAssetName
    public string LastInstalledTag { get; set; } = "";

    // Actual installed filename, e.g. "UE4SS_v3.0.1-1106-g3a2d2bc1.zip". Rolling releases like
    // "experimental-latest" never change their tag, just the file, so this is the real version check.
    public string LastInstalledAssetName { get; set; } = "";

    public bool AutoInstall { get; set; } = false;

    // GitHub PAT, only needed if check interval is short enough to hit the 60/hr unauthed limit
    public string? GitHubToken { get; set; }

    public bool IncludePrerelease { get; set; } = false;

    // Relative to Pal\Binaries\Win64. UE4SS now nests everything except dwmapi.dll under a
    // "ue4ss" subfolder (dwmapi.dll itself never needs preserving, it's just the proxy loader).
    public List<string> PreservePaths { get; set; } = new()
    {
        Path.Combine("ue4ss", "Mods"),
        Path.Combine("ue4ss", "UE4SS-settings.ini"),
    };

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PalUpdater");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static string LogPath => Path.Combine(ConfigDir, "log.txt");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null)
                {
                    MigrateOldPreservePaths(cfg);
                    return cfg;
                }
            }
        }
        catch
        {
            // corrupt config, fall back to defaults
        }
        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    // Configs saved before v1.1.0 stored "Mods" and "UE4SS-settings.ini" relative to Win64 root.
    // UE4SS moved those under a "ue4ss" subfolder, so upgrade any exact matches on load - leaves
    // anything the user customized untouched.
    private static void MigrateOldPreservePaths(AppConfig cfg)
    {
        var changed = false;
        for (var i = 0; i < cfg.PreservePaths.Count; i++)
        {
            if (cfg.PreservePaths[i] == "Mods")
            {
                cfg.PreservePaths[i] = Path.Combine("ue4ss", "Mods");
                changed = true;
            }
            else if (cfg.PreservePaths[i] == "UE4SS-settings.ini")
            {
                cfg.PreservePaths[i] = Path.Combine("ue4ss", "UE4SS-settings.ini");
                changed = true;
            }
        }
        if (changed) cfg.Save();
    }

    // Updates fields in place instead of reassigning, so TrayAppContext and an open SettingsForm
    // (both holding this same instance) stay in sync
    public void CopyFrom(AppConfig other)
    {
        GameRootPath = other.GameRootPath;
        CheckIntervalHours = other.CheckIntervalHours;
        LastInstalledTag = other.LastInstalledTag;
        LastInstalledAssetName = other.LastInstalledAssetName;
        AutoInstall = other.AutoInstall;
        GitHubToken = other.GitHubToken;
        IncludePrerelease = other.IncludePrerelease;
        PreservePaths = other.PreservePaths;
    }

    public string ResolvedInstallPath =>
        string.IsNullOrWhiteSpace(GameRootPath) ? "" : Path.Combine(GameRootPath, "Pal", "Binaries", "Win64");
}
