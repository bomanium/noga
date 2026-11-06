using System.Diagnostics;
using Noga.Ffmpeg;

namespace Noga.Tests;

/// <summary>
/// Single repeated frames (docs/decisions/2026-11-06-repeated-frames.md): the second FreezeMeter against freezedetect
/// at -80 dB and d a frame and a half, its two-frame freezes; each one information, a cadence summed up, longer holds
/// left to freeze.
/// </summary>
public sealed class RepeatTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("noga-repeat-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // testsrc2 for 6 s, two frames shown twice (a dropped frame filled with the one before; loop's start counts from 0
    // after the frame it names, so these are frames 49 and 100) and one held for five frames (a hold, not a repeat).
    [Theory]
    [InlineData("ffv1", "mkv")]
    [InlineData("libx264", "mp4")]
    public void TwoRepeatsAreFreezedetectsAndInformation(string codec, string extension)
    {
        string file = Make($"repeats.{extension}", "loop=loop=1:size=1:start=50,loop=loop=1:size=1:start=101,loop=loop=4:size=1:start=127,setpts=N/25/TB", codec);

        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);

        FfmpegCli.AssertRepeats(file, measured, Profile.EbuR128);
        Assert.Equal([2.0, 4.04], measured.Repeats.Select(t => Math.Round(t, 2)));
        var findings = Profile.EbuR128.Evaluate(measured).Findings.Where(f => f.Check == "repeat").ToList();
        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal(Severity.Info, f.Severity));
        Assert.Equal("00:00:02:00", findings[0].Start?.Timecode);
        Assert.Equal(Profile.EbuR128.RepeatFilter, findings[0].CheckedAgainst);
    }

    [Fact]
    public void AProfileCanMakeThemWarnings()
    {
        var profile = Profiles.Read("""{"name":"strict","repeatsWarn":true}""");
        var measured = new Measurements { Duration = 4, RateNumerator = 25, RateDenominator = 1, Repeats = [2.04] };

        var finding = Assert.Single(profile.Evaluate(measured).Findings, f => f.Check == "repeat");

        Assert.Equal(Severity.Warning, finding.Severity);
    }

    // Every fifth frame repeated, as 3:2 pulldown leaves it: one line, not each repeat.
    [Fact]
    public void ACadenceIsSummedUp()
    {
        string file = Make("cadence.mkv", "shuffleframes=0 1 1 3 4", "ffv1");

        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);

        FfmpegCli.AssertRepeats(file, measured, Profile.EbuR128);
        var finding = Assert.Single(Profile.EbuR128.Evaluate(measured).Findings, f => f.Check == "repeat");
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.StartsWith("Frames repeat in a cadence: ", finding.Message, StringComparison.Ordinal);
        Assert.Contains("(20%)", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoRepeatsSaysSo()
    {
        var measured = new Measurements { Duration = 4, RateNumerator = 25, RateDenominator = 1 };

        var finding = Assert.Single(Profile.EbuR128.Evaluate(measured).Findings, f => f.Check == "repeat");

        Assert.StartsWith("No repeated frames", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatsInBlackAreLeftOut()
    {
        var measured = new Measurements { Duration = 4, RateNumerator = 25, RateDenominator = 1, Black = [new TimeRange(1, 3)], Repeats = [2.04] };

        var finding = Assert.Single(Profile.EbuR128.Evaluate(measured).Findings, f => f.Check == "repeat");

        Assert.StartsWith("No repeated frames", finding.Message, StringComparison.Ordinal);
    }

    string Make(string name, string filter, string codec)
    {
        string file = Path.Combine(_folder, name);
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=25:d=6", "-vf", filter, "-c:v", codec, "-pix_fmt", "yuv420p", file])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, log);
        return file;
    }
}
