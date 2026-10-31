namespace MediaQc.Tests;

public class ProfileTests
{
    // tools/make-samples.sh's programme: bars and tone 0-5 s, black and silence 5-7 s, picture 7-17 s, black and
    // silence 17-20 s.
    static Measurements Programme(double integrated = -23, double truePeak = -3) => new()
    {
        Duration = 20,
        RateNumerator = 25,
        RateDenominator = 1,
        StartTimecode = "10:00:00:00",
        Loudness = new LoudnessMeasure(integrated, 5.5, truePeak),
        Bars = [new TimeRange(0, 5)],
        Tone = [new ToneSpan(0, 5, 1000, -18)],
        Black = [new TimeRange(5, 7), new TimeRange(17, 20)],
        Silences = [new TimeRange(5, 7), new TimeRange(17, 20)],
    };

    static Finding Only(QcReport report, string check) => Assert.Single(report.Findings, f => f.Check == check);

    [Theory]
    [InlineData(-23.0, Severity.Pass)]
    [InlineData(-22.5, Severity.Pass)]
    [InlineData(-23.54, Severity.Pass)]
    [InlineData(-23.56, Severity.Warning)]
    [InlineData(-22.0, Severity.Warning)]
    [InlineData(-21.9, Severity.Failure)]
    [InlineData(-24.2, Severity.Failure)]
    public void IntegratedLoudnessWithinHalfALuPassesAndWithinOneWarns(double integrated, Severity expected) =>
        Assert.Equal(expected, Only(Profile.EbuR128.Evaluate(Programme(integrated)), "loudness.integrated").Severity);

    [Theory]
    [InlineData(-1.0, Severity.Pass)]
    [InlineData(-1.04, Severity.Pass)]
    [InlineData(-0.9, Severity.Failure)]
    [InlineData(0.5, Severity.Failure)]
    public void TruePeakAboveMinusOneFails(double truePeak, Severity expected) =>
        Assert.Equal(expected, Only(Profile.EbuR128.Evaluate(Programme(truePeak: truePeak)), "loudness.true-peak").Severity);

    [Fact]
    public void SilentAudioFails()
    {
        var report = Profile.EbuR128.Evaluate(Programme() with { Loudness = new LoudnessMeasure(null, 0, double.NegativeInfinity) });

        Assert.Equal(Severity.Failure, Only(report, "loudness.integrated").Severity);
        Assert.Equal(QcReport.Failed, report.ExitCode());
    }

    [Fact]
    public void NoAudioFails() =>
        Assert.Equal(Severity.Failure, Only(Profile.EbuR128.Evaluate(Programme() with { Loudness = null }), "loudness.integrated").Severity);

    [Fact]
    public void BlackAndSilenceMidProgrammeWarnAndAtTheTailAreReported()
    {
        var report = Profile.EbuR128.Evaluate(Programme());
        var black = report.Findings.Where(f => f.Check == "black").ToList();

        Assert.Equal([Severity.Warning, Severity.Info], black.Select(f => f.Severity));
        Assert.Equal("10:00:05:00", black[0].Start?.Timecode);
        Assert.Equal("10:00:07:00", black[0].End?.Timecode);
        Assert.Contains("mid-programme", black[0].Message, StringComparison.Ordinal);
        Assert.Contains("tail", black[1].Message, StringComparison.Ordinal);
        Assert.Equal([Severity.Warning, Severity.Info], report.Findings.Where(f => f.Check == "silence").Select(f => f.Severity));
        Assert.Equal(QcReport.Warnings, report.ExitCode());
        Assert.Equal(QcReport.Failed, report.ExitCode(warningsFail: true));
    }

    [Fact]
    public void BlackAtTheHeadIsReported() =>
        Assert.Equal(Severity.Info, Only(Profile.EbuR128.Evaluate(Programme() with { Black = [new TimeRange(0, 3)] }), "black").Severity);

    [Fact]
    public void SpansShorterThanTheMinimumAreLeftOut() =>
        Assert.DoesNotContain(Profile.EbuR128.Evaluate(Programme() with { Black = [new TimeRange(8, 9.9)] }).Findings, f => f.Check == "black");

    [Fact]
    public void BarsToneAndRangeAreReportedNotJudged()
    {
        var report = Profile.EbuR128.Evaluate(Programme());

        Assert.Equal(Severity.Info, Only(report, "bars").Severity);
        Assert.Equal(Severity.Info, Only(report, "tone").Severity);
        Assert.Equal(Severity.Info, Only(report, "loudness.range").Severity);
        Assert.Equal("Line-up tone, 1000 Hz at -18.0 dBFS, for 5.00 s", Only(report, "tone").Message);
    }

    [Fact]
    public void CleanFilePasses()
    {
        var report = Profile.EbuR128.Evaluate(Programme() with { Black = [], Silences = [] });

        Assert.Equal(Severity.Info, report.Verdict);
        Assert.Equal(QcReport.Passed, report.ExitCode());
    }

    [Fact]
    public void EveryFindingSaysWhatItIsCheckedAgainst() =>
        Assert.All(Profile.EbuR128.Evaluate(Programme()).Findings, f => Assert.False(string.IsNullOrWhiteSpace(f.CheckedAgainst)));

    [Fact]
    public void TimecodesAreDropFrameAt2997()
    {
        var report = Profile.EbuR128.Evaluate(Programme() with { RateNumerator = 30000, RateDenominator = 1001, StartTimecode = null });

        Assert.True(report.DropFrame);
        Assert.Equal("00:00:00;00", report.StartTimecode);
    }
}
