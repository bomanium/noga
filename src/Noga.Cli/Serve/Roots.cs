namespace Noga.Cli.Serve;

/// <summary>
/// The folders a shared server lets its users see and check (<c>--root</c>, and the <c>--watch</c> folder;
/// docs/decisions/2026-11-01-shared-machine.md): a path counts when it is a root or inside one, reached without a
/// symbolic link or junction (which could lead out of it). No roots: the whole machine, as on localhost.
/// </summary>
sealed class Roots
{
    static readonly StringComparison Comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    readonly List<string> _folders = [];

    /// <summary>The roots, each checked to be a folder.</summary>
    /// <exception cref="DirectoryNotFoundException">One isn't a folder.</exception>
    public Roots(IEnumerable<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        foreach (string folder in folders)
        {
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            if (!Directory.Exists(full))
                throw new DirectoryNotFoundException($"No such folder: {folder}");
            if (!_folders.Contains(full, StringComparer.FromComparison(Comparison)))
                _folders.Add(full);
        }
    }

    /// <summary>Whether access is limited to the roots.</summary>
    public bool Any => _folders.Count > 0;

    /// <summary>The roots, as full paths.</summary>
    public IReadOnlyList<string> Folders => _folders;

    /// <summary>Whether <paramref name="path"/> may be seen: there are no roots, or it is in one without a link on the way.</summary>
    public bool Contains(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!Any)
            return true;
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        foreach (string root in _folders)
        {
            if (!string.Equals(full, root, Comparison) && !full.StartsWith(root + Path.DirectorySeparatorChar, Comparison))
                continue;
            for (string? at = full; at is not null && !string.Equals(at, root, Comparison); at = Path.GetDirectoryName(at))
            {
                FileSystemInfo info = Directory.Exists(at) ? new DirectoryInfo(at) : new FileInfo(at);
                if (info.LinkTarget is not null)
                    return false;
            }

            return true;
        }

        return false;
    }

    /// <summary>Whether <paramref name="path"/> is one of the roots.</summary>
    public bool IsRoot(string path) =>
        _folders.Contains(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), StringComparer.FromComparison(Comparison));
}
