using Noga.Checking;

namespace Noga.Tests;

// Gaps in the picture's timestamps (docs/decisions/2026-11-05-dropped-frames.md): frames missing fail, a variable frame
// rate warns and its gaps aren't judged; each packet's time ffprobe -show_packets', on both paths.
public sealed class DroppedFrameTests : IClassFixture<OddFiles>
{
    readonly OddFiles _files;

    public DroppedFrameTests(OddFiles files) => _files = files;

    // dropped.mov lacks frames 20 to 22: one gap of 3, 00:00:00:20 to 00:00:00:23; the picture still 20 s long.
    [Fact]
    public void FramesMissingFail()
    {
        string file = _files.Path("dropped.mov");
        var measured = Ffmpeg.FfmpegMeasurer.Measure(file, Profile.EbuR128);
        var report = Profile.EbuR128.Evaluate(measured);

        FfmpegCli.AssertPackets(file, measured);
        var gap = Assert.Single(report.Findings, f => f.Check == "frames.dropped");
        Assert.Equal(Severity.Failure, gap.Severity);
        Assert.Equal(3, gap.Value);
        Assert.Equal("3 frames missing: the picture jumps 0.160 s", gap.Message);
        Assert.Equal("00:00:00:20", gap.Start?.Timecode);
        Assert.Equal("00:00:00:23", gap.End?.Timecode);
        Assert.Equal(Profile.DroppedFilter, gap.CheckedAgainst);
        Assert.Equal("0108F", gap.EbuQc?.Id);
        Assert.Equal(Severity.Pass, report.Findings.Single(f => f.Check == "decode.frames").Severity);
        Assert.Equal(Severity.Pass, report.Findings.Single(f => f.Check == "av.length").Severity);
        Assert.Equal(QcReport.Failed, report.ExitCode());
    }

    // dropped.mp4 lacks frames 10 and 30: the managed path reads it as the FFmpeg path does.
    [Fact]
    public void BothPathsFindTheSameGaps()
    {
        string file = _files.Path("dropped.mp4");
        Assert.True(Managed.ManagedMeasurer.CanMeasure(file));
        var managed = Managed.ManagedMeasurer.Measure(file, Profile.EbuR128);
        var ffmpeg = Ffmpeg.FfmpegMeasurer.Measure(file, Profile.EbuR128);

        FfmpegCli.AssertPackets(file, managed);
        FfmpegCli.AssertPackets(file, ffmpeg);
        foreach (var measured in (Measurements[])[managed, ffmpeg])
        {
            var gaps = Profile.EbuR128.Evaluate(measured).Findings.Where(f => f.Check == "frames.dropped").ToList();
            Assert.Equal(["00:00:00:10", "00:00:01:05"], gaps.Select(g => g.Start?.Timecode));
            Assert.All(gaps, g => Assert.Equal((Severity.Failure, 1.0), (g.Severity, g.Value)));
        }
    }

    [Theory]
    [InlineData("whole_moov_first.mov")]
    [InlineData("whole_moov_first.mp4")]
    [InlineData("edit_list.mp4")]
    [InlineData("one_frame.mp4")]
    [InlineData("drop_frame.mov")]
    public void AWholePictureHasNoGaps(string name)
    {
        var report = Checker.Run(_files.Path(name), _files.Path("dropped-" + name)).Report;

        Assert.DoesNotContain(report.Findings, f => f.Check.StartsWith("frames.", StringComparison.Ordinal) && f.Severity > Severity.Pass);
        if (name != "one_frame.mp4")
            Assert.Equal(Severity.Pass, report.Findings.Single(f => f.Check == "frames.dropped").Severity);
    }

    // variable_rate.mov's frames are alternately one frame and 1.3 frames apart.
    [Fact]
    public void AVariableFrameRateWarnsAndItsGapsArentJudged()
    {
        string file = _files.Path("variable_rate.mov");
        var measured = Ffmpeg.FfmpegMeasurer.Measure(file, Profile.EbuR128);
        var report = Profile.EbuR128.Evaluate(measured);

        FfmpegCli.AssertPackets(file, measured);
        var vfr = Assert.Single(report.Findings, f => f.Check == "frames.variable-rate");
        Assert.Equal(Severity.Warning, vfr.Severity);
        Assert.Equal("0042F", vfr.EbuQc?.Id);
        Assert.DoesNotContain(report.Findings, f => f.Check == "frames.dropped");
    }

    // maxDroppedFrames 3 lets dropped.mov's gap be reported, not failed.
    [Fact]
    public void AProfileCanAllowSome()
    {
        var lenient = Profiles.Read("""{ "name": "x", "maxDroppedFrames": 3 }""");
        var report = lenient.Evaluate(Ffmpeg.FfmpegMeasurer.Measure(_files.Path("dropped.mov"), lenient));

        var gap = Assert.Single(report.Findings, f => f.Check == "frames.dropped");
        Assert.Equal(Severity.Info, gap.Severity);
        Assert.EndsWith("(the profile allows 3)", gap.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "maxDroppedFrames": -1 }"""));
    }

    // Past 20 gaps, the rest are one finding: 3000 frames at 25 fps with every hundredth from the 100th missing.
    [Fact]
    public void ManyGapsAreSummed()
    {
        var frames = Enumerable.Range(0, 3000).Where(n => n < 100 || n % 100 != 0).ToList();
        var packets = VideoPackets.From(frames.Select(n => (n * 0.04, 10L, n == 0))) with { Steps = Ffmpeg.Packets.Steps(frames.Select(n => (long?)n), new FrameReader.Rational(1, 25), new FrameReader.Rational(25, 1)) };
        var measured = new Measurements { Duration = 120, RateNumerator = 25, RateDenominator = 1, Packets = packets };

        var gaps = Profile.EbuR128.Evaluate(measured).Findings.Where(f => f.Check == "frames.dropped").ToList();

        Assert.Equal(21, gaps.Count);
        Assert.Equal("9 frames missing in 9 more gaps, 29 in all", gaps[^1].Message);
        Assert.All(gaps, g => Assert.Equal(Severity.Failure, g.Severity));
    }
}
