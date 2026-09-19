using Microsoft.Win32;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace RenameConv.Personalization;

internal static class ThemeManager
{
    private static readonly ConditionalWeakTable<Control, SelectionStyle> SelectionStyles = new();

    public static bool IsDark(string theme) => theme == "dark" || (theme == "system" && IsSystemDark());

    public static void Apply(Control control, string theme)
    {
        var dark = IsDark(theme);
        Apply(control, dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control, dark ? Color.Gainsboro : SystemColors.ControlText, dark ? Color.FromArgb(48, 48, 48) : SystemColors.Window);
    }

    public static void Apply(ToolStrip toolStrip, string theme)
    {
        var dark = IsDark(theme);
        var background = dark ? Color.FromArgb(38, 38, 38) : SystemColors.Control;
        var foreground = dark ? Color.Gainsboro : SystemColors.ControlText;
        toolStrip.BackColor = background;
        toolStrip.ForeColor = foreground;
        toolStrip.Renderer = new ToolStripProfessionalRenderer(new MenuColorTable(dark));
        if (toolStrip is ContextMenuStrip menu)
        {
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
        }
        foreach (ToolStripItem item in toolStrip.Items)
        {
            item.BackColor = background;
            item.ForeColor = foreground;
        }
    }

    private static void Apply(Control control, Color background, Color foreground, Color inputBackground)
    {
        control.BackColor = control is TextBoxBase or ListBox or ComboBox ? inputBackground : background;
        control.ForeColor = foreground;
        if (control is ListBox listBox) ConfigureListBox(listBox, background.R < 80);
        if (control is ComboBox comboBox) ConfigureComboBox(comboBox, background.R < 80);
        foreach (Control child in control.Controls) Apply(child, background, foreground, inputBackground);
    }

    private static void ConfigureListBox(ListBox listBox, bool dark)
    {
        var style = SelectionStyles.GetValue(listBox, _ => new SelectionStyle());
        style.IsDark = dark;
        if (style.IsAttached) return;
        style.IsAttached = true;
        listBox.DrawMode = DrawMode.OwnerDrawFixed;
        listBox.DrawItem += (_, eventArgs) => DrawItem(listBox, eventArgs, style);
    }

    private static void ConfigureComboBox(ComboBox comboBox, bool dark)
    {
        var style = SelectionStyles.GetValue(comboBox, _ => new SelectionStyle());
        style.IsDark = dark;
        if (style.IsAttached) return;
        style.IsAttached = true;
        comboBox.DrawMode = DrawMode.OwnerDrawFixed;
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.DrawItem += (_, eventArgs) => DrawItem(comboBox, eventArgs, style);
    }

    private static void DrawItem(Control control, DrawItemEventArgs eventArgs, SelectionStyle style)
    {
        var selected = (eventArgs.State & DrawItemState.Selected) != 0;
        var background = selected
            ? style.IsDark ? Color.FromArgb(61, 96, 156) : Color.FromArgb(0, 120, 215)
            : control.BackColor;
        var foreground = selected ? Color.White : control.ForeColor;
        using var brush = new SolidBrush(background);
        eventArgs.Graphics.FillRectangle(brush, eventArgs.Bounds);
        if (eventArgs.Index >= 0)
        {
            var text = control switch
            {
                ListBox listBox => listBox.GetItemText(listBox.Items[eventArgs.Index]),
                ComboBox comboBox => comboBox.GetItemText(comboBox.Items[eventArgs.Index]),
                _ => string.Empty
            };
            TextRenderer.DrawText(eventArgs.Graphics, text, control.Font, eventArgs.Bounds, foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
        eventArgs.DrawFocusRectangle();
    }

    private sealed class SelectionStyle
    {
        public bool IsDark { get; set; }
        public bool IsAttached { get; set; }
    }

    private sealed class MenuColorTable : ProfessionalColorTable
    {
        private readonly bool _dark;

        public MenuColorTable(bool dark)
        {
            _dark = dark;
        }

        public override Color ToolStripDropDownBackground => _dark ? Color.FromArgb(38, 38, 38) : SystemColors.Control;
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color MenuItemSelected => _dark ? Color.FromArgb(61, 96, 156) : Color.FromArgb(0, 120, 215);
        public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
        public override Color MenuItemBorder => MenuItemSelected;
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
