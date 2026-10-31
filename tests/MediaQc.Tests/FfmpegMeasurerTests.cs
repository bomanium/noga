using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using FrameReader;
using MediaQc.Ffmpeg;

namespace MediaQc.Tests;

/// <summary>
/// The FFmpeg path's measures on a generated file (FrameReader's TestMedia: FFmpeg's own sources, in this process, so
/// no media is committed), each checked against the ffmpeg command line on PATH as Profile's checked-against texts say.
/// </summary>
public sealed partial class FfmpegMeasurerTests : IClassFixture<FfmpegMeasurerTests.Programme>
{
    readonly Programme _programme;

    public FfmpegMeasurerTests(Programme programme) => _programme = programme;

    Measurements Measured => _programme.Measured;

    [Fact]
    public void DurationAndFrameRateAreTheFilesHeaders()
    {
        Assert.Equal(14, Measured.Duration, 0.05);
        Assert.Equal((25, 1), (Measured.RateNumerator, Measured.RateDenominator));
    }

    [Fact]
    public void LoudnessIsEbur128sWithinATenthOfALu()
    {
        var ffmpeg = _programme.Ffmpeg("-af ebur128=peak=true:framelog=quiet");
        var loudness = Assert.NotNull(Measured.Loudness);

        Assert.Equal(Number(ffmpeg, @"I:\s+(-?[\d.]+) LUFS"), Assert.NotNull(loudness.Integrated), 0.1);
        Assert.Equal(Number(ffmpeg, @"LRA:\s+([\d.]+) LU"), loudness.Range, 0.1);
        Assert.Equal(Number(ffmpeg, @"True peak:\s+Peak:\s+(-?[\d.]+) dBFS"), loudness.TruePeak, 0.1);
    }

    [Fact]
    public void SilencesAreSilencedetectsWithinTenMilliseconds()
    {
        var expected = Spans(_programme.Ffmpeg($"-af {FfmpegMeasurer.SilenceGraph(Profile.EbuR128)}"));

        Assert.Equal(2, expected.Count);
        Assert.Equal(expected.Count, Measured.Silences.Count);
        foreach (var (want, got) in expected.Zip(Measured.Silences))
        {
            Assert.Equal(want.Start, got.Start, 0.01);
            Assert.Equal(want.End, got.End, 0.01);
        }

        // The second runs to the end of the file.
        Assert.Equal(Measured.Duration, Measured.Silences[1].End, 0.01);
    }

    [Fact]
    public void ToneIsAtItsAstatsLevelAndEndsWhereABandpassSays()
    {
        var tone = Assert.Single(Measured.Tone);
        Assert.Equal(1000, tone.Frequency);

        // The tone's ends: where 1 kHz falls below -40 dBFS (the finder's own threshold) and comes back.
        var quiet = Spans(_programme.Ffmpeg("-af bandpass=f=1000,silencedetect=n=-40dB:d=1"));
        Assert.Equal(0, tone.Start, 0.02);
        Assert.Equal(quiet[0].Start, tone.End, 0.02);

        // The level: the loudest channel's RMS over the tone, as a sine's peak (+3.01 dB).
        string stats = _programme.Ffmpeg(Invariant($"-af atrim=start={tone.Start}:end={tone.End},astats=measure_perchannel=RMS_level:measure_overall=none"));
        double rms = RmsLevel().Matches(stats).Max(m => Parse(m.Groups[1].Value));
        Assert.Equal(rms + (20 * Math.Log10(Math.Sqrt(2))), tone.Level, 0.1);
    }

    [Fact]
    public void AFileWithoutAudioHasNoAudioMeasures()
    {
        string path = Path.Combine(_programme.Folder, "silent.mov");
        TestMedia.Write(path, new TestMediaOptions { Video = ["testsrc2=d=1"] });

        var measured = FfmpegMeasurer.Measure(path, Profile.EbuR128);

        Assert.Null(measured.Loudness);
        Assert.Empty(measured.Silences);
        Assert.Empty(measured.Tone);
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

    [GeneratedRegex(@"RMS level dB: (-?[\d.]+)")]
    private static partial Regex RmsLevel();

    /// <summary>
    /// A 14 s programme, generated once for the class: black under 1 kHz line-up tone at -18 dBFS (0-6 s), black and
    /// silence (6-9 s), picture under 1 kHz tone too short to be line-up (9-11 s), black and silence to the end
    /// (11-14 s); PCM audio in MOV, as a ProRes delivery has, and measured once.
    /// </summary>
    public sealed class Programme : IDisposable
    {
        public Programme()
        {
            Folder = Directory.CreateTempSubdirectory("mediaqc-").FullName;
            Path = System.IO.Path.Combine(Folder, "programme.mov");
            Measured = FfmpegMeasurer.Measure(Write(Path), Profile.EbuR128);
        }

        public string Folder { get; }

        public string Path { get; }

        public Measurements Measured { get; }

        /// <summary>What the ffmpeg command line on PATH prints (its log) for <c>ffmpeg -i programme.mov ARGS -f null -</c>.</summary>
        public string Ffmpeg(string args)
        {
            var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
            foreach (string arg in (string[])["-hide_banner", "-nostats", "-i", Path, .. args.Split(' '), "-f", "null", "-"])
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

        public void Dispose() => Directory.Delete(Folder, recursive: true);

        static string Write(string path)
        {
            Libraries.Load();
            TestMedia.Write(path, new TestMediaOptions
            {
                Video = ["color=c=black:d=9", "testsrc2=d=2", "color=c=black:d=3"],
                Audio = "sine=frequency=1000:duration=14,volume=volume=0:enable='between(t,6,9)+gte(t,11)'",
                AudioEncoder = "pcm_s16le",
            });
            return path;
        }
    }
}
