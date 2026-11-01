using MediaQc.Ffmpeg;
using MediaQc.Managed;

namespace MediaQc.Tests;

/// <summary>
/// make-samples.sh's 20 s programme (bars and tone 0-5 s, black and silence 5-8 s, picture and pink noise 8-17 s,
/// black and silence 17-20 s; timecode from 10:00:00:00) in each delivery format, measured on the FFmpeg path: what was put in, and each figure
/// against the ffmpeg command line.
/// </summary>
public sealed class SampleTests : IClassFixture<Samples>
{
    readonly Samples _samples;

    public SampleTests(Samples samples) => _samples = samples;

    public static TheoryData<string> Programmes => [.. Samples.Programmes];

    [Theory]
    [MemberData(nameof(Programmes))]
    public void TheProgrammeIsWhatWasPutIn(string programme)
    {
        var measured = FfmpegMeasurer.Measure(Path.Combine(_samples.Folder, programme), Profile.EbuR128);

        Assert.Equal(20, measured.Duration, 0.05);
        Assert.Equal((25, 1), (measured.RateNumerator, measured.RateDenominator));
        Assert.Equal("10:00:00:00", measured.StartTimecode);
        Assert.DoesNotContain("timecode", measured.NotMeasured);
        var tone = Assert.Single(measured.Tone);
        Assert.Equal((0, 5), (Math.Round(tone.Start, 1), Math.Round(tone.End, 1)));
        Assert.Equal(-18, tone.Level, 0.1);
        Assert.Equal(tone.End, measured.LoudnessFrom);
        Assert.Equal(2, measured.Silences.Count);
        Assert.Equal((5, 8), (Math.Round(measured.Silences[0].Start, 1), Math.Round(measured.Silences[0].End, 1)));
        Assert.Equal((17, 20), (Math.Round(measured.Silences[1].Start, 1), Math.Round(measured.Silences[1].End, 1)));
    }

    [Theory]
    [MemberData(nameof(Programmes))]
    public void EachFigureIsFfmpegs(string programme)
    {
        string file = Path.Combine(_samples.Folder, programme);
        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);

        FfmpegCli.AssertLoudness(file, measured);
        FfmpegCli.AssertSilences(file, measured);
        FfmpegCli.AssertTone(file, measured.Tone[0]);
    }

    [Fact]
    public void TheManagedPathReadsTheH264FileOnly()
    {
        Assert.True(ManagedMeasurer.CanMeasure(Path.Combine(_samples.Folder, "programme_h264.mp4")));
        Assert.False(ManagedMeasurer.CanMeasure(Path.Combine(_samples.Folder, "programme_prores.mov")));
        Assert.False(ManagedMeasurer.CanMeasure(Path.Combine(_samples.Folder, "programme_dnxhr.mxf")));
    }

    // docs/decisions/2026-10-31-decoding.md: the same MP4 through both paths gives the same measures, within the
    // tolerances each figure states, and the managed path's figures are ffmpeg's too.
    [Fact]
    public void BothDecodingPathsAgreeOnTheH264File()
    {
        string file = Path.Combine(_samples.Folder, "programme_h264.mp4");
        var ffmpeg = FfmpegMeasurer.Measure(file, Profile.EbuR128);
        var managed = ManagedMeasurer.Measure(file, Profile.EbuR128);

        Assert.Equal(ffmpeg.Duration, managed.Duration, 0.05);
        Assert.Equal((ffmpeg.RateNumerator, ffmpeg.RateDenominator), (managed.RateNumerator, managed.RateDenominator));
        Assert.Equal(ffmpeg.NotMeasured, managed.NotMeasured);
        Assert.Equal("10:00:00:00", managed.StartTimecode);
        Assert.Equal(ffmpeg.LoudnessFrom, managed.LoudnessFrom, 0.02);
        var (a, b) = (Assert.NotNull(ffmpeg.Loudness), Assert.NotNull(managed.Loudness));
        Assert.Equal(Assert.NotNull(a.Integrated), Assert.NotNull(b.Integrated), 0.1);
        Assert.Equal(a.Range, b.Range, 0.1);
        Assert.Equal(a.TruePeak, b.TruePeak, 0.1);
        Assert.Equal(ffmpeg.Silences.Count, managed.Silences.Count);
        foreach (var (x, y) in ffmpeg.Silences.Zip(managed.Silences))
        {
            Assert.Equal(x.Start, y.Start, 0.01);
            Assert.Equal(x.End, y.End, 0.01);
        }

        Assert.Equal(ffmpeg.Tone.Count, managed.Tone.Count);
        foreach (var (x, y) in ffmpeg.Tone.Zip(managed.Tone))
        {
            Assert.Equal(x.Start, y.Start, 0.02);
            Assert.Equal(x.End, y.End, 0.02);
            Assert.Equal(x.Level, y.Level, 0.1);
        }

        FfmpegCli.AssertLoudness(file, managed);
        FfmpegCli.AssertSilences(file, managed);
        FfmpegCli.AssertTone(file, managed.Tone[0]);
    }
}
