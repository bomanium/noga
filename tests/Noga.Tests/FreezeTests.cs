using System.Diagnostics;
using Noga.Ffmpeg;

namespace Noga.Tests;

/// <summary>Frozen pictures (docs/decisions/2026-11-03-freeze-frames.md): FreezeMeter against freezedetect, and how a freeze is judged.</summary>
public sealed class FreezeTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("noga-freeze-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // 3 s moving, its last frame held for 4 s, 3 s moving again, then held for 3 s to the end: a freeze mid-programme
    // and one at the tail.
    [Fact]
    public void FreezesAreFreezedetectsAndAMidProgrammeOneWarns()
    {
        string file = Path.Combine(_folder, "frozen.mkv");
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-filter_complex",
            "testsrc2=s=320x240:r=25:d=3,tpad=stop_mode=clone:stop_duration=4[a];testsrc2=s=320x240:r=25:d=3,tpad=stop_mode=clone:stop_duration=3[b];[a][b]concat=n=2:v=1:a=0",
            "-c:v", "ffv1", file])
        {
            start.ArgumentList.Add(arg);
        }

        using (var process = Process.Start(start)!)
        {
            string log = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, log);
        }

        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);

        FfmpegCli.AssertFreezes(file, measured, Profile.EbuR128);
        Assert.Equal([(3, 7), (10, 13)], measured.Freezes.Select(f => (Math.Round(f.Start), Math.Round(f.End))));
        var findings = Profile.EbuR128.Evaluate(measured).Findings.Where(f => f.Check == "freeze").ToList();
        Assert.Equal([Severity.Warning, Severity.Info], findings.Select(f => f.Severity));
        Assert.StartsWith("Picture frozen for 4.0", findings[0].Message, StringComparison.Ordinal);
        Assert.Equal("0044B", findings[0].EbuQc?.Id);
    }

    // Black and bars are still by nature: only a freeze's part outside them counts, from MinFreeze on.
    [Fact]
    public void BlackAndBarsArentFreezes()
    {
        var measured = new Measurements
        {
            Duration = 30,
            RateNumerator = 25,
            RateDenominator = 1,
            Bars = [new TimeRange(0, 5)],
            Black = [new TimeRange(5, 8), new TimeRange(20, 23)],
            Freezes = [new TimeRange(0, 5), new TimeRange(5, 8), new TimeRange(19, 23), new TimeRange(23.5, 27)],
        };

        var findings = Profile.EbuR128.Evaluate(measured).Findings.Where(f => f.Check == "freeze").ToList();

        var freeze = Assert.Single(findings);
        Assert.Equal((23.5, 27.0), (freeze.Start?.Seconds, freeze.End?.Seconds));
        Assert.Equal(Severity.Warning, freeze.Severity);
    }

    [Fact]
    public void AProfileFileSetsTheFreezeNoiseAndLength()
    {
        var profile = Profiles.Read("""{ "name": "x", "freezeNoise": 0.002, "minFreeze": 1 }""");

        Assert.Equal((0.002, 1.0), (profile.FreezeNoise, profile.MinFreeze));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "minFreeze": 0 }"""));
    }
}
