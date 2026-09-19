using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RenameConv.Updates;

internal sealed class UpdateManager
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/SposobWQ/RenameConv/releases/latest";
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<UpdateRelease?> FindAvailableUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.UserAgent.ParseAdd("RenameConv/1.1.0");
        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var release = JsonSerializer.Deserialize<GitHubRelease>(await response.Content.ReadAsStringAsync(cancellationToken));
        if (release is null || !Version.TryParse(release.TagName.TrimStart('v', 'V'), out var latestVersion)) return null;

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 1, 0);
        currentVersion = new Version(currentVersion.Major, currentVersion.Minor, Math.Max(0, currentVersion.Build));
        if (latestVersion <= currentVersion) return null;

        var edition = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "tools")) ? "RenameConvPortable" : "RenameConvOnline";
        var package = release.Assets.FirstOrDefault(asset => asset.Name.Contains(edition, StringComparison.OrdinalIgnoreCase) && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        return new UpdateRelease(latestVersion, release.HtmlUrl, package?.DownloadUrl, package?.Name);
    }

    public async Task<string> DownloadAsync(UpdateRelease release, IProgress<int> progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(release.PackageUrl) || string.IsNullOrWhiteSpace(release.PackageName)) throw new InvalidOperationException("В релизе нет ZIP-пакета для обновления.");

        var directory = Path.Combine(Path.GetTempPath(), "RenameConv", "updates");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"{Guid.NewGuid():N}.download");
        var packagePath = Path.Combine(directory, release.PackageName);

        try
        {
            using var response = await _client.GetAsync(release.PackageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var length = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = File.Create(temporaryPath);
            var buffer = new byte[81920];
            long total = 0;
            int read;

            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                total += read;
                if (length is > 0) progress.Report((int)(total * 100 / length.Value));
            }

            File.Move(temporaryPath, packagePath, true);
            return packagePath;
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    public void StartUpdater(string packagePath)
    {
        var updaterPath = Path.Combine(AppContext.BaseDirectory, "RenameConvUpdater.exe");
        if (!File.Exists(updaterPath)) throw new FileNotFoundException("Не найден RenameConvUpdater.exe.", updaterPath);

        var updaterDirectory = Path.Combine(Path.GetTempPath(), "RenameConv", "updater");
        Directory.CreateDirectory(updaterDirectory);
        var temporaryUpdaterPath = Path.Combine(updaterDirectory, $"RenameConvUpdater-{Guid.NewGuid():N}.exe");
        File.Copy(updaterPath, temporaryUpdaterPath, true);
        var startInfo = new ProcessStartInfo(temporaryUpdaterPath) { UseShellExecute = true };
        startInfo.ArgumentList.Add(packagePath);
        startInfo.ArgumentList.Add(AppContext.BaseDirectory);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        Process.Start(startInfo);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
