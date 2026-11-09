using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Noga.Ffmpeg;

namespace Noga.Tests;

/// <summary>
/// The acx and acx-master profiles (docs/decisions/2026-11-09-acx-profile.md): an MP3's frames against ffprobe's
/// packets, the RMS level and sample peak against astats, and how each of ACX's figures is judged.
/// </summary>
public sealed class AcxTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("noga-acx-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // White noise (anoisesrc's uniform samples, ±a: RMS a/√3) at -20 dB RMS, peaks near -15 dBFS: within ACX's window.
    const string Speech = "anoisesrc=d=8:r=44100:a=0.1732:seed=7";

    string Make(string name, string source, params string[] output)
    {
        string file = Path.Combine(_folder, name);
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-f", "lavfi", "-i", source, .. output, file])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"ffmpeg failed:\n{log}");
        return file;
    }

    string Mp3(string name, string source, string rate = "192k", int sampleRate = 44100) =>
        Make(name, source, "-ar", sampleRate.ToString(CultureInfo.InvariantCulture), "-c:a", "libmp3lame", "-b:a", rate);

    // The packets ffprobe gives (one a frame), each size's bit rate the Layer III one whose frame is that long.
    static (long Count, long Bytes, Dictionary<int, long> Rates) Packets(string file, int sampleRate)
    {
        int[] rates = [32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
        var sizes = FfmpegCli.Ffprobe(file, "-select_streams", "a:0", "-show_packets", "-show_entries", "packet=size", "-of", "csv=p=0")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => int.Parse(line.Split(',')[0], CultureInfo.InvariantCulture))
            .ToList();
        var counted = sizes
            .Select(size => 1000 * rates.Single(kbps => (144_000 * kbps / sampleRate) is var length && (size == length || size == length + 1)))
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => (long)g.Count());
        return (sizes.Count, sizes.Sum(), counted);
    }

    static void AssertFramesAreFfprobes(string file, Mp3Frames frames, int sampleRate = 44100)
    {
        var (count, bytes, rates) = Packets(file, sampleRate);
        Assert.Equal(count, frames.Count);
        Assert.Equal(bytes, frames.Bytes);
        Assert.Equal(rates.OrderBy(r => r.Key), frames.BitRates.OrderBy(r => r.Key));
    }

    // astats' Overall RMS level and Peak level for the track.
    static (double Rms, double Peak) Astats(string file, int track = 1)
    {
        string log = FfmpegCli.Run(file, $"-map 0:a:{track - 1} -af astats=measure_perchannel=none:measure_overall=Peak_level+RMS_level");
        double Level(string name) => double.Parse(Regex.Match(log, $@"{name} level dB: (\S+)").Groups[1].Value, CultureInfo.InvariantCulture);
        return (Level("RMS"), Level("Peak"));
    }

    static void AssertLevelsAreAstats(string file, Measurements measured)
    {
        var (rms, peak) = Astats(file);
        Assert.Equal(rms, Some(measured.Channels).OverallRms, 0.05);
        Assert.Equal(peak, Assert.NotNull(Assert.NotNull(measured.Loudness).SamplePeak), 0.01);
    }

    static T Some<T>(T? value)
        where T : class
    {
        Assert.NotNull(value);
        return value;
    }

    static Finding Single(QcReport report, string check) => Assert.Single(report.Findings, f => f.Check == check);

    [Fact]
    public void ACbrMp3sFramesAreFfprobesPackets()
    {
        string file = Mp3("cbr.mp3", Speech);

        var frames = Some(Mp3Headers.Read(file));

        AssertFramesAreFfprobes(file, frames);
        Assert.True(frames.Constant);
        Assert.Equal(192_000, frames.Lowest);
        Assert.Equal("Info", frames.Tag);
    }

    [Fact]
    public void AVbrMp3sFramesAreFfprobesPackets()
    {
        string file = Make("vbr.mp3", "anoisesrc=d=4:r=44100:a=0.3:seed=3,volume='if(lt(t,2),1,0.01)':eval=frame", "-c:a", "libmp3lame", "-q:a", "2");

        var frames = Some(Mp3Headers.Read(file));

        AssertFramesAreFfprobes(file, frames);
        Assert.False(frames.Constant);
        Assert.Equal("Xing", frames.Tag);
    }

    // Cover art in the ID3v2 tag (larger than the reader's buffer) is skipped; the file is sound only.
    [Fact]
    public void AnMp3WithCoverArtIsSoundOnly()
    {
        string sound = Mp3("plain.mp3", Speech);
        string art = Make("cover.png", "testsrc2=s=640x640:d=1,noise=alls=100:allf=t", "-frames:v", "1");
        string file = Path.Combine(_folder, "cover.mp3");
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", sound, "-i", art, "-map", "0", "-map", "1", "-c", "copy", "-disposition:v", "attached_pic", "-id3v2_version", "3", file])
            start.ArgumentList.Add(arg);
        using (var process = Process.Start(start)!)
        {
            string log = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"ffmpeg failed:\n{log}");
        }

        Assert.True(new FileInfo(file).Length - new FileInfo(sound).Length > 1 << 16);
        var measured = FfmpegMeasurer.Measure(file, Profiles.Acx);

        Assert.False(measured.HasPicture);
        AssertFramesAreFfprobes(file, Some(measured.Mp3));
        Assert.Equal(Severity.Pass, Single(Profiles.Acx.Evaluate(measured), "format.bit-rate-mode").Severity);
    }

    // A file that isn't an MP3 has no Layer III frames.
    [Fact]
    public void AWavHasNoMp3Frames() =>
        Assert.Null(Mp3Headers.Read(Make("tone.wav", "sine=f=440:d=1:r=44100", "-c:a", "pcm_s16le")));

    // A file within every one of ACX's figures passes, its loudness and true peak reported, not judged.
    [Fact]
    public void AnMp3WithinAcxsFiguresPasses()
    {
        string file = Mp3("chapter.mp3", Speech);

        var measured = FfmpegMeasurer.Measure(file, Profiles.Acx);
        var report = Profiles.Acx.Evaluate(measured);

        AssertLevelsAreAstats(file, measured);
        AssertFramesAreFfprobes(file, Some(measured.Mp3));
        Assert.True(report.Findings.All(f => f.Severity is not (Severity.Failure or Severity.Warning)), string.Join("\n", report.Findings.Where(f => f.Severity is Severity.Failure or Severity.Warning).Select(f => f.Message)));
        Assert.Equal(Severity.Pass, Single(report, "level.rms").Severity);
        Assert.Equal(Severity.Pass, Single(report, "level.peak").Severity);
        Assert.Equal(Severity.Pass, Single(report, "format.bit-rate-mode").Severity);
        Assert.Equal(Severity.Pass, Single(report, "format.audio-bit-rate").Severity);
        Assert.Equal(Severity.Info, Single(report, "loudness.integrated").Severity);
        Assert.Equal(Severity.Info, Single(report, "loudness.true-peak").Severity);
        Assert.Equal(Profile.RmsFilter, Single(report, "level.rms").CheckedAgainst);
        Assert.Matches(@"^RMS level -20\.\d dBFS \(-23 to -18 dBFS\)$", Single(report, "level.rms").Message);
    }

    // Too quiet, and a click to -0.9 dBFS: each its own failure.
    [Fact]
    public void QuietOrPeakingFails()
    {
        string quiet = Mp3("quiet.mp3", "anoisesrc=d=5:r=44100:a=0.05:seed=1");
        string click = Mp3("click.mp3", "aevalsrc=exprs=0.15*sin(2*PI*440*t)+if(between(t\\,2\\,2.0005)\\,0.75\\,0):s=44100:d=5");

        var (q, c) = (FfmpegMeasurer.Measure(quiet, Profiles.Acx), FfmpegMeasurer.Measure(click, Profiles.Acx));

        AssertLevelsAreAstats(quiet, q);
        AssertLevelsAreAstats(click, c);
        Assert.Equal(Severity.Failure, Single(Profiles.Acx.Evaluate(q), "level.rms").Severity);
        Assert.Equal(Severity.Pass, Single(Profiles.Acx.Evaluate(q), "level.peak").Severity);
        Assert.Equal(Severity.Pass, Single(Profiles.Acx.Evaluate(c), "level.rms").Severity);
        var peak = Single(Profiles.Acx.Evaluate(c), "level.peak");
        Assert.Equal(Severity.Failure, peak.Severity);
        Assert.Contains("(at most -3.0 dBFS)", peak.Message, StringComparison.Ordinal);
        Assert.Equal(Profile.SamplePeakFilter, peak.CheckedAgainst);
    }

    // 128 kb/s, a variable bit rate, 48 kHz: each a format failure; a WAV under acx fails the container and codec.
    [Fact]
    public void TheFormatIsAcxs()
    {
        var low = Profiles.Acx.Evaluate(FfmpegMeasurer.Measure(Mp3("low.mp3", Speech, "128k"), Profiles.Acx));
        var high = Profiles.Acx.Evaluate(FfmpegMeasurer.Measure(Mp3("48k.mp3", Speech, sampleRate: 48000), Profiles.Acx));
        var vbr = Profiles.Acx.Evaluate(FfmpegMeasurer.Measure(Make("vbr.mp3", Speech, "-c:a", "libmp3lame", "-q:a", "0"), Profiles.Acx));
        string wav = Make("master.wav", Speech, "-c:a", "pcm_s16le");
        var master = FfmpegMeasurer.Measure(wav, Profiles.Acx);

        Assert.Equal(Severity.Failure, Single(low, "format.audio-bit-rate").Severity);
        Assert.Equal("Track 1: Audio bit rate 128 kb/s (expected at least 192 kb/s)", Single(low, "format.audio-bit-rate").Message);
        Assert.Equal(Severity.Pass, Single(low, "format.bit-rate-mode").Severity);
        Assert.Equal(Severity.Failure, Single(high, "format.sample-rate").Severity);
        Assert.Equal(Severity.Failure, Single(vbr, "format.bit-rate-mode").Severity);
        Assert.StartsWith("Track 1: Variable bit rate: ", Single(vbr, "format.bit-rate-mode").Message, StringComparison.Ordinal);
        Assert.Null(master.Mp3);
        Assert.Equal(Severity.Failure, Single(Profiles.Acx.Evaluate(master), "format.container").Severity);
        Assert.Equal(Severity.Failure, Single(Profiles.Acx.Evaluate(master), "format.audio-codec").Severity);
    }

    // acx-master: a WAV master's levels and sample rate, without the MP3's rules; a stereo pair's RMS is both channels'.
    [Fact]
    public void AcxMasterChecksAWavsLevels()
    {
        string wav = Make("stereo.wav", "aevalsrc=exprs=0.15*sin(2*PI*300*t)|0.1*sin(2*PI*500*t):s=44100:d=5", "-c:a", "pcm_s24le");

        var measured = FfmpegMeasurer.Measure(wav, Profiles.AcxMaster);
        var report = Profiles.AcxMaster.Evaluate(measured);

        AssertLevelsAreAstats(wav, measured);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Failure);
        Assert.Equal(Severity.Pass, Single(report, "level.rms").Severity);
        Assert.Equal(Severity.Pass, Single(report, "format.sample-rate").Severity);
        Assert.DoesNotContain(report.Findings, f => f.Check is "format.container" or "format.bit-rate-mode" or "format.audio-bit-rate");
    }

    // Other profiles neither judge nor report the RMS level and sample peak.
    [Fact]
    public void OtherProfilesLeaveTheLevelsOut()
    {
        var measured = FfmpegMeasurer.Measure(Mp3("chapter.mp3", Speech), Profile.EbuR128);
        Assert.DoesNotContain(Profile.EbuR128.Evaluate(measured).Findings, f => f.Check.StartsWith("level.", StringComparison.Ordinal));
    }

    [Fact]
    public void TheProfilesAreNamed()
    {
        Assert.Same(Profiles.Acx, Profiles.Get("acx"));
        Assert.Same(Profiles.AcxMaster, Profiles.Get("ACX-master"));
        Assert.Null(Profiles.Acx.TargetLoudness);
        Assert.Null(Profiles.Acx.ToneLevel);
        Assert.Equal(7200, Profiles.AcxMaster.Format?.MaxDuration);
    }

    // A profile file starts from acx and changes its figures; a level above 0 dBFS or a window upside down is refused.
    [Fact]
    public void AProfileFileSetsTheLevels()
    {
        var profile = Profiles.Read("""{ "name": "podcast", "basedOn": "acx", "minRms": -20, "maxSamplePeak": -1, "targetLoudness": -16, "format": { "minAudioBitRate": 128000, "constantBitRate": false } }""");

        Assert.Equal(-20, profile.MinRms);
        Assert.Equal(-18, profile.MaxRms);
        Assert.Equal(-1, profile.MaxSamplePeak);
        Assert.Equal(-16, profile.TargetLoudness);
        Assert.Equal(128_000, profile.Format?.MinAudioBitRate);
        Assert.False(profile.Format?.ConstantBitRate);
    }

    [Theory]
    [InlineData("""{ "name": "x", "maxRms": 1 }""")]
    [InlineData("""{ "name": "x", "maxSamplePeak": 0.5 }""")]
    [InlineData("""{ "name": "x", "minRms": -10, "maxRms": -20 }""")]
    [InlineData("""{ "name": "x", "format": { "minAudioBitRate": 0 } }""")]
    public void ALevelOutOfItsRangeIsRefused(string json) =>
        Assert.Throws<ArgumentException>(() => Profiles.Read(json));

    // The bit rate mode without an MP3's frames: PCM is constant, any other codec isn't known and fails.
    [Fact]
    public void TheBitRateModeOfOtherCodecs()
    {
        var spec = new FormatSpec { ConstantBitRate = true };
        MediaFormat Of(string codec) => new("mov,mp4,m4a,3gp,3g2,mj2", 60, null, [new AudioFormat(1, codec, 44100, 1, "mono", null)]);

        Assert.Equal(Severity.Pass, Assert.Single(spec.Judge(Of("pcm_s16le"), null)).Severity);
        var aac = Assert.Single(spec.Judge(Of("aac"), null));
        Assert.Equal(Severity.Failure, aac.Severity);
        Assert.Equal("Track 1: Bit rate mode not known: aac (only an MP3's frames are read) (expected constant)", aac.Message);
    }
}
