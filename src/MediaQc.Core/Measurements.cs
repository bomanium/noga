namespace MediaQc;

/// <summary>
/// What was measured in one file, whichever decoder read it (ManagedCodecs or FFmpeg through FrameReader:
/// docs/decisions/2026-10-31-decoding.md). Times are seconds from the first frame. A profile judges these.
/// </summary>
public sealed record Measurements
{
    /// <summary>The file's duration in seconds.</summary>
    public required double Duration { get; init; }

    /// <summary>The video frame rate's numerator (25 for 25 fps, 30000 for 29.97).</summary>
    public required long RateNumerator { get; init; }

    /// <summary>The video frame rate's denominator (1, or 1001 for 29.97).</summary>
    public required long RateDenominator { get; init; }

    /// <summary>The file's start timecode (its timecode track), or null when it has none.</summary>
    public string? StartTimecode { get; init; }

    /// <summary>
    /// Where the loudness is measured from (seconds): the end of the line-up tone (<see cref="LineUp.End"/>), 0 when
    /// there is none. Programme loudness leaves line-up out (docs/decisions/2026-10-31-loudness-after-line-up.md).
    /// </summary>
    public double LoudnessFrom { get; init; }

    /// <summary>The first audio track, whose figures these are; null when the file has none or its tags weren't read.</summary>
    public AudioTrack? AudioTrack { get; init; }

    /// <summary>Every audio track's description, in order (ffprobe's); empty when none or the tags weren't read.</summary>
    public IReadOnlyList<AudioTrack> AudioTracks { get; init; } = [];

    /// <summary>The file's format as ffprobe describes it (docs/decisions/2026-11-03-delivery-format.md); null when it couldn't be read.</summary>
    public MediaFormat? Format { get; init; }

    /// <summary>
    /// The audio tracks after the first, each measured as the first is (its own line-up, silences and loudness); empty
    /// when the file has one, or the decoder path measures only the first.
    /// </summary>
    public IReadOnlyList<TrackMeasures> OtherTracks { get; init; } = [];

    /// <summary>The programme loudness from <see cref="LoudnessFrom"/>, or null when the file has no audio.</summary>
    public LoudnessMeasure? Loudness { get; init; }

    /// <summary>The black spans (blackdetect's), in order.</summary>
    public IReadOnlyList<TimeRange> Black { get; init; } = [];

    /// <summary>The silences (silencedetect's), in order.</summary>
    public IReadOnlyList<TimeRange> Silences { get; init; } = [];

    /// <summary>The frozen pictures (freezedetect's), in order (docs/decisions/2026-11-03-freeze-frames.md).</summary>
    public IReadOnlyList<TimeRange> Freezes { get; init; } = [];

    /// <summary>Whether the picture looks interlaced, idet's totals (docs/decisions/2026-11-03-interlacing.md); null when not measured.</summary>
    public InterlaceMeasures? Interlace { get; init; }

    /// <summary>The first audio track's channels one by one; null when not measured.</summary>
    public ChannelMeasures? Channels { get; init; }

    /// <summary>The colour-bar spans, in order.</summary>
    public IReadOnlyList<TimeRange> Bars { get; init; } = [];

    /// <summary>The line-up tone segments, in order.</summary>
    public IReadOnlyList<ToneSpan> Tone { get; init; } = [];

    /// <summary>
    /// The spans where more than the profile's share of a frame's pixels are outside broadcast range (signalstats'
    /// BRNG), colour bars left out (their PLUGE is below black on purpose), each with its worst frame's share.
    /// </summary>
    public IReadOnlyList<RangeSpan> OutOfRange { get; init; } = [];

    /// <summary>The frame with the most pixels outside broadcast range, bars left out; null without video measures.</summary>
    public FrameShare? WorstOutOfRange { get; init; }

    /// <summary>The active picture as cropdetect finds it over the whole file; null when not measured (or 3 frames or fewer).</summary>
    public ActivePicture? Crop { get; init; }

    /// <summary>
    /// Each frame's picture type in display order, one character each as ffprobe -show_frames prints pict_type (I, P,
    /// B; ? when the decoder gives none); empty without video measures.
    /// </summary>
    public string PictureTypes { get; init; } = "";

    /// <summary>
    /// The shots, in order, covering the video (PySceneDetect detect-adaptive's, through ShotDetector); null when not
    /// detected.
    /// </summary>
    public IReadOnlyList<TimeRange>? Shots { get; init; }

    /// <summary>The video packets' keyframes and sizes (ffprobe -show_packets'); null when not read.</summary>
    public VideoPackets? Packets { get; init; }

    /// <summary>
    /// The checks this decoder path doesn't measure yet ("black", "bars"): their lists above are empty because
    /// nothing looked, not because nothing was found, and the report says so.
    /// </summary>
    public IReadOnlyList<string> NotMeasured { get; init; } = [];
}

/// <summary>A span of the file, in seconds from the first frame: <paramref name="Start"/> inclusive, <paramref name="End"/> exclusive.</summary>
public readonly record struct TimeRange(double Start, double End)
{
    /// <summary>The span's length in seconds.</summary>
    public double Duration => End - Start;
}

/// <summary>
/// One audio track's measures (<paramref name="Number"/> from 1, as the report counts them; ffmpeg's 0:a:Number-1): where its
/// loudness is measured from (the end of its line-up), its loudness, silences and line-up tone.
/// </summary>
public sealed record TrackMeasures(int Number, double LoudnessFrom, LoudnessMeasure Loudness, IReadOnlyList<TimeRange> Silences, IReadOnlyList<ToneSpan> Tone, ChannelMeasures? Channels = null);

/// <summary>
/// An audio track's channels one by one (docs/decisions/2026-11-03-channel-faults.md): each one's silences, RMS level
/// (dBFS; -∞ for digital silence), runs of samples at full scale and samples there, and the first two's mean phase correlation (1 in
/// phase, 0 unrelated, -1 inverted; null for a mono track).
/// </summary>
/// <summary>
/// FFmpeg's idet totals (docs/decisions/2026-11-03-interlacing.md): how many frames each frame alone, and through
/// idet's four-frame history, looks top field first, bottom field first, progressive or undetermined.
/// </summary>
public sealed record InterlaceMeasures(long SingleTff, long SingleBff, long SingleProgressive, long SingleUndetermined, long MultiTff, long MultiBff, long MultiProgressive, long MultiUndetermined)
{
    /// <summary>Whether more frames look interlaced than progressive (idet's multiple-frame totals).</summary>
    public bool LooksInterlaced => MultiTff + MultiBff > MultiProgressive;

    /// <summary>Whether more of the interlaced-looking frames show the top field first.</summary>
    public bool TopFieldFirst => MultiTff >= MultiBff;
}

public sealed record ChannelMeasures(int Channels, double Duration, IReadOnlyList<IReadOnlyList<TimeRange>> Silences, IReadOnlyList<double> RmsLevels, IReadOnlyList<int> ClippedRuns, IReadOnlyList<long> FullScaleSamples, double? Phase);

/// <summary>A span of line-up tone: its frequency in Hz and its level in dBFS (the loudest channel's RMS as a sine's peak).</summary>
public readonly record struct ToneSpan(double Start, double End, double Frequency, double Level);

/// <summary>EBU R128 / ITU-R BS.1770 loudness: integrated (LUFS; null below the absolute gate), range (LU) and true peak (dBTP).</summary>
public readonly record struct LoudnessMeasure(double? Integrated, double Range, double TruePeak);

/// <summary>A span (seconds) where frames have too many pixels outside broadcast range, and the worst frame's share (0 to 1).</summary>
public readonly record struct RangeSpan(double Start, double End, double WorstShare)
{
    /// <summary>
    /// <paramref name="spans"/> (in order) with those less than <paramref name="gap"/> seconds apart joined into one,
    /// from the first's start to the last's end with the worst share among them: frames hovering about the threshold
    /// make one warning, not one per frame (docs/decisions/2026-11-01-open-films.md).
    /// </summary>
    public static IReadOnlyList<RangeSpan> Merge(IEnumerable<RangeSpan> spans, double gap)
    {
        ArgumentNullException.ThrowIfNull(spans);
        var merged = new List<RangeSpan>();
        foreach (var span in spans)
        {
            if (merged.Count > 0 && span.Start - merged[^1].End < gap)
                merged[^1] = new RangeSpan(merged[^1].Start, Math.Max(merged[^1].End, span.End), Math.Max(merged[^1].WorstShare, span.WorstShare));
            else
                merged.Add(span);
        }

        return merged;
    }
}

/// <summary>One frame's share (0 to 1) of something, at <paramref name="Seconds"/>.</summary>
public readonly record struct FrameShare(double Seconds, double Share);

/// <summary>
/// cropdetect's active picture: <paramref name="Width"/> by <paramref name="Height"/> at (<paramref name="X"/>,
/// <paramref name="Y"/>) in a <paramref name="FrameWidth"/> by <paramref name="FrameHeight"/> frame (crop=w:h:x:y).
/// </summary>
public readonly record struct ActivePicture(int FrameWidth, int FrameHeight, int Width, int Height, int X, int Y)
{
    /// <summary>Whether the active picture is the whole frame, to cropdetect's rounding (16).</summary>
    public bool FillsFrame => FrameWidth - Width < 16 && FrameHeight - Height < 16;
}

/// <summary>
/// The video stream's packets, as ffprobe -show_packets gives them: where the keyframes are (the packets flagged K) and
/// how many bytes there are, in all and in each second (by pts, seconds from the first frame: second k is [k, k + 1)).
/// </summary>
public sealed record VideoPackets
{
    /// <summary>Each keyframe's time (seconds), in order.</summary>
    public required IReadOnlyList<double> Keyframes { get; init; }

    /// <summary>The bytes of every video packet.</summary>
    public required long Bytes { get; init; }

    /// <summary>The bytes of the packets in each second, from second 0.</summary>
    public required IReadOnlyList<long> BytesPerSecond { get; init; }

    /// <summary>The packets of one stream: each one's time (seconds), size (bytes) and whether it is a keyframe.</summary>
    public static VideoPackets From(IEnumerable<(double Seconds, long Size, bool Key)> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        var keyframes = new List<double>();
        var perSecond = new List<long>();
        long bytes = 0;
        foreach (var (seconds, size, key) in packets)
        {
            bytes += size;
            int second = (int)Math.Max(0, Math.Floor(seconds));
            while (perSecond.Count <= second)
                perSecond.Add(0);
            perSecond[second] += size;
            if (key)
                keyframes.Add(seconds);
        }

        keyframes.Sort();
        return new VideoPackets { Keyframes = keyframes, Bytes = bytes, BytesPerSecond = perSecond };
    }
}
