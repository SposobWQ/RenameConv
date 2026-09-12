namespace RenameConv.Services;

internal static class FileChangeFilter
{
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".part", ".crdownload", ".bin", ".json", ".trn", ".bak", ".ndjgz",
        ".lock", ".iig", ".jsonlz4", ".baklz4", ".swidtag", ".customDestinations-ms", ".cs",
        ".dll", ".old", ".log", ".orig", ".xcu", ".xcd", ".swo", ".swp"
    };

    public static bool ShouldIgnore(string path)
    {
        var fileName = Path.GetFileName(path);
        if (IgnoredExtensions.Contains(Path.GetExtension(path))) return true;
        if (fileName.StartsWith("~$", StringComparison.Ordinal) || fileName.StartsWith(".~lock.", StringComparison.Ordinal) || fileName.StartsWith("~", StringComparison.Ordinal)) return true;

        try { return (File.GetAttributes(path) & FileAttributes.Temporary) != 0; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static bool IsRenameConvTemporary(string name) => name.Contains(".__renameconv__", StringComparison.OrdinalIgnoreCase);
}
