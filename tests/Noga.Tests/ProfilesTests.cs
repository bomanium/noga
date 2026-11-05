namespace Noga.Tests;

/// <summary>
/// The shipped profiles' thresholds (docs/decisions/2026-11-01-profiles.md; OP-59's and ARIB TR-B32's from their own
/// documents, docs/decisions/2026-11-02-op59-arib-figures.md) and profile files.
/// </summary>
public sealed class ProfilesTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("noga-profiles-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    static Measurements Programme(double integrated, double truePeak) => new()
    {
        Duration = 20,
        RateNumerator = 25,
        RateDenominator = 1,
        Loudness = new LoudnessMeasure(integrated, 5, truePeak),
    };

    static Severity Verdict(Profile profile, string check, double integrated, double truePeak) =>
        Assert.Single(profile.Evaluate(Programme(integrated, truePeak)).Findings, f => f.Check == check).Severity;

    [Theory]
    [InlineData("ebu-r128", -23.0, Severity.Pass)]
    [InlineData("ebu-r128", -24.0, Severity.Warning)]
    [InlineData("atsc-a85", -24.0, Severity.Pass)]
    [InlineData("atsc-a85", -22.0, Severity.Pass)]
    [InlineData("atsc-a85", -26.1, Severity.Failure)]
    [InlineData("op-59", -24.0, Severity.Pass)]
    [InlineData("op-59", -23.0, Severity.Pass)]
    [InlineData("op-59", -22.9, Severity.Failure)]
    [InlineData("op-59", -25.0, Severity.Pass)]
    [InlineData("op-59", -25.1, Severity.Failure)]
    [InlineData("arib-tr-b32", -24.0, Severity.Pass)]
    [InlineData("arib-tr-b32", -23.0, Severity.Pass)]
    [InlineData("arib-tr-b32", -22.9, Severity.Failure)]
    [InlineData("arib-tr-b32", -25.0, Severity.Pass)]
    [InlineData("arib-tr-b32", -25.1, Severity.Failure)]
    public void EachProfileHoldsItsLoudnessTarget(string name, double integrated, Severity expected) =>
        Assert.Equal(expected, Verdict(Profiles.Get(name), "loudness.integrated", integrated, -6));

    [Theory]
    [InlineData("ebu-r128", -1.5, Severity.Pass)]
    [InlineData("atsc-a85", -1.5, Severity.Failure)]
    [InlineData("atsc-a85", -2.0, Severity.Pass)]
    [InlineData("op-59", -2.0, Severity.Pass)]
    [InlineData("op-59", -1.9, Severity.Failure)]
    [InlineData("arib-tr-b32", -1.0, Severity.Pass)]
    [InlineData("arib-tr-b32", -0.9, Severity.Failure)]
    public void EachProfileHoldsItsTruePeak(string name, double truePeak, Severity expected) =>
        Assert.Equal(expected, Verdict(Profiles.Get(name), "loudness.true-peak", -23, truePeak));

    [Fact]
    public void TheReportNamesTheProfile() =>
        Assert.Equal("atsc-a85", Profiles.AtscA85.Evaluate(Programme(-24, -6)).Profile);

    [Fact]
    public void AProfileFileStartsFromOneAndChangesWhatItSays()
    {
        string path = Path.Combine(_folder, "broadcaster.json");
        File.WriteAllText(path, """
            {
              // A broadcaster's own: ATSC's target, a stricter peak, longer black allowed.
              "name": "broadcaster",
              "basedOn": "atsc-a85",
              "maxTruePeak": -3,
              "minBlack": 4,
            }
            """);

        var profile = Profiles.Get(path);

        Assert.Equal("broadcaster", profile.Name);
        Assert.Equal(-24, profile.TargetLoudness);
        Assert.Equal(2, profile.LoudnessPass);
        Assert.Equal(-3, profile.MaxTruePeak);
        Assert.Equal(4, profile.MinBlack);
        Assert.Equal(Profile.EbuR128.SilenceNoise, profile.SilenceNoise);
    }

    [Theory]
    [InlineData("""{ "maxTruePeak": -2 }""", "no name")]
    [InlineData("""{ "name": "x", "maxTruPeak": -2 }""", "isn't a profile")]
    [InlineData("""{ "name": "x", "loudnessPass": -1 }""", "loudnessPass")]
    [InlineData("""{ "name": "x", "maxOutOfRange": 5 }""", "maxOutOfRange")]
    [InlineData("""{ "name": "x", "basedOn": "nab" }""", "No profile nab")]
    [InlineData("not json", "isn't a profile")]
    public void AProfileFileThatDoesntHoldIsRefusedSayingWhy(string json, string why) =>
        Assert.Contains(why, Assert.Throws<ArgumentException>(() => Profiles.Read(json, "test.json")).Message, StringComparison.Ordinal);

    [Fact]
    public void AnUnknownProfileNamesTheKnownOnes() =>
        Assert.Contains("ebu-r128, ebu-r128-s1, atsc-a85, op-59, arib-tr-b32", Assert.Throws<ArgumentException>(() => Profiles.Get("netflix")).Message, StringComparison.Ordinal);
}
