namespace RenameConv.Services;

internal static class ToolLocator
{
    public static string? FindFfmpeg() => FindTool("FFMPEG_PATH", "ffmpeg.exe");
    public static string? FindFfprobe() => FindTool("FFPROBE_PATH", "ffprobe.exe");

    public static string? FindLibreOffice(string? selectedPath = null)
    {
        if (!string.IsNullOrWhiteSpace(selectedPath) && File.Exists(selectedPath)) return selectedPath;
        var configured = Environment.GetEnvironmentVariable("LIBREOFFICE_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

        foreach (var directory in SearchDirectories())
        {
            foreach (var relative in new[]
            {
                Path.Combine("tools", "LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe"),
                Path.Combine("LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe"),
                Path.Combine("tools", "LibreOffice", "program", "soffice.exe"),
                Path.Combine("LibreOffice", "program", "soffice.exe")
            })
            {
                var candidate = Path.Combine(directory, relative);
                if (File.Exists(candidate)) return candidate;
            }
        }

        foreach (var candidate in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe")
        })
        {
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static string? FindTool(string environmentVariable, string executableName)
    {
        var configured = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

        foreach (var directory in SearchDirectories())
        {
            foreach (var relative in new[]
            {
                executableName,
                Path.Combine("tools", "ffmpeg", "bin", executableName),
                Path.Combine("ffmpeg", "bin", executableName)
            })
            {
                var candidate = Path.Combine(directory, relative);
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> SearchDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var managedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RenameConv");
        if (seen.Add(managedRoot)) yield return managedRoot;
        foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            {
                if (seen.Add(directory.FullName)) yield return directory.FullName;
            }
        }
    }
}
