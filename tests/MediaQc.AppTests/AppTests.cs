using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using MediaQc.App;
using MediaQc.Checking;

[assembly: AvaloniaTestApplication(typeof(MediaQc.AppTests.TestApp))]

namespace MediaQc.AppTests;

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
    readonly string _folder = Directory.CreateTempSubdirectory("mediaqc-app-").FullName;
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
            Findings = [new Finding { Check = "loudness.integrated", Severity = severity, Message = "m", CheckedAgainst = "ebur128" }],
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

    // Runs the UI thread's jobs until every check is done or failed.
    static void Settle(AppModel model)
    {
        for (int i = 0; i < 200 && model.Jobs.Any(j => j.State is JobState.Queued or JobState.Measuring); i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    static string Text(Control row) =>
        string.Join(" | ", row.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

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
        Settle(model);

        Assert.Equal(["good.mov", "bad.mov", "ep01.mxf"], _checked.Select(c => c.File));
        var rows = window.Rows.Select(Text).ToList();
        Assert.Equal(3, rows.Count);
        Assert.StartsWith("Failed | Report | JSON | ep01.mxf", rows[0], StringComparison.Ordinal);
        Assert.StartsWith("Couldn't read | bad.mov", rows[1], StringComparison.Ordinal);
        Assert.Contains("not media", rows[1], StringComparison.Ordinal);
        Assert.StartsWith("Passed | Report | JSON | good.mov", rows[2], StringComparison.Ordinal);
        Assert.Equal(2, window.Rows[2].GetLogicalDescendants().OfType<Button>().Count());
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
        Settle(model);

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

    static IStorageItem Storage(TopLevel top, string path) =>
        (Directory.Exists(path)
            ? top.StorageProvider.TryGetFolderFromPathAsync(path).GetAwaiter().GetResult()
            : top.StorageProvider.TryGetFileFromPathAsync(path).GetAwaiter().GetResult() as IStorageItem)
        ?? throw new InvalidOperationException($"No storage item for {path}");
}
