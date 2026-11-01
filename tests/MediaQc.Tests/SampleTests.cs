using MediaQc.Ffmpeg;

namespace MediaQc.Tests;

/// <summary>
/// make-samples.sh's 20 s programme (bars and tone 0-5 s, black and silence 5-7 s, picture and pink noise 7-17 s,
/// black and silence 17-20 s) in each delivery format, measured on the FFmpeg path: what was put in, and each figure
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
        var tone = Assert.Single(measured.Tone);
        Assert.Equal((0, 5), (Math.Round(tone.Start, 1), Math.Round(tone.End, 1)));
        Assert.Equal(-18, tone.Level, 0.1);
        Assert.Equal(tone.End, measured.LoudnessFrom);
        Assert.Equal(2, measured.Silences.Count);
        Assert.Equal((5, 7), (Math.Round(measured.Silences[0].Start, 1), Math.Round(measured.Silences[0].End, 1)));
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
}
