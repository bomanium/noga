namespace MediaQc;

/// <summary>
/// An item of EBU's QC catalogue (qc.ebu.io, the EBU QC test items broadcasters name their checks by): its ID, name and
/// the version it was mapped against (docs/decisions/2026-11-01-ebu-qc-items.md).
/// </summary>
public sealed record EbuQcItem(string Id, string Name, string Version)
{
    /// <summary>The item's page in the catalogue.</summary>
    public string Url => $"https://qc.ebu.io/items/{Id}";

    /// <summary>
    /// The catalogue item a check is, read from qc.ebu.io's API on 2026-11-01; null for checks it has none for: the
    /// bitrate measured from the packets (0158W reads the wrapper's), the shot list (0128B finds short shots, not
    /// shots), the format's summary, the container, the transfer, the matrix and the audio track count.
    /// </summary>
    public static EbuQcItem? For(string check) => check switch
    {
        "loudness.integrated" or "loudness.range" or "loudness.from" => new("0010B", "Loudness", "2.0"),
        "loudness.true-peak" => new("0084B", "Audio Peaks (TP)", "1.0"),
        "silence" => new("0078B", "Audio Silence", "3.0"),
        "tone" => new("0014B", "Audio Test Tones", "2.0"),
        "bars" => new("0052B", "Video Test Pattern Expected", "3.0"),
        "black" => new("0272B", "Black Frames Report", "1.0"),
        "range" => new("0051B", "Video Signal Levels", "2.0"),
        "crop" => new("0015B", "Black Bars", "3.0"),
        "frames" or "keyframes" => new("0046F", "GOP Length - Structure", "1.0"),
        "timecode" => new("0026W", "Timecode", "3.0"),
        "audio" => new("0004F", "Audio Channel Count", "2.0"),
        "channel.dead" or "channel.silence" => new("0078B", "Audio Silence", "3.0"),
        "channel.phase" => new("0012B", "Audio Phase Error", "2.0"),
        "channel.clipping" => new("0005B", "Audio Digital Clipping", "2.0"),
        "channel.dual-mono" => new("0124B", "Mono Audio", "2.0"),

        // The delivery format (docs/decisions/2026-11-03-delivery-format.md), read from the API on 2026-11-03: the
        // essence's (F) items, a wrapper's (W) where the catalogue has no other.
        "format.video-codec" => new("0105F", "Video Codec", "1.0"),
        "format.video-profile" => new("0035F", "Video Profile & Level", "1.0"),
        "format.frame-size" => new("0163W", "Sampled Frame Size", "2.0"),
        "format.frame-rate" => new("0042F", "Frame Rate", "2.0"),
        "format.scan" => new("0048F", "Video Scanning", "1.0"),
        "format.field-order" => new("0041F", "Field Order", "1.0"),
        "format.pixel-format" => new("0034F", "Chroma Sub-sampling", "1.0"),
        "format.bit-depth" => new("0032F", "Video Bit Depth", "1.0"),
        "format.colour-primaries" => new("0083F", "Colour Primaries", "1.0"),
        "format.colour-range" => new("0221W", "Colour Range", "1.0"),
        "format.display-aspect" => new("0069F", "Display Aspect Ratio", "2.0"),
        "format.audio-codec" => new("0104F", "Audio Codec", "1.0"),
        "format.sample-rate" => new("0013F", "Audio Sample Rate", "2.0"),
        "format.audio-channels" => new("0004F", "Audio Channel Count", "2.0"),
        "format.audio-bit-depth" => new("0003F", "Audio Bit Depth", "3.0"),
        "format.duration" => new("0213W", "Container Duration", "1.0"),
        "format.start-timecode" => new("0026W", "Timecode", "3.0"),
        _ => null,
    };
}
