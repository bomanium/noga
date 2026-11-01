using FrameReader;

namespace MediaQc.Ffmpeg;

/// <summary>
/// Reads a file's start timecode and its first audio track's description (<see cref="AudioTrack"/>) in-process: FrameReader's MediaProbe.ProbeJson (ffprobe's JSON, tags included) and
/// <see cref="TimecodeTag.Pick"/>. Both decoding paths use it; the managed path loads FFmpeg's libraries for it alone
/// (docs/decisions/2026-11-01-start-timecode.md).
/// </summary>
public static class StartTimecode
{
    /// <summary>
    /// <paramref name="measured"/> with <paramref name="path"/>'s start timecode (null when it has none). When FFmpeg's
    /// libraries don't load or can't open the file, "timecode" is listed as not measured and timecodes count from
    /// 00:00:00:00.
    /// </summary>
    public static Measurements Add(Measurements measured, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(measured);
        ArgumentNullException.ThrowIfNull(path);
        string json;
        try
        {
            Libraries.Load();
            json = MediaProbe.ProbeJson(path, new ProbeOptions { CancellationToken = cancellationToken });
        }
        catch (FrameReaderException)
        {
            return measured with { StartTimecode = null, NotMeasured = [.. measured.NotMeasured, "timecode"] };
        }

        return measured with
        {
            StartTimecode = TimecodeTag.Pick(json, measured.RateNumerator, measured.RateDenominator),
            AudioTrack = AudioTrack.First(json),
        };
    }
}
