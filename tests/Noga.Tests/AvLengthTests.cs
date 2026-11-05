namespace Noga.Tests;

// The sound's length and start against the picture's (docs/decisions/2026-11-05-av-lengths.md): within a frame passes,
// over a second fails, between warns; each figure ffprobe's and ffmpeg's.
public sealed class AvLengthTests : IClassFixture<OddFiles>
{
    readonly OddFiles _files;

    public AvLengthTests(OddFiles files) => _files = files;

    [Theory]
    [InlineData("whole_moov_first.mov", Severity.Pass, Severity.Pass)]
    [InlineData("whole_moov_first.mp4", Severity.Pass, Severity.Pass)]
    [InlineData("sound_short.mov", Severity.Warning, Severity.Pass)]
    [InlineData("sound_late.mov", Severity.Warning, Severity.Warning)]
    [InlineData("short_audio.mp4", Severity.Failure, Severity.Pass)]
    public void TheSoundIsJudgedAgainstThePicture(string name, Severity length, Severity start)
    {
        string file = _files.Path(name);
        var measured = Ffmpeg.FfmpegMeasurer.Measure(file, Profile.EbuR128);
        var report = Profile.EbuR128.Evaluate(measured);

        FfmpegCli.AssertDecode(file, measured);
        FfmpegCli.AssertStarts(file, measured);
        var found = Assert.Single(report.Findings, f => f.Check == "av.length");
        Assert.Equal(length, found.Severity);
        Assert.Equal(Profile.AvLengthFilter, found.CheckedAgainst);
        Assert.Equal("0082W", found.EbuQc?.Id);
        Assert.Equal(start, Assert.Single(report.Findings, f => f.Check == "av.start").Severity);
    }

    // Sound 3 frames short: the missing sound is at its end; sound 5 frames late: at the start.
    [Fact]
    public void TheFindingSaysWhereTheSoundIsMissing()
    {
        var shortSound = Find("sound_short.mov", "av.length");
        Assert.Equal("The sound is 0.12 s (3 frames) shorter than the picture: 1.88 s against 2.00 s", shortSound.Message);
        Assert.Equal("00:00:01:22", shortSound.Start?.Timecode);
        Assert.Equal("00:00:02:00", shortSound.End?.Timecode);

        var late = Find("sound_late.mov", "av.length");
        Assert.Equal("00:00:00:00", late.Start?.Timecode);
        Assert.Equal("00:00:00:05", late.End?.Timecode);
        Assert.Equal("The sound starts 0.20 s (5 frames) after the picture", Find("sound_late.mov", "av.start").Message);
    }

    // A profile file sets the limits: 5 frames lets the late sound pass; 0.1 s fails the short one.
    [Fact]
    public void AProfileSetsTheLimits()
    {
        var lenient = Profiles.Read("""{ "name": "x", "syncFrames": 5 }""");
        var strict = Profiles.Read("""{ "name": "x", "syncFailure": 0.1 }""");
        var late = Ffmpeg.FfmpegMeasurer.Measure(_files.Path("sound_late.mov"), lenient);
        var shortSound = Ffmpeg.FfmpegMeasurer.Measure(_files.Path("sound_short.mov"), strict);

        Assert.Equal(Severity.Pass, lenient.Evaluate(late).Findings.Single(f => f.Check == "av.start").Severity);
        Assert.Equal(Severity.Failure, strict.Evaluate(shortSound).Findings.Single(f => f.Check == "av.length").Severity);
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "syncFrames": -1 }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "syncFailure": 0 }"""));
    }

    // Sound only has no picture to be against.
    [Fact]
    public void SoundOnlyIsntJudged()
    {
        var report = Profile.EbuR128.Evaluate(Ffmpeg.FfmpegMeasurer.Measure(_files.Path("sound_only.wav"), Profile.EbuR128));
        Assert.DoesNotContain(report.Findings, f => f.Check.StartsWith("av.", StringComparison.Ordinal));
    }

    Finding Find(string name, string check) =>
        Profile.EbuR128.Evaluate(Ffmpeg.FfmpegMeasurer.Measure(_files.Path(name), Profile.EbuR128)).Findings.Single(f => f.Check == check);
}
