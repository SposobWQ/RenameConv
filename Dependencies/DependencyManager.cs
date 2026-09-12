using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using RenameConv.Services;

namespace RenameConv.Dependencies;

internal sealed class DependencyManager
{
    private const string FfmpegReleaseUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest";
    private const string LibreOfficePageUrl = "https://portableapps.com/apps/office/libreoffice_portable";
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly Action<string> _report;
    private readonly SemaphoreSlim _ffmpegLock = new(1, 1);
    private readonly SemaphoreSlim _officeLock = new(1, 1);

    public DependencyManager(Action<string> report)
    {
        _report = report;
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("RenameConv/1.1.0");
    }

    public async Task<(string Ffmpeg, string Ffprobe)> EnsureFfmpegAsync(CancellationToken cancellationToken)
    {
        var ffmpeg = ToolLocator.FindFfmpeg();
        var ffprobe = ToolLocator.FindFfprobe();
        if (ffmpeg is not null && ffprobe is not null) return (ffmpeg, ffprobe);

        await _ffmpegLock.WaitAsync(cancellationToken);
        try
        {
            ffmpeg = ToolLocator.FindFfmpeg();
            ffprobe = ToolLocator.FindFfprobe();
            if (ffmpeg is not null && ffprobe is not null) return (ffmpeg, ffprobe);
            await InstallFfmpegAsync(cancellationToken);
            return (ToolLocator.FindFfmpeg() ?? throw new InvalidOperationException("FFmpeg не найден после загрузки."), ToolLocator.FindFfprobe() ?? throw new InvalidOperationException("FFprobe не найден после загрузки."));
        }
        finally
        {
            _ffmpegLock.Release();
        }
    }

    public async Task<string?> EnsureLibreOfficeAsync(CancellationToken cancellationToken)
    {
        var office = ToolLocator.FindLibreOffice();
        if (office is not null) return office;

        await _officeLock.WaitAsync(cancellationToken);
        try
        {
            office = ToolLocator.FindLibreOffice();
            if (office is not null) return office;
            await InstallLibreOfficeAsync(cancellationToken);
            return ToolLocator.FindLibreOffice();
        }
        finally
        {
            _officeLock.Release();
        }
    }

    public async Task<IReadOnlyList<DependencyUpdateResult>> UpdateAllAsync(CancellationToken cancellationToken)
    {
        var results = new List<DependencyUpdateResult>();
        try
        {
            await _ffmpegLock.WaitAsync(cancellationToken);
            try
            {
                await InstallFfmpegAsync(cancellationToken);
                results.Add(new DependencyUpdateResult("FFmpeg", true, "FFmpeg обновлён."));
            }
            finally
            {
                _ffmpegLock.Release();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            results.Add(new DependencyUpdateResult("FFmpeg", false, $"FFmpeg: {ex.Message}"));
        }

        try
        {
            await _officeLock.WaitAsync(cancellationToken);
            try
            {
                await InstallLibreOfficeAsync(cancellationToken);
                results.Add(new DependencyUpdateResult("LibreOffice", true, "LibreOffice Portable обновлён."));
            }
            finally
            {
                _officeLock.Release();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            results.Add(new DependencyUpdateResult("LibreOffice", false, $"LibreOffice: {ex.Message}"));
        }

        return results;
    }

    private async Task InstallFfmpegAsync(CancellationToken cancellationToken)
    {
        _report("Загрузка FFmpeg...");
        var release = await GetJsonAsync<FfmpegRelease>(FfmpegReleaseUrl, cancellationToken) ?? throw new InvalidOperationException("Не удалось получить информацию о версии FFmpeg.");
        var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "winarm64" : "win64";
        var asset = release.Assets.FirstOrDefault(item => item.Name.Contains(architecture, StringComparison.OrdinalIgnoreCase) && item.Name.Contains("gpl-shared", StringComparison.OrdinalIgnoreCase) && item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Не найден подходящий архив FFmpeg.");
        var temporaryDirectory = CreateTemporaryDirectory();
        var archivePath = Path.Combine(temporaryDirectory, asset.Name);

        try
        {
            await DownloadFileAsync(asset.DownloadUrl, archivePath, cancellationToken);
            var extractPath = Path.Combine(temporaryDirectory, "extract");
            ZipFile.ExtractToDirectory(archivePath, extractPath);
            var executable = Directory.GetFiles(extractPath, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidOperationException("Архив FFmpeg не содержит ffmpeg.exe.");
            var sourceBin = Path.GetDirectoryName(executable)!;
            var destinationBin = Path.Combine(ManagedToolsDirectory, "ffmpeg", "bin");
            ReplaceDirectory(sourceBin, destinationBin);
            _report("FFmpeg готов к работе.");
        }
        finally
        {
            DeleteDirectory(temporaryDirectory);
        }
    }

    private async Task InstallLibreOfficeAsync(CancellationToken cancellationToken)
    {
        _report("Загрузка LibreOffice Portable...");
        var page = await _client.GetStringAsync(LibreOfficePageUrl, cancellationToken);
        var versionMatch = Regex.Match(page, @"Version\s+([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase);
        if (!versionMatch.Success) throw new InvalidOperationException("Не удалось определить версию LibreOffice Portable.");
        var version = versionMatch.Groups[1].Value;
        var installerName = $"LibreOfficePortable_{version}_MultilingualStandard.paf.exe";
        var installerUrl = $"https://download.documentfoundation.org/libreoffice/portable/{version}/{installerName}";
        var temporaryDirectory = CreateTemporaryDirectory();
        var installerPath = Path.Combine(temporaryDirectory, installerName);

        try
        {
            await DownloadFileAsync(installerUrl, installerPath, cancellationToken);
            Directory.CreateDirectory(ManagedToolsDirectory);
            var process = Process.Start(new ProcessStartInfo(installerPath, $"/S /D={Quote(ManagedToolsDirectory)}") { UseShellExecute = false, CreateNoWindow = true })
                ?? throw new InvalidOperationException("Не удалось запустить установщик LibreOffice Portable.");
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0) throw new InvalidOperationException($"Установщик LibreOffice завершился с кодом {process.ExitCode}.");
            if (ToolLocator.FindLibreOffice() is null) throw new InvalidOperationException("LibreOffice не найден после установки.");
            _report("LibreOffice Portable готов к работе.");
        }
        finally
        {
            DeleteDirectory(temporaryDirectory);
        }
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static string ManagedToolsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RenameConv", "tools");
    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "RenameConv", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void ReplaceDirectory(string source, string destination)
    {
        var temporaryDestination = $"{destination}.new";
        DeleteDirectory(temporaryDestination);
        CopyDirectory(source, temporaryDestination);
        DeleteDirectory(destination);
        Directory.Move(temporaryDestination, destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private sealed class FfmpegRelease
    {
        [JsonPropertyName("assets")]
        public List<FfmpegAsset> Assets { get; set; } = [];
    }

    private sealed class FfmpegAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
