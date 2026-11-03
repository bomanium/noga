namespace Noga.Tests;

/// <summary>
/// The line-up tone's level and what a profile asks for at the head and the tail
/// (docs/decisions/2026-11-03-line-up-and-head-tail.md).
/// </summary>
public sealed class HeadTailTests
{
    // A programme laid out as make-samples.sh's: bars and tone 0-5 s, black and silence 5-8 s, picture to 17 s, black
    // and silence to 20 s.
    static Measurements Programme(double toneLevel = -18) => new()
    {
        Duration = 20,
        RateNumerator = 25,
        RateDenominator = 1,
        Bars = [new TimeRange(0, 5)],
        Tone = [new ToneSpan(0, 5, 1000, toneLevel)],
        Black = [new TimeRange(5, 8), new TimeRange(17, 20)],
        Silences = [new TimeRange(5, 8), new TimeRange(17, 20)],
    };

    static Finding Only(QcReport report, string check) => Assert.Single(report.Findings, f => f.Check == check);

    [Theory]
    [InlineData("ebu-r128", -18.4, Severity.Pass)]
    [InlineData("ebu-r128", -18.6, Severity.Warning)]
    [InlineData("atsc-a85", -20.0, Severity.Pass)]
    [InlineData("atsc-a85", -18.0, Severity.Warning)]
    [InlineData("op-59", -12.0, Severity.Info)]
    [InlineData("arib-tr-b32", -12.0, Severity.Info)]
    public void TheToneLevelIsJudgedByTheProfile(string profile, double level, Severity severity) =>
        Assert.Equal(severity, Only(Profiles.Get(profile).Evaluate(Programme(level)), "tone").Severity);

    [Fact]
    public void WhatTheHeadAndTailHaveIsMeasuredAgainstWhatTheProfileAsks()
    {
        var profile = Profile.EbuR128 with { HeadBars = 5, HeadTone = 5, HeadBlack = 3, TailBlack = 3 };

        var report = profile.Evaluate(Programme());

        foreach (string check in (string[])["head.bars", "head.tone", "head.black", "tail.black"])
            Assert.Equal(Severity.Pass, Only(report, check).Severity);
        Assert.Equal("Black with silence before the programme for 3.00 s (at least 3 s)", Only(report, "head.black").Message);
        Assert.Equal(17, Only(report, "tail.black").Start?.Seconds);
    }

    [Fact]
    public void AHeadOrTailShortOfWhatIsAskedFails()
    {
        var profile = Profile.EbuR128 with { HeadBars = 30, HeadTone = 30, HeadBlack = 10, TailBlack = 5 };
        var measured = Programme() with { Silences = [new TimeRange(5, 8)] };

        var report = profile.Evaluate(measured);

        Assert.Equal("Bars at the head for 5.00 s (at least 30 s)", Only(report, "head.bars").Message);
        Assert.All((string[])["head.bars", "head.tone", "head.black", "tail.black"], c => Assert.Equal(Severity.Failure, Only(report, c).Severity));
        Assert.Equal("No black with silence at the tail (at least 5 s)", Only(report, "tail.black").Message);
        Assert.Equal(QcReport.Failed, report.ExitCode());
    }

    // Without bars, the black before the programme is the one at the start; nothing asked, nothing judged.
    [Fact]
    public void WithoutBarsTheHeadsBlackIsFromTheStart()
    {
        var measured = Programme() with { Bars = [], Tone = [], Black = [new TimeRange(0, 4)], Silences = [new TimeRange(0, 4)] };

        var report = (Profile.EbuR128 with { HeadBlack = 4 }).Evaluate(measured);

        Assert.Equal(Severity.Pass, Only(report, "head.black").Severity);
        Assert.DoesNotContain(Profile.EbuR128.Evaluate(measured).Findings, f => f.Check.StartsWith("head.", StringComparison.Ordinal) || f.Check.StartsWith("tail.", StringComparison.Ordinal));
    }

    [Fact]
    public void AProfileFileSetsTheToneAndTheHeadAndTail()
    {
        var profile = Profiles.Read("""{ "name": "x", "basedOn": "atsc-a85", "toneTolerance": 1, "headBars": 60, "tailBlack": 2 }""");

        Assert.Equal((-20.0, 1.0, 60.0, 2.0), (profile.ToneLevel, profile.ToneTolerance, profile.HeadBars, profile.TailBlack));
        Assert.Equal(-18, Profiles.Read("""{ "name": "y", "basedOn": "op-59", "toneLevel": -18 }""").ToneLevel);
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "z", "toneLevel": 3 }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "z", "headBars": -1 }"""));
    }
}
