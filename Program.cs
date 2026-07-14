using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

var explicitRoots = args.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
var watchAllDrives = explicitRoots.Length == 0;
var ffmpegPath = ResolveTool("FFMPEG_PATH", "ffmpeg.exe");
var ffprobePath = ResolveTool("FFPROBE_PATH", "ffprobe.exe");
var officePath = ResolveOffice();
var activity = "Ожидание переименования";
var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RenameConv");
Directory.CreateDirectory(logDirectory);
var logPath = Path.Combine(logDirectory, "RenameConv.log");
var logLock = new object();

if (!watchAllDrives && explicitRoots.Any(path => !Directory.Exists(path)))
{
    MessageBox.Show("Одна из папок для наблюдения не найдена.", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Error);
    return 1;
}

if (ffmpegPath is null || ffprobePath is null)
{
    MessageBox.Show("Не найдены ffmpeg.exe и ffprobe.exe.\nПоместите папку ffmpeg\\bin рядом с программой или задайте FFMPEG_PATH/FFPROBE_PATH.", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Error);
    return 1;
}

void Report(string message)
{
    lock (logLock)
    {
        activity = message;
        File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}", Encoding.UTF8);
    }
}

Report(watchAllDrives ? "Запуск. Наблюдение за всеми доступными дисками." : $"Запуск. Наблюдаемые папки: {string.Join(", ", explicitRoots)}");

var queue = new BlockingCollection<ConversionRequest>();
var seen = new ConcurrentDictionary<string, DateTime>();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); queue.CompleteAdding(); };

var worker = Task.Run(async () =>
{
    foreach (var request in queue.GetConsumingEnumerable(cancellation.Token))
    {
        try { await ConvertAsync(request, ffmpegPath, ffprobePath, officePath, Report, cancellation.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Report($"Ошибка {request.TargetPath}: {ex.Message}"); }
    }
}, cancellation.Token);

var watchers = new Dictionary<string, FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);
var watchersLock = new object();

void QueueRenamed(object? _, RenamedEventArgs e)
{
    if (Directory.Exists(e.FullPath) || IsTemporary(e.Name ?? string.Empty)) return;
    var sourceExtension = Path.GetExtension(e.OldName);
    var targetExtension = Path.GetExtension(e.Name);
    if (string.IsNullOrWhiteSpace(sourceExtension) || string.IsNullOrWhiteSpace(targetExtension) ||
        sourceExtension.Equals(targetExtension, StringComparison.OrdinalIgnoreCase)) return;

    // Проводник уже изменил имя. На этом этапе исходный файл — e.FullPath.
    // Старое расширение сохраняем только как подсказку; формат проверяет ffprobe.
    var key = e.FullPath.ToUpperInvariant();
    if (seen.TryGetValue(key, out var last) && DateTime.UtcNow - last < TimeSpan.FromSeconds(2)) return;
    seen[key] = DateTime.UtcNow;
    try
    {
        queue.Add(new ConversionRequest(e.FullPath, sourceExtension, targetExtension));
        Report($"В очереди: {e.OldName} → {e.Name}");
    }
    catch (InvalidOperationException) { }
}

void AddWatcher(string root)
{
    lock (watchersLock)
    {
        if (watchers.ContainsKey(root) || !Directory.Exists(root)) return;
        try
        {
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName,
                InternalBufferSize = 64 * 1024,
                EnableRaisingEvents = true
            };
            watcher.Renamed += QueueRenamed;
            watcher.Error += (_, e) => Report($"Наблюдение за {root}: {e.GetException().Message}");
            watchers.Add(root, watcher);
            Report($"Наблюдение включено: {root}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Report($"Не удалось наблюдать {root}: {ex.Message}");
        }
    }
}

void RefreshWatchers()
{
    var roots = watchAllDrives
        ? DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network).Select(d => d.RootDirectory.FullName)
        : explicitRoots;
    foreach (var root in roots) AddWatcher(root);
}

RefreshWatchers();

Application.EnableVisualStyles();
using var notifyIcon = new NotifyIcon
{
    Icon = SystemIcons.Application,
    Text = "RenameConv — фоновый конвертер",
    Visible = true,
    ContextMenuStrip = new ContextMenuStrip()
};
notifyIcon.ContextMenuStrip.Items.Add("Статус", null, (_, _) => MessageBox.Show(activity, "RenameConv"));
notifyIcon.ContextMenuStrip.Items.Add("Открыть журнал", null, (_, _) => Process.Start(new ProcessStartInfo(logDirectory) { UseShellExecute = true }));
notifyIcon.ContextMenuStrip.Items.Add("Выход", null, (_, _) =>
{
    queue.CompleteAdding();
    cancellation.Cancel();
    Application.Exit();
});
notifyIcon.DoubleClick += (_, _) => MessageBox.Show(activity, "RenameConv");
using var driveTimer = new System.Windows.Forms.Timer { Interval = 10_000 };
driveTimer.Tick += (_, _) => { if (watchAllDrives) RefreshWatchers(); };
driveTimer.Start();
Application.Run();

driveTimer.Stop();
notifyIcon.Visible = false;
lock (watchersLock)
{
    foreach (var watcher in watchers.Values) watcher.Dispose();
    watchers.Clear();
}
await worker;
return 0;

static bool IsTemporary(string name) => name.Contains(".__renameconv__", StringComparison.OrdinalIgnoreCase);

static async Task ConvertAsync(ConversionRequest request, string ffmpeg, string ffprobe, string? office, Action<string> report, CancellationToken token)
{
    await WaitForFileAsync(request.TargetPath, token);

    if (IsDocumentExtension(request.SourceExtension) || IsDocumentExtension(request.TargetExtension))
    {
        await ConvertDocumentAsync(request, office, report, token);
        return;
    }

    var probe = await RunAsync(ffprobe, $"-v error -show_entries format=format_name -of default=nw=1:nk=1 {Quote(request.TargetPath)}", token);
    if (probe.ExitCode != 0 || string.IsNullOrWhiteSpace(probe.Output))
    {
        report($"Пропущено (не медиа): {Path.GetFileName(request.TargetPath)}");
        return;
    }

    if (!TryGetTargetProfile(request.TargetExtension, out var profile))
    {
        // FFmpeg знает больше контейнеров, чем наш список безопасных пресетов.
        // Для редкого расширения даём ему самому выбрать допустимые кодеки.
        profile = new EncodingProfile("-map 0", false);
        report($"Редкий формат {request.TargetExtension}: используется автоматический профиль FFmpeg");
    }

    var temporaryPath = Path.Combine(
        Path.GetDirectoryName(request.TargetPath)!,
        $"{Path.GetFileNameWithoutExtension(request.TargetPath)}.__renameconv__{request.TargetExtension}");

    // Сначала пробуем быструю перепаковку без потери качества. При несовместимости повторяем с перекодированием.
    var copySucceeded = false;
    if (profile.TryStreamCopy)
    {
        var copy = await RunAsync(ffmpeg, $"-y -i {Quote(request.TargetPath)} -map 0 -c copy {Quote(temporaryPath)}", token);
        copySucceeded = copy.ExitCode == 0;
    }

    if (!copySucceeded)
    {
        DeleteIfExists(temporaryPath);
        var encode = await RunAsync(ffmpeg, $"-y -i {Quote(request.TargetPath)} {profile.EncodingArguments} {Quote(temporaryPath)}", token);
        if (encode.ExitCode != 0)
        {
            DeleteIfExists(temporaryPath);
            report($"Конвертация не удалась: {Path.GetFileName(request.TargetPath)}");
            return;
        }
    }

    File.Move(temporaryPath, request.TargetPath, true);
    report($"Готово: {Path.GetFileName(request.TargetPath)} ({probe.Output.Trim()})");
}

static async Task ConvertDocumentAsync(ConversionRequest request, string? office, Action<string> report, CancellationToken token)
{
    if (office is null)
    {
        report($"Пропущено: для документов требуется LibreOffice: {Path.GetFileName(request.TargetPath)}");
        return;
    }

    if (!IsDocumentExtension(request.TargetExtension))
    {
        report($"Пропущено: неподдерживаемый целевой формат документа {request.TargetExtension}");
        return;
    }

    var folder = Path.GetDirectoryName(request.TargetPath)!;
    var name = Path.GetFileNameWithoutExtension(request.TargetPath);
    var sourceCopy = Path.Combine(folder, $"{name}.__renameconv_input__{request.SourceExtension}");
    var officeOutput = Path.Combine(folder, $"{name}.__renameconv_input__{request.TargetExtension}");

    try
    {
        File.Copy(request.TargetPath, sourceCopy, true);
        var result = await RunAsync(office, $"--headless --convert-to {request.TargetExtension.TrimStart('.')} --outdir {Quote(folder)} {Quote(sourceCopy)}", token);
        if (result.ExitCode != 0 || !File.Exists(officeOutput))
        {
            report($"Не удалось конвертировать документ: {Path.GetFileName(request.TargetPath)}");
            return;
        }
        File.Move(officeOutput, request.TargetPath, true);
        report($"Готово: {Path.GetFileName(request.TargetPath)} (LibreOffice)");
    }
    finally
    {
        DeleteIfExists(sourceCopy);
        DeleteIfExists(officeOutput);
    }
}

static string? ResolveTool(string environmentVariable, string executableName)
{
    var configured = Environment.GetEnvironmentVariable(environmentVariable);
    if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

    foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(root);
        while (directory is not null)
        {
            foreach (var relative in new[]
            {
                executableName,
                Path.Combine("tools", "ffmpeg", "bin", executableName),
                Path.Combine("ffmpeg", "bin", executableName) // совместимость со старой структурой
            })
            {
                var candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }
            directory = directory.Parent;
        }
    }
    return null;
}

static string? ResolveOffice()
{
    var configured = Environment.GetEnvironmentVariable("LIBREOFFICE_PATH");
    if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

    // Переносимый вариант, упакованный рядом с RenameConv.exe.
    foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(root);
        while (directory is not null)
        {
            foreach (var relative in new[]
            {
                Path.Combine("tools", "LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe"),
                Path.Combine("LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe")
            })
            {
                var candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }
            directory = directory.Parent;
        }
    }

    foreach (var candidate in new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe")
    })
    {
        if (File.Exists(candidate)) return candidate;
    }
    return null;
}

static bool IsDocumentExtension(string extension) => extension.ToLowerInvariant() is
    ".pdf" or ".doc" or ".docx" or ".odt" or ".rtf" or ".txt" or ".html" or ".htm" or
    ".xls" or ".xlsx" or ".ods" or ".csv" or ".ppt" or ".pptx" or ".odp";

static bool TryGetTargetProfile(string extension, out EncodingProfile profile)
{
    var profiles = new Dictionary<string, EncodingProfile>(StringComparer.OrdinalIgnoreCase)
    {
        // Видео
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

        // Аудио
        [".mp3"] = new("-map 0:a:0 -vn -c:a libmp3lame -q:a 2", true),
        [".wav"] = new("-map 0:a:0 -vn -c:a pcm_s16le", true),
        [".flac"] = new("-map 0:a:0 -vn -c:a flac", true),
        [".m4a"] = new("-map 0:a:0 -vn -c:a aac -b:a 192k", true),
        [".aac"] = new("-map 0:a:0 -vn -c:a aac -b:a 192k", true),
        [".ogg"] = new("-map 0:a:0 -vn -c:a libvorbis -q:a 6", true),
        [".opus"] = new("-map 0:a:0 -vn -c:a libopus -b:a 160k", true),
        [".wma"] = new("-map 0:a:0 -vn -c:a wmav2 -b:a 192k", true),
        [".aiff"] = new("-map 0:a:0 -vn -c:a pcm_s16be", true),

        // Изображения
        [".jpg"] = new("-map 0:v:0 -frames:v 1 -q:v 2", false),
        [".jpeg"] = new("-map 0:v:0 -frames:v 1 -q:v 2", false),
        [".png"] = new("-map 0:v:0 -frames:v 1 -c:v png", false),
        [".webp"] = new("-map 0:v:0 -frames:v 1 -c:v libwebp -q:v 80", false),
        [".bmp"] = new("-map 0:v:0 -frames:v 1 -c:v bmp", false),
        [".tif"] = new("-map 0:v:0 -frames:v 1 -c:v tiff", false),
        [".tiff"] = new("-map 0:v:0 -frames:v 1 -c:v tiff", false),
        [".gif"] = new("-map 0:v:0 -vf fps=15 -loop 0", false)
    };
    return profiles.TryGetValue(extension, out profile!);
}

static async Task WaitForFileAsync(string path, CancellationToken token)
{
    for (var i = 0; i < 20; i++)
    {
        token.ThrowIfCancellationRequested();
        try { using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None); return; }
        catch (IOException) { await Task.Delay(250, token); }
    }
    throw new IOException("Файл всё ещё занят другой программой.");
}

static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string arguments, CancellationToken token)
{
    using var process = new Process { StartInfo = new ProcessStartInfo(fileName, arguments) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true } };
    try { process.Start(); }
    catch (System.ComponentModel.Win32Exception ex) { throw new InvalidOperationException($"Не найден {fileName}. Установите FFmpeg или задайте путь через переменную окружения.", ex); }
    var output = process.StandardOutput.ReadToEndAsync();
    var errors = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync(token);
    return (process.ExitCode, (await output) + (await errors));
}

static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
static void DeleteIfExists(string path) { if (File.Exists(path)) File.Delete(path); }

record ConversionRequest(string TargetPath, string SourceExtension, string TargetExtension);
record EncodingProfile(string EncodingArguments, bool TryStreamCopy);
