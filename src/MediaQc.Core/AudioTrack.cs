using System.Globalization;
using System.Text.Json;

namespace MediaQc;

/// <summary>
/// The audio track the report judges: the file's first (ffmpeg's 0:a:0), of <paramref name="Count"/>, as ffprobe
/// describes it (docs/decisions/2026-11-01-open-films.md: ffmpeg on its own picks the track with the most channels,
/// so a file with several gets compared with -map 0:a:0).
/// </summary>
/// <param name="Count">How many audio tracks the file has.</param>
/// <param name="Codec">ffprobe's codec_name (aac, pcm_s24le, mp3, ...).</param>
/// <param name="Channels">Its channels.</param>
/// <param name="Layout">ffprobe's channel_layout (stereo, 5.1(side), ...); null when unspecified.</param>
public sealed record AudioTrack(int Count, string Codec, int Channels, string? Layout)
{
    /// <summary>"track 1 of 2: mp3, 2 channels (stereo)".</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"track 1 of {Count}: {Codec}, {Channels} channel{(Channels == 1 ? "" : "s")}{(Layout is null ? "" : $" ({Layout})")}");

    /// <summary>The first audio track in ffprobe's JSON (-show_streams); null when there is none.</summary>
    public static AudioTrack? First(string probeJson)
    {
        ArgumentNullException.ThrowIfNull(probeJson);
        using var json = JsonDocument.Parse(probeJson);
        if (!json.RootElement.TryGetProperty("streams", out var streams))
            return null;
        var audio = streams.EnumerateArray().Where(s => s.TryGetProperty("codec_type", out var type) && type.GetString() == "audio").ToList();
        if (audio.Count == 0)
            return null;
        var first = audio[0];
        return new AudioTrack(
            audio.Count,
            first.TryGetProperty("codec_name", out var codec) ? codec.GetString() ?? "unknown" : "unknown",
            first.TryGetProperty("channels", out var channels) ? channels.GetInt32() : 0,
            first.TryGetProperty("channel_layout", out var layout) ? layout.GetString() : null);
    }
}
