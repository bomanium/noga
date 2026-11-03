using System.Diagnostics;
using MediaQc.Ffmpeg;

namespace MediaQc.Tests;

/// <summary>Interlacing from the picture (docs/decisions/2026-11-03-interlacing.md): InterlaceMeter against idet, and the picture against the flag.</summary>
public sealed class InterlaceTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("mediaqc-interlace-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // 50p moving picture woven into 25i, top field first; flagged as it is, or (setfield=prog) as progressive.
    string Woven(string name, bool flagProgressive)
    {
        string file = Path.Combine(_folder, name);
        string filter = flagProgressive ? "interlace=scan=tff,setfield=prog" : "interlace=scan=tff";
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc2=s=720x576:r=50:d=4", "-vf", filter, "-c:v", "prores_ks", file])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, log);
        return file;
    }

    [Fact]
    public void AnInterlacedPictureFlaggedSoPasses()
    {
        string file = Woven("flagged.mov", flagProgressive: false);

        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);

        FfmpegCli.AssertInterlace(file, measured);
        var interlace = Assert.IsType<InterlaceMeasures>(measured.Interlace);
        Assert.True(interlace.LooksInterlaced);
        Assert.True(interlace.TopFieldFirst);
        var findings = Profile.EbuR128.Evaluate(measured).Findings;
        Assert.StartsWith("The picture looks interlaced, top field first", Assert.Single(findings, f => f.Check == "interlace").Message, StringComparison.Ordinal);
        Assert.Equal(Severity.Pass, Assert.Single(findings, f => f.Check == "interlace.flag").Severity);
    }

    [Fact]
    public void AnInterlacedPictureFlaggedProgressiveWarns()
    {
        string file = Woven("unflagged.mov", flagProgressive: true);

        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);

        FfmpegCli.AssertInterlace(file, measured);
        var flag = Assert.Single(Profile.EbuR128.Evaluate(measured).Findings, f => f.Check == "interlace.flag");
        Assert.Equal(Severity.Warning, flag.Severity);
        Assert.StartsWith("The stream says progressive (progressive), but the picture looks interlaced", flag.Message, StringComparison.Ordinal);
        Assert.Equal("0048B", flag.EbuQc?.Id);
    }

    // Nothing either way (every frame undetermined), or no flag: reported, not judged.
    [Theory]
    [InlineData(0, 0, 0, 10, "progressive")]
    [InlineData(0, 0, 10, 0, null)]
    public void WithoutAnAnswerOrAFlagNothingIsJudged(long tff, long bff, long progressive, long undetermined, string? order)
    {
        var video = new VideoFormat("prores", "HQ", 720, 576, "25/1", order, "yuv422p10le", 10, null, null, null, null, "5:4");
        var measured = new Measurements
        {
            Duration = 4,
            RateNumerator = 25,
            RateDenominator = 1,
            Format = new MediaFormat("mov", 4, video, []),
            Interlace = new InterlaceMeasures(0, 0, 0, 0, tff, bff, progressive, undetermined),
        };

        var findings = Profile.EbuR128.Evaluate(measured).Findings;

        Assert.Single(findings, f => f.Check == "interlace");
        Assert.DoesNotContain(findings, f => f.Check == "interlace.flag");
    }
}
