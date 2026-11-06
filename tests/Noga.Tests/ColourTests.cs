using System.Diagnostics;
using FrameReader;
using Noga.Ffmpeg;

namespace Noga.Tests;

/// <summary>
/// Constant-colour frames (docs/decisions/2026-11-06-constant-colour.md): ColourMeter against signalstats, and how a
/// run of one flat colour is judged.
/// </summary>
public sealed class ColourTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("noga-colour-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // 2 s moving, 3 s of red, 2 s moving, 3 s of black, 2 s moving, then 2.6 s of grey to the end: red mid-programme
    // warns, the black is black's, the grey at the tail is information.
    [Theory]
    [InlineData("yuv420p")]
    [InlineData("yuv422p10le")]
    public void FlatColoursAreSignalstatsAndAMidProgrammeOneWarns(string pixelFormat)
    {
        string file = Make(pixelFormat);

        var meter = new ColourMeter(Profile.EbuR128.ColourSpread, Profile.EbuR128.MinConstantColour);
        Pictures.Measure(file, colours: meter);
        FfmpegCli.AssertSignalstats(file, meter.Frames, Profile.EbuR128.ColourSpread * (pixelFormat.Contains("p10", StringComparison.Ordinal) ? 4 : 1));
        Assert.Contains(meter.Frames, f => f is null);

        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);
        FfmpegCli.AssertConstantColour(file, measured, Profile.EbuR128);
        Assert.Equal([(2, 5), (7, 10), (12, 14.6)], measured.ConstantColour.Select(c => (Math.Round(c.Start, 1), Math.Round(c.End, 1))));

        var report = Profile.EbuR128.Evaluate(measured);
        var colour = report.Findings.Where(f => f.Check == "colour").ToList();
        Assert.Equal([Severity.Warning, Severity.Info], colour.Select(f => f.Severity));
        Assert.Matches(@"^One colour, #F[EF]000[01] \(Y 8[12], Cb 9[01], Cr 24[01]\),", colour[0].Message);
        Assert.EndsWith("for 3.00 s mid-programme", colour[0].Message, StringComparison.Ordinal);
        Assert.EndsWith("for 2.60 s at the tail", colour[1].Message, StringComparison.Ordinal);
        Assert.Equal("0016B", colour[0].EbuQc?.Id);

        // Still pictures, but reported as their colour and black: not as freezes.
        Assert.DoesNotContain(report.Findings, f => f.Check == "freeze");
    }

    // Black and bars are reported as such: only a run's part outside them counts, from MinConstantColour on.
    [Fact]
    public void BlackAndBarsArentConstantColour()
    {
        var measured = new Measurements
        {
            Duration = 30,
            RateNumerator = 25,
            RateDenominator = 1,
            Bars = [new TimeRange(0, 5)],
            Black = [new TimeRange(10, 13)],
            ConstantColour = [ColourSpan.Of(0, 5, 128, 128, 128, false, false), ColourSpan.Of(10, 13, 16, 128, 128, false, false), ColourSpan.Of(20, 23, 81, 90, 240, false, false)],
            Freezes = [new TimeRange(20, 23)],
        };

        var findings = Profile.EbuR128.Evaluate(measured).Findings;

        var colour = Assert.Single(findings, f => f.Check == "colour");
        Assert.Equal((20.0, 23.0), (colour.Start?.Seconds, colour.End?.Seconds));
        Assert.Equal(Severity.Warning, colour.Severity);
        Assert.DoesNotContain(findings, f => f.Check == "freeze");
    }

    [Theory]
    [InlineData(235, 128, 128, false, false, "#FFFFFF")]
    [InlineData(16, 128, 128, false, false, "#000000")]
    [InlineData(62.5594, 102.3359, 240, false, false, "#FF0000")]
    [InlineData(0, 128, 128, true, false, "#000000")]
    [InlineData(81.481, 90.2032, 240, false, true, "#FF0000")]
    public void TheColourIsGivenAsRgb(double y, double cb, double cr, bool fullRange, bool sd, string rgb) =>
        Assert.Equal(rgb, ColourSpan.Of(0, 1, y, cb, cr, fullRange, sd).Rgb);

    [Fact]
    public void AProfileFileSetsTheSpreadAndLength()
    {
        var profile = Profiles.Read("""{ "name": "x", "colourSpread": 2, "minConstantColour": 0.5 }""");

        Assert.Equal((2.0, 0.5), (profile.ColourSpread, profile.MinConstantColour));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "minConstantColour": 0 }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "colourSpread": -1 }"""));
    }

    string Make(string pixelFormat)
    {
        string file = Path.Combine(_folder, $"colour-{pixelFormat}.mkv");
        const string Size = "s=320x240:r=25";
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-filter_complex",
            $"testsrc2={Size}:d=2[a];color=c=red:{Size}:d=3[b];testsrc2={Size}:d=2[c];color=c=black:{Size}:d=3[d];testsrc2={Size}:d=2[e];color=c=gray:{Size}:d=2.6[f];[a][b][c][d][e][f]concat=n=6:v=1:a=0,format={pixelFormat}",
            "-c:v", "ffv1", file])
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, log);
        return file;
    }
}
