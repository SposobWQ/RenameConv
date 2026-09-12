using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RenameConv.Personalization;

internal static class TrayIconFactory
{
    public static Icon Create(string colorName, bool darkTheme)
    {
        var accent = colorName switch
        {
            "violet" => Color.FromArgb(139, 92, 246),
            "green" => Color.FromArgb(34, 197, 94),
            "orange" => Color.FromArgb(249, 115, 22),
            _ => Color.FromArgb(59, 130, 246)
        };
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var background = new SolidBrush(darkTheme ? Color.FromArgb(28, 28, 28) : Color.White);
        using var accentBrush = new SolidBrush(accent);
        using var arrowPen = new Pen(Color.White, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.FillEllipse(background, 1, 1, 30, 30);
        graphics.FillEllipse(accentBrush, 4, 4, 24, 24);
        graphics.DrawLine(arrowPen, 10, 16, 22, 16);
        graphics.DrawLine(arrowPen, 18, 11, 23, 16);
        graphics.DrawLine(arrowPen, 18, 21, 23, 16);
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
