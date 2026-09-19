using System.Diagnostics;
using System.IO.Compression;
using System.Windows.Forms;

namespace RenameConvUpdater;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            MessageBox.Show("Недостаточно аргументов для обновления.", "RenameConv Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            var packagePath = Path.GetFullPath(args[0]);
            var installationPath = Path.GetFullPath(args[1]);
            if (!File.Exists(packagePath)) throw new FileNotFoundException("Не найден пакет обновления.", packagePath);
            if (!Directory.Exists(installationPath)) throw new DirectoryNotFoundException("Не найдена папка программы.");
            if (!int.TryParse(args[2], out var processId)) throw new ArgumentException("Некорректный идентификатор процесса.");

            WaitForApplicationExit(processId);
            ExtractPackage(packagePath, installationPath);
            File.Delete(packagePath);

            var applicationPath = Path.Combine(installationPath, "RenameConv.exe");
            if (!File.Exists(applicationPath)) throw new FileNotFoundException("Не найден RenameConv.exe после обновления.", applicationPath);
            Process.Start(new ProcessStartInfo(applicationPath) { UseShellExecute = true, WorkingDirectory = installationPath });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "RenameConv Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void WaitForApplicationExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit(60_000);
        }
        catch (ArgumentException) { }
    }

    private static void ExtractPackage(string packagePath, string installationPath)
    {
        var root = Path.GetFullPath(installationPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(installationPath, entry.FullName));
            if (!destinationPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Пакет содержит недопустимый путь.");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, true);
        }
    }
}
