using RenameConv;
using System.Windows.Forms;

ApplicationConfiguration.Initialize();
using var singleInstance = new Mutex(true, "Local\\RenameConv.SingleInstance", out var isFirstInstance);
if (!isFirstInstance)
{
    if (!args.Contains("--autostart", StringComparer.OrdinalIgnoreCase))
    {
        MessageBox.Show("RenameConv уже запущен. Откройте его настройки через значок в системном трее.", "RenameConv", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    return 0;
}

using var application = new RenameConvApplication(args);
return await application.RunAsync();
