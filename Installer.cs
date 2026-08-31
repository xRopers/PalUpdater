using System.IO.Compression;

namespace PalUpdater;

public static class Installer
{
    // Files/folders that belong to the USER (mods they installed, their settings) rather than
    // to the UE4SS core package itself. These get preserved across an update.
    private static readonly string[] PreserveRelativePaths =
    {
        "Mods",                 // whole mods folder, including mods.txt
        "UE4SS-settings.ini",
    };

    public static void Install(string zipPath, string installPath)
    {
        Directory.CreateDirectory(installPath);

        var backupDir = Path.Combine(Path.GetTempPath(), "PalUpdater_backup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupDir);

        Exception? extractionError = null;

        // 1. Back up anything user-owned that currently exists
        foreach (var rel in PreserveRelativePaths)
        {
            var src = Path.Combine(installPath, rel);
            var dst = Path.Combine(backupDir, rel);

            if (Directory.Exists(src))
            {
                CopyDirectory(src, dst);
                Logger.Log($"Backed up folder: {rel}");
            }
            else if (File.Exists(src))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, true);
                Logger.Log($"Backed up file: {rel}");
            }
        }

        // 2. Extract the new UE4SS build over the install folder.
        // If this throws partway through, we still fall through to step 3 so the
        // user's mods/config get restored rather than left missing.
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) // directory entry
                    continue;

                var destPath = Path.Combine(installPath, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                entry.ExtractToFile(destPath, overwrite: true);
            }
            Logger.Log("Extracted new UE4SS build.");
        }
        catch (Exception ex)
        {
            extractionError = ex;
            Logger.Log($"Extraction failed partway through: {ex.Message}. Restoring your mods/config, but the core UE4SS files may be incomplete - re-run the update.");
        }

        // 3. Restore the user's mods/config back on top, always
        try
        {
            foreach (var rel in PreserveRelativePaths)
            {
                var src = Path.Combine(backupDir, rel);
                var dst = Path.Combine(installPath, rel);

                if (Directory.Exists(src))
                {
                    CopyDirectory(src, dst);
                    Logger.Log($"Restored folder: {rel}");
                }
                else if (File.Exists(src))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(src, dst, true);
                    Logger.Log($"Restored file: {rel}");
                }
            }
        }
        finally
        {
            try { Directory.Delete(backupDir, true); } catch { /* best effort */ }
        }

        if (extractionError != null)
            throw extractionError;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);

        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }
}
