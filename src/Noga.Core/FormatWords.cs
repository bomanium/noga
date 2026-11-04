using System.Globalization;

namespace Noga;

/// <summary>
/// A file's format in words a client reads, for the HTML report's header: "Apple ProRes 422 HQ, 1920×1080, 25 fps
/// progressive, 4:2:2 10-bit, BT.709, limited range, 16:9" rather than ffprobe's "prores (HQ) ... yuv422p10le ... tv"
/// (docs/decisions/2026-11-04-format-words.md). ffprobe's exact values stay in the format finding and the JSON report.
/// </summary>
public static class FormatWords
{
    /// <summary>
    /// The header's rows, label and text: Video, Audio (or Audio 1, Audio 2, ...), Container and Duration;
    /// <paramref name="file"/>'s extension tells MOV from MP4, which ffprobe names alike.
    /// </summary>
    public static IReadOnlyList<(string Label, string Text)> Rows(MediaFormat format, string file)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(file);
        var rows = new List<(string, string)>();
        if (format.Video is { } video)
            rows.Add(("Video", Video(video)));
        if (format.Audio.Count == 0)
            rows.Add(("Audio", "none"));
        foreach (var audio in format.Audio)
            rows.Add((format.Audio.Count == 1 ? "Audio" : $"Audio {audio.Number}", Audio(audio)));
        rows.Add(("Container", Container(format.Container, file)));
        if (format.Duration is { } duration)
            rows.Add(("Duration", Duration(duration, format.Video?.Rate)));
        return rows;
    }

    /// <summary>"Apple ProRes 422 HQ, 1920×1080, 25 fps progressive, 4:2:2 10-bit, BT.709, limited range, 16:9".</summary>
    public static string Video(VideoFormat video)
    {
        ArgumentNullException.ThrowIfNull(video);
        var parts = new List<string> { Codec(video.Codec, video.Profile), Invariant($"{video.Width}×{video.Height}") };
        string scan = video.FieldOrder switch
        {
            "progressive" => "progressive",
            "tt" or "tb" => "interlaced, top field first",
            "bb" or "bt" => "interlaced, bottom field first",
            _ => "",
        };
        string rate = video.Rate is { } r ? Invariant($"{r:0.###} fps") : "";
        if ($"{rate} {scan}".Trim() is { Length: > 0 } timing)
            parts.Add(timing);
        if (Sampling(video.PixelFormat, video.BitDepth) is { } sampling)
            parts.Add(sampling);
        parts.Add(Colour(video));
        parts.Add(video.DisplayAspect);
        return string.Join(", ", parts);
    }

    /// <summary>"PCM 24-bit, 48 kHz, stereo"; "AAC, 48 kHz, 5.1".</summary>
    public static string Audio(AudioFormat audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        string codec = audio.Codec switch
        {
            var c when c.StartsWith("pcm_", StringComparison.Ordinal) => audio.BitDepth is { } bits ? Invariant($"PCM {bits}-bit") : "PCM",
            "aac" => "AAC",
            "mp3" => "MP3",
            "ac3" => "Dolby Digital (AC-3)",
            "eac3" => "Dolby Digital Plus (E-AC-3)",
            "opus" => "Opus",
            "flac" => "FLAC",
            var c => c,
        };
        var parts = new List<string> { codec };
        if (audio.SampleRate is { } rate)
            parts.Add(rate % 1000 == 0 ? Invariant($"{rate / 1000} kHz") : Invariant($"{rate / 1000.0:0.#} kHz"));
        parts.Add(audio.Layout switch
        {
            "mono" => "mono",
            "stereo" => "stereo",
            { } layout => layout.Replace("(side)", "", StringComparison.Ordinal),
            null => audio.Channels == 1 ? "1 channel" : Invariant($"{audio.Channels} channels"),
        });
        return string.Join(", ", parts);
    }

    // The codec and its profile by name: "Apple ProRes 422 HQ", "Avid DNxHR SQ", "H.264 High".
    static string Codec(string codec, string? profile) => (codec, profile) switch
    {
        ("prores", "HQ") => "Apple ProRes 422 HQ",
        ("prores", "LT") => "Apple ProRes 422 LT",
        ("prores", "Proxy") => "Apple ProRes 422 Proxy",
        ("prores", "Standard") => "Apple ProRes 422",
        ("prores", "4444") => "Apple ProRes 4444",
        ("prores", "4444XQ") => "Apple ProRes 4444 XQ",
        ("prores", _) => "Apple ProRes",
        ("dnxhd", { } p) when p.StartsWith("DNXHR ", StringComparison.Ordinal) => $"Avid DNxHR {p["DNXHR ".Length..]}",
        ("dnxhd", _) => "Avid DNxHD",
        ("h264", { } p) => $"H.264 {p}",
        ("h264", null) => "H.264",
        ("hevc", { } p) => $"H.265 (HEVC) {p}",
        ("hevc", null) => "H.265 (HEVC)",
        ("mpeg2video", _) => "MPEG-2",
        ("ffv1", _) => "FFV1",
        ("jpeg2000", _) => "JPEG 2000",
        ("av1", _) => "AV1",
        ("vp9", _) => "VP9",
        (_, { } p) => $"{codec} {p}",
        _ => codec,
    };

    // The chroma sampling and the depth from ffprobe's pix_fmt: "4:2:2 10-bit".
    static string? Sampling(string? pixelFormat, int? bitDepth)
    {
        if (pixelFormat is null)
            return bitDepth is { } bits ? Invariant($"{bits}-bit") : null;
        string chroma = pixelFormat switch
        {
            var p when p.StartsWith("yuv420", StringComparison.Ordinal) || p.StartsWith("yuvj420", StringComparison.Ordinal) => "4:2:0",
            var p when p.StartsWith("yuv422", StringComparison.Ordinal) || p.StartsWith("yuvj422", StringComparison.Ordinal) => "4:2:2",
            var p when p.StartsWith("yuv444", StringComparison.Ordinal) || p.StartsWith("yuvj444", StringComparison.Ordinal) => "4:4:4",
            var p when p.StartsWith("yuva444", StringComparison.Ordinal) => "4:4:4 with alpha",
            var p when p.StartsWith("gray", StringComparison.Ordinal) => "greyscale",
            var p when p.Contains("rgb", StringComparison.Ordinal) || p.StartsWith("gbr", StringComparison.Ordinal) => "RGB",
            var p => p,
        };
        int? depth = bitDepth ?? (pixelFormat.Contains("p10", StringComparison.Ordinal) ? 10 : pixelFormat.Contains("p12", StringComparison.Ordinal) ? 12 : null);
        return depth is { } d ? Invariant($"{chroma} {d}-bit") : chroma;
    }

    // "BT.709, limited range" when primaries, transfer and matrix agree; each named when they don't ("BT.2020
    // primaries, PQ transfer, BT.2020 matrix, limited range"); the untagged said so.
    static string Colour(VideoFormat video)
    {
        string? primaries = Name(Tagged(video.ColourPrimaries)), transfer = Name(Tagged(video.Transfer)), matrix = Name(Tagged(video.Matrix));
        string range = Tagged(video.ColourRange) switch
        {
            "tv" => "limited range",
            "pc" => "full range",
            null => "range not tagged",
            var r => r,
        };
        if (primaries is null && transfer is null && matrix is null)
            return $"colour not tagged, {range}";
        if (primaries is not null && primaries == transfer && transfer == matrix)
            return $"{matrix}, {range}";
        (string Name, string? Value)[] tags = [("primaries", primaries), ("transfer", transfer), ("matrix", matrix)];
        var parts = tags.Where(t => t.Value is not null).Select(t => $"{t.Value} {t.Name}").ToList();
        var missing = tags.Where(t => t.Value is null).Select(t => t.Name).ToList();
        if (missing.Count > 0)
            parts.Add($"{string.Join(" and ", missing)} not tagged");
        parts.Add(range);
        return string.Join(", ", parts);
    }

    // ffprobe's colour names as their standards: bt709 BT.709, bt2020nc BT.2020, smpte2084 PQ, arib-std-b67 HLG.
    static string? Name(string? value) => value switch
    {
        null => null,
        "bt709" => "BT.709",
        "bt2020" or "bt2020nc" or "bt2020c" or "bt2020-10" or "bt2020-12" => "BT.2020",
        "smpte2084" => "PQ",
        "arib-std-b67" => "HLG",
        "bt470bg" or "smpte170m" or "bt601" => "BT.601",
        "smpte432" => "Display P3",
        var v => v,
    };

    static string? Tagged(string? value) => value is null or "unknown" or "unspecified" or "reserved" ? null : value;

    // The container by name, the file's extension telling MOV from MP4 (ffprobe names both "mov,mp4,m4a,3gp,3g2,mj2").
    static string Container(string container, string file)
    {
        string extension = Path.GetExtension(file).ToLowerInvariant();
        return container switch
        {
            var c when c.StartsWith("mov,", StringComparison.Ordinal) => extension switch
            {
                ".mp4" or ".m4v" => "MPEG-4 (MP4)",
                ".m4a" => "MPEG-4 audio (M4A)",
                _ => "QuickTime (MOV)",
            },
            "mxf" => "MXF",
            var c when c.StartsWith("matroska", StringComparison.Ordinal) => extension == ".webm" ? "WebM" : "Matroska (MKV)",
            "mpegts" => "MPEG transport stream",
            "mpeg" => "MPEG program stream",
            "avi" => "AVI",
            "wav" => "WAV",
            "aiff" => "AIFF",
            var c => c,
        };
    }

    // "00:00:20:00 (20.00 s)" at the video's rate, else seconds alone.
    static string Duration(double seconds, double? rate)
    {
        string plain = Invariant($"{seconds:0.00} s");
        if (rate is not { } fps || fps <= 0)
            return plain;
        int whole = (int)Math.Round(fps);
        long frames = (long)Math.Round(seconds * fps);
        long totalSeconds = frames / whole;
        return Invariant($"{totalSeconds / 3600:00}:{totalSeconds / 60 % 60:00}:{totalSeconds % 60:00}:{frames % whole:00} ({plain})");
    }

    static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
