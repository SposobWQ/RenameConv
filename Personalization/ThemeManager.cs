using Microsoft.Win32;
using System.Windows.Forms;

namespace RenameConv.Personalization;

internal static class ThemeManager
{
    public static bool IsDark(string theme) => theme == "dark" || (theme == "system" && IsSystemDark());

    public static void Apply(Control control, string theme)
    {
        var dark = IsDark(theme);
        Apply(control, dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control, dark ? Color.Gainsboro : SystemColors.ControlText, dark ? Color.FromArgb(48, 48, 48) : SystemColors.Window);
    }

    public static void Apply(ToolStrip toolStrip, string theme)
    {
        var dark = IsDark(theme);
        toolStrip.BackColor = dark ? Color.FromArgb(38, 38, 38) : SystemColors.Control;
        toolStrip.ForeColor = dark ? Color.Gainsboro : SystemColors.ControlText;
        foreach (ToolStripItem item in toolStrip.Items)
        {
            item.BackColor = toolStrip.BackColor;
            item.ForeColor = toolStrip.ForeColor;
        }
    }

    private static void Apply(Control control, Color background, Color foreground, Color inputBackground)
    {
        control.BackColor = control is TextBoxBase or ListBox or ComboBox ? inputBackground : background;
        control.ForeColor = foreground;
        foreach (Control child in control.Controls) Apply(child, background, foreground, inputBackground);
    }

    private static bool IsSystemDark()
    {
        try
        {
            var value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            return value is int lightTheme && lightTheme == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
