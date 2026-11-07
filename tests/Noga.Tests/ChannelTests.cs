using System.Diagnostics;
using FrameReader;
using Noga.Ffmpeg;
using ChannelMeter = Noga.Ffmpeg.ChannelMeter;

namespace Noga.Tests;

/// <summary>
/// An audio track's channels one by one (docs/decisions/2026-11-03-channel-faults.md): ChannelMeter's figures on
/// generated stereo files against the ffmpeg command line, and how each fault is judged.
/// </summary>
public sealed class ChannelTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("noga-channels-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // 10 s at 48 kHz, 16-bit: left a 1 kHz tone throughout; right inverted for 3 s, silent for 3 s, then the same as left.
    const string Mixed = "0.5*sin(2*PI*1000*t)|if(lt(t,3),-0.5*sin(2*PI*1000*t),if(lt(t,6),0,0.5*sin(2*PI*1000*t)))";

    string Wav(string name, string exprs, double seconds = 10)
    {
        string file = Path.Combine(_folder, name);
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-f", "lavfi", "-i", $"aevalsrc=exprs={exprs.Replace(",", "\\,", StringComparison.Ordinal)}:s=48000:d={seconds}", "-c:a", "pcm_s16le", file])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"ffmpeg failed:\n{log}");
        return file;
    }

    static ChannelMeasures Measure(string file, Profile profile)
    {
        Libraries.Load();
        using var audio = new AudioReader(file, new AudioReaderOptions());
        var meter = new ChannelMeter(audio.SampleRate, audio.Channels, profile.SilenceNoise, TimeSpan.FromSeconds(profile.MinSilence), profile.ClipRun);
        while (audio.TryRead(out var chunk))
            meter.Add(chunk.Samples);
        return meter.Finish();
    }

    [Fact]
    public void EachChannelsFiguresAreFfmpegs()
    {
        string file = Wav("mixed.wav", Mixed);

        var channels = Measure(file, Profile.EbuR128);

        FfmpegCli.AssertChannels(file, channels, Profile.EbuR128);
        Assert.Empty(channels.Silences[0]);
        var silence = Assert.Single(channels.Silences[1]);
        Assert.Equal((3, 6), (Math.Round(silence.Start, 1), Math.Round(silence.End, 1)));

        // -1 for 3 s, 0 for 3 s (right silent), +1 for 4 s, but where both are 0 (one sample in 24 at 1 kHz, in the
        // first 6 s) a sample counts as 1: (3 · -22/24 + 3 · 1/24 + 4) / 10.
        Assert.Equal(0.1375, Assert.NotNull(channels.Phase), 0.001);
        Assert.Equal([0, 0], channels.ClippedRuns);
    }

    // A 100 Hz tone at 1.5 times full scale, clipped to 16 bits: each half cycle one long run at full scale, up and down,
    // so the samples at full scale are astats' Peak count exactly.
    [Fact]
    public void ClippedRunsAreTheTimesAChannelReachesFullScale()
    {
        string file = Wav("clipped.wav", "1.5*sin(2*PI*100*t)|0.25*sin(2*PI*100*t)", 2);

        var channels = Measure(file, Profile.EbuR128);

        FfmpegCli.AssertChannels(file, channels, Profile.EbuR128, fullScaleBothWays: true);
        Assert.Equal(400, channels.ClippedRuns[0]);
        Assert.InRange(channels.FullScaleSamples[0], 50_000, 53_000);
        Assert.Equal(0, channels.ClippedRuns[1]);
    }

    [Fact]
    public void AnInvertedPairAndDualMonoAreFfmpegs()
    {
        string inverted = Wav("inverted.wav", "0.5*sin(2*PI*440*t)|-0.5*sin(2*PI*440*t)", 3);
        string dual = Wav("dual.wav", "0.3*sin(2*PI*440*t)|0.3*sin(2*PI*440*t)", 3);

        var (a, b) = (Measure(inverted, Profile.EbuR128), Measure(dual, Profile.EbuR128));

        FfmpegCli.AssertChannels(inverted, a, Profile.EbuR128);
        FfmpegCli.AssertChannels(dual, b, Profile.EbuR128);

        // A sample where both are 0 counts as 1 (aphasemeter's): one in 600 at 440 Hz.
        Assert.Equal(-1, Assert.NotNull(a.Phase), 0.01);
        Assert.Equal(1, Assert.NotNull(b.Phase), 0.001);
    }

    static ChannelMeasures Channels(double? phase = 0.8, IReadOnlyList<IReadOnlyList<TimeRange>>? silences = null, int[]? clipped = null, double[]? rms = null) =>
        new(2, 60, silences ?? [[], []], rms ?? [-20, -21], clipped ?? [0, 0], [0, 0], phase);

    static IReadOnlyList<Finding> Judge(ChannelMeasures channels, IReadOnlyList<TimeRange>? together = null) =>
        Profile.EbuR128.Evaluate(new Measurements
        {
            Duration = 60,
            RateNumerator = 25,
            RateDenominator = 1,
            Channels = channels,
            Silences = together ?? [],
        }).Findings.Where(f => f.Check.StartsWith("channel.", StringComparison.Ordinal)).ToList();

    [Fact]
    public void AChannelSilentThroughoutFailsWhileTheOtherHasSound()
    {
        var findings = Judge(Channels(phase: 0, silences: [[], [new TimeRange(0, 60)]]));

        var dead = Assert.Single(findings, f => f.Check == "channel.dead");
        Assert.Equal(Severity.Failure, dead.Severity);
        Assert.Equal("Channel 2 of 2 is silent throughout while the others have sound", dead.Message);
        Assert.DoesNotContain(findings, f => f.Check is "channel.silence" or "channel.phase");
        Assert.Equal("0078B", dead.EbuQc?.Id);
    }

    // Both channels silent at once is the track's silence (reported as silence), not a channel's: only the part where the
    // other has sound counts, from MinSilence on.
    [Fact]
    public void AChannelsSilenceCountsWhereTheOtherHasSound()
    {
        var findings = Judge(
            Channels(silences: [[new TimeRange(10, 12)], [new TimeRange(10, 15), new TimeRange(30, 31)]]),
            together: [new TimeRange(10, 12)]);

        var silence = Assert.Single(findings, f => f.Check == "channel.silence");
        Assert.Equal(Severity.Warning, silence.Severity);
        Assert.Equal("Channel 2 of 2 silent for 3.00 s while the others have sound", silence.Message);
        Assert.Equal(12, silence.Start?.Seconds);
        Assert.DoesNotContain(findings, f => f.Check == "channel.dead");
    }

    [Theory]
    [InlineData(-0.31, Severity.Warning)]
    [InlineData(-0.3, Severity.Pass)]
    [InlineData(0.95, Severity.Pass)]
    public void APairBelowTheMinimumPhaseIsOutOfPhase(double phase, Severity severity)
    {
        var finding = Assert.Single(Judge(Channels(phase)), f => f.Check == "channel.phase");

        Assert.Equal(severity, finding.Severity);
        Assert.Equal("0012B", finding.EbuQc?.Id);
    }

    [Fact]
    public void ClippingWarnsPerChannelAndDualMonoIsReported()
    {
        var findings = Judge(Channels(phase: 0.9995, clipped: [0, 7], rms: [-20, -20.05]));

        var clip = Assert.Single(findings, f => f.Check == "channel.clipping");
        Assert.Equal((Severity.Warning, "Channel 2 of 2 clips: 7 runs of 3 or more samples at full scale"), (clip.Severity, clip.Message));
        Assert.Equal(Severity.Info, Assert.Single(findings, f => f.Check == "channel.dual-mono").Severity);
        Assert.Equal(Severity.Pass, Assert.Single(Judge(Channels()), f => f.Check == "channel.clipping").Severity);
        Assert.DoesNotContain(Judge(Channels(phase: 0.9995, rms: [-20, -23])), f => f.Check == "channel.dual-mono");
    }

    [Fact]
    public void AProfileFileSetsThePhaseAndTheClippedRun()
    {
        var profile = Profiles.Read("""{ "name": "strict", "minPhase": 0, "clipRun": 2 }""");

        Assert.Equal((0.0, 2), (profile.MinPhase, profile.ClipRun));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "minPhase": -2 }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "clipRun": 0 }"""));
    }
}
