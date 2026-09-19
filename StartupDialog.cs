using System.Windows.Forms;
using RenameConv.Localization;
using RenameConv.Personalization;

namespace RenameConv;

internal sealed class StartupDialog : Form
{
    public StartupDialog(Localizer localizer, string theme)
    {
        Text = localizer["StartupTitle"];
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(430, 156);

        var message = new Label
        {
            AutoSize = false,
            Location = new Point(18, 18),
            Size = new Size(394, 72),
            Text = localizer["StartupMessage"]
        };
        var confirm = new Button
        {
            Text = localizer["StartupConfirm"],
            DialogResult = DialogResult.OK,
            Location = new Point(310, 105),
            Size = new Size(102, 32)
        };
        Controls.AddRange([message, confirm]);
        AcceptButton = confirm;
        ThemeManager.Apply(this, theme);
    }
}
