namespace Noga;

/// <summary>
/// The sound a WAV, RF64, BW64 or AIFF header lists (docs/decisions/2026-11-06-sound-headers.md): <paramref name="Kind"/>
/// ("WAV", "RF64", "BW64", "AIFF", "AIFF-C") and the sample frames it lists at <paramref name="SampleRate"/> Hz, null when
/// the header was never finished (a WAV data size of 0 or 0xFFFFFFFF, an AIFF frame count of 0 with sound after it), as
/// a writer that couldn't seek back leaves it.
/// </summary>
public sealed record SoundHeader(string Kind, long? Frames, int SampleRate)
{
    /// <summary>The seconds listed; null when unfinished.</summary>
    public double? Seconds => Frames is { } frames && SampleRate > 0 ? (double)frames / SampleRate : null;
}
