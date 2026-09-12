using System.Reflection;
using System.Windows.Forms;
using RenameConv.Localization;
using RenameConv.Personalization;

namespace RenameConv;

internal sealed class SettingsDialog : Form
{
    private readonly Localizer _localizer;
    private readonly CheckBox _watchAllDrives = new() { AutoSize = true };
    private readonly Label _foldersLabel = new() { AutoSize = true };
    private readonly ListBox _folders = new() { IntegralHeight = false };
    private readonly Button _addFolder = new();
    private readonly Button _removeFolder = new();
    private readonly CheckBox _checkForUpdates = new() { AutoSize = true };
    private readonly CheckBox _autoUpdateDependencies = new() { AutoSize = true };
    private readonly Label _languageLabel = new() { AutoSize = true };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _themeLabel = new() { AutoSize = true };
    private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _iconColorLabel = new() { AutoSize = true };
    private readonly ComboBox _iconColor = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _version = new() { AutoSize = true };
    private readonly Button _save = new();
    private readonly Button _cancel = new() { DialogResult = DialogResult.Cancel };

    public AppSettings Settings { get; private set; }

    public SettingsDialog(AppSettings currentSettings)
    {
        Settings = currentSettings.Copy();
        _localizer = new Localizer(Settings.Language);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 530);

        _watchAllDrives.Location = new Point(18, 18);
        _watchAllDrives.Checked = Settings.WatchAllDrives;
        _watchAllDrives.CheckedChanged += (_, _) => UpdateFolderControls();
        _foldersLabel.Location = new Point(18, 55);
        _folders.Location = new Point(18, 80);
        _folders.Size = new Size(405, 170);
        _folders.SelectionMode = SelectionMode.MultiExtended;
        _folders.Items.AddRange(Settings.WatchedFolders.Cast<object>().ToArray());

        _addFolder.Location = new Point(435, 80);
        _addFolder.Size = new Size(108, 32);
        _addFolder.Click += AddFolder;
        _removeFolder.Location = new Point(435, 120);
        _removeFolder.Size = new Size(108, 32);
        _removeFolder.Click += RemoveFolders;

        _checkForUpdates.Location = new Point(18, 266);
        _checkForUpdates.Checked = Settings.CheckForUpdatesOnStartup;
        _autoUpdateDependencies.Location = new Point(18, 296);
        _autoUpdateDependencies.Checked = Settings.AutoUpdateDependencies;

        _languageLabel.Location = new Point(18, 336);
        _language.Location = new Point(175, 332);
        _language.Size = new Size(130, 30);
        _language.SelectedIndexChanged += (_, _) => ChangeLanguage();
        _themeLabel.Location = new Point(18, 376);
        _theme.Location = new Point(175, 372);
        _theme.Size = new Size(180, 30);
        _theme.SelectedIndexChanged += (_, _) => ThemeManager.Apply(this, GetThemeValue());
        _iconColorLabel.Location = new Point(18, 416);
        _iconColor.Location = new Point(175, 412);
        _iconColor.Size = new Size(180, 30);

        _version.Location = new Point(18, 466);
        _save.Location = new Point(370, 474);
        _save.Size = new Size(84, 34);
        _save.Click += Save;
        _cancel.Location = new Point(462, 474);
        _cancel.Size = new Size(82, 34);

        Controls.AddRange([_watchAllDrives, _foldersLabel, _folders, _addFolder, _removeFolder, _checkForUpdates, _autoUpdateDependencies, _languageLabel, _language, _themeLabel, _theme, _iconColorLabel, _iconColor, _version, _save, _cancel]);
        AcceptButton = _save;
        CancelButton = _cancel;
        RefreshLocalizedChoices();
        _language.SelectedIndex = Settings.Language == "en" ? 1 : 0;
        _theme.SelectedIndex = Settings.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        _iconColor.SelectedIndex = Settings.TrayIconColor switch { "violet" => 1, "green" => 2, "orange" => 3, _ => 0 };
        ApplyLocalization();
        UpdateFolderControls();
        ThemeManager.Apply(this, GetThemeValue());
    }

    private void AddFolder(object? sender, EventArgs eventArgs)
    {
        using var dialog = new FolderBrowserDialog { Description = _localizer["ChooseFolder"] };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var folder = Path.GetFullPath(dialog.SelectedPath);
        if (!_folders.Items.Cast<string>().Contains(folder, StringComparer.OrdinalIgnoreCase)) _folders.Items.Add(folder);
    }

    private void RemoveFolders(object? sender, EventArgs eventArgs)
    {
        foreach (var item in _folders.SelectedItems.Cast<object>().ToArray()) _folders.Items.Remove(item);
    }

    private void ChangeLanguage()
    {
        if (_language.SelectedIndex < 0) return;
        var themeIndex = _theme.SelectedIndex;
        var colorIndex = _iconColor.SelectedIndex;
        _localizer.SetLanguage(_language.SelectedIndex == 1 ? "en" : "ru");
        RefreshLocalizedChoices();
        _theme.SelectedIndex = themeIndex < 0 ? 0 : themeIndex;
        _iconColor.SelectedIndex = colorIndex < 0 ? 0 : colorIndex;
        ApplyLocalization();
    }

    private void Save(object? sender, EventArgs eventArgs)
    {
        var folders = _folders.Items.Cast<string>().ToList();
        if (!_watchAllDrives.Checked && folders.Count == 0)
        {
            MessageBox.Show(this, _localizer["FolderRequired"], "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Settings = new AppSettings
        {
            WatchAllDrives = _watchAllDrives.Checked,
            WatchedFolders = folders,
            CheckForUpdatesOnStartup = _checkForUpdates.Checked,
            AutoUpdateDependencies = _autoUpdateDependencies.Checked,
            Language = _language.SelectedIndex == 1 ? "en" : "ru",
            Theme = GetThemeValue(),
            TrayIconColor = GetIconColorValue()
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void RefreshLocalizedChoices()
    {
        _language.Items.Clear();
        _language.Items.AddRange(["Русский", "English"]);
        _theme.Items.Clear();
        _theme.Items.AddRange([_localizer["ThemeSystem"], _localizer["ThemeLight"], _localizer["ThemeDark"]]);
        _iconColor.Items.Clear();
        _iconColor.Items.AddRange([_localizer["ColorBlue"], _localizer["ColorViolet"], _localizer["ColorGreen"], _localizer["ColorOrange"]]);
    }

    private void UpdateFolderControls()
    {
        var enabled = !_watchAllDrives.Checked;
        _folders.Enabled = enabled;
        _addFolder.Enabled = enabled;
        _removeFolder.Enabled = enabled;
    }

    private void ApplyLocalization()
    {
        Text = _localizer["SettingsTitle"];
        _watchAllDrives.Text = _localizer["WatchAllDrives"];
        _foldersLabel.Text = _localizer["WatchFolders"];
        _addFolder.Text = _localizer["AddFolder"];
        _removeFolder.Text = _localizer["Remove"];
        _checkForUpdates.Text = _localizer["CheckUpdatesAtStartup"];
        _autoUpdateDependencies.Text = _localizer["CheckDependenciesAtStartup"];
        _languageLabel.Text = _localizer["Language"];
        _themeLabel.Text = _localizer["Theme"];
        _iconColorLabel.Text = _localizer["TrayIconColor"];
        _version.Text = $"{_localizer["Version"]} {GetVersion()}";
        _save.Text = _localizer["Save"];
        _cancel.Text = _localizer["Cancel"];
    }

    private string GetThemeValue() => _theme.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };
    private string GetIconColorValue() => _iconColor.SelectedIndex switch { 1 => "violet", 2 => "green", 3 => "orange", _ => "blue" };
    private static string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0";
}
