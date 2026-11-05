using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Noga.Ffmpeg;

namespace Noga.Tests;

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
    public static string Probe(string file) => Ffprobe(file, "-show_format", "-show_streams", "-of", "json");

    /// <summary>What <c>ffprobe -v error ARGS FILE</c> prints.</summary>
    public static string Ffprobe(string file, params string[] args)
    {
        var start = new ProcessStartInfo("ffprobe") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string arg in (string[])["-v", "error", .. args, file])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"ffprobe {string.Join(' ', args)} failed:\n{error.Result}");
        return output;
    }

    /// <summary>Each frame's picture type is ffprobe -show_frames' pict_type, exactly.</summary>
    public static void AssertPictureTypes(string file, Measurements measured)
    {
        string types = Ffprobe(file, "-select_streams", "v:0", "-show_entries", "frame=pict_type", "-of", "csv=p=0");

        // A frame with side data gets more columns after its type ("I,").
        Assert.Equal(string.Concat(types.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line => line[0])), measured.PictureTypes);
    }

    /// <summary>The keyframes, the bytes in all and each second, and each packet's time are ffprobe -show_packets', exactly.</summary>
    public static void AssertPackets(string file, Measurements measured)
    {
        using var json = System.Text.Json.JsonDocument.Parse(Ffprobe(file, "-select_streams", "v:0", "-show_entries", "stream=time_base:packet=pts,dts,size,flags", "-of", "json"));
        string[] timeBase = json.RootElement.GetProperty("streams")[0].GetProperty("time_base").GetString()!.Split('/');
        double unit = Parse(timeBase[0]) / Parse(timeBase[1]);
        var expected = VideoPackets.From(json.RootElement.GetProperty("packets").EnumerateArray().Select(p =>
        {
            string pts = p.TryGetProperty("pts", out var value) ? value.ToString() : p.GetProperty("dts").ToString();
            return (Parse(pts) * unit, long.Parse(p.GetProperty("size").GetString()!, CultureInfo.InvariantCulture), p.GetProperty("flags").GetString()!.Contains('K', StringComparison.Ordinal));
        }));
        Assert.NotNull(measured.Packets);
        var got = measured.Packets;

        Assert.Equal(expected.Bytes, got.Bytes);
        Assert.Equal(expected.BytesPerSecond, got.BytesPerSecond);
        Assert.Equal(expected.Keyframes, got.Keyframes);
        Assert.Equal(expected.Count, got.Count);
        Assert.Equal(expected.Times, got.Times);
    }

    /// <summary>
    /// What was decoded against what the file lists (Profile.DecodeFilter, SoundDecodeFilter): the frames the container
    /// lists, the packets read and the frames decoded are ffprobe -count_packets -count_frames' exactly, and each audio
    /// track's decoded length is the samples ffmpeg's astats counts, within one AAC frame (1024 samples).
    /// </summary>
    public static void AssertDecode(string file, Measurements measured)
    {
        if (measured.HasPicture)
        {
            using var json = System.Text.Json.JsonDocument.Parse(Ffprobe(file, "-select_streams", "v:0", "-count_packets", "-count_frames", "-show_entries", "stream=nb_frames,nb_read_packets,nb_read_frames", "-of", "json"));
            var stream = json.RootElement.GetProperty("streams")[0];
            long Count(string name) => long.Parse(stream.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);
            Assert.NotNull(measured.Packets);
            Assert.Equal(Count("nb_read_packets"), measured.Packets.Count);
            Assert.Equal(Count("nb_read_frames"), measured.PictureTypes.Length);
            Assert.Equal(stream.TryGetProperty("nb_frames", out _) ? (int)Count("nb_frames") : null, measured.Format?.Video?.ListedFrames);
        }

        var tracks = new List<(int Number, ChannelMeasures? Channels)> { (1, measured.Channels) };
        tracks.AddRange(measured.OtherTracks.Select(t => (t.Number, t.Channels)));
        foreach (var (number, channels) in tracks)
        {
            if (channels is null)
                continue;
            string log = Run(file, $"-map 0:a:{number - 1} -af astats=measure_overall=Number_of_samples:measure_perchannel=none");
            long samples = long.Parse(NumberOfSamples().Match(log).Groups[1].Value, CultureInfo.InvariantCulture);
            int rate = measured.Format!.Audio.Single(a => a.Number == number).SampleRate!.Value;
            Assert.InRange(channels.Duration * rate, samples - 1024, samples + 1024);
        }
    }

    /// <summary>
    /// Where each stream starts (Profile.AvStartFilter): the picture's and every audio track's start_time, ffprobe's
    /// exactly (to its microseconds).
    /// </summary>
    public static void AssertStarts(string file, Measurements measured)
    {
        using var json = System.Text.Json.JsonDocument.Parse(Ffprobe(file, "-show_entries", "stream=codec_type,start_time", "-of", "json"));
        var streams = json.RootElement.GetProperty("streams").EnumerateArray().ToList();
        static double? Start(System.Text.Json.JsonElement stream) =>
            stream.TryGetProperty("start_time", out var value) ? double.Parse(value.GetString()!, CultureInfo.InvariantCulture) : null;
        Assert.NotNull(measured.Format);
        var format = measured.Format;
        if (format.Video is { } video)
            Assert.Equal(Start(streams.First(s => s.GetProperty("codec_type").GetString() == "video")), video.StartTime);
        var audio = streams.Where(s => s.GetProperty("codec_type").GetString() == "audio").ToList();
        Assert.Equal(audio.Select(Start), format.Audio.Select(a => a.StartTime));
    }

    [GeneratedRegex(@"Number of samples: (\d+)")]
    private static partial Regex NumberOfSamples();

    /// <summary>The start timecode is the tag the ffprobe command line gives.</summary>
    public static void AssertStartTimecode(string file, Measurements measured) =>
        Assert.Equal(TimecodeTag.Pick(Probe(file), measured.RateNumerator, measured.RateDenominator), measured.StartTimecode);

    /// <summary>
    /// The format is ffprobe -show_format -show_streams' values, exactly (docs/decisions/2026-11-03-delivery-format.md),
    /// with one allowance: a colour value (primaries, transfer, matrix, range) the command line's ffprobe leaves
    /// unstated may be stated in-process, where FrameReader's probe reads a ProRes frame's header (ffprobe 6.1 and 8.0
    /// don't print it). Any value both state is the same.
    /// </summary>
    public static void AssertFormat(string file, Measurements measured)
    {
        var expected = MediaFormat.From(Probe(file));
        Assert.NotNull(measured.Format);
        var format = measured.Format;
        Assert.Equal(expected.Container, format.Container);
        Assert.Equal(expected.Duration, format.Duration);
        Assert.Equal(expected.Audio, format.Audio);
        if (expected.Video is null || format.Video is null)
        {
            Assert.Equal(expected.Video, format.Video);
            return;
        }

        var (cli, ours) = (expected.Video, format.Video);
        Assert.Equal((cli.Codec, cli.Profile, cli.Width, cli.Height, cli.FrameRate, cli.FieldOrder, cli.PixelFormat, cli.BitDepth, cli.DisplayAspect),
            (ours.Codec, ours.Profile, ours.Width, ours.Height, ours.FrameRate, ours.FieldOrder, ours.PixelFormat, ours.BitDepth, ours.DisplayAspect));
        foreach (var (said, read) in (IEnumerable<(string?, string?)>)[(cli.ColourPrimaries, ours.ColourPrimaries), (cli.Transfer, ours.Transfer), (cli.Matrix, ours.Matrix), (cli.ColourRange, ours.ColourRange)])
            Assert.True(said is null || said == read, $"ffprobe says {said}, noga read {read}");
    }

    /// <summary>The loudness is ebur128's from the same point (ffmpeg -ss), within 0.1 LU and 0.1 dB.</summary>
    public static void AssertLoudness(string file, Measurements measured) =>
        AssertLoudness(file, Assert.NotNull(measured.Loudness), measured.LoudnessFrom, 1);

    /// <summary>Every figure of an audio track after the first, against ffmpeg -map 0:a:N as for the first.</summary>
    public static void AssertTrack(string file, TrackMeasures track)
    {
        AssertLoudness(file, track.Loudness, track.LoudnessFrom, track.Number);
        AssertSilences(file, track.Silences, track.Number);
        if (track.Tone.Count > 0)
            AssertTone(file, track.Tone[0], track.Number);
    }

    static void AssertLoudness(string file, LoudnessMeasure loudness, double from, int track)
    {
        var ffmpeg = Run(file, Invariant($"-map 0:a:{track - 1} -af ebur128=peak=true:framelog=quiet"), from > 0 ? Invariant($"-ss {from}") : "");

        Assert.Equal(Number(ffmpeg, @"I:\s+(-?[\d.]+) LUFS"), Assert.NotNull(loudness.Integrated), 0.1);
        Assert.Equal(Number(ffmpeg, @"LRA:\s+([\d.]+) LU"), loudness.Range, 0.1);
        Assert.Equal(Number(ffmpeg, @"True peak:\s+Peak:\s+(-?[\d.]+) dBFS"), loudness.TruePeak, 0.1);

        // The loudest momentary and short-term loudness (Profile.MomentaryFilter, ShortTermFilter): the highest M and S
        // of ebur128's 100 ms readings within 0.1 LU (they print to 0.1), and its reading at the time given as high.
        string log = Run(file, Invariant($"-map 0:a:{track - 1} -af ebur128=framelog=verbose -v verbose"), from > 0 ? Invariant($"-ss {from}") : "");
        var readings = Readings().Matches(log).Select(m => (
            Time: double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            M: double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            S: double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture))).ToList();
        Assert.NotEmpty(readings);
        void Loudest(double? measured, double at, Func<(double Time, double M, double S), double> value)
        {
            double highest = readings.Max(value);
            if (measured is not { } loudest)
            {
                Assert.True(highest < -120, "ebur128 has a reading where none was measured");
                return;
            }

            Assert.Equal(highest, loudest, 0.1);
            var there = readings.MinBy(r => Math.Abs(r.Time - (at - from)));
            Assert.Equal(highest, value(there), 0.1);
        }

        Loudest(loudness.MaxMomentary, loudness.MaxMomentaryAt, r => r.M);
        Loudest(loudness.MaxShortTerm, loudness.MaxShortTermAt, r => r.S);
    }

    [GeneratedRegex(@"t:\s*([\d.]+)\s+TARGET:\S+ LUFS\s+M:\s*(-?[\d.]+)\s+S:\s*(-?[\d.]+)")]
    private static partial Regex Readings();

    /// <summary>The silences are silencedetect's with the profile's options, each end within 10 ms.</summary>
    public static void AssertSilences(string file, Measurements measured) => AssertSilences(file, measured.Silences, 1);

    static void AssertSilences(string file, IReadOnlyList<TimeRange> silences, int track)
    {
        var expected = Spans(Run(file, Invariant($"-map 0:a:{track - 1} -af {FfmpegMeasurer.SilenceGraph(Profile.EbuR128)}")));

        Assert.Equal(expected.Count, silences.Count);
        foreach (var (want, got) in expected.Zip(silences))
        {
            Assert.Equal(want.Start, got.Start, 0.01);
            Assert.Equal(want.End, got.End, 0.01);
        }
    }

    /// <summary>
    /// The first tone ends where a 1 kHz bandpass falls below -40 dBFS (the finder's own threshold), within 20 ms, and
    /// its level is the loudest channel's astats RMS over it as a sine's peak (+3.01 dB), within 0.1 dB.
    /// </summary>
    public static void AssertTone(string file, ToneSpan tone, int track = 1)
    {
        Assert.Equal(1000, tone.Frequency);
        var quiet = Spans(Run(file, Invariant($"-map 0:a:{track - 1} -af bandpass=f=1000,silencedetect=n=-40dB:d=1")));
        Assert.Equal(quiet[0].Start, tone.End, 0.02);

        string stats = Run(file, Invariant($"-map 0:a:{track - 1} -af atrim=start={tone.Start}:end={tone.End},astats=measure_perchannel=RMS_level:measure_overall=none"));
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

    /// <summary>
    /// A track's channels against the command line (docs/decisions/2026-11-03-channel-faults.md): each channel's
    /// silences are silencedetect's on that channel alone (pan=mono), each end within 10 ms; its RMS level astats', within
    /// 0.1 dB; the phase the mean of aphasemeter's over 4800-sample frames, within 0.01; and, with
    /// <paramref name="fullScaleBothWays"/> (a channel that reaches full scale up and down), its samples at full scale
    /// astats' Peak count, the samples at its highest and lowest level, exactly.
    /// </summary>
    public static void AssertChannels(string file, ChannelMeasures channels, Profile profile, int track = 1, bool fullScaleBothWays = false)
    {
        string map = Invariant($"-map 0:a:{track - 1}");
        for (int c = 0; c < channels.Channels; c++)
        {
            var expected = Spans(Run(file, Invariant($"{map} -af pan=mono|c0=c{c},silencedetect=n={profile.SilenceNoise}dB:d={profile.MinSilence}")));
            Assert.Equal(expected.Count, channels.Silences[c].Count);
            foreach (var (e, a) in expected.Zip(channels.Silences[c]))
            {
                Assert.Equal(e.Start, a.Start, 0.01);
                Assert.Equal(e.End, a.End, 0.01);
            }
        }

        string astats = Run(file, Invariant($"{map} -af astats"));
        var levels = AstatsChannel().Matches(astats).ToList();
        Assert.Equal(channels.Channels, levels.Count);
        for (int c = 0; c < channels.Channels; c++)
        {
            string rms = levels[c].Groups[1].Value;
            int peaks = int.Parse(levels[c].Groups[2].Value, CultureInfo.InvariantCulture);
            if (rms == "-inf")
                Assert.True(double.IsNegativeInfinity(channels.RmsLevels[c]));
            else
                Assert.Equal(Parse(rms), channels.RmsLevels[c], 0.1);
            if (fullScaleBothWays && channels.FullScaleSamples[c] > 0)
                Assert.Equal(peaks, channels.FullScaleSamples[c]);
        }

        if (channels.Phase is { } phase)
        {
            string log = Run(file, Invariant($"{map} -af pan=stereo|c0=c0|c1=c1,asetnsamples=n=4800,aphasemeter=video=0,ametadata=print:key=lavfi.aphasemeter.phase"));
            var frames = PhaseValue().Matches(log).Select(m => Parse(m.Groups[1].Value)).ToList();
            Assert.NotEmpty(frames);
            Assert.Equal(frames.Average(), phase, 0.01);
        }
    }

    /// <summary>
    /// The freezes are freezedetect's with the profile's n and d, each end within one frame (one still frozen at the end,
    /// which freezedetect leaves open, ends with the video).
    /// </summary>
    public static void AssertFreezes(string file, Measurements measured, Profile profile)
    {
        string log = Run(file, Invariant($"-an -vf freezedetect=n={profile.FreezeNoise}:d={profile.MinFreeze}"));
        var starts = FreezeStart().Matches(log).Select(m => Parse(m.Groups[1].Value)).ToList();
        var ends = FreezeEnd().Matches(log).Select(m => Parse(m.Groups[1].Value)).ToList();
        double frame = (double)measured.RateDenominator / measured.RateNumerator;
        Assert.Equal(starts.Count, measured.Freezes.Count);
        for (int i = 0; i < starts.Count; i++)
        {
            Assert.Equal(starts[i], measured.Freezes[i].Start, frame);
            if (i < ends.Count)
                Assert.Equal(ends[i], measured.Freezes[i].End, frame);
            else
                Assert.Equal(measured.Duration, measured.Freezes[i].End, frame);
        }
    }

    /// <summary>idet's totals are the command line's idet's (with its defaults), exactly; the last it prints, as a graph made again prints its own.</summary>
    public static void AssertInterlace(string file, Measurements measured)
    {
        var interlace = Assert.IsType<InterlaceMeasures>(measured.Interlace);
        string log = Run(file, "-an -vf idet");
        var single = IdetTotals().Matches(log).Last(m => m.Groups[1].Value == "Single");
        var multi = IdetTotals().Matches(log).Last(m => m.Groups[1].Value == "Multi");
        long At(Match m, int i) => long.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);

        Assert.Equal((At(single, 2), At(single, 3), At(single, 4), At(single, 5)), (interlace.SingleTff, interlace.SingleBff, interlace.SingleProgressive, interlace.SingleUndetermined));
        Assert.Equal((At(multi, 2), At(multi, 3), At(multi, 4), At(multi, 5)), (interlace.MultiTff, interlace.MultiBff, interlace.MultiProgressive, interlace.MultiUndetermined));
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

    [GeneratedRegex(@"(Single|Multi) frame detection: TFF:\s*(\d+) BFF:\s*(\d+) Progressive:\s*(\d+) Undetermined:\s*(\d+)")]
    private static partial Regex IdetTotals();

    [GeneratedRegex(@"freeze_start: (-?[\d.]+)")]
    private static partial Regex FreezeStart();

    [GeneratedRegex(@"freeze_end: (-?[\d.]+)")]
    private static partial Regex FreezeEnd();

    // astats' per-channel block: its RMS level, then (further on) its Peak count.
    [GeneratedRegex(@"Channel: \d+[\s\S]*?RMS level dB: (-?[\d.]+|-inf)[\s\S]*?Peak count: (\d+)")]
    private static partial Regex AstatsChannel();

    [GeneratedRegex(@"lavfi\.aphasemeter\.phase=(-?[\d.]+(?:e-?\d+)?)")]
    private static partial Regex PhaseValue();

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
