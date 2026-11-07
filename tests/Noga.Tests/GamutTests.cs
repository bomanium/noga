using System.Diagnostics;
using FrameReader;
using Noga.Ffmpeg;
using GamutMeter = Noga.Ffmpeg.GamutMeter;

namespace Noga.Tests;

/// <summary>
/// Pixels outside RGB gamut (EBU R 103; docs/decisions/2026-11-06-rgb-gamut.md): GamutMeter against FFmpeg's geq, and
/// how they are judged.
/// </summary>
public sealed class GamutTests : IDisposable
{
    // BT.601's Kr and Kb: the test files are 320x240 and untagged.
    const double Kr = 0.299, Kb = 0.114;

    readonly string _folder = Directory.CreateTempSubdirectory("noga-gamut-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // 1 s of grey, 2 s of a chroma ramp (its saturated corners out of gamut), 1 s of grey with a patch of 0.5% out of
    // gamut: the ramp warns, the patch is under R 103's 1%.
    [Theory]
    [InlineData("yuv422p")]
    [InlineData("yuv420p")]
    [InlineData("yuv422p10le")]
    public void TheSharesAreGeqsAndASpanOverOnePercentWarns(string pixelFormat)
    {
        string file = Make(pixelFormat);
        var profile = Profile.EbuR128;

        var meter = new GamutMeter(profile.GamutLow, profile.GamutHigh);
        Pictures.Measure(file, gamut: meter);
        Assert.Equal((Kr, Kb), meter.Matrix);
        var shares = FfmpegCli.GamutShares(file, profile.GamutLow, profile.GamutHigh, Kr, Kb);
        Assert.Equal(shares.Count, meter.Shares.Count);
        for (int i = 0; i < shares.Count; i++)
            Assert.Equal(shares[i], meter.Shares[i], 0.00001);

        var measured = FfmpegMeasurer.Measure(file, profile);
        FfmpegCli.AssertGamut(file, measured, profile, Kr, Kb);
        var span = Assert.Single(measured.OutOfGamut);
        Assert.Equal((1.0, 3.0), (Math.Round(span.Start, 2), Math.Round(span.End, 2)));
        Assert.InRange(meter.Shares[^1], 0.004, 0.006);

        var finding = Assert.Single(profile.Evaluate(measured).Findings, f => f.Check == "gamut");
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.StartsWith("Pixels outside RGB gamut (-5% to 105%): up to ", finding.Message, StringComparison.Ordinal);
        Assert.EndsWith("(over 1%) for 2.00 s", finding.Message, StringComparison.Ordinal);
        Assert.Equal("0051B", finding.EbuQc?.Id);
    }

    // The arithmetic at the limits: white and black in gamut, and a code past 105% or below -5% not.
    [Fact]
    public void TheLimitsAreRgbs()
    {
        var meter = new GamutMeter(-0.05, 1.05, "bt709");
        Add(meter, 128, 128, 128, 0);

        Assert.True(meter.InGamut(235, 128, 128));
        Assert.True(meter.InGamut(16, 128, 128));
        Assert.True(meter.InGamut(16 + 229, 128, 128));
        Assert.False(meter.InGamut(16 + 230, 128, 128));
        Assert.True(meter.InGamut(16 - 10, 128, 128));
        Assert.False(meter.InGamut(16 - 11, 128, 128));

        // 100% red (BT.709: Y 63, Cb 102, Cr 240) is in gamut; Cr at its top with mid grey isn't.
        Assert.True(meter.InGamut(63, 102, 240));
        Assert.False(meter.InGamut(126, 128, 240));

        // No code is in gamut with both chroma samples at their extremes: every pixel there is outside.
        Assert.DoesNotContain(Enumerable.Range(0, 256), y => meter.InGamut(y, 16, 16));
        var empty = new GamutMeter(-0.05, 1.05, "bt709");
        Add(empty, 128, 16, 16, 0);
        Assert.Equal(1.0, empty.Shares[0]);
    }

    // Bars are 100% colours with a PLUGE below black on purpose: their frames are left out.
    [Fact]
    public void BarsAreLeftOut()
    {
        var meter = new GamutMeter(-0.05, 1.05, "bt709");
        for (int i = 0; i < 50; i++)
            Add(meter, 126, 128, 240, i * 0.04);
        for (int i = 50; i < 100; i++)
            Add(meter, 126, 128, 128, i * 0.04);

        var (spans, worst) = meter.Finish([new TimeRange(0, 2)], 0.01, 1, 0.04);
        Assert.Empty(spans);
        Assert.Equal(0, worst?.Share);

        var (all, _) = meter.Finish([], 0.01, 1, 0.04);
        Assert.Equal((0.0, 2.0), (all.Single().Start, Math.Round(all.Single().End, 9)));
    }

    [Fact]
    public void WithinTheShareItPasses()
    {
        var measured = new Measurements { Duration = 10, RateNumerator = 25, RateDenominator = 1, WorstOutOfGamut = new FrameShare(4, 0.004) };

        var finding = Assert.Single(Profile.EbuR128.Evaluate(measured).Findings, f => f.Check == "gamut");

        Assert.Equal(Severity.Pass, finding.Severity);
        Assert.Equal("Pixels outside RGB gamut (-5% to 105%): at most 0.40% of the picture (at most 1%)", finding.Message);
    }

    [Fact]
    public void AProfileFileSetsTheShareAndLimits()
    {
        var profile = Profiles.Read("""{ "name": "x", "maxOutOfGamut": 0.02, "gamutLow": -0.03, "gamutHigh": 1.03 }""");

        Assert.Equal((0.02, -0.03, 1.03), (profile.MaxOutOfGamut, profile.GamutLow, profile.GamutHigh));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "gamutLow": 0.1 }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "maxOutOfGamut": 2 }"""));
    }

    // A 4x2 frame of one colour, 4:4:4 at 8 bits, limited range.
    static void Add(GamutMeter meter, byte y, byte cb, byte cr, double seconds)
    {
        byte[] Plane(byte value) => [.. Enumerable.Repeat(value, 8)];
        meter.Add(Plane(y), 4, Plane(cb), 4, Plane(cr), 4, 4, 2, ChromaSubsampling.Yuv444, 8, false, seconds);
    }

    string Make(string pixelFormat)
    {
        string file = Path.Combine(_folder, $"gamut-{pixelFormat}.mkv");
        const string Ramp = "between(T,0.999,2.999)";
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-f", "lavfi", "-i",
            $"nullsrc=s=320x240:r=25:d=4,format=yuv444p,geq=lum=128:cb='if({Ramp},16+X*224/W,128)':cr='if({Ramp},16+Y*224/H,if(gte(T,3)*lt(X,16)*lt(Y,24),240,128))',format={pixelFormat}",
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
