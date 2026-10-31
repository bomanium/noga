using System.Text.Json;

namespace MediaQc.Tests;

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

        Assert.Equal("media-qc", root.GetProperty("tool").GetString());
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
    }
}
