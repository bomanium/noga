using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using MediaQc.Ffmpeg;

namespace MediaQc.Tests;

/// <summary>
/// The ffmpeg command line on PATH, and the comparisons of a file's measures with it that Profile's checked-against
/// texts state: each figure, the filter and options it is checked against, and the tolerance.
/// </summary>
internal static partial class FfmpegCli
{
    /// <summary>What ffmpeg prints (its log) for <c>ffmpeg [INPUT] -i FILE ARGS -f null -</c>.</summary>
    public static string Run(string file, string args, string input = "")
    {
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-nostats", .. input.Split(' ', StringSplitOptions.RemoveEmptyEntries), "-i", file, .. args.Split(' '), "-f", "null", "-"])
            start.ArgumentList.Add(arg);
        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new InvalidOperationException("These tests compare with the ffmpeg command line: put ffmpeg on PATH (CI installs it).", e);
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            string log = process.StandardError.ReadToEnd();
            process.WaitForExit();
            _ = output.Result;
            Assert.True(process.ExitCode == 0, $"ffmpeg {args} failed:\n{log}");
            return log;
        }
    }

    /// <summary>What ffprobe -show_format -show_streams -of json prints for <paramref name="file"/>.</summary>
    public static string Probe(string file)
    {
        var start = new ProcessStartInfo("ffprobe") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string arg in (string[])["-v", "error", "-show_format", "-show_streams", "-of", "json", file])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        string json = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"ffprobe failed:\n{error.Result}");
        return json;
    }

    /// <summary>The start timecode is the tag the ffprobe command line gives.</summary>
    public static void AssertStartTimecode(string file, Measurements measured) =>
        Assert.Equal(TimecodeTag.Pick(Probe(file), measured.RateNumerator, measured.RateDenominator), measured.StartTimecode);

    /// <summary>The loudness is ebur128's from the same point (ffmpeg -ss), within 0.1 LU and 0.1 dB.</summary>
    public static void AssertLoudness(string file, Measurements measured)
    {
        var ffmpeg = Run(file, "-af ebur128=peak=true:framelog=quiet", measured.LoudnessFrom > 0 ? Invariant($"-ss {measured.LoudnessFrom}") : "");
        var loudness = Assert.NotNull(measured.Loudness);

        Assert.Equal(Number(ffmpeg, @"I:\s+(-?[\d.]+) LUFS"), Assert.NotNull(loudness.Integrated), 0.1);
        Assert.Equal(Number(ffmpeg, @"LRA:\s+([\d.]+) LU"), loudness.Range, 0.1);
        Assert.Equal(Number(ffmpeg, @"True peak:\s+Peak:\s+(-?[\d.]+) dBFS"), loudness.TruePeak, 0.1);
    }

    /// <summary>The silences are silencedetect's with the profile's options, each end within 10 ms.</summary>
    public static void AssertSilences(string file, Measurements measured)
    {
        var expected = Spans(Run(file, $"-af {FfmpegMeasurer.SilenceGraph(Profile.EbuR128)}"));

        Assert.Equal(expected.Count, measured.Silences.Count);
        foreach (var (want, got) in expected.Zip(measured.Silences))
        {
            Assert.Equal(want.Start, got.Start, 0.01);
            Assert.Equal(want.End, got.End, 0.01);
        }
    }

    /// <summary>
    /// The first tone ends where a 1 kHz bandpass falls below -40 dBFS (the finder's own threshold), within 20 ms, and
    /// its level is the loudest channel's astats RMS over it as a sine's peak (+3.01 dB), within 0.1 dB.
    /// </summary>
    public static void AssertTone(string file, ToneSpan tone)
    {
        Assert.Equal(1000, tone.Frequency);
        var quiet = Spans(Run(file, "-af bandpass=f=1000,silencedetect=n=-40dB:d=1"));
        Assert.Equal(quiet[0].Start, tone.End, 0.02);

        string stats = Run(file, Invariant($"-af atrim=start={tone.Start}:end={tone.End},astats=measure_perchannel=RMS_level:measure_overall=none"));
        double rms = RmsLevel().Matches(stats).Max(m => Parse(m.Groups[1].Value));
        Assert.Equal(rms + (20 * Math.Log10(Math.Sqrt(2))), tone.Level, 0.1);
    }

    /// <summary>
    /// Black spans are blackdetect's with the profile's options, each end within one frame (blackdetect ends a span at
    /// the file's end at the last frame's pts, a frame before the end the report gives).
    /// </summary>
    public static void AssertBlack(string file, Measurements measured)
    {
        var profile = Profile.EbuR128;
        string log = Run(file, Invariant($"-an -vf blackdetect=d={profile.MinBlack}:pix_th={profile.BlackPixelThreshold}:pic_th={profile.BlackPictureThreshold}"));
        var starts = BlackStart().Matches(log).Select(m => Parse(m.Groups[1].Value)).ToList();
        var ends = BlackEnd().Matches(log).Select(m => Parse(m.Groups[1].Value)).ToList();
        double frame = (double)measured.RateDenominator / measured.RateNumerator;

        Assert.Equal(starts.Count, measured.Black.Count);
        foreach (var ((start, end), got) in starts.Zip(ends).Zip(measured.Black))
        {
            Assert.Equal(start, got.Start, frame + 1e-6);
            Assert.Equal(end, got.End, frame + 1e-6);
        }
    }

    /// <summary>The worst frame's share of pixels outside broadcast range is signalstats' BRNG for that frame, exactly (to 1e-6).</summary>
    public static void AssertWorstOutOfRange(string file, Measurements measured)
    {
        var worst = Assert.NotNull(measured.WorstOutOfRange);
        string log = Run(file, "-an -vf signalstats=stat=brng,metadata=print:key=lavfi.signalstats.BRNG");
        var frames = SignalstatsFrame().Matches(log).Select(m => (Time: Parse(m.Groups[1].Value), Brng: Parse(m.Groups[2].Value))).ToList();
        var at = frames.MinBy(f => Math.Abs(f.Time - worst.Seconds));

        Assert.Equal(worst.Seconds, at.Time, 1e-3);
        Assert.Equal(at.Brng, worst.Share, 1e-6);
    }

    /// <summary>The active picture is cropdetect's last suggestion with its defaults, exactly.</summary>
    public static void AssertCrop(string file, Measurements measured)
    {
        var crop = Assert.NotNull(measured.Crop);
        var last = CropDetect().Matches(Run(file, "-an -vf cropdetect")).Last();

        Assert.Equal(
            (int.Parse(last.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(last.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(last.Groups[3].Value, CultureInfo.InvariantCulture), int.Parse(last.Groups[4].Value, CultureInfo.InvariantCulture)),
            (crop.Width, crop.Height, crop.X, crop.Y));
    }

    static double Number(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        Assert.True(match.Success, $"ffmpeg printed no {pattern}:\n{text}");
        return Parse(match.Groups[1].Value);
    }

    static List<TimeRange> Spans(string silencedetect)
    {
        var starts = SilenceStart().Matches(silencedetect).Select(m => Parse(m.Groups[1].Value)).ToList();
        var ends = SilenceEnd().Matches(silencedetect).Select(m => Parse(m.Groups[1].Value)).ToList();
        Assert.Equal(starts.Count, ends.Count);
        return [.. starts.Zip(ends, (s, e) => new TimeRange(s, e))];
    }

    static double Parse(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"silence_start: (-?[\d.]+)")]
    private static partial Regex SilenceStart();

    [GeneratedRegex(@"silence_end: (-?[\d.]+)")]
    private static partial Regex SilenceEnd();

    [GeneratedRegex(@"black_start:(-?[\d.]+)")]
    private static partial Regex BlackStart();

    [GeneratedRegex(@"black_end:(-?[\d.]+)")]
    private static partial Regex BlackEnd();

    [GeneratedRegex(@"pts_time:(-?[\d.]+)\s*\n.*lavfi\.signalstats\.BRNG=(-?[\d.e+-]+)")]
    private static partial Regex SignalstatsFrame();

    [GeneratedRegex(@"crop=(\d+):(\d+):(\d+):(\d+)")]
    private static partial Regex CropDetect();

    [GeneratedRegex(@"RMS level dB: (-?[\d.]+)")]
    private static partial Regex RmsLevel();
}
