using Microsoft.Win32;

namespace RenameConv.Services;

internal static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RenameConv";

    public static void Configure(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true) ?? throw new InvalidOperationException("Не удалось открыть раздел автозагрузки Windows.");
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Application.ExecutablePath}\" --autostart", RegistryValueKind.String);
            return;
        }

        key.DeleteValue(ValueName, false);
    }
}
