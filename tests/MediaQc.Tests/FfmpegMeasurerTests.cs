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
public sealed partial class FfmpegMeasurerTests : IClassFixture<Programme>
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
    public void LoudnessIsEbur128sAfterTheLineUpWithinATenthOfALu()
    {
        Assert.Equal(Measured.Tone[0].End, Measured.LoudnessFrom);
        var ffmpeg = _programme.Ffmpeg("-af ebur128=peak=true:framelog=quiet", Invariant($"-ss {Measured.LoudnessFrom}"));
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
}
