namespace MediaQc;

/// <summary>Where a file's line-up ends: what programme loudness is measured after.</summary>
public static class LineUp
{
    /// <summary>
    /// The end of the line-up tone (seconds), or 0 when there is none: the first tone segment, when only silence
    /// comes before it (it starts the file, or a silence from the start runs up to it within one 20 ms tone window).
    /// A tone that runs to the end of the audio (<paramref name="duration"/>, within 0.1 s) leaves no programme to
    /// measure apart from it: then 0, the whole file.
    /// </summary>
    public static double End(IReadOnlyList<ToneSpan> tone, IReadOnlyList<TimeRange> silences, double duration)
    {
        ArgumentNullException.ThrowIfNull(tone);
        ArgumentNullException.ThrowIfNull(silences);
        if (tone.Count == 0)
            return 0;

        const double window = 0.02;
        var first = tone[0];
        bool atHead = first.Start <= window || silences.Any(s => s.Start <= window && s.End >= first.Start - window);
        return atHead && first.End < duration - 0.1 ? first.End : 0;
    }
}
