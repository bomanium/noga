using System.Text.Json;

namespace MediaQc;

/// <summary>
/// A file's start timecode from ffprobe's JSON (-show_format -show_streams), which FrameReader's MediaProbe.ProbeJson
/// gives in-process (docs/decisions/2026-11-01-start-timecode.md): the video stream's timecode tag (MOV and MP4: their
/// tmcd track's), else the container's (MXF), else any stream's, when it is a timecode at the file's rate.
/// </summary>
public static class TimecodeTag
{
    /// <summary>What it is checked against: ffprobe's own tags.</summary>
    public const string CheckedAgainst = "ffprobe -show_entries format_tags=timecode:stream_tags=timecode (the same tag)";

    /// <summary>The start timecode in <paramref name="json"/>; null when there is none valid at the rate.</summary>
    public static string? Pick(string json, long rateNumerator, long rateDenominator)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var streams = root.TryGetProperty("streams", out var s) ? s.EnumerateArray().ToList() : [];
        var candidates = streams.Where(IsVideo).Select(Tag)
            .Append(root.TryGetProperty("format", out var format) ? Tag(format) : null)
            .Concat(streams.Select(Tag));
        return candidates.FirstOrDefault(tc => tc is not null && IsValid(tc, rateNumerator, rateDenominator));

        static bool IsVideo(JsonElement stream) => stream.TryGetProperty("codec_type", out var type) && type.GetString() == "video";

        static string? Tag(JsonElement element) =>
            element.TryGetProperty("tags", out var tags) && tags.TryGetProperty("timecode", out var timecode) ? timecode.GetString() : null;
    }

    static bool IsValid(string timecode, long rateNumerator, long rateDenominator)
    {
        try
        {
            _ = new Timecode(rateNumerator, rateDenominator, timecode);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
