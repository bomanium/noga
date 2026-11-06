using System.Diagnostics;
using System.Globalization;
using Noga.Ffmpeg;

namespace Noga.Tests;

/// <summary>
/// WAV, RF64, BW64 and AIFF headers against the sound that decodes (docs/decisions/2026-11-06-sound-headers.md): on
/// files ffmpeg wrote whole the header lists exactly astats' samples and ffprobe's duration; a WAV cut short, which
/// FFmpeg reads as a shorter one, fails decode.sound; a header never finished warns.
/// </summary>
public sealed class SoundHeaderTests : IDisposable
{
    const string Tone = "sine=f=1000:d=4:sample_rate=48000";

    readonly string _folder = Directory.CreateTempSubdirectory("noga-sound-header-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [InlineData("s16.wav", "-c:a pcm_s16le", "WAV")]
    [InlineData("s24.wav", "-c:a pcm_s24le", "WAV")]
    [InlineData("rf64.wav", "-c:a pcm_s24le -rf64 always", "RF64")]
    [InlineData("s24.aiff", "-c:a pcm_s24be", "AIFF")]
    [InlineData("f32.aiff", "-c:a pcm_f32be", "AIFF-C")]
    public void AWholeFilesHeaderListsWhatDecodes(string name, string codec, string kind)
    {
        string file = Make(name, codec);

        var header = SoundHeaders.Read(file);

        Assert.NotNull(header);
        Assert.Equal(kind, header.Kind);
        Assert.Equal(48000, header.SampleRate);
        Assert.Equal(FfmpegCli.Samples(file), header.Frames);
        Assert.Equal(Duration(file), header.Seconds!.Value, 6);
    }

    [Fact]
    public void Bw64ReadsAsRf64Does()
    {
        string file = Make("bw64.wav", "-c:a pcm_s24le -rf64 always");
        byte[] data = File.ReadAllBytes(file);
        "BW64"u8.CopyTo(data);
        File.WriteAllBytes(file, data);

        var header = SoundHeaders.Read(file);

        Assert.Equal("BW64", header?.Kind);
        Assert.Equal(FfmpegCli.Samples(file), header?.Frames);
    }

    // FFmpeg reads a WAV cut short as a shorter one (its duration from the bytes there are); the header still lists 4 s.
    [Fact]
    public void AWavCutShortFails()
    {
        string file = Cut(Make("cut.wav", "-c:a pcm_s24le"), 600_000);
        Assert.True(Duration(file) < 3, "ffprobe's duration is the bytes there are");

        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);
        var finding = Assert.Single(Profile.EbuR128.Evaluate(measured).Findings, f => f.Check == "decode.sound");

        Assert.Equal(Severity.Failure, finding.Severity);
        Assert.Contains("of the 4.00 s the WAV header lists decoded", finding.Message, StringComparison.Ordinal);
        Assert.Equal(Profile.SoundHeaderFilter, finding.CheckedAgainst);
        Assert.Equal(FfmpegCli.Samples(file) / 48000.0, measured.Channels!.Duration, 3);
    }

    // RF64 and AIFF cut short: ffprobe's duration is the header's already, and the header says the same.
    [Theory]
    [InlineData("cut_rf64.wav", "-c:a pcm_s24le -rf64 always")]
    [InlineData("cut.aiff", "-c:a pcm_s24be")]
    public void Rf64AndAiffCutShortFail(string name, string codec)
    {
        string file = Cut(Make(name, codec), 600_000);
        Assert.Equal(4, Duration(file), 6);

        var finding = Assert.Single(Profile.EbuR128.Evaluate(FfmpegMeasurer.Measure(file, Profile.EbuR128)).Findings, f => f.Check == "decode.sound");

        Assert.Equal(Severity.Failure, finding.Severity);
        Assert.Contains("of the 4.00 s", finding.Message, StringComparison.Ordinal);
    }

    // A WAV written to a pipe: ffmpeg can't go back to fill in its sizes, and FFmpeg reads it ignoring them.
    [Fact]
    public void AWavHeaderNeverFinishedWarns()
    {
        string file = Path.Combine(_folder, "pipe.wav");
        using (var output = File.Create(file))
            Ffmpeg(output, "-f", "lavfi", "-i", Tone, "-c:a", "pcm_s24le", "-f", "wav", "-");

        var header = SoundHeaders.Read(file);
        Assert.Equal("WAV", header?.Kind);
        Assert.Null(header?.Frames);

        var finding = Assert.Single(Profile.EbuR128.Evaluate(FfmpegMeasurer.Measure(file, Profile.EbuR128)).Findings, f => f.Check == "decode.sound");
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.StartsWith("The WAV header was never finished", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAiffHeaderNeverFinishedListsNoLength()
    {
        string file = Path.Combine(_folder, "pipe.aiff");
        using (var output = File.Create(file))
            Ffmpeg(output, "-f", "lavfi", "-i", Tone, "-c:a", "pcm_s24be", "-f", "aiff", "-");

        Assert.Null(SoundHeaders.Read(file)?.Frames);
        Assert.Equal("AIFF", SoundHeaders.Read(file)?.Kind);
    }

    [Fact]
    public void AWholeWavPasses()
    {
        string file = Make("whole.wav", "-c:a pcm_s24le");

        var finding = Assert.Single(Profile.EbuR128.Evaluate(FfmpegMeasurer.Measure(file, Profile.EbuR128)).Findings, f => f.Check == "decode.sound");

        Assert.Equal(Severity.Pass, finding.Severity);
        Assert.Contains("of the 4.00 s the track lists", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherFilesHaveNoSoundHeader()
    {
        string file = Path.Combine(_folder, "text.wav");
        File.WriteAllText(file, "not a wav at all");

        Assert.Null(SoundHeaders.Read(file));
    }

    string Make(string name, string codec)
    {
        string file = Path.Combine(_folder, name);
        Ffmpeg(null, ["-f", "lavfi", "-i", Tone, "-ac", "2", .. codec.Split(' '), file]);
        return file;
    }

    static string Cut(string file, int bytes)
    {
        byte[] data = File.ReadAllBytes(file);
        File.WriteAllBytes(file, data[..bytes]);
        return file;
    }

    static double Duration(string file) =>
        double.Parse(FfmpegCli.Ffprobe(file, "-show_entries", "stream=duration", "-of", "csv=p=0").Trim(), CultureInfo.InvariantCulture);

    static void Ffmpeg(Stream? output, params string[] args)
    {
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string arg in (string[])["-v", "error", "-y", .. args])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var log = process.StandardError.ReadToEndAsync();
        if (output is null)
            _ = process.StandardOutput.ReadToEnd();
        else
            process.StandardOutput.BaseStream.CopyTo(output);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"ffmpeg {string.Join(' ', args)} failed:\n{log.Result}");
    }
}
