using System.IO.Compression;

namespace PalUpdater;

public static class Installer
{
    public static void Install(string zipPath, string installPath, IEnumerable<string> preservePaths)
    {
        Directory.CreateDirectory(installPath);

        var backupDir = Path.Combine(Path.GetTempPath(), "PalUpdater_backup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupDir);

        var preserveList = preservePaths.ToList();
        Exception? extractionError = null;

        foreach (var rel in preserveList)
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

        // Restore mods/config regardless of whether extraction succeeded
        try
        {
            foreach (var rel in preserveList)
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
