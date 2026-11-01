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

    /// <summary>The programme loudness from <see cref="LoudnessFrom"/>, or null when the file has no audio.</summary>
    public LoudnessMeasure? Loudness { get; init; }

    /// <summary>The black spans (blackdetect's), in order.</summary>
    public IReadOnlyList<TimeRange> Black { get; init; } = [];

    /// <summary>The silences (silencedetect's), in order.</summary>
    public IReadOnlyList<TimeRange> Silences { get; init; } = [];

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

/// <summary>A span of line-up tone: its frequency in Hz and its level in dBFS (the loudest channel's RMS as a sine's peak).</summary>
public readonly record struct ToneSpan(double Start, double End, double Frequency, double Level);

/// <summary>EBU R128 / ITU-R BS.1770 loudness: integrated (LUFS; null below the absolute gate), range (LU) and true peak (dBTP).</summary>
public readonly record struct LoudnessMeasure(double? Integrated, double Range, double TruePeak);

/// <summary>A span (seconds) where frames have too many pixels outside broadcast range, and the worst frame's share (0 to 1).</summary>
public readonly record struct RangeSpan(double Start, double End, double WorstShare);

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
