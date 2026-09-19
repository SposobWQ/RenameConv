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
    private const string LibreOfficeDownloadPageUrl = "https://www.libreoffice.org/download/download-libreoffice/";
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
                results.Add(new DependencyUpdateResult("LibreOffice", true, "LibreOffice подготовлен."));
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

    public async Task<string?> EnsureLibreOfficeAsync(string? selectedPath, CancellationToken cancellationToken)
    {
        var office = ToolLocator.FindLibreOffice(selectedPath);
        if (office is not null) return office;

        await _officeLock.WaitAsync(cancellationToken);
        try
        {
            office = ToolLocator.FindLibreOffice(selectedPath);
            if (office is not null) return office;
            await InstallLibreOfficeAsync(cancellationToken);
            return ToolLocator.FindLibreOffice(selectedPath);
        }
        finally
        {
            _officeLock.Release();
        }
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
        _report("Загрузка LibreOffice...");
        var page = await _client.GetStringAsync(LibreOfficeDownloadPageUrl, cancellationToken);
        var versionMatch = Regex.Match(page, @"LibreOffice_([0-9]+(?:\.[0-9]+)+)_Win_x86-64\.msi", RegexOptions.IgnoreCase);
        if (!versionMatch.Success) throw new InvalidOperationException("Не удалось определить версию LibreOffice для Windows x64.");
        var version = versionMatch.Groups[1].Value;
        var installerName = $"LibreOffice_{version}_Win_x86-64.msi";
        var installerUrl = $"https://download.documentfoundation.org/libreoffice/stable/{version}/win/x86_64/{installerName}";
        var temporaryDirectory = CreateTemporaryDirectory();
        var installerPath = Path.Combine(temporaryDirectory, installerName);
        var extractPath = Path.Combine(temporaryDirectory, "extract");

        try
        {
            await DownloadFileAsync(installerUrl, installerPath, cancellationToken);
            Directory.CreateDirectory(extractPath);
            var startInfo = new ProcessStartInfo("msiexec.exe") { UseShellExecute = false, CreateNoWindow = true };
            startInfo.ArgumentList.Add("/a");
            startInfo.ArgumentList.Add(installerPath);
            startInfo.ArgumentList.Add("/qn");
            startInfo.ArgumentList.Add("/norestart");
            startInfo.ArgumentList.Add($"TARGETDIR={extractPath}");
            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Не удалось запустить распаковку LibreOffice.");
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode is not 0 and not 3010) throw new InvalidOperationException($"Распаковка LibreOffice завершилась с кодом {process.ExitCode}.");
            if (Directory.GetFiles(extractPath, "soffice.exe", SearchOption.AllDirectories).Length == 0) throw new InvalidOperationException("В распакованном LibreOffice не найден soffice.exe.");
            ReplaceDirectory(extractPath, Path.Combine(ManagedToolsDirectory, "LibreOffice"));
            _report("LibreOffice готов к работе.");
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
