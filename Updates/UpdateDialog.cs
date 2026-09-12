using System.Windows.Forms;
using RenameConv.Localization;
using RenameConv.Personalization;

namespace RenameConv.Updates;

internal sealed class UpdateDialog : Form
{
    private readonly UpdateRelease _release;
    private readonly UpdateManager _manager;
    private readonly Action _stopApplication;
    private readonly Localizer _localizer;
    private readonly Label _status = new() { AutoSize = true };
    private readonly ProgressBar _progress = new() { Visible = false };
    private readonly Button _install = new();
    private readonly Button _cancel = new() { DialogResult = DialogResult.Cancel };

    public UpdateDialog(UpdateRelease release, UpdateManager manager, Action stopApplication, Localizer localizer, string theme)
    {
        _release = release;
        _manager = manager;
        _stopApplication = stopApplication;
        _localizer = localizer;
        Text = _localizer["UpdateTitle"];
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(440, 188);

        var label = new Label
        {
            AutoSize = true,
            Location = new Point(18, 18),
            Text = string.Format(_localizer["UpdateAvailable"], _release.Version, GetCurrentVersion())
        };

        _status.Location = new Point(18, 76);
        _status.Text = string.IsNullOrWhiteSpace(_release.PackageUrl) ? _localizer["UpdatePackageMissing"] : _localizer["UpdateReady"];
        _progress.Location = new Point(18, 104);
        _progress.Size = new Size(404, 22);
        _install.Location = new Point(190, 140);
        _install.Size = new Size(142, 32);
        _install.Text = _localizer["DownloadInstall"];
        _install.Enabled = !string.IsNullOrWhiteSpace(_release.PackageUrl);
        _install.Click += InstallAsync;
        _cancel.Location = new Point(340, 140);
        _cancel.Size = new Size(82, 32);
        _cancel.Text = _localizer["Later"];
        Controls.AddRange([label, _status, _progress, _install, _cancel]);
        CancelButton = _cancel;
        ThemeManager.Apply(this, theme);
    }

    private async void InstallAsync(object? sender, EventArgs eventArgs)
    {
        _install.Enabled = false;
        _cancel.Enabled = false;
        _progress.Visible = true;
        _status.Text = _localizer["DownloadUpdate"];
        var progress = new Progress<int>(value =>
        {
            _progress.Value = value;
            _status.Text = string.Format(_localizer["DownloadingProgress"], value);
        });

        try
        {
            var packagePath = await _manager.DownloadAsync(_release, progress, CancellationToken.None);
            _status.Text = _localizer["StartUpdate"];
            _manager.StartUpdater(packagePath);
            Close();
            _stopApplication();
        }
        catch (Exception ex)
        {
            _status.Text = string.Format(_localizer["UpdateFailed"], ex.Message);
            _install.Enabled = true;
            _cancel.Enabled = true;
        }
    }

    private static string GetCurrentVersion() => typeof(UpdateDialog).Assembly.GetName().Version?.ToString(3) ?? "1.1.0";
}
