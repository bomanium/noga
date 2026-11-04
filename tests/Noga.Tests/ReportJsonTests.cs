using System.Text.Json;

namespace Noga.Tests;

public class ReportJsonTests
{
    [Fact]
    public void HasTheVerdictExitCodeAndFindings()
    {
        var measured = new Measurements
        {
            Duration = 20,
            RateNumerator = 25,
            RateDenominator = 1,
            Loudness = new LoudnessMeasure(-23.1, 5, double.NegativeInfinity),
            Black = [new TimeRange(5, 7)],
        };

        using var json = JsonDocument.Parse(ReportJson.Write(Profile.EbuR128.Evaluate(measured), "programme.mov"));
        var root = json.RootElement;

        Assert.Equal("Noga", root.GetProperty("tool").GetString());
        Assert.Equal("programme.mov", root.GetProperty("file").GetString());
        Assert.Equal("ebu-r128", root.GetProperty("profile").GetString());
        Assert.Equal("Warning", root.GetProperty("verdict").GetString());
        Assert.Equal(3, root.GetProperty("exitCode").GetInt32());
        var black = root.GetProperty("findings").EnumerateArray().Single(f => f.GetProperty("check").GetString() == "black");
        Assert.Equal("00:00:05:00", black.GetProperty("start").GetProperty("timecode").GetString());
        Assert.Equal(5, black.GetProperty("start").GetProperty("seconds").GetDouble());
        Assert.StartsWith("blackdetect=d=2:pix_th=0.10:pic_th=0.98", black.GetProperty("checkedAgainst").GetString(), StringComparison.Ordinal);
        var peak = root.GetProperty("findings").EnumerateArray().Single(f => f.GetProperty("check").GetString() == "loudness.true-peak");
        Assert.Equal("-Infinity", peak.GetProperty("value").GetString());
        Assert.Equal("True peak: none, the sound is silent (at most -1.0 dBTP)", peak.GetProperty("message").GetString());
    }

    [Fact]
    public void EachFindingNamesItsEbuQcItem()
    {
        var measured = new Measurements { Duration = 20, RateNumerator = 25, RateDenominator = 1, Loudness = new LoudnessMeasure(-23, 5, -3), Black = [new TimeRange(5, 7)] };
        var report = Profile.EbuR128.Evaluate(measured);

        using var json = JsonDocument.Parse(ReportJson.Write(report, "programme.mov"));
        var black = json.RootElement.GetProperty("findings").EnumerateArray().Single(f => f.GetProperty("check").GetString() == "black").GetProperty("ebuQc");
        string html = ReportHtml.Write(report, "programme.mov");

        Assert.Equal("0272B", black.GetProperty("id").GetString());
        Assert.Equal("Black Frames Report", black.GetProperty("name").GetString());
        Assert.Equal("https://qc.ebu.io/items/0272B", black.GetProperty("url").GetString());
        Assert.Contains("<a href=\"https://qc.ebu.io/items/0010B\"", html, StringComparison.Ordinal);
        Assert.Contains(">0084B Audio Peaks (TP)</a>", html, StringComparison.Ordinal);
    }

    // Every check the profile reports is an EBU QC item, but the three the catalogue has none for
    // (docs/decisions/2026-11-01-ebu-qc-items.md).
    [Fact]
    public void EveryCheckButBitrateAndShotsHasAnEbuQcItem()
    {
        var measured = new Measurements
        {
            Duration = 20,
            RateNumerator = 25,
            RateDenominator = 1,
            StartTimecode = "10:00:00:00",
            AudioTrack = new AudioTrack(1, 1, "pcm_s24le", 2, "stereo"),
            Loudness = new LoudnessMeasure(-23, 5, -3),
            LoudnessFrom = 5,
            Bars = [new TimeRange(0, 5)],
            Tone = [new ToneSpan(0, 5, 1000, -18)],
            Black = [new TimeRange(5, 7)],
            Silences = [new TimeRange(5, 7)],
            OutOfRange = [new RangeSpan(8, 9, 0.02)],
            Crop = new ActivePicture(1920, 1080, 1920, 800, 0, 140),
            PictureTypes = "IPPB",
            Packets = VideoPackets.From([(0, 100, true)]),
            Shots = [new TimeRange(0, 20)],
            NotMeasured = ["timecode"],
        };

        var findings = Profile.EbuR128.Evaluate(measured).Findings;

        Assert.Equal(["bitrate", "shot", "shots"], findings.Where(f => f.EbuQc is null).Select(f => f.Check).Distinct().Order());
        Assert.Equal(
            ["0004F", "0010B", "0014B", "0015B", "0026W", "0046F", "0051B", "0052B", "0078B", "0084B", "0272B"],
            findings.Where(f => f.EbuQc is not null).Select(f => f.EbuQc!.Id).Distinct().Order());
    }
}
