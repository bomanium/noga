using System.Text.Json;
using Noga.Checking;
using Noga.Cli;

namespace Noga.Tests;

/// <summary>
/// What a check does with odd and broken files (docs/decisions/2026-11-04-odd-files.md): a report, or a read error (exit
/// 2) that names the file and says why; never an unhandled exception, nor a fault of Noga's own.
/// </summary>
public sealed class OddFileTests : IClassFixture<OddFiles>
{
    readonly OddFiles _files;

    public OddFileTests(OddFiles files) => _files = files;

    public static TheoryData<string> Reported => [.. OddFiles.Reported.Keys, .. OddFiles.Damaged];

    public static TheoryData<string> Unreadable => [.. OddFiles.Unreadable];

    [Theory]
    [MemberData(nameof(Reported))]
    public void AnOddFileIsReported(string name)
    {
        string folder = _files.Path("reports-" + name);

        var (code, output, error) = Run("check", _files.Path(name), "--out", folder);

        Assert.True(error.Length == 0, error);
        Assert.Contains(code, (int[])[QcReport.Passed, QcReport.Failed, QcReport.Warnings]);
        Assert.Contains($"{name}: ", output, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(folder, name + ".qc.json")));
        Assert.Equal(code, json.RootElement.GetProperty("exitCode").GetInt32());
        Assert.True(File.Exists(System.IO.Path.Combine(folder, name + ".qc.html")));
    }

    [Theory]
    [MemberData(nameof(Unreadable))]
    public void AFileThatCantBeReadIsAReadError(string name)
    {
        var (code, output, error) = Run("check", _files.Path(name), "--out", _files.Path("reports-" + name));

        Assert.Equal(CommandLine.Usage, code);
        Assert.Empty(output);
        Assert.Contains($"can't read {_files.Path(name)}", error, StringComparison.Ordinal);
        Assert.DoesNotContain("a fault in Noga", error, StringComparison.Ordinal);
    }

    // The managed path reads H.264 in MP4, but not to the end of one cut short or overwritten: those go to the FFmpeg
    // path, which reads the frames there are.
    [Theory]
    [InlineData("cut_moov_first.mp4")]
    [InlineData("garbled.mp4")]
    public void ADamagedMp4TheManagedPathCantFinishGoesToTheFfmpegPath(string name)
    {
        Assert.True(Managed.ManagedMeasurer.CanMeasure(_files.Path(name)));
        Assert.ThrowsAny<InvalidDataException>(() => Managed.ManagedMeasurer.Measure(_files.Path(name), Profile.EbuR128));

        var result = Checker.Run(_files.Path(name), _files.Path("checker-" + name));

        Assert.Contains(result.Report.Findings, f => f.Check == "frames");
    }

    [Theory]
    [InlineData("h264_444.mp4")]
    [InlineData("h264_422_10.mp4")]
    public void H264InAPixelFormatShotDetectorDoesntTakeGoesToTheFfmpegPath(string name) =>
        Assert.False(Managed.ManagedMeasurer.CanMeasure(_files.Path(name)));

    // A check that fails in a way nobody foresaw is that file's failure: the web UI's and the app's queue goes on.
    [Fact]
    public async Task AFaultInOneCheckDoesntStopTheQueue()
    {
        using var jobs = new Jobs((file, _) => file == "first"
            ? throw new InvalidOperationException("something nobody foresaw")
            : throw new CheckException("can't read second"));

        jobs.Add("first");
        jobs.Add("second");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (jobs.All.Any(j => j.State is JobState.Queued or JobState.Measuring))
            await jobs.Changed.WaitAsync(timeout.Token);

        var first = jobs.All.Single(j => j.File == "first");
        Assert.Equal(JobState.Failed, first.State);
        Assert.Contains("something nobody foresaw", first.Error, StringComparison.Ordinal);
        Assert.Equal("can't read second", jobs.All.Single(j => j.File == "second").Error);
    }

    static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = CommandLine.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
