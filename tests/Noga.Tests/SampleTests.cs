using Noga.Checking;
using Noga.Managed;

namespace Noga.Tests;

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
        Assert.Equal(1, Assert.IsType<AudioTrack>(measured.AudioTrack).Count);
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
        FfmpegCli.AssertFormat(file, measured);
        Assert.NotNull(measured.Channels);
        FfmpegCli.AssertChannels(file, measured.Channels, Profile.EbuR128);
        FfmpegCli.AssertFreezes(file, measured, Profile.EbuR128);
        FfmpegCli.AssertRepeats(file, measured, Profile.EbuR128);
        FfmpegCli.AssertConstantColour(file, measured, Profile.EbuR128);
        FfmpegCli.AssertInterlace(file, measured);
        FfmpegCli.AssertLoudness(file, measured);
        FfmpegCli.AssertSilences(file, measured);
        FfmpegCli.AssertTone(file, measured.Tone[0]);
        FfmpegCli.AssertBlack(file, measured);
        FfmpegCli.AssertWorstOutOfRange(file, measured);
        FfmpegCli.AssertGamutFrames(file, Profile.EbuR128, 0.2126, 0.0722, every: 25);
        FfmpegCli.AssertCrop(file, measured);
        FfmpegCli.AssertPictureTypes(file, measured);
        FfmpegCli.AssertPackets(file, measured);
        FfmpegCli.AssertDecode(file, measured);
        FfmpegCli.AssertStarts(file, measured);
    }

    // The ProRes programme by the 1080p25 ProRes template: its format is the template's, but make-samples.sh doesn't tag
    // its colour, which a delivery would need. prores_ks writes a matrix and range in each frame all the same, which
    // depend on the ffmpeg that made the file (6.1: bt709, 8.0: bt470bg), so only the untagged two are sure to fail.
    [Fact]
    public void TheProResProgrammeMeetsItsTemplateButForTheColourTags()
    {
        var report = Profiles.Get("hd-1080p25-prores-hq").Evaluate(_samples.Ffmpeg("programme_prores.mov"));

        var format = report.Findings.Where(f => f.Check.StartsWith("format.", StringComparison.Ordinal)).ToList();
        Assert.Equal(17, format.Count);
        var failed = format.Where(f => f.Severity == Severity.Failure).Select(f => f.Check).ToList();
        Assert.Contains("format.colour-primaries", failed);
        Assert.Contains("format.transfer", failed);
        Assert.Empty(failed.Except(["format.colour-primaries", "format.transfer", "format.matrix", "format.colour-range"]));
        Assert.Contains(format, f => f is { Check: "format.scan", Severity: Severity.Pass });
        Assert.Contains(format, f => f is { Check: "format.start-timecode", Severity: Severity.Pass });
        Assert.Equal(QcReport.Failed, report.ExitCode());
    }

    [Fact]
    public void TheRampIsOutOfRangeThroughout()
    {
        var measured = _samples.Ffmpeg("out_of_range.mov");

        var span = Assert.Single(measured.OutOfRange);
        Assert.Equal((0, 5), Rounded(span.Start, span.End));
        Assert.Equal(Assert.NotNull(measured.WorstOutOfRange).Share, span.WorstShare);
        FfmpegCli.AssertWorstOutOfRange(_samples["out_of_range.mov"], measured);
        FfmpegCli.AssertGamutFrames(_samples["out_of_range.mov"], Profile.EbuR128, 0.2126, 0.0722, every: 25);
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

    // The ProRes file's PCM track has a sample an audio sample: asking whether the managed path reads it mustn't build
    // that track's sample table (some 50 MB for these 20 s, 1.6 GB for 10 minutes: docs/notes/performance.md).
    [Fact]
    public void AskingTheManagedPathDoesNotReadAPcmTrack()
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.False(ManagedMeasurer.CanMeasure(_samples["programme_prores.mov"]));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 4 << 20);
    }

    // Jobs runs two checks at once by default (docs/decisions/2026-11-04-checks-side-by-side.md): checks in the same
    // process side by side, on both paths at once, give the same reports as one at a time.
    [Fact]
    public async Task ChecksSideBySideGiveTheSameReports()
    {
        string folder = Directory.CreateTempSubdirectory("noga-side-by-side").FullName;
        try
        {
            string Json(CheckResult result, string file) => ReportJson.Write(result.Report with { Source = null }, Path.GetFileName(file));
            var files = Samples.Programmes.Select(p => _samples[p]).ToArray();
            var alone = files.ToDictionary(f => f, f => Json(Checker.Run(f, Path.Combine(folder, "alone")), f));

            var results = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
            using (var jobs = new Jobs((file, token) =>
            {
                var result = Checker.Run(file, Path.Combine(folder, "side-by-side"), cancellationToken: token);
                results[file] = Json(result, file);
                return result;
            },
                files.Length))
            {
                foreach (string file in files)
                    jobs.Add(file);
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                for (var changed = jobs.Changed; jobs.All.Any(j => j.State is JobState.Queued or JobState.Measuring); changed = jobs.Changed)
                    await changed.WaitAsync(timeout.Token);
                Assert.All(jobs.All, j => Assert.Equal(JobState.Done, j.State));

                // They did run side by side: each started before the first one finished.
                var firstDone = jobs.All.Min(j => j.Finished);
                Assert.All(jobs.All, j => Assert.True(j.Started < firstDone));
            }

            foreach (string file in files)
                Assert.Equal(alone[file], results[file]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
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
        FfmpegCli.AssertFormat(file, managed);
        Assert.NotNull(managed.Channels);
        FfmpegCli.AssertChannels(file, managed.Channels, Profile.EbuR128);
        FfmpegCli.AssertFreezes(file, managed, Profile.EbuR128);
        FfmpegCli.AssertRepeats(file, managed, Profile.EbuR128);
        FfmpegCli.AssertConstantColour(file, managed, Profile.EbuR128);
        FfmpegCli.AssertInterlace(file, managed);
        Assert.Equal(ffmpeg.AudioTrack, managed.AudioTrack);
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
        Assert.Equal(ffmpeg.OutOfGamut, managed.OutOfGamut);
        Assert.Equal(ffmpeg.WorstOutOfGamut, managed.WorstOutOfGamut);
        Assert.Equal(ffmpeg.Crop, managed.Crop);
        Assert.Equal(ffmpeg.PictureTypes, managed.PictureTypes);
        Assert.NotNull(ffmpeg.Shots);
        Assert.NotNull(managed.Shots);
        Assert.Equal(ffmpeg.Shots, managed.Shots);
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
        FfmpegCli.AssertDecode(file, managed);
    }
}
