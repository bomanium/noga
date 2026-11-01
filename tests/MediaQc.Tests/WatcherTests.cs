using MediaQc.Cli.Serve;

namespace MediaQc.Tests;

/// <summary>The watch folder's scans, at chosen times: which files are checked, and when.</summary>
public sealed class WatcherTests : IDisposable
{
    static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    readonly string _folder = Directory.CreateTempSubdirectory("mediaqc-watch-").FullName;
    readonly List<string> _checked = [];
    readonly Watcher _watcher;

    public WatcherTests() => _watcher = new Watcher(_folder, _checked.Add);

    public void Dispose()
    {
        _watcher.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    string Write(string name, int bytes = 10)
    {
        string path = Path.Combine(_folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    void ScanAt(double seconds) => _watcher.Scan(Start.AddSeconds(seconds));

    [Fact]
    public void AMediaFileWithoutAReportIsCheckedOnceItHasSettled()
    {
        string file = Write("programme.mov");

        ScanAt(0);
        ScanAt(4.9);
        Assert.Empty(_checked);
        ScanAt(5);
        ScanAt(10);

        Assert.Equal([file], _checked);
    }

    [Fact]
    public void AFileStillBeingCopiedWaitsUntilItStopsChanging()
    {
        string file = Write("master.mxf", 10);
        ScanAt(0);
        Write("master.mxf", 20);
        ScanAt(4);
        ScanAt(8.9);
        Assert.Empty(_checked);

        ScanAt(9);

        Assert.Equal([file], _checked);
    }

    [Fact]
    public void SubfoldersAreWatchedToo()
    {
        string file = Write(Path.Combine("show", "ep01", "ep01.mp4"));

        ScanAt(0);
        ScanAt(5);

        Assert.Equal([file], _checked);
    }

    [Fact]
    public void CheckedFilesReportsHiddenOnesAndOtherFilesAreLeftAlone()
    {
        Write("done.mov");
        Write("done.mov.qc.json");
        Write("done.mov.qc.html");
        Write(".partial.mov");
        Write(Path.Combine(".trash", "old.mov"));
        Write("notes.txt");

        ScanAt(0);
        ScanAt(5);

        Assert.Empty(_checked);
    }

    [Fact]
    public void AFolderThatIsntThereIsRefused() =>
        Assert.Throws<DirectoryNotFoundException>(() => new Watcher(Path.Combine(_folder, "missing"), _ => { }));
}
