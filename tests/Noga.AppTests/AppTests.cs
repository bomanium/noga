using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Noga.App;
using Noga.Checking;

[assembly: AvaloniaTestApplication(typeof(Noga.AppTests.TestApp))]

namespace Noga.AppTests;

/// <summary>The app on Avalonia's headless platform, with its Fluent theme.</summary>
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>
/// The desktop app (docs/decisions/2026-11-01-desktop-app.md) with a stand-in check: dropping files and folders queues
/// their media files, each check shows its verdict, and the profile chosen is the one checks are judged by.
/// </summary>
public sealed class AppTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("noga-app-").FullName;
    readonly List<(string File, string Profile)> _checked = [];

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // Passes "good.mov", fails anything else's loudness; can't read "bad.mov".
    CheckResult Check(string file, Profile profile, CancellationToken cancellationToken)
    {
        lock (_checked)
            _checked.Add((Path.GetFileName(file), profile.Name));
        if (Path.GetFileName(file) == "bad.mov")
            throw new CheckException($"can't read {file}: not media");
        var severity = Path.GetFileName(file) == "good.mov" ? Severity.Pass : Severity.Failure;
        var report = new QcReport
        {
            Profile = profile.Name,
            StartTimecode = "00:00:00:00",
            DropFrame = false,
            Findings = [new Finding { Check = "loudness.integrated", Severity = severity, Message = severity == Severity.Pass ? "fine" : "Integrated loudness -34.8 LUFS", CheckedAgainst = "ebur128" }],
        };
        File.WriteAllText(file + ".qc.html", ReportHtml.Write(report, Path.GetFileName(file)));
        File.WriteAllText(file + ".qc.json", ReportJson.Write(report, Path.GetFileName(file)));
        return new CheckResult(report, file + ".qc.html", file + ".qc.json");
    }

    string Touch(string relative)
    {
        string path = Path.Combine(_folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "frames");
        return path;
    }

    // Runs the UI thread's jobs until every check is done or failed and the window shows each as the queue has it
    // (the window redraws from a background wait, so its rows lag the queue a little); fails after 10 s.
    static void Settle(AppModel model, MainWindow window)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < until)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var jobs = model.Jobs;
            var shown = window.Rows.Select(r => ((r as Border)?.Child as Control)?.Tag as Job).ToList();
            if (jobs.All(j => j.State is JobState.Done or JobState.Failed) && shown.SequenceEqual(jobs))
                return;
            Thread.Sleep(10);
        }

        Assert.Fail("The checks didn't finish, or the window didn't show them, within 10 s.");
    }

    static string Text(Control row) =>
        string.Join(" | ", row.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    // A finished check's row stays the same control when more are queued and redrawn, so the focus and a click on its
    // Report button aren't lost to a row rebuilt (docs/decisions/2026-11-04-rows-in-place.md).
    [AvaloniaFact]
    public void RowsStayWhenOthersAreAdded()
    {
        string first = Touch("first.mov"), second = Touch("second.mov");
        using var model = new AppModel(Check);
        var window = new MainWindow(model);
        window.Show();

        model.Add([first]);
        Settle(model, window);
        var row = window.Rows.Single();
        model.Add([second]);
        Settle(model, window);

        Assert.Equal(2, window.Rows.Count);
        Assert.Same(row, window.Rows[1]);
    }

    [AvaloniaFact]
    public void DroppedFilesAndFoldersAreQueuedAndShowTheirVerdicts()
    {
        string good = Touch("good.mov"), bad = Touch("bad.mov");
        Touch(Path.Combine("show", "ep01.mxf"));
        Touch(Path.Combine("show", "notes.txt"));
        Touch(Path.Combine("show", "ep01.mxf.qc.json"));
        using var model = new AppModel(Check);
        var window = new MainWindow(model);
        window.Show();

        var data = new DataTransfer();
        foreach (string path in (string[])[good, bad, Path.Combine(_folder, "show")])
            data.Add(DataTransferItem.CreateFile(Storage(window, path)));
        window.Dropped(data);
        Settle(model, window);

        Assert.Equal(["good.mov", "bad.mov", "ep01.mxf"], _checked.Select(c => c.File));
        var rows = window.Rows.Select(Text).ToList();
        Assert.Equal(3, rows.Count);
        Assert.StartsWith("Failed | Report | JSON | ep01.mxf | took ", rows[0], StringComparison.Ordinal);
        Assert.Contains($" s · {Path.Combine(_folder, "show")} | ", rows[0], StringComparison.Ordinal);
        Assert.EndsWith(" | Integrated loudness -34.8 LUFS", rows[0], StringComparison.Ordinal);
        Assert.StartsWith("Couldn't read | bad.mov", rows[1], StringComparison.Ordinal);
        Assert.EndsWith("can't read bad.mov: not media", rows[1], StringComparison.Ordinal);
        Assert.StartsWith("Passed | Report | JSON | good.mov", rows[2], StringComparison.Ordinal);
        Assert.Equal(2, window.Rows[2].GetLogicalDescendants().OfType<Button>().Count());

        // With checks there, the drop zone keeps to its first line; files dragged over the window light it up.
        var hints = window.DropZone.GetLogicalDescendants().OfType<TextBlock>().ToList();
        Assert.True(hints[0].IsVisible);
        Assert.False(hints[1].IsVisible);
        window.Highlight(true);
        Assert.NotNull(window.DropZone.Background);
        window.Highlight(false);
        Assert.Null(window.DropZone.Background);
    }

    // Until the first check, the drop zone fills the window; folders can be picked as well as files.
    [AvaloniaFact]
    public void AnEmptyWindowIsTheDropZone()
    {
        using var model = new AppModel(Check);
        var window = new MainWindow(model);
        window.Show();

        var body = (Grid)window.DropZone.Parent!;
        Assert.True(body.RowDefinitions[0].Height.IsStar);
        Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), b => b.Content as string == "Add folder…");
    }

    [AvaloniaFact]
    public void ChecksAreJudgedByTheProfileChosen()
    {
        using var model = new AppModel(Check);
        var window = new MainWindow(model);
        window.Show();
        var profiles = window.GetLogicalDescendants().OfType<ComboBox>().Single();

        profiles.SelectedIndex = Profiles.All.ToList().IndexOf(Profiles.AtscA85);
        model.Add([Touch("good.mov")]);
        Settle(model, window);

        Assert.Equal([("good.mov", "atsc-a85")], _checked);
    }

    [AvaloniaFact]
    public void ADropWithoutVideoSaysSo()
    {
        Touch(Path.Combine("docs", "notes.txt"));
        using var model = new AppModel(Check);
        var window = new MainWindow(model);
        window.Show();

        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateFile(Storage(window, Path.Combine(_folder, "docs"))));
        window.Dropped(data);

        Assert.Empty(model.Jobs);
        Assert.Contains(window.DropZone.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("No video files", StringComparison.Ordinal) == true);
    }

    // The window has the app's icon, the embedded 256 px PNG (tools/make-icons.sh). The headless platform keeps no
    // image to read back, so the resource itself is checked.
    [AvaloniaFact]
    public void TheWindowHasTheAppIcon()
    {
        using var model = new AppModel(Check);
        var window = new MainWindow(model);
        window.Show();

        Assert.NotNull(window.Icon);
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("Noga.App.icon.png");
        Assert.NotNull(stream);
        var header = new byte[24];
        stream.ReadExactly(header);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], header[..4]);
        Assert.Equal(256, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16))); // IHDR's width
        Assert.Equal(256, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20))); // and height
    }

    static IStorageItem Storage(TopLevel top, string path) =>
        (Directory.Exists(path)
            ? top.StorageProvider.TryGetFolderFromPathAsync(path).GetAwaiter().GetResult()
            : top.StorageProvider.TryGetFileFromPathAsync(path).GetAwaiter().GetResult() as IStorageItem)
        ?? throw new InvalidOperationException($"No storage item for {path}");
}
