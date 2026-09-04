using System.Net.Http.Headers;
using System.Text.Json;
using PalUpdater.Models;

namespace PalUpdater;

public class UpdateChecker
{
    private const string Owner = "UE4SS-RE";
    private const string Repo = "RE-UE4SS";

    // Shared for the app's lifetime instead of a new HttpClient per check
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
        // Token can change between checks, refresh the auth header on the shared client
        Http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(gitHubToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", gitHubToken);
    }

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
                // /latest skips prereleases, so pull the list and take the newest
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

    // Skips dev/experimental (zDEV-prefixed) and symbol/pdb bundles
    public static GitHubAsset? PickAsset(GitHubRelease release)
    {
        return release.Assets
            .Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .Where(a => !a.Name.StartsWith("zDEV", StringComparison.OrdinalIgnoreCase))
            .Where(a => !a.Name.Contains("pdb", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Size)
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
            // never let logging crash the app
        }
    }
}
