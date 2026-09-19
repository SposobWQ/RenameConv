using System.Text.Json;

namespace RenameConv;

internal sealed class AppSettings
{
    public bool WatchAllDrives { get; set; } = true;
    public List<string> WatchedFolders { get; set; } = [];
    public bool CheckForUpdatesOnStartup { get; set; }
    public bool AutoUpdateDependencies { get; set; }
    public DateTime? LastDependencyUpdateUtc { get; set; }
    public bool StartWithWindows { get; set; }
    public string LibreOfficePath { get; set; } = string.Empty;
    public string Language { get; set; } = "ru";
    public string Theme { get; set; } = "system";
    public string TrayIconColor { get; set; } = "blue";

    public AppSettings Copy() => new()
    {
        WatchAllDrives = WatchAllDrives,
        WatchedFolders = [.. WatchedFolders],
        CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
        AutoUpdateDependencies = AutoUpdateDependencies,
        LastDependencyUpdateUtc = LastDependencyUpdateUtc,
        StartWithWindows = StartWithWindows,
        LibreOfficePath = LibreOfficePath,
        Language = Language,
        Theme = Theme,
        TrayIconColor = TrayIconColor
    };
}

internal static class SettingsManager
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenameConv");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            return Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings());
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var normalized = Normalize(settings);
        Directory.CreateDirectory(SettingsDirectory);
        var temporaryPath = $"{SettingsPath}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(normalized, SerializerOptions));
        File.Move(temporaryPath, SettingsPath, true);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var folders = new List<string>();
        foreach (var path in settings.WatchedFolders ?? [])
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try { folders.Add(Path.GetFullPath(path)); }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
        }
        settings.WatchedFolders = folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        settings.Language = string.Equals(settings.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";
        settings.Theme = settings.Theme?.ToLowerInvariant() is "light" or "dark" ? settings.Theme.ToLowerInvariant() : "system";
        settings.TrayIconColor = settings.TrayIconColor?.ToLowerInvariant() is "violet" or "green" or "orange" ? settings.TrayIconColor.ToLowerInvariant() : "blue";
        settings.LibreOfficePath = string.IsNullOrWhiteSpace(settings.LibreOfficePath) ? string.Empty : settings.LibreOfficePath.Trim();
        return settings;
    }
}
