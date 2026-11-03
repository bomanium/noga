using Noga.Checking;

namespace Noga.Cli.Serve;

/// <summary>
/// <c>noga serve --watch FOLDER</c> (docs/decisions/2026-11-01-watch-folder.md): the media files in the folder and
/// its subfolders that have no report yet (FILE.qc.json beside them) are checked, those there at the start and those
/// that arrive, each once its size and time have stayed the same for <see cref="Settle"/> (its copy has finished).
/// The folder is scanned every <see cref="Interval"/> rather than followed by file-system events, which network
/// shares, where hand-off folders often are, don't deliver reliably.
/// </summary>
sealed class Watcher : IDisposable
{
    /// <summary>How long a file must stay the same before it is checked.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(5);

    /// <summary>How often the folder is scanned.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    readonly Dictionary<string, (long Size, DateTime Written, DateTimeOffset Since)> _seen = new(StringComparer.Ordinal);
    readonly HashSet<string> _queued = new(StringComparer.Ordinal);
    readonly Action<string> _add;
    readonly CancellationTokenSource _stopping = new();
    Task? _loop;

    /// <summary>Watches <paramref name="folder"/>, giving each file to check to <paramref name="add"/> (a full path).</summary>
    /// <exception cref="DirectoryNotFoundException">There is no such folder.</exception>
    public Watcher(string folder, Action<string> add)
    {
        Folder = Path.GetFullPath(folder);
        if (!Directory.Exists(Folder))
            throw new DirectoryNotFoundException($"No such folder: {folder}");
        _add = add;
    }

    /// <summary>The folder watched, as a full path.</summary>
    public string Folder { get; }

    /// <summary>Scans every <see cref="Interval"/> until disposed.</summary>
    public void Start() => _loop = Task.Run(async () =>
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                Scan(DateTimeOffset.Now);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The folder went away for a moment (a share reconnecting): the next scan tries again.
            }
        }
        while (await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false));
    });

    /// <summary>
    /// One scan at <paramref name="now"/>: each media file without a report that has stayed the same for
    /// <see cref="Settle"/> is given to check, once.
    /// </summary>
    public void Scan(DateTimeOffset now)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in new DirectoryInfo(Folder).EnumerateFiles("*", options))
        {
            string path = file.FullName;
            if (_queued.Contains(path) || file.Name.StartsWith('.') || Folders.IsReport(file.Name)
                || !MediaFiles.IsMedia(file.Name) || File.Exists(path + ".qc.json")
                || Path.GetRelativePath(Folder, path).Split(Path.DirectorySeparatorChar).Any(p => p.StartsWith('.')))
            {
                continue;
            }

            present.Add(path);
            var state = (file.Length, file.LastWriteTimeUtc);
            if (!_seen.TryGetValue(path, out var seen) || (seen.Size, seen.Written) != state)
            {
                _seen[path] = (file.Length, file.LastWriteTimeUtc, now);
            }
            else if (now - seen.Since >= Settle)
            {
                _seen.Remove(path);
                _queued.Add(path);
                _add(path);
            }
        }

        foreach (string gone in _seen.Keys.Where(k => !present.Contains(k)).ToList())
            _seen.Remove(gone);
    }

    public void Dispose()
    {
        _stopping.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }

        _stopping.Dispose();
    }
}
