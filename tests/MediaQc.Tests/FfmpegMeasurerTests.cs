using FrameReader;
using MediaQc.Ffmpeg;

namespace MediaQc.Tests;

/// <summary>
/// The FFmpeg path's measures on a generated file (FrameReader's TestMedia: FFmpeg's own sources, in this process, so
/// no media is committed), each checked against the ffmpeg command line on PATH as Profile's checked-against texts say.
/// make-samples.sh's files are checked the same way in <see cref="SampleTests"/>.
/// </summary>
public sealed class FfmpegMeasurerTests : IClassFixture<Programme>
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
    public void AFileWithoutATimecodeStartsAtZero()
    {
        Assert.Null(Measured.StartTimecode);
        Assert.DoesNotContain("timecode", Measured.NotMeasured);
    }

    [Fact]
    public void LoudnessIsEbur128sAfterTheLineUpWithinATenthOfALu()
    {
        Assert.Equal(Measured.Tone[0].End, Measured.LoudnessFrom);
        FfmpegCli.AssertLoudness(_programme.Path, Measured);
    }

    [Fact]
    public void SilencesAreSilencedetectsWithinTenMilliseconds()
    {
        FfmpegCli.AssertSilences(_programme.Path, Measured);

        // Two, the second running to the end of the file.
        Assert.Equal(2, Measured.Silences.Count);
        Assert.Equal(Measured.Duration, Measured.Silences[1].End, 0.01);
    }

    [Fact]
    public void ToneIsAtItsAstatsLevelAndEndsWhereABandpassSays()
    {
        var tone = Assert.Single(Measured.Tone);
        Assert.Equal(0, tone.Start, 0.02);
        FfmpegCli.AssertTone(_programme.Path, tone);
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
}
