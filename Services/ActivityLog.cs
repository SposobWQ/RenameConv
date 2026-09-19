using System.Text;

namespace RenameConv.Services;

internal sealed class ActivityLog
{
    private readonly object _sync = new();
    private readonly string _path;

    public ActivityLog()
    {
        DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RenameConv");
        Directory.CreateDirectory(DirectoryPath);
        _path = Path.Combine(DirectoryPath, "RenameConv.log");
    }

    public string DirectoryPath { get; }
    public string LatestMessage { get; private set; } = "Ожидание переименования";

    public void Write(string message)
    {
        lock (_sync)
        {
            LatestMessage = message;
            try
            {
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}", Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
