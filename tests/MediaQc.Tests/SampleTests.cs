using MediaQc.Managed;

namespace MediaQc.Tests;

/// <summary>
/// make-samples.sh's files measured on both decoding paths: what was put in, and each figure against the ffmpeg
/// command line. The programme (20 s; timecode from 10:00:00:00): bars and tone 0-5 s, black and silence 5-8 s,
/// picture and pink noise 8-17 s, black and silence 17-20 s, as ProRes/MOV, DNxHR/MXF and H.264/MP4; then a ramp over
/// every code value (out of range), and a 2.39:1 letterbox.
/// </summary>
public sealed class SampleTests : IClassFixture<Samples>
{
    readonly Samples _samples;

    public SampleTests(Samples samples) => _samples = samples;

    public static TheoryData<string> Programmes => [.. Samples.Programmes];

    static (double, double) Rounded(double start, double end) => (Math.Round(start, 1), Math.Round(end, 1));

    [Theory]
    [MemberData(nameof(Programmes))]
    public void TheProgrammeIsWhatWasPutIn(string programme)
    {
        var measured = _samples.Ffmpeg(programme);

        Assert.Equal(20, measured.Duration, 0.05);
        Assert.Equal((25, 1), (measured.RateNumerator, measured.RateDenominator));
        Assert.Equal("10:00:00:00", measured.StartTimecode);
        Assert.Empty(measured.NotMeasured);
        var tone = Assert.Single(measured.Tone);
        Assert.Equal((0, 5), Rounded(tone.Start, tone.End));
        Assert.Equal(-18, tone.Level, 0.1);
        Assert.Equal(tone.End, measured.LoudnessFrom);
        Assert.Equal([(5, 8), (17, 20)], measured.Silences.Select(s => Rounded(s.Start, s.End)));
        Assert.Equal([(0, 5)], measured.Bars.Select(s => Rounded(s.Start, s.End)));
        Assert.Equal([(5, 8), (17, 20)], measured.Black.Select(s => Rounded(s.Start, s.End)));
        Assert.Empty(measured.OutOfRange);
        Assert.True(Assert.NotNull(measured.WorstOutOfRange).Share < Profile.EbuR128.MaxOutOfRange);
        Assert.True(Assert.NotNull(measured.Crop).FillsFrame);
        Assert.Equal(500, measured.PictureTypes.Length);
        Assert.Equal('I', measured.PictureTypes[0]);
        Assert.NotNull(measured.Packets);
        var packets = measured.Packets;
        Assert.Equal(0, packets.Keyframes[0]);
        Assert.Equal(20, packets.BytesPerSecond.Count);
        if (programme != "programme_h264.mp4")
        {
            // ProRes and DNxHR are intra-only: every frame an I-frame and a keyframe.
            Assert.Equal(new string('I', 500), measured.PictureTypes);
            Assert.Equal(500, packets.Keyframes.Count);
        }
    }

    [Theory]
    [MemberData(nameof(Programmes))]
    public void EachFigureIsFfmpegs(string programme)
    {
        string file = _samples[programme];
        var measured = _samples.Ffmpeg(programme);

        FfmpegCli.AssertStartTimecode(file, measured);
        FfmpegCli.AssertLoudness(file, measured);
        FfmpegCli.AssertSilences(file, measured);
        FfmpegCli.AssertTone(file, measured.Tone[0]);
        FfmpegCli.AssertBlack(file, measured);
        FfmpegCli.AssertWorstOutOfRange(file, measured);
        FfmpegCli.AssertCrop(file, measured);
        FfmpegCli.AssertPictureTypes(file, measured);
        FfmpegCli.AssertPackets(file, measured);
    }

    [Fact]
    public void TheRampIsOutOfRangeThroughout()
    {
        var measured = _samples.Ffmpeg("out_of_range.mov");

        var span = Assert.Single(measured.OutOfRange);
        Assert.Equal((0, 5), Rounded(span.Start, span.End));
        Assert.Equal(Assert.NotNull(measured.WorstOutOfRange).Share, span.WorstShare);
        FfmpegCli.AssertWorstOutOfRange(_samples["out_of_range.mov"], measured);
        Assert.Equal(Severity.Warning, Assert.Single(Profile.EbuR128.Evaluate(measured).Findings, f => f.Check == "range").Severity);
    }

    [Fact]
    public void TheLetterboxIsCropdetects()
    {
        var measured = _samples.Ffmpeg("letterbox.mov");

        Assert.Equal(new ActivePicture(1920, 1080, 1920, 800, 0, 140), measured.Crop);
        FfmpegCli.AssertCrop(_samples["letterbox.mov"], measured);
    }

    [Fact]
    public void TheManagedPathReadsTheH264FileOnly()
    {
        Assert.True(ManagedMeasurer.CanMeasure(_samples["programme_h264.mp4"]));
        Assert.False(ManagedMeasurer.CanMeasure(_samples["programme_prores.mov"]));
        Assert.False(ManagedMeasurer.CanMeasure(_samples["programme_dnxhr.mxf"]));
    }

    // docs/decisions/2026-10-31-decoding.md: the same MP4 through both paths gives the same measures, within the
    // tolerances each figure states (the picture's exactly: ManagedCodecs' H.264 is bit-exact with FFmpeg's), and the
    // managed path's figures are ffmpeg's too.
    [Fact]
    public void BothDecodingPathsAgreeOnTheH264File()
    {
        string file = _samples["programme_h264.mp4"];
        var ffmpeg = _samples.Ffmpeg("programme_h264.mp4");
        var managed = _samples.Managed("programme_h264.mp4");

        Assert.Equal(ffmpeg.Duration, managed.Duration, 0.05);
        Assert.Equal((ffmpeg.RateNumerator, ffmpeg.RateDenominator), (managed.RateNumerator, managed.RateDenominator));
        Assert.Equal(ffmpeg.NotMeasured, managed.NotMeasured);
        Assert.Equal(ffmpeg.StartTimecode, managed.StartTimecode);
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

        Assert.Equal(ffmpeg.Black, managed.Black);
        Assert.Equal(ffmpeg.Bars, managed.Bars);
        Assert.Equal(ffmpeg.OutOfRange, managed.OutOfRange);
        Assert.Equal(ffmpeg.WorstOutOfRange, managed.WorstOutOfRange);
        Assert.Equal(ffmpeg.Crop, managed.Crop);
        Assert.Equal(ffmpeg.PictureTypes, managed.PictureTypes);
        Assert.NotNull(ffmpeg.Packets);
        Assert.NotNull(managed.Packets);
        var (p, q) = (ffmpeg.Packets, managed.Packets);
        Assert.Equal(p.Bytes, q.Bytes);
        Assert.Equal(p.BytesPerSecond, q.BytesPerSecond);
        Assert.Equal(p.Keyframes, q.Keyframes);

        FfmpegCli.AssertStartTimecode(file, managed);
        FfmpegCli.AssertLoudness(file, managed);
        FfmpegCli.AssertSilences(file, managed);
        FfmpegCli.AssertTone(file, managed.Tone[0]);
        FfmpegCli.AssertBlack(file, managed);
        FfmpegCli.AssertWorstOutOfRange(file, managed);
        FfmpegCli.AssertCrop(file, managed);
        FfmpegCli.AssertPictureTypes(file, managed);
        FfmpegCli.AssertPackets(file, managed);
    }
}
