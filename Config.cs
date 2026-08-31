using System.Text.Json;

namespace PalUpdater;

public class AppConfig
{
    // Root folder of the Palworld install (the folder that contains "Pal\Binaries\Win64")
    public string GameRootPath { get; set; } = "";

    // How often to check for updates, in hours
    public int CheckIntervalHours { get; set; } = 6;

    // Last UE4SS tag we successfully installed, e.g. "v3.0.1"
    public string LastInstalledTag { get; set; } = "";

    // If true, install updates automatically; if false, just notify and wait for manual "Install"
    public bool AutoInstall { get; set; } = false;

    // Optional GitHub personal access token to raise the 60/hr unauthenticated rate limit.
    // Only needed if you set very short check intervals. Stored locally only.
    public string? GitHubToken { get; set; }

    // Include prerelease / dev builds when checking for "latest"
    public bool IncludePrerelease { get; set; } = false;

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
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // fall through to defaults if the config is corrupt
        }
        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    // Copies values from another instance into this one in place, preserving object identity.
    // Used to refresh from disk without breaking references shared with other windows/classes
    // (e.g. TrayAppContext and an open SettingsForm both hold the same AppConfig instance).
    public void CopyFrom(AppConfig other)
    {
        GameRootPath = other.GameRootPath;
        CheckIntervalHours = other.CheckIntervalHours;
        LastInstalledTag = other.LastInstalledTag;
        AutoInstall = other.AutoInstall;
        GitHubToken = other.GitHubToken;
        IncludePrerelease = other.IncludePrerelease;
    }

    // Resolves the actual folder UE4SS files get dropped into
    public string ResolvedInstallPath =>
        string.IsNullOrWhiteSpace(GameRootPath) ? "" : Path.Combine(GameRootPath, "Pal", "Binaries", "Win64");
}
