using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using RenameConv.Models;
using RenameConv.Services;
using RenameConv.Updates;
using RenameConv.Localization;
using RenameConv.Personalization;
using RenameConv.Dependencies;

namespace RenameConv;

internal sealed class RenameConvApplication : IDisposable
{
    private readonly string[] _arguments;
    private readonly ActivityLog _log = new();
    private readonly BlockingCollection<ConversionRequest> _queue = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly UpdateManager _updates = new();
    private readonly DependencyManager _dependencies;
    private readonly Localizer _localizer;
    private AppSettings _settings;
    private FolderWatcherService? _watchers;
    private NotifyIcon? _notifyIcon;
    private Icon? _trayIcon;
    private System.Windows.Forms.Timer? _refreshTimer;
    private Task? _worker;
    private string[] _commandLineRoots = [];
    private bool _usesCommandLineRoots;
    private readonly bool _isAutoStartInvocation;
    private StartupDialog? _startupDialog;
    private int _stopping;

    public RenameConvApplication(string[] arguments)
    {
        _arguments = arguments;
        _isAutoStartInvocation = arguments.Contains("--autostart", StringComparer.OrdinalIgnoreCase);
        _settings = SettingsManager.Load();
        _localizer = new Localizer(_settings.Language);
        _dependencies = new DependencyManager(_log.Write);
    }

    public async Task<int> RunAsync()
    {
        if (!await InitializeAsync()) return 1;

        Application.Run();
        RequestStop();
        if (_worker is not null)
        {
            try { await _worker; }
            catch (OperationCanceledException) { }
        }

        return 0;
    }

    public void Dispose()
    {
        RequestStop();
        _refreshTimer?.Dispose();
        _notifyIcon?.Dispose();
        _trayIcon?.Dispose();
        _watchers?.Dispose();
        _cancellation.Dispose();
        _queue.Dispose();
    }

    private async Task<bool> InitializeAsync()
    {
        try
        {
            _commandLineRoots = _arguments.Where(argument => !argument.Equals("--autostart", StringComparison.OrdinalIgnoreCase)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (ArgumentException)
        {
            MessageBox.Show("Указан некорректный путь для наблюдения.", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _usesCommandLineRoots = _commandLineRoots.Length > 0;
        if (_usesCommandLineRoots && _commandLineRoots.Any(path => !Directory.Exists(path)))
        {
            MessageBox.Show("Одна из папок для наблюдения не найдена.", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        var ffmpeg = ToolLocator.FindFfmpeg();
        var ffprobe = ToolLocator.FindFfprobe();
        if (ffmpeg is null || ffprobe is null)
        {
            try
            {
                (ffmpeg, ffprobe) = await _dependencies.EnsureFfmpegAsync(_cancellation.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
            {
                MessageBox.Show($"Не удалось автоматически загрузить FFmpeg: {ex.Message}", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        var conversion = new ConversionService(ffmpeg, ffprobe, ToolLocator.FindLibreOffice(_settings.LibreOfficePath), _dependencies, _log.Write);
        _worker = Task.Run(() => ProcessQueueAsync(conversion, _cancellation.Token));
        _watchers = new FolderWatcherService(_queue, _log.Write);
        ApplyWatcherConfiguration();
        CreateTrayIcon();
        if (!_isAutoStartInvocation)
        {
            _startupDialog = new StartupDialog(_localizer, _settings.Theme);
            _startupDialog.FormClosed += (_, _) => _startupDialog?.Dispose();
            _startupDialog.Show();
        }
        try
        {
            AutoStartService.Configure(_settings.StartWithWindows);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
        {
            _log.Write($"Не удалось обновить автозапуск: {ex.Message}");
        }
        Console.CancelKeyPress += OnConsoleCancel;
        _log.Write(_usesCommandLineRoots ? $"Запуск. Наблюдаемые папки: {string.Join(", ", _commandLineRoots)}" : _settings.WatchAllDrives ? "Запуск. Наблюдение за всеми доступными дисками." : $"Запуск. Наблюдаемые папки: {string.Join(", ", _settings.WatchedFolders)}");
        return true;
    }

    private async Task ProcessQueueAsync(ConversionService conversion, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var request in _queue.GetConsumingEnumerable(cancellationToken))
            {
                try { await conversion.ConvertAsync(request, cancellationToken); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _log.Write($"Ошибка {request.TargetPath}: {ex.Message}"); }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void ApplyWatcherConfiguration()
    {
        if (_watchers is null) return;
        if (_usesCommandLineRoots)
        {
            _watchers.Configure(false, _commandLineRoots);
            return;
        }

        _watchers.Configure(_settings.WatchAllDrives, _settings.WatchedFolders);
    }

    private void CreateTrayIcon()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _notifyIcon?.Dispose();
        _trayIcon?.Dispose();
        var menu = new ContextMenuStrip();
        menu.Items.Add(_localizer["Settings"], null, (_, _) => ShowSettings());
        menu.Items.Add(_localizer["CheckUpdates"], null, async (_, _) => await CheckForUpdatesAsync());
        menu.Items.Add(_localizer["CheckDependencies"], null, async (_, _) => await CheckDependenciesAsync());
        menu.Items.Add(_localizer["Status"], null, (_, _) => MessageBox.Show(_log.LatestMessage, "RenameConv"));
        menu.Items.Add(_localizer["OpenLog"], null, (_, _) => Process.Start(new ProcessStartInfo(_log.DirectoryPath) { UseShellExecute = true }));
        menu.Items.Add(_localizer["Exit"], null, (_, _) => RequestStop());
        ThemeManager.Apply(menu, _settings.Theme);
        _trayIcon = TrayIconFactory.Create(_settings.TrayIconColor, ThemeManager.IsDark(_settings.Theme));

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = _localizer["TrayTooltip"],
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => MessageBox.Show(_log.LatestMessage, "RenameConv");

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 10_000 };
        _refreshTimer.Tick += (_, _) => _watchers?.Refresh();
        _refreshTimer.Start();

        if ((_settings.CheckForUpdatesOnStartup || ShouldCheckDependenciesAutomatically()) && !_usesCommandLineRoots)
        {
            EventHandler? checkOnIdle = null;
            checkOnIdle = async (_, _) =>
            {
                Application.Idle -= checkOnIdle;
                if (_settings.CheckForUpdatesOnStartup) await CheckForUpdatesAsync(false);
                if (ShouldCheckDependenciesAutomatically()) await CheckDependenciesAsync(false);
            };
            Application.Idle += checkOnIdle;
        }
    }

    private void ShowSettings()
    {
        using var dialog = new SettingsDialog(_settings);
        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            SettingsManager.Save(dialog.Settings);
            _settings = dialog.Settings;
            AutoStartService.Configure(_settings.StartWithWindows);
            _localizer.SetLanguage(_settings.Language);
            CreateTrayIcon();
            if (_usesCommandLineRoots)
            {
                _log.Write("Настройки сохранены. Параметры запуска с папками действуют до перезапуска.");
            }
            else
            {
                ApplyWatcherConfiguration();
                _log.Write(_settings.WatchAllDrives ? "Настройки применены. Наблюдение за всеми доступными дисками." : $"Настройки применены. Наблюдаемые папки: {string.Join(", ", _settings.WatchedFolders)}");
            }
        }
        catch (IOException ex)
        {
            MessageBox.Show($"Не удалось сохранить настройки: {ex.Message}", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show($"Не удалось сохранить настройки: {ex.Message}", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckForUpdatesAsync(bool showNoUpdateMessage = true)
    {
        try
        {
            var release = await _updates.FindAvailableUpdateAsync();
            if (release is null)
            {
                if (showNoUpdateMessage) MessageBox.Show("Установлена последняя версия.", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new UpdateDialog(release, _updates, RequestStop, _localizer, _settings.Theme);
            dialog.ShowDialog();
        }
        catch (HttpRequestException)
        {
            if (showNoUpdateMessage) MessageBox.Show("Не удалось подключиться к GitHub для проверки обновлений.", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (TaskCanceledException)
        {
            if (showNoUpdateMessage) MessageBox.Show("Проверка обновлений превысила время ожидания.", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (System.Text.Json.JsonException)
        {
            if (showNoUpdateMessage) MessageBox.Show("GitHub вернул некорректные данные релиза.", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task CheckDependenciesAsync(bool showResultMessage = true)
    {
        try
        {
            var results = await _dependencies.UpdateAllAsync(_cancellation.Token);
            var message = string.Join(Environment.NewLine, results.Select(result => result.Message));
            _log.Write(message);
            if (results.All(result => result.Changed))
            {
                _settings.LastDependencyUpdateUtc = DateTime.UtcNow;
                SettingsManager.Save(_settings);
            }
            if (showResultMessage) MessageBox.Show(message, "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (showResultMessage) MessageBox.Show($"Не удалось обновить зависимости: {ex.Message}", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private bool ShouldCheckDependenciesAutomatically()
    {
        if (!_settings.AutoUpdateDependencies) return false;
        return _settings.LastDependencyUpdateUtc is null || DateTime.UtcNow - _settings.LastDependencyUpdateUtc.Value >= TimeSpan.FromDays(7);
    }

    private void OnConsoleCancel(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        RequestStop();
    }

    private void RequestStop()
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0) return;
        _refreshTimer?.Stop();
        _notifyIcon?.Dispose();
        _watchers?.Dispose();
        _queue.CompleteAdding();
        _cancellation.Cancel();
        Application.Exit();
    }
}
