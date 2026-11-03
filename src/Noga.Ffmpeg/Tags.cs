using System.Globalization;
using System.Text.Json;
using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// What a decoder that doesn't read the container's tags needs from them, as ffprobe's JSON gives them (FrameReader's
/// ProbeJson, in-process) for the first video stream: for PictureMeter the range and rotation, for ShotDetector the
/// rest of what its file path would read.
/// </summary>
/// <param name="FullRange">color_range pc, or a yuvj pixel format.</param>
/// <param name="Rotation">The display rotation, degrees counterclockwise.</param>
/// <param name="AverageFrameRate">avg_frame_rate.</param>
/// <param name="PixelFormat">pix_fmt.</param>
/// <param name="ColorSpace">color_space; empty when unknown.</param>
/// <param name="ChromaLocation">chroma_location; null when unspecified.</param>
/// <param name="StartPts">start_pts.</param>
/// <param name="FrameCount">nb_frames; null when not given.</param>
/// <param name="Duration">The container's duration; null when not given.</param>
/// <param name="FieldOrder">field_order.</param>
/// <param name="Container">containername.</param>
public sealed record Tags(
    bool FullRange,
    int Rotation,
    Rational AverageFrameRate,
    string PixelFormat,
    string ColorSpace,
    string? ChromaLocation,
    long StartPts,
    long? FrameCount,
    TimeSpan? Duration,
    FieldOrder FieldOrder,
    string Container)
{
    /// <summary>Reads <paramref name="path"/>'s tags.</summary>
    /// <exception cref="InvalidDataException">The file has no video stream.</exception>
    public static Tags Read(string path, CancellationToken cancellationToken = default)
    {
        Libraries.Load();
        using var json = JsonDocument.Parse(MediaProbe.ProbeJson(path, new ProbeOptions { CancellationToken = cancellationToken }));
        var root = json.RootElement;
        foreach (var stream in root.GetProperty("streams").EnumerateArray())
        {
            if (Text(stream, "codec_type") != "video")
                continue;
            string format = Text(stream, "pix_fmt") ?? "yuv420p";
            int rotation = 0;
            if (stream.TryGetProperty("side_data_list", out var sideData))
            {
                foreach (var item in sideData.EnumerateArray())
                {
                    if (item.TryGetProperty("rotation", out var degrees) && degrees.TryGetDouble(out double value))
                        rotation = (int)Math.Round(value);
                }
            }

            string[] rate = (Text(stream, "avg_frame_rate") ?? "0/1").Split('/');
            var container = root.GetProperty("format");
            return new Tags(
                Text(stream, "color_range") == "pc" || format.StartsWith("yuvj", StringComparison.Ordinal),
                rotation,
                new Rational(int.Parse(rate[0], CultureInfo.InvariantCulture), int.Parse(rate[1], CultureInfo.InvariantCulture)),
                format,
                Text(stream, "color_space") ?? "",
                Text(stream, "chroma_location"),
                stream.TryGetProperty("start_pts", out var start) ? start.GetInt64() : 0,
                Text(stream, "nb_frames") is { } frames ? long.Parse(frames, CultureInfo.InvariantCulture) : null,
                Text(container, "duration") is { } seconds ? TimeSpan.FromSeconds(double.Parse(seconds, CultureInfo.InvariantCulture)) : null,
                Text(stream, "field_order") switch
                {
                    "progressive" => FieldOrder.Progressive,
                    "tt" => FieldOrder.TopFirst,
                    "bb" => FieldOrder.BottomFirst,
                    "tb" => FieldOrder.TopCodedBottomFirst,
                    "bt" => FieldOrder.BottomCodedTopFirst,
                    _ => FieldOrder.Unknown,
                },
                Text(container, "containername") ?? "");
        }

        throw new InvalidDataException($"{path} has no video stream.");
    }

    /// <summary>
    /// How many audio tracks <paramref name="path"/> has (its headers, through FFmpeg's libraries); 1 when the libraries
    /// don't load or the file can't be opened, so the caller's own reading decides.
    /// </summary>
    public static int AudioTrackCount(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            Libraries.Load();
            return MediaProbe.Probe(path, new ProbeOptions { CancellationToken = cancellationToken }).Streams.Count(s => s.Type == StreamType.Audio);
        }
        catch (FrameReaderException)
        {
            return 1;
        }
    }

    static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value.ToString() : null;
}
