namespace RenameConv.Localization;

internal sealed class Localizer
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Texts = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["ru"] = new Dictionary<string, string>
        {
            ["Settings"] = "Настройки...",
            ["CheckUpdates"] = "Проверить обновления",
            ["CheckDependencies"] = "Проверить зависимости",
            ["Status"] = "Статус",
            ["OpenLog"] = "Открыть журнал",
            ["Exit"] = "Выход",
            ["SettingsTitle"] = "Настройки RenameConv",
            ["WatchAllDrives"] = "Наблюдать все доступные диски",
            ["WatchFolders"] = "Папки для наблюдения:",
            ["AddFolder"] = "Добавить папку...",
            ["Remove"] = "Удалить",
            ["CheckUpdatesAtStartup"] = "Проверять обновления RenameConv при запуске",
            ["CheckDependenciesAtStartup"] = "Автоматически обновлять зависимости при запуске",
            ["Language"] = "Язык интерфейса:",
            ["Theme"] = "Тема оформления:",
            ["ThemeSystem"] = "Как в Windows",
            ["ThemeLight"] = "Светлая",
            ["ThemeDark"] = "Тёмная",
            ["TrayIconColor"] = "Цвет значка в трее:",
            ["ColorBlue"] = "Синий",
            ["ColorViolet"] = "Фиолетовый",
            ["ColorGreen"] = "Зелёный",
            ["ColorOrange"] = "Оранжевый",
            ["Version"] = "Версия:",
            ["Save"] = "Сохранить",
            ["Cancel"] = "Отмена",
            ["ChooseFolder"] = "Выберите папку для наблюдения",
            ["FolderRequired"] = "Добавьте хотя бы одну папку или включите наблюдение за всеми дисками.",
            ["LatestVersion"] = "Установлена последняя версия.",
            ["UpdateTitle"] = "Обновление RenameConv",
            ["UpdateAvailable"] = "Доступна версия {0}.\nТекущая версия: {1}",
            ["UpdateReady"] = "Готово к загрузке обновления.",
            ["UpdatePackageMissing"] = "В релизе нет ZIP-пакета для автообновления.",
            ["DownloadingProgress"] = "Скачивание обновления: {0}%",
            ["UpdateFailed"] = "Ошибка обновления: {0}",
            ["DownloadInstall"] = "Скачать и установить",
            ["Later"] = "Позже",
            ["DownloadUpdate"] = "Скачивание обновления...",
            ["StartUpdate"] = "Запуск обновления...",
            ["DependencyTitle"] = "Зависимости RenameConv",
            ["UpdateDependencies"] = "Проверить и обновить",
            ["Ready"] = "Готово",
            ["TrayTooltip"] = "RenameConv — фоновый конвертер"
        },
        ["en"] = new Dictionary<string, string>
        {
            ["Settings"] = "Settings...",
            ["CheckUpdates"] = "Check for updates",
            ["CheckDependencies"] = "Check dependencies",
            ["Status"] = "Status",
            ["OpenLog"] = "Open log folder",
            ["Exit"] = "Exit",
            ["SettingsTitle"] = "RenameConv Settings",
            ["WatchAllDrives"] = "Watch all available drives",
            ["WatchFolders"] = "Folders to watch:",
            ["AddFolder"] = "Add folder...",
            ["Remove"] = "Remove",
            ["CheckUpdatesAtStartup"] = "Check RenameConv updates at startup",
            ["CheckDependenciesAtStartup"] = "Automatically update dependencies at startup",
            ["Language"] = "Interface language:",
            ["Theme"] = "Theme:",
            ["ThemeSystem"] = "Match Windows",
            ["ThemeLight"] = "Light",
            ["ThemeDark"] = "Dark",
            ["TrayIconColor"] = "Tray icon color:",
            ["ColorBlue"] = "Blue",
            ["ColorViolet"] = "Violet",
            ["ColorGreen"] = "Green",
            ["ColorOrange"] = "Orange",
            ["Version"] = "Version:",
            ["Save"] = "Save",
            ["Cancel"] = "Cancel",
            ["ChooseFolder"] = "Select a folder to watch",
            ["FolderRequired"] = "Add at least one folder or enable watching all drives.",
            ["LatestVersion"] = "You are using the latest version.",
            ["UpdateTitle"] = "RenameConv Update",
            ["UpdateAvailable"] = "Version {0} is available.\nCurrent version: {1}",
            ["UpdateReady"] = "Ready to download the update.",
            ["UpdatePackageMissing"] = "The release has no ZIP package for automatic update.",
            ["DownloadingProgress"] = "Downloading update: {0}%",
            ["UpdateFailed"] = "Update error: {0}",
            ["DownloadInstall"] = "Download and install",
            ["Later"] = "Later",
            ["DownloadUpdate"] = "Downloading update...",
            ["StartUpdate"] = "Starting update...",
            ["DependencyTitle"] = "RenameConv Dependencies",
            ["UpdateDependencies"] = "Check and update",
            ["Ready"] = "Ready",
            ["TrayTooltip"] = "RenameConv — background converter"
        }
    };

    public Localizer(string? language)
    {
        Language = NormalizeLanguage(language);
    }

    public string Language { get; private set; }

    public string this[string key] => Texts[Language].TryGetValue(key, out var value) ? value : key;

    public void SetLanguage(string? language)
    {
        Language = NormalizeLanguage(language);
    }

    public static string NormalizeLanguage(string? language) => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";
}
