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
    /// shots).
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
        _ => null,
    };
}
