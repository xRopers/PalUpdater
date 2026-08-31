using System.Net.Http.Headers;
using System.Text.Json;
using PalUpdater.Models;

namespace PalUpdater;

public class UpdateChecker
{
    private const string Owner = "UE4SS-RE";
    private const string Repo = "RE-UE4SS";

    // Shared across all checks for the app's lifetime. Creating a fresh HttpClient (and its
    // underlying connection pool) on every 6-hour check and never disposing it was pure waste -
    // one long-lived client is both lighter and the officially recommended pattern.
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PalUpdater", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public UpdateChecker(string? gitHubToken)
    {
        // The token can change between checks (user edits it in Settings), so refresh the
        // auth header on the shared client each time rather than spinning up a new client.
        Http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(gitHubToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", gitHubToken);
    }

    // Returns the newest release (optionally including prereleases), or null on failure.
    public async Task<GitHubRelease?> GetLatestReleaseAsync(bool includePrerelease)
    {
        try
        {
            if (!includePrerelease)
            {
                var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
                var json = await Http.GetStringAsync(url);
                return JsonSerializer.Deserialize<GitHubRelease>(json);
            }
            else
            {
                // "latest" endpoint skips prereleases, so pull the list and take the first non-draft one
                var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page=5";
                var json = await Http.GetStringAsync(url);
                var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json);
                return releases?.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to check GitHub releases: {ex.Message}");
            return null;
        }
    }

    // Picks the main UE4SS zip asset, skipping dev/experimental (zDEV-prefixed) and symbol/pdb bundles.
    public static GitHubAsset? PickAsset(GitHubRelease release)
    {
        return release.Assets
            .Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .Where(a => !a.Name.StartsWith("zDEV", StringComparison.OrdinalIgnoreCase))
            .Where(a => !a.Name.Contains("pdb", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Size) // the full package is bigger than any partial/symbols-only zip
            .FirstOrDefault();
    }

    public async Task DownloadAssetAsync(GitHubAsset asset, string destinationZipPath)
    {
        using var response = await Http.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var fs = new FileStream(destinationZipPath, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fs);
    }
}

public static class Logger
{
    private static readonly object Lock = new();

    public static event Action<string>? OnLog;

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        OnLog?.Invoke(line);
        try
        {
            lock (Lock)
            {
                var dir = Path.GetDirectoryName(AppConfig.LogPath)!;
                Directory.CreateDirectory(dir);
                File.AppendAllText(AppConfig.LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // logging must never crash the app
        }
    }
}
