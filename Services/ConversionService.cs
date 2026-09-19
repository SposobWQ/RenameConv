using System.ComponentModel;
using System.Diagnostics;
using RenameConv.Dependencies;
using RenameConv.Models;

namespace RenameConv.Services;

internal sealed class ConversionService
{
    private static readonly IReadOnlyDictionary<string, EncodingProfile> Profiles = new Dictionary<string, EncodingProfile>(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = new("-c:v libx264 -crf 20 -preset veryfast -c:a aac -b:a 192k -movflags +faststart", true),
        [".m4v"] = new("-c:v libx264 -crf 20 -preset veryfast -c:a aac -b:a 192k -movflags +faststart", true),
        [".mkv"] = new("-c:v libx264 -crf 20 -preset veryfast -c:a aac -b:a 192k", true),
        [".mov"] = new("-c:v libx264 -crf 20 -preset veryfast -c:a aac -b:a 192k -movflags +faststart", true),
        [".avi"] = new("-c:v libx264 -crf 20 -preset veryfast -c:a libmp3lame -b:a 192k", true),
        [".webm"] = new("-c:v libvpx-vp9 -crf 32 -b:v 0 -row-mt 1 -c:a libopus -b:a 128k", true),
        [".flv"] = new("-c:v libx264 -crf 20 -preset veryfast -c:a aac -b:a 128k", true),
        [".wmv"] = new("-c:v wmv2 -b:v 2500k -c:a wmav2 -b:a 192k", true),
        [".mpg"] = new("-c:v mpeg2video -q:v 3 -c:a mp2 -b:a 192k", true),
        [".mpeg"] = new("-c:v mpeg2video -q:v 3 -c:a mp2 -b:a 192k", true),
        [".3gp"] = new("-c:v h263 -c:a aac -b:a 96k", true),
        [".ogv"] = new("-c:v libtheora -q:v 7 -c:a libvorbis -q:a 5", true),
        [".mp3"] = new("-map 0:a:0 -vn -c:a libmp3lame -q:a 2", true),
        [".wav"] = new("-map 0:a:0 -vn -c:a pcm_s16le", true),
        [".flac"] = new("-map 0:a:0 -vn -c:a flac", true),
        [".m4a"] = new("-map 0:a:0 -vn -c:a aac -b:a 192k", true),
        [".aac"] = new("-map 0:a:0 -vn -c:a aac -b:a 192k", true),
        [".ogg"] = new("-map 0:a:0 -vn -c:a libvorbis -q:a 6", true),
        [".opus"] = new("-map 0:a:0 -vn -c:a libopus -b:a 160k", true),
        [".wma"] = new("-map 0:a:0 -vn -c:a wmav2 -b:a 192k", true),
        [".aiff"] = new("-map 0:a:0 -vn -c:a pcm_s16be", true),
        [".jpg"] = new("-map 0:v:0 -frames:v 1 -q:v 2", false),
        [".jpeg"] = new("-map 0:v:0 -frames:v 1 -q:v 2", false),
        [".png"] = new("-map 0:v:0 -frames:v 1 -c:v png", false),
        [".webp"] = new("-map 0:v:0 -frames:v 1 -c:v libwebp -q:v 80", false),
        [".bmp"] = new("-map 0:v:0 -frames:v 1 -c:v bmp", false),
        [".tif"] = new("-map 0:v:0 -frames:v 1 -c:v tiff", false),
        [".tiff"] = new("-map 0:v:0 -frames:v 1 -c:v tiff", false),
        [".gif"] = new("-map 0:v:0 -vf fps=15 -loop 0", false)
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".odt", ".rtf", ".txt", ".html", ".htm", ".xls", ".xlsx", ".ods", ".csv", ".ppt", ".pptx", ".odp"
    };

    private readonly string _ffmpeg;
    private readonly string _ffprobe;
    private string? _office;
    private readonly DependencyManager _dependencies;
    private readonly Action<string> _report;

    public ConversionService(string ffmpeg, string ffprobe, string? office, DependencyManager dependencies, Action<string> report)
    {
        _ffmpeg = ffmpeg;
        _ffprobe = ffprobe;
        _office = office;
        _dependencies = dependencies;
        _report = report;
    }

    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        await WaitForFileAsync(request.TargetPath, cancellationToken);
        if (IsDocument(request.SourceExtension) || IsDocument(request.TargetExtension))
        {
            await ConvertDocumentAsync(request, cancellationToken);
            return;
        }

        var probe = await RunAsync(_ffprobe, $"-v error -show_entries format=format_name -of default=nw=1:nk=1 {Quote(request.TargetPath)}", cancellationToken);
        if (probe.ExitCode != 0 || string.IsNullOrWhiteSpace(probe.Output))
        {
            _report($"Пропущено (не медиа): {Path.GetFileName(request.TargetPath)}");
            return;
        }

        if (!Profiles.TryGetValue(request.TargetExtension, out var profile))
        {
            profile = new EncodingProfile("-map 0", false);
            _report($"Редкий формат {request.TargetExtension}: используется автоматический профиль FFmpeg");
        }

        var temporaryPath = Path.Combine(Path.GetDirectoryName(request.TargetPath)!, $"{Path.GetFileNameWithoutExtension(request.TargetPath)}.__renameconv__{request.TargetExtension}");
        var copied = false;
        if (profile.TryStreamCopy)
        {
            var copy = await RunAsync(_ffmpeg, $"-y -i {Quote(request.TargetPath)} -map 0 -c copy {Quote(temporaryPath)}", cancellationToken);
            copied = copy.ExitCode == 0;
        }

        if (!copied)
        {
            DeleteIfExists(temporaryPath);
            var encode = await RunAsync(_ffmpeg, $"-y -i {Quote(request.TargetPath)} {profile.EncodingArguments} {Quote(temporaryPath)}", cancellationToken);
            if (encode.ExitCode != 0)
            {
                DeleteIfExists(temporaryPath);
                _report($"Конвертация не удалась: {Path.GetFileName(request.TargetPath)}");
                return;
            }
        }

        File.Move(temporaryPath, request.TargetPath, true);
        _report($"Готово: {Path.GetFileName(request.TargetPath)} ({probe.Output.Trim()})");
    }

    private async Task ConvertDocumentAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        if (_office is null)
        {
            try
            {
                _office = await _dependencies.EnsureLibreOfficeAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
            {
                _report($"Не удалось загрузить LibreOffice: {ex.Message}");
                return;
            }
            if (_office is null)
            {
                _report($"Не удалось подготовить LibreOffice: {Path.GetFileName(request.TargetPath)}");
                return;
            }
        }

        if (!IsDocument(request.TargetExtension))
        {
            _report($"Пропущено: неподдерживаемый целевой формат документа {request.TargetExtension}");
            return;
        }

        var folder = Path.GetDirectoryName(request.TargetPath)!;
        var name = Path.GetFileNameWithoutExtension(request.TargetPath);
        var sourceCopy = Path.Combine(folder, $"{name}.__renameconv_input__{request.SourceExtension}");
        var officeOutput = Path.Combine(folder, $"{name}.__renameconv_input__{request.TargetExtension}");

        try
        {
            File.Copy(request.TargetPath, sourceCopy, true);
            var result = await RunAsync(_office, $"--headless --convert-to {request.TargetExtension.TrimStart('.')} --outdir {Quote(folder)} {Quote(sourceCopy)}", cancellationToken);
            if (result.ExitCode != 0 || !File.Exists(officeOutput))
            {
                _report($"Не удалось конвертировать документ: {Path.GetFileName(request.TargetPath)}");
                return;
            }

            File.Move(officeOutput, request.TargetPath, true);
            _report($"Готово: {Path.GetFileName(request.TargetPath)} (LibreOffice)");
        }
        finally
        {
            DeleteIfExists(sourceCopy);
            DeleteIfExists(officeOutput);
        }
    }

    private static bool IsDocument(string extension) => DocumentExtensions.Contains(extension);

    private static async Task WaitForFileAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(250, cancellationToken);
            }
        }

        throw new IOException("Файл всё ещё занят другой программой.");
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        try { process.Start(); }
        catch (Win32Exception ex) { throw new InvalidOperationException($"Не найден {fileName}.", ex); }

        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            throw;
        }

        return (process.ExitCode, (await output) + (await errors));
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
    private static void DeleteIfExists(string path) { if (File.Exists(path)) File.Delete(path); }
}
