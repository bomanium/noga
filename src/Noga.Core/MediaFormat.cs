using System.Globalization;
using System.Text.Json;

namespace Noga;

/// <summary>
/// A file's format as ffprobe describes it (-show_format -show_streams; docs/decisions/2026-11-03-delivery-format.md):
/// the container, the first video stream, every audio stream and the duration. Read from FrameReader's ProbeJson on
/// both decoding paths, which prints what ffprobe prints, so each value is ffprobe's, exactly.
/// </summary>
/// <param name="Container">The demuxer's names (format_name: "mov,mp4,m4a,3gp,3g2,mj2", "mxf").</param>
/// <param name="Duration">The container's duration in seconds (format.duration); null when it doesn't say.</param>
/// <param name="Video">The first video stream; null when there is none.</param>
/// <param name="Audio">Every audio stream, in order (the tracks ffmpeg's 0:a:N reads).</param>
public sealed record MediaFormat(string Container, double? Duration, VideoFormat? Video, IReadOnlyList<AudioFormat> Audio)
{
    /// <summary>What every value is read from.</summary>
    public const string CheckedAgainst = "ffprobe -show_format -show_streams";

    /// <summary>
    /// One line: "prores (HQ) 1920x1080 25 fps progressive, yuv422p10le 10-bit, bt709/bt709/bt709 tv, 16:9; audio:
    /// pcm_s24le 48000 Hz 2 ch 24-bit; mov,mp4,m4a,3gp,3g2,mj2, 20.00 s".
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Video is { } v)
            parts.Add(v.ToString());
        parts.Add(Audio.Count == 0 ? "no audio" : $"audio: {string.Join("; ", Audio)}");
        parts.Add(Duration is { } d ? Invariant($"{Container}, {d:0.00} s") : Container);
        return string.Join("; ", parts);
    }

    /// <summary>The format in ffprobe's JSON (-show_format -show_streams).</summary>
    public static MediaFormat From(string probeJson)
    {
        ArgumentNullException.ThrowIfNull(probeJson);
        using var json = JsonDocument.Parse(probeJson);
        var root = json.RootElement;
        string container = "unknown";
        double? duration = null;
        if (root.TryGetProperty("format", out var format))
        {
            container = Text(format, "format_name") ?? container;
            duration = Number(format, "duration");
        }

        VideoFormat? video = null;
        var audio = new List<AudioFormat>();
        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                string? type = Text(stream, "codec_type");
                if (type == "video" && video is null && !AttachedPicture(stream))
                    video = VideoFormat.From(stream);
                else if (type == "audio")
                    audio.Add(AudioFormat.From(stream, audio.Count + 1));
            }
        }

        return new MediaFormat(container, duration, video, audio);
    }

    // Cover art in an MP4 or MKV is a video stream too; it isn't the picture.
    static bool AttachedPicture(JsonElement stream) =>
        stream.TryGetProperty("disposition", out var disposition)
        && disposition.TryGetProperty("attached_pic", out var attached)
        && attached.ValueKind == JsonValueKind.Number && attached.GetInt32() == 1;

    internal static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    internal static int? Integer(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : null;
    }

    internal static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            ? number
            : null;

    internal static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// A video stream as ffprobe describes it. Values ffprobe doesn't print are null (a file that doesn't tag its colour,
/// say), except the display aspect ratio, which is the frame's own when the stream gives none (square pixels).
/// </summary>
/// <param name="Codec">codec_name (prores, dnxhd, h264, ...).</param>
/// <param name="Profile">profile (HQ, DNXHR SQ, High, ...).</param>
/// <param name="Width">width.</param>
/// <param name="Height">height.</param>
/// <param name="FrameRate">r_frame_rate as ffprobe prints it ("25/1", "30000/1001").</param>
/// <param name="FieldOrder">field_order (progressive, tt, bb, tb, bt).</param>
/// <param name="PixelFormat">pix_fmt (yuv422p10le, yuv420p, ...).</param>
/// <param name="BitDepth">bits_per_raw_sample.</param>
/// <param name="ColourPrimaries">color_primaries (bt709, bt2020, ...).</param>
/// <param name="Transfer">color_transfer (bt709, smpte2084, arib-std-b67, ...).</param>
/// <param name="Matrix">color_space, the matrix (bt709, bt2020nc, ...).</param>
/// <param name="ColourRange">color_range (tv, pc).</param>
/// <param name="DisplayAspect">display_aspect_ratio ("16:9").</param>
public sealed record VideoFormat(
    string Codec,
    string? Profile,
    int Width,
    int Height,
    string? FrameRate,
    string? FieldOrder,
    string? PixelFormat,
    int? BitDepth,
    string? ColourPrimaries,
    string? Transfer,
    string? Matrix,
    string? ColourRange,
    string DisplayAspect)
{
    /// <summary>The frame rate as a number (25, 29.97...); null when ffprobe gives none.</summary>
    public double? Rate => Rational(FrameRate);

    /// <summary>"prores (HQ) 1920x1080 25 fps progressive, yuv422p10le 10-bit, bt709/bt709/bt709 tv, 16:9".</summary>
    public override string ToString()
    {
        string profile = Profile is null ? "" : $" ({Profile})";
        string rate = Rate is { } r ? MediaFormat.Invariant($" {r:0.###} fps") : "";
        string scan = FieldOrder is null ? "" : $" {FieldOrder}";
        string depth = BitDepth is { } b ? MediaFormat.Invariant($" {b}-bit") : "";
        string colour = $"{ColourPrimaries ?? "?"}/{Transfer ?? "?"}/{Matrix ?? "?"} {ColourRange ?? "?"}";
        return MediaFormat.Invariant($"{Codec}{profile} {Width}x{Height}{rate}{scan}, {PixelFormat ?? "?"}{depth}, {colour}, {DisplayAspect}");
    }

    /// <summary>"25/1", "30000/1001", "25" or "29.97" as a number; null for anything else, or a zero denominator.</summary>
    public static double? Rational(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        string[] parts = text.Split('/');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double over)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double under))
        {
            return under == 0 ? null : over / under;
        }

        return parts.Length == 1 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double rate) ? rate : null;
    }

    internal static VideoFormat From(JsonElement stream)
    {
        int width = MediaFormat.Integer(stream, "width") ?? 0, height = MediaFormat.Integer(stream, "height") ?? 0;
        string? aspect = MediaFormat.Text(stream, "display_aspect_ratio");
        if (aspect is null or "0:1" or "N/A")
            aspect = Reduced(width, height);
        return new VideoFormat(
            MediaFormat.Text(stream, "codec_name") ?? "unknown",
            MediaFormat.Text(stream, "profile"),
            width,
            height,
            MediaFormat.Text(stream, "r_frame_rate"),
            MediaFormat.Text(stream, "field_order"),
            MediaFormat.Text(stream, "pix_fmt"),
            MediaFormat.Integer(stream, "bits_per_raw_sample"),
            MediaFormat.Text(stream, "color_primaries"),
            MediaFormat.Text(stream, "color_transfer"),
            MediaFormat.Text(stream, "color_space"),
            MediaFormat.Text(stream, "color_range"),
            aspect);
    }

    static string Reduced(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return "unknown";
        int a = width, b = height;
        while (b != 0)
            (a, b) = (b, a % b);
        return MediaFormat.Invariant($"{width / a}:{height / a}");
    }
}

/// <summary>An audio stream as ffprobe describes it: track <paramref name="Number"/> (from 1; ffmpeg's 0:a:Number-1).</summary>
/// <param name="Number">Which audio track, from 1.</param>
/// <param name="Codec">codec_name (pcm_s24le, aac, ...).</param>
/// <param name="SampleRate">sample_rate (Hz).</param>
/// <param name="Channels">channels.</param>
/// <param name="Layout">channel_layout (stereo, 5.1(side), ...).</param>
/// <param name="BitDepth">bits_per_raw_sample, else bits_per_sample; null for a codec that has none (AAC).</param>
public sealed record AudioFormat(int Number, string Codec, int? SampleRate, int Channels, string? Layout, int? BitDepth)
{
    /// <summary>"pcm_s24le 48000 Hz 2 ch (stereo) 24-bit".</summary>
    public override string ToString()
    {
        string rate = SampleRate is { } r ? MediaFormat.Invariant($" {r} Hz") : "";
        string layout = Layout is null ? "" : $" ({Layout})";
        string depth = BitDepth is { } b ? MediaFormat.Invariant($" {b}-bit") : "";
        return MediaFormat.Invariant($"{Codec}{rate} {Channels} ch{layout}{depth}");
    }

    internal static AudioFormat From(JsonElement stream, int number)
    {
        int? depth = MediaFormat.Integer(stream, "bits_per_raw_sample") is { } raw and > 0 ? raw
            : MediaFormat.Integer(stream, "bits_per_sample") is { } bits and > 0 ? bits
            : null;
        return new AudioFormat(
            number,
            MediaFormat.Text(stream, "codec_name") ?? "unknown",
            MediaFormat.Integer(stream, "sample_rate"),
            MediaFormat.Integer(stream, "channels") ?? 0,
            MediaFormat.Text(stream, "channel_layout"),
            depth);
    }
}
