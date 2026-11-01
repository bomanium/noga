using System.Globalization;
using System.Text.Json;

namespace MediaQc;

/// <summary>
/// An audio track as ffprobe describes it: number <paramref name="Number"/> (from 1; ffmpeg's 0:a:Number-1) of
/// <paramref name="Count"/>. Every track is judged (docs/decisions/2026-11-01-every-audio-track.md), each compared with
/// ffmpeg -map 0:a:N (ffmpeg on its own picks the track with the most channels: docs/decisions/2026-11-01-open-films.md).
/// </summary>
/// <param name="Number">Which track, from 1.</param>
/// <param name="Count">How many audio tracks the file has.</param>
/// <param name="Codec">ffprobe's codec_name (aac, pcm_s24le, mp3, ...).</param>
/// <param name="Channels">Its channels.</param>
/// <param name="Layout">ffprobe's channel_layout (stereo, 5.1(side), ...); null when unspecified.</param>
public sealed record AudioTrack(int Number, int Count, string Codec, int Channels, string? Layout)
{
    /// <summary>"track 1 of 2: mp3, 2 channels (stereo)".</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"track {Number} of {Count}: {Codec}, {Channels} channel{(Channels == 1 ? "" : "s")}{(Layout is null ? "" : $" ({Layout})")}");

    /// <summary>The first audio track in ffprobe's JSON (-show_streams); null when there is none.</summary>
    public static AudioTrack? First(string probeJson) => All(probeJson).FirstOrDefault();

    /// <summary>Every audio track in ffprobe's JSON (-show_streams), in order.</summary>
    public static IReadOnlyList<AudioTrack> All(string probeJson)
    {
        ArgumentNullException.ThrowIfNull(probeJson);
        using var json = JsonDocument.Parse(probeJson);
        if (!json.RootElement.TryGetProperty("streams", out var streams))
            return [];
        var audio = streams.EnumerateArray().Where(s => s.TryGetProperty("codec_type", out var type) && type.GetString() == "audio").ToList();
        return [.. audio.Select((track, i) => new AudioTrack(
            i + 1,
            audio.Count,
            track.TryGetProperty("codec_name", out var codec) ? codec.GetString() ?? "unknown" : "unknown",
            track.TryGetProperty("channels", out var channels) ? channels.GetInt32() : 0,
            track.TryGetProperty("channel_layout", out var layout) ? layout.GetString() : null))];
    }
}
