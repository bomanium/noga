namespace Noga.Tests;

// The loudest momentary and short-term loudness and the loudness range's window (docs/decisions/2026-11-05-short-term-loudness.md):
// reported always, judged where a profile sets a limit, a failure past it; each figure ebur128's.
public sealed class LoudnessLimitTests : IClassFixture<OddFiles>
{
    readonly OddFiles _files;

    public LoudnessLimitTests(OddFiles files) => _files = files;

    Measurements Loud => Ffmpeg.FfmpegMeasurer.Measure(_files.Path("loud.mov"), Profile.EbuR128);

    [Fact]
    public void TheLoudestMomentsAreEbur128s()
    {
        var measured = Loud;
        FfmpegCli.AssertLoudness(_files.Path("loud.mov"), measured);
        var loudness = Assert.NotNull(measured.Loudness);
        Assert.InRange(Assert.NotNull(loudness.MaxShortTerm), -18, 0);
        Assert.True(loudness.MaxMomentary >= loudness.MaxShortTerm);
    }

    // Sound that never fills a block has no level there (ebur128 prints its starting -120.7 LUFS): 1 s fills the
    // 400 ms block and not the 3 s one, 0.2 s neither (docs/decisions/2026-11-08-loudest-blocks-upstream.md).
    [Theory]
    [InlineData(1.0, true)]
    [InlineData(0.2, false)]
    public void ABlockNeverFilledHasNoLevel(double seconds, bool momentary)
    {
        const int Rate = 48000;
        var meter = new FrameReader.LoudnessMeter(Rate, 1, null);
        meter.Add([.. Enumerable.Range(0, (int)(seconds * Rate)).Select(n => (float)(0.1 * Math.Sin(2 * Math.PI * 1000 * n / Rate)))]);

        var blocks = Ffmpeg.FfmpegMeasurer.Blocks(meter.Finish().Loudness);

        Assert.Equal(momentary, blocks.Momentary is not null);
        if (momentary)
            Assert.InRange(blocks.Momentary!.Value, -30, -15);
        Assert.Null(blocks.ShortTerm);
    }

    // ebu-r128 reports them; ebu-r128-s1 fails a short-term above -18 LUFS, its timecodes the 3 s window's.
    [Fact]
    public void S1FailsShortTermAboveMinus18()
    {
        var measured = Loud;
        var reported = Profile.EbuR128.Evaluate(measured).Findings.Single(f => f.Check == "loudness.short-term");
        var judged = Profiles.EbuR128S1.Evaluate(measured).Findings.Single(f => f.Check == "loudness.short-term");

        Assert.Equal(Severity.Info, reported.Severity);
        Assert.Equal(Severity.Failure, judged.Severity);
        Assert.Contains("(at most -18.0 LUFS)", judged.Message, StringComparison.Ordinal);
        Assert.Equal(Profile.ShortTermFilter, judged.CheckedAgainst);
        Assert.Equal("0010B", judged.EbuQc?.Id);
        Assert.Equal(3, judged.End!.Value.Seconds - judged.Start!.Value.Seconds, 0.001);
        Assert.Equal(Severity.Info, Profile.EbuR128.Evaluate(measured).Findings.Single(f => f.Check == "loudness.momentary").Severity);
        Assert.Equal(Severity.Info, Profile.EbuR128.Evaluate(measured).Findings.Single(f => f.Check == "loudness.range").Severity);
    }

    // A profile file sets each limit: within passes, past fails.
    [Fact]
    public void AProfileFileSetsTheLimits()
    {
        var measured = Loud;
        var lenient = Profiles.Read("""{ "name": "x", "maxShortTerm": 0, "maxMomentary": 0, "maxRange": 20 }""");
        var strict = Profiles.Read("""{ "name": "x", "basedOn": "ebu-r128-s1", "maxMomentary": -30, "minRange": 5 }""");

        Severity Of(Profile profile, string check) => profile.Evaluate(measured).Findings.Single(f => f.Check == check).Severity;
        Assert.Equal(Severity.Pass, Of(lenient, "loudness.short-term"));
        Assert.Equal(Severity.Pass, Of(lenient, "loudness.momentary"));
        Assert.Equal(Severity.Pass, Of(lenient, "loudness.range"));
        Assert.Equal(Severity.Failure, Of(strict, "loudness.short-term"));
        Assert.Equal(Severity.Failure, Of(strict, "loudness.momentary"));
        Assert.Equal(Severity.Failure, Of(strict, "loudness.range"));
    }

    [Theory]
    [InlineData("""{ "name": "x", "maxShortTerm": 1 }""")]
    [InlineData("""{ "name": "x", "maxMomentary": 3 }""")]
    [InlineData("""{ "name": "x", "minRange": -1 }""")]
    [InlineData("""{ "name": "x", "minRange": 10, "maxRange": 5 }""")]
    public void ALimitOutOfItsRangeIsRefused(string json) =>
        Assert.Throws<ArgumentException>(() => Profiles.Read(json));

    // Shorter than 3 s has no short-term reading, as ebur128 has none: not reported.
    [Fact]
    public void ShorterThanAWindowHasNoReading()
    {
        var measured = Ffmpeg.FfmpegMeasurer.Measure(_files.Path("sound_short.mov"), Profile.EbuR128);
        FfmpegCli.AssertLoudness(_files.Path("sound_short.mov"), measured);
        Assert.DoesNotContain(Profiles.EbuR128S1.Evaluate(measured).Findings, f => f.Check == "loudness.short-term");
    }
}
