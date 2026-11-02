using MediaQc.Checking;

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
    /// <summary>Whether <paramref name="name"/> is a report mediaqc wrote.</summary>
    public static bool IsReport(string name) => MediaFiles.IsReport(name);

    /// <summary>Where the picker starts: the user's home folder.</summary>
    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>What lists the drives on Windows (the root elsewhere): a drive's parent.</summary>
    public const string Drives = "*";

    /// <summary>
    /// <paramref name="path"/>'s contents; <see cref="Drives"/>: the drives on Windows (the root elsewhere), or, when
    /// <paramref name="roots"/> limit access, the roots. Inside roots, links are left out (they could lead out of them).
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">There is no such folder.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder can't be read, or isn't in the roots.</exception>
    public static Listing List(string? path, Roots? roots = null)
    {
        bool limited = roots is { Any: true };
        if (string.IsNullOrEmpty(path) || path == Drives)
        {
            if (limited)
                return new Listing(null, null, [.. roots!.Folders.Select(f => new Entry(Path.GetFileName(f) is { Length: > 0 } name ? name : f, f, true, null, false))]);
            if (!OperatingSystem.IsWindows())
                return List("/");
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => new Entry(d.Name, d.RootDirectory.FullName, true, null, false));
            return new Listing(null, null, [.. drives]);
        }

        if (limited && !roots!.Contains(path))
            throw new UnauthorizedAccessException($"Not in a shared folder: {path}");
        var folder = new DirectoryInfo(Path.GetFullPath(path));
        if (!folder.Exists)
            throw new DirectoryNotFoundException($"No such folder: {path}");
        var skip = FileAttributes.Hidden | FileAttributes.System | (limited ? FileAttributes.ReparsePoint : 0);
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = skip };
        var folders = folder.EnumerateDirectories("*", options)
            .Where(d => !d.Name.StartsWith('.') && (!limited || d.LinkTarget is null))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new Entry(d.Name, d.FullName, true, null, false));
        var files = folder.EnumerateFiles("*", options)
            .Where(f => !f.Name.StartsWith('.') && !IsReport(f.Name) && (!limited || f.LinkTarget is null))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new Entry(f.Name, f.FullName, false, f.Length, MediaFiles.IsMedia(f.Name)));
        string? parent = limited && roots!.IsRoot(folder.FullName)
            ? Drives
            : folder.Parent?.FullName ?? (OperatingSystem.IsWindows() ? Drives : null);
        return new Listing(folder.FullName, parent, [.. folders, .. files]);
    }
}
