using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PalUpdater;

public static class SteamLocator
{
    // Returns the Palworld root folder (contains "Pal" subfolder) or null if not found.
    public static string? TryFindPalworld()
    {
        var steamPath = GetSteamInstallPath();
        if (steamPath == null) return null;

        var libraryFolders = new List<string> { Path.Combine(steamPath, "steamapps") };

        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            try
            {
                var text = File.ReadAllText(vdfPath);
                // crude but effective: pull every quoted path from the vdf
                foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
                {
                    var lib = m.Groups[1].Value.Replace("\\\\", "\\");
                    libraryFolders.Add(Path.Combine(lib, "steamapps"));
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not parse libraryfolders.vdf: {ex.Message}");
            }
        }

        foreach (var lib in libraryFolders.Distinct())
        {
            var candidate = Path.Combine(lib, "common", "Palworld");
            if (Directory.Exists(Path.Combine(candidate, "Pal")))
                return candidate;
        }

        return null;
    }

    private static string? GetSteamInstallPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string
                   ?? key?.GetValue("InstallPath") as string;
        }
        catch
        {
            return null;
        }
    }
}
