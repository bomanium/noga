namespace MediaQc.Tests;

public class ProfileTests
{
    // A programme shaped like tools/make-samples.sh's: bars and tone 0-5 s, black and silence 5-7 s, picture 7-17 s,
    // black and silence 17-20 s.
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

    [Fact]
    public void LoudnessAfterTheLineUpSaysFromWhere()
    {
        var from = Only(Profile.EbuR128.Evaluate(Programme() with { LoudnessFrom = 5 }), "loudness.from");

        Assert.Equal(Severity.Info, from.Severity);
        Assert.Equal("10:00:05:00", from.Start?.Timecode);
        Assert.Contains("-ss 5 ", from.CheckedAgainst, StringComparison.Ordinal);
    }

    [Fact]
    public void LoudnessOfTheWholeFileSaysNothingMore() =>
        Assert.DoesNotContain(Profile.EbuR128.Evaluate(Programme()).Findings, f => f.Check == "loudness.from");

    [Fact]
    public void ASpanOverOnePercentOutOfRangeWarns()
    {
        var report = Profile.EbuR128.Evaluate(Programme() with
        {
            OutOfRange = [new RangeSpan(9, 10.5, 0.043)],
            WorstOutOfRange = new FrameShare(9.2, 0.043),
        });

        var range = Only(report, "range");
        Assert.Equal(Severity.Warning, range.Severity);
        Assert.Equal("10:00:09:00", range.Start?.Timecode);
        Assert.Contains("4.3%", range.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithinOnePercentOutOfRangePassesAndSaysTheWorst()
    {
        var range = Only(Profile.EbuR128.Evaluate(Programme() with { WorstOutOfRange = new FrameShare(12, 0.0067) }), "range");

        Assert.Equal(Severity.Pass, range.Severity);
        Assert.Contains("0.67%", range.Message, StringComparison.Ordinal);
        Assert.Equal("10:00:12:00", range.Start?.Timecode);
    }

    [Theory]
    [InlineData(1920, 800, 0, 140, "Active picture 1920x800 (2.40:1) at 0,140 in the 1920x1080 frame")]
    [InlineData(1920, 1072, 0, 4, "The picture fills the 1920x1080 frame")]
    public void TheActivePictureIsInformation(int width, int height, int x, int y, string message)
    {
        var crop = Only(Profile.EbuR128.Evaluate(Programme() with { Crop = new ActivePicture(1920, 1080, width, height, x, y) }), "crop");

        Assert.Equal(Severity.Info, crop.Severity);
        Assert.Equal(message, crop.Message);
        Assert.Contains("cropdetect", crop.CheckedAgainst, StringComparison.Ordinal);
    }

    [Fact]
    public void FrameTypesKeyframesAndBitrateAreInformation()
    {
        var measured = Programme() with
        {
            PictureTypes = "IBBP" + new string('B', 46) + "IPPP",
            Packets = VideoPackets.From([(0, 1_000_000, true), (2, 250_000, true), (2.5, 250_000, false)]),
        };

        var report = Profile.EbuR128.Evaluate(measured);

        var frames = Only(report, "frames");
        Assert.Equal(Severity.Info, frames.Severity);
        Assert.Equal("54 frames: 2 I, 4 P, 48 B; at most 50 frames from one I-frame to the next", frames.Message);
        var keyframes = Only(report, "keyframes");
        Assert.Equal("2 keyframes, at most 2.00 s apart", keyframes.Message);
        Assert.Equal("10:00:00:00", keyframes.Start?.Timecode);
        var bitrate = Only(report, "bitrate");
        Assert.Equal("Video bitrate 0.6 Mb/s on average, at most 8.0 Mb/s in a second", bitrate.Message);
        Assert.Equal("10:00:00:00", bitrate.Start?.Timecode);
        Assert.Equal(Severity.Info, report.Findings.Where(f => f.Check is "frames" or "keyframes" or "bitrate").Max(f => f.Severity));
    }

    [Fact]
    public void IntraOnlyVideoSaysSo()
    {
        var measured = Programme() with
        {
            PictureTypes = "III",
            Packets = VideoPackets.From([(0, 10, true), (0.04, 10, true), (0.08, 10, true)]),
        };

        var report = Profile.EbuR128.Evaluate(measured);

        Assert.Equal("All 3 frames intra-coded (I)", Only(report, "frames").Message);
        Assert.Equal("Every frame is a keyframe (3)", Only(report, "keyframes").Message);
    }

    [Fact]
    public void TheShotListIsInformation()
    {
        var measured = Programme() with { Shots = [new TimeRange(0, 5), new TimeRange(5, 8), new TimeRange(8, 20)] };

        var report = Profile.EbuR128.Evaluate(measured);

        var shots = Only(report, "shots");
        Assert.Equal("3 shots, the shortest 3.00 s", shots.Message);
        Assert.Equal(Profile.ShotsFilter, shots.CheckedAgainst);
        var each = report.Findings.Where(f => f.Check == "shot").ToList();
        Assert.Equal(["Shot 1, 5.00 s", "Shot 2, 3.00 s", "Shot 3, 12.00 s"], each.Select(f => f.Message));
        Assert.Equal("10:00:05:00", each[1].Start?.Timecode);
        Assert.Equal("10:00:08:00", each[1].End?.Timecode);
        Assert.All(each.Append(shots), f => Assert.Equal(Severity.Info, f.Severity));
    }
}
