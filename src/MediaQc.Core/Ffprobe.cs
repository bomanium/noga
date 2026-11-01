using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace MediaQc;

/// <summary>
/// The start timecode as ffprobe reads it, through the ffprobe on PATH (docs/decisions/2026-11-01-timecode-via-ffprobe.md):
/// neither decoding path reads a file's tags yet, so this stands in until FrameReader does. Nothing of ffprobe is shipped;
/// without it the timecode isn't read and the report says so.
/// </summary>
public static class Ffprobe
{
    /// <summary>What it is checked against: the same command, so the same tag.</summary>
    public const string TimecodeCommand = "ffprobe -show_entries format_tags=timecode:stream_tags=timecode";

    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The start timecode of <paramref name="path"/>: the video stream's timecode tag (MOV and MP4: their tmcd track's),
    /// else the container's (MXF), else any stream's; null when it has none, or none valid at the frame rate.
    /// <see cref="StartTimecode.Read"/> is false when ffprobe isn't on PATH or fails.
    /// </summary>
    public static StartTimecode ReadStartTimecode(string path, long rateNumerator, long rateDenominator)
    {
        ArgumentNullException.ThrowIfNull(path);
        var start = new ProcessStartInfo("ffprobe") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-v", "error", "-show_entries", "format_tags=timecode:stream_tags=timecode:stream=codec_type", "-of", "json", path])
            start.ArgumentList.Add(arg);
        string json;
        try
        {
            using var process = Process.Start(start)!;
            var error = process.StandardError.ReadToEndAsync();
            json = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(Timeout))
            {
                process.Kill();
                return default;
            }

            _ = error.Result;
            if (process.ExitCode != 0)
                return default;
        }
        catch (Win32Exception)
        {
            return default;
        }

        return new StartTimecode(true, Pick(json, rateNumerator, rateDenominator));
    }

    /// <summary>
    /// <paramref name="measured"/> with the file's start timecode; when ffprobe can't be run, "timecode" is listed as not
    /// measured and timecodes count from 00:00:00:00.
    /// </summary>
    public static Measurements WithStartTimecode(Measurements measured, string path)
    {
        ArgumentNullException.ThrowIfNull(measured);
        var timecode = ReadStartTimecode(path, measured.RateNumerator, measured.RateDenominator);
        return timecode.Read
            ? measured with { StartTimecode = timecode.Value }
            : measured with { StartTimecode = null, NotMeasured = [.. measured.NotMeasured, "timecode"] };
    }

    /// <summary>The timecode in ffprobe's JSON, as <see cref="ReadStartTimecode"/> picks it.</summary>
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

/// <summary>Whether ffprobe ran (<paramref name="Read"/>), and the start timecode it gave (null: the file has none).</summary>
public readonly record struct StartTimecode(bool Read, string? Value);
