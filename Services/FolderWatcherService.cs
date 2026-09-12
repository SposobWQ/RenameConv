using System.Collections.Concurrent;
using RenameConv.Models;

namespace RenameConv.Services;

internal sealed class FolderWatcherService : IDisposable
{
    private readonly BlockingCollection<ConversionRequest> _queue;
    private readonly Action<string> _report;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _seen = new();
    private readonly object _sync = new();
    private bool _watchAllDrives;
    private string[] _configuredRoots = [];
    private bool _disposed;

    public FolderWatcherService(BlockingCollection<ConversionRequest> queue, Action<string> report)
    {
        _queue = queue;
        _report = report;
    }

    public void Configure(bool watchAllDrives, IEnumerable<string> roots)
    {
        ThrowIfDisposed();
        lock (_sync)
        {
            _watchAllDrives = watchAllDrives;
            _configuredRoots = roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            DisposeWatchers();
        }
        Refresh();
    }

    public void Refresh()
    {
        ThrowIfDisposed();
        IEnumerable<string> roots;
        lock (_sync)
        {
            roots = _watchAllDrives
                ? DriveInfo.GetDrives()
                    .Where(drive => drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network)
                    .Select(drive => drive.RootDirectory.FullName)
                    .ToArray()
                : _configuredRoots.Where(Directory.Exists).ToArray();
        }

        foreach (var root in roots) AddWatcher(root);
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_sync)
        {
            if (_disposed) return;
            DisposeWatchers();
            _disposed = true;
        }
    }

    private void AddWatcher(string root)
    {
        lock (_sync)
        {
            if (_watchers.ContainsKey(root) || !Directory.Exists(root)) return;
            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName,
                    InternalBufferSize = 64 * 1024,
                    EnableRaisingEvents = true
                };
                watcher.Renamed += OnRenamed;
                watcher.Error += (_, eventArgs) => _report($"Наблюдение за {root}: {eventArgs.GetException()?.Message ?? "неизвестная ошибка"}");
                _watchers.Add(root, watcher);
                _report($"Наблюдение включено: {root}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _report($"Не удалось наблюдать {root}: {ex.Message}");
            }
        }
    }

    private void OnRenamed(object? sender, RenamedEventArgs eventArgs)
    {
        if (FileChangeFilter.ShouldIgnore(eventArgs.FullPath) || Directory.Exists(eventArgs.FullPath) || FileChangeFilter.IsRenameConvTemporary(eventArgs.Name ?? string.Empty)) return;

        var sourceExtension = Path.GetExtension(eventArgs.OldName);
        var targetExtension = Path.GetExtension(eventArgs.Name);
        if (string.IsNullOrWhiteSpace(sourceExtension) || string.IsNullOrWhiteSpace(targetExtension) || sourceExtension.Equals(targetExtension, StringComparison.OrdinalIgnoreCase)) return;

        var key = eventArgs.FullPath.ToUpperInvariant();
        if (_seen.TryGetValue(key, out var lastSeen) && DateTime.UtcNow - lastSeen < TimeSpan.FromSeconds(2)) return;
        _seen[key] = DateTime.UtcNow;

        try
        {
            _queue.Add(new ConversionRequest(eventArgs.FullPath, sourceExtension, targetExtension));
            _report($"В очереди: {eventArgs.OldName} → {eventArgs.Name}");
        }
        catch (InvalidOperationException) { }
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers.Values) watcher.Dispose();
        _watchers.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FolderWatcherService));
    }
}
