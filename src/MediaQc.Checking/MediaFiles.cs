namespace MediaQc.Checking;

/// <summary>Which files are media to check, by extension: for the web UI's picker, its watch folder and the desktop app.</summary>
public static class MediaFiles
{
    /// <summary>The extensions counted as media.</summary>
    public static IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mov", ".mp4", ".m4v", ".mxf", ".mkv", ".avi", ".mts", ".m2ts", ".ts", ".mpg", ".mpeg", ".webm", ".wav", ".mp3", ".m4a",
    };

    /// <summary>Whether <paramref name="path"/> has a media file's extension.</summary>
    public static bool IsMedia(string path) => Extensions.Contains(Path.GetExtension(path));

    /// <summary>Whether <paramref name="name"/> is a report mediaqc wrote (FILE.qc.html or FILE.qc.json).</summary>
    public static bool IsReport(string name) =>
        name.EndsWith(".qc.html", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".qc.json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The media files <paramref name="paths"/> name: each file itself (whatever its extension: it was asked for), and
    /// the media files in each folder and its subfolders, hidden ones and reports left out, in order.
    /// </summary>
    public static IEnumerable<string> In(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        foreach (string path in paths)
        {
            if (File.Exists(path))
            {
                yield return Path.GetFullPath(path);
            }
            else if (Directory.Exists(path))
            {
                var files = new DirectoryInfo(path).EnumerateFiles("*", options)
                    .Where(f => !f.Name.StartsWith('.') && !IsReport(f.Name) && IsMedia(f.Name))
                    .Select(f => f.FullName)
                    .Order(StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                    yield return file;
            }
        }
    }
}
