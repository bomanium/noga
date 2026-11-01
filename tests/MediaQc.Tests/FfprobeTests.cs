namespace MediaQc.Tests;

public class FfprobeTests
{
    // What ffprobe -show_entries format_tags=timecode:stream_tags=timecode:stream=codec_type -of json gives.
    const string Mov = """{"streams":[{"codec_type":"video","tags":{"timecode":"10:00:00:00"}},{"codec_type":"data","tags":{"timecode":"10:00:00:00"}}],"format":{"tags":{}}}""";
    const string Mxf = """{"streams":[{"codec_type":"video","tags":{}}],"format":{"tags":{"timecode":"09:59:30:00"}}}""";
    const string None = """{"streams":[{"codec_type":"video","tags":{}},{"codec_type":"audio"}],"format":{"tags":{"encoder":"Lavf"}}}""";

    [Fact]
    public void MovAndMp4GiveTheVideoStreamsTag() => Assert.Equal("10:00:00:00", Ffprobe.Pick(Mov, 25, 1));

    [Fact]
    public void MxfGivesTheContainersTag() => Assert.Equal("09:59:30:00", Ffprobe.Pick(Mxf, 25, 1));

    [Fact]
    public void AFileWithoutATimecodeGivesNull() => Assert.Null(Ffprobe.Pick(None, 25, 1));

    [Fact]
    public void ATimecodeThatIsntOneAtTheRateIsIgnored() => Assert.Null(Ffprobe.Pick(Mov.Replace("10:00:00:00", "10:00:00:27", StringComparison.Ordinal), 25, 1));

    [Fact]
    public void WithoutFfprobeTheTimecodeIsNotMeasured()
    {
        var measured = new Measurements { Duration = 1, RateNumerator = 25, RateDenominator = 1, NotMeasured = ["black"] };

        var report = Profile.EbuR128.Evaluate(measured with { NotMeasured = [.. measured.NotMeasured, "timecode"] });

        var finding = Assert.Single(report.Findings, f => f.Check == "timecode");
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Contains("ffprobe", finding.Message, StringComparison.Ordinal);
        Assert.Equal("00:00:00:00", report.StartTimecode);
    }
}
