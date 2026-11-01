namespace MediaQc.Cli.Serve;

/// <summary>A folder's contents for the page's file picker: its subfolders, then its files, hidden ones left out.</summary>
sealed record Listing(string? Path, string? Parent, IReadOnlyList<Entry> Entries);

/// <summary>A folder or a file in a <see cref="Listing"/>; <paramref name="Size"/> in bytes for a file.</summary>
sealed record Entry(string Name, string Path, bool IsFolder, long? Size, bool IsMedia);

/// <summary>
/// The machine's folders, as the server sees them: the web UI picks files where they are, so nothing is uploaded
/// (docs/decisions/2026-11-01-web-ui.md). Only folder names, file names and sizes: no file is read here.
/// </summary>
static class Folders
{
    /// <summary>The file extensions marked as media (the picker shows other files too, dimmed).</summary>
    static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mov", ".mp4", ".m4v", ".mxf", ".mkv", ".avi", ".mts", ".m2ts", ".ts", ".mpg", ".mpeg", ".webm", ".wav", ".mp3", ".m4a",
    };

    /// <summary>Where the picker starts: the user's home folder.</summary>
    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>What lists the drives on Windows (the root elsewhere): a drive's parent.</summary>
    public const string Drives = "*";

    /// <summary><paramref name="path"/>'s contents; <see cref="Drives"/>: the drives on Windows (the root elsewhere).</summary>
    /// <exception cref="DirectoryNotFoundException">There is no such folder.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder can't be read.</exception>
    public static Listing List(string? path)
    {
        if (string.IsNullOrEmpty(path) || path == Drives)
        {
            if (!OperatingSystem.IsWindows())
                return List("/");
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => new Entry(d.Name, d.RootDirectory.FullName, true, null, false));
            return new Listing(null, null, [.. drives]);
        }

        var folder = new DirectoryInfo(Path.GetFullPath(path));
        if (!folder.Exists)
            throw new DirectoryNotFoundException($"No such folder: {path}");
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        var folders = folder.EnumerateDirectories("*", options)
            .Where(d => !d.Name.StartsWith('.'))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new Entry(d.Name, d.FullName, true, null, false));
        var files = folder.EnumerateFiles("*", options)
            .Where(f => !f.Name.StartsWith('.') && !f.Name.EndsWith(".qc.html", StringComparison.OrdinalIgnoreCase) && !f.Name.EndsWith(".qc.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new Entry(f.Name, f.FullName, false, f.Length, MediaExtensions.Contains(f.Extension)));
        string? parent = folder.Parent?.FullName ?? (OperatingSystem.IsWindows() ? Drives : null);
        return new Listing(folder.FullName, parent, [.. folders, .. files]);
    }
}
