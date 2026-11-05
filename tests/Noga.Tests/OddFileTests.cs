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

    // A file cut short lists frames that aren't there, and its sound stops with them; one overwritten in the middle
    // has frames that don't decode (docs/decisions/2026-11-04-odd-files.md: each a failure).
    [Theory]
    [InlineData("cut_moov_first.mov", "decode.frames")]
    [InlineData("cut_moov_first.mp4", "decode.frames")]
    [InlineData("garbled.mov", "decode.errors")]
    [InlineData("garbled.mp4", "decode.errors")]
    public void WhatADamagedFileLacksFailsIt(string name, string check)
    {
        var report = Checker.Run(_files.Path(name), _files.Path("decode-" + name)).Report;

        Assert.Contains(report.Findings, f => f.Check == check && f.Severity == Severity.Failure);
        if (name.StartsWith("cut_", StringComparison.Ordinal))
            Assert.Contains(report.Findings, f => f.Check == "decode.sound" && f.Severity == Severity.Failure);
        Assert.Equal(QcReport.Failed, report.ExitCode());
    }

    [Theory]
    [InlineData("whole_moov_first.mov")]
    [InlineData("whole_moov_first.mp4")]
    [InlineData("vfr.mp4")]
    [InlineData("edit_list.mp4")]
    [InlineData("one_frame.mp4")]
    [InlineData("two_audio.mp4")]
    [InlineData("surround.mov")]
    public void AWholeFileDecodesToTheEnd(string name)
    {
        var report = Checker.Run(_files.Path(name), _files.Path("decode-" + name)).Report;

        Assert.Contains(report.Findings, f => f.Check == "decode.frames" && f.Severity == Severity.Pass);
        Assert.Contains(report.Findings, f => f.Check == "decode.sound" && f.Severity == Severity.Pass);
        Assert.DoesNotContain(report.Findings, f => f.Check.StartsWith("decode.", StringComparison.Ordinal) && f.Severity == Severity.Failure);
    }

    [Theory]
    [InlineData("cut_moov_first.mov")]
    [InlineData("cut_moov_first.mp4")]
    [InlineData("garbled.mov")]
    [InlineData("garbled.mp4")]
    [InlineData("whole_moov_first.mov")]
    [InlineData("two_audio.mp4")]
    public void WhatWasDecodedIsFfprobesAndFfmpegs(string name) =>
        FfmpegCli.AssertDecode(_files.Path(name), Ffmpeg.FfmpegMeasurer.Measure(_files.Path(name), Profile.EbuR128));

    // Sound that is all tone, shorter than the picture: no line-up (it runs to the sound's end), so measured from the start.
    [Fact]
    public void ToneToTheEndOfShortSoundIsNoLineUp()
    {
        Assert.True(Managed.ManagedMeasurer.CanMeasure(_files.Path("all_tone.mp4")));
        Assert.Equal(0, Ffmpeg.FfmpegMeasurer.Measure(_files.Path("all_tone.mov"), Profile.EbuR128).LoudnessFrom);
        Assert.Equal(0, Managed.ManagedMeasurer.Measure(_files.Path("all_tone.mp4"), Profile.EbuR128).LoudnessFrom);
    }

    // Sound only (docs/decisions/2026-11-04-odd-files.md): its sound checked and judged, its picture's checks not made, the
    // head's bars and black not asked of it.
    [Theory]
    [InlineData("sound_only.wav")]
    [InlineData("sound_only.mov")]
    [InlineData("sound_only.aiff")]
    [InlineData("cover_art.m4a")]
    public void SoundOnlyIsCheckedForItsSound(string name)
    {
        string file = _files.Path(name);
        var profile = Profile.EbuR128 with { HeadBars = 30, HeadTone = 1, HeadBlack = 10, TailBlack = 5 };

        var measured = Ffmpeg.FfmpegMeasurer.Measure(file, profile);
        var report = profile.Evaluate(measured);

        Assert.False(measured.HasPicture);
        Assert.Null(measured.Format?.Video);
        FfmpegCli.AssertLoudness(file, measured);
        FfmpegCli.AssertChannels(file, measured.Channels!, profile);
        FfmpegCli.AssertDecode(file, measured);
        Assert.Contains(report.Findings, f => f.Check == "picture" && f.Severity == Severity.Info);
        Assert.Contains(report.Findings, f => f.Check == "loudness.integrated");
        Assert.Contains(report.Findings, f => f.Check == "decode.sound" && f.Severity == Severity.Pass);
        Assert.Contains(report.Findings, f => f.Check == "head.tone");
        Assert.DoesNotContain(report.Findings, f => f.Check is "head.bars" or "head.black" or "tail.black" or "frames" or "black" or "bars");
    }

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
        for (var changed = jobs.Changed; jobs.All.Any(j => j.State is JobState.Queued or JobState.Measuring); changed = jobs.Changed)
            await changed.WaitAsync(timeout.Token);

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
