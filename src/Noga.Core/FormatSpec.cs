using System.Globalization;

namespace Noga;

/// <summary>
/// The format a delivery spec asks for (docs/decisions/2026-11-03-delivery-format.md): a profile file's optional
/// "format" section. Each value given is compared with ffprobe's (<see cref="MediaFormat"/>) and fails the file when it
/// differs; a value left out isn't checked. The audio values hold for every audio track.
/// </summary>
public sealed record FormatSpec
{
    /// <summary>The demuxer, one of ffprobe's format_name names ("mov" also matches an MP4: one demuxer reads both; "mxf").</summary>
    public string? Container { get; init; }

    /// <summary>codec_name of the video (prores, dnxhd, h264, hevc, ...).</summary>
    public string? VideoCodec { get; init; }

    /// <summary>The video's profile as ffprobe names it (HQ, DNXHR HQ, High, Main 10, ...), ignoring case.</summary>
    public string? VideoProfile { get; init; }

    /// <summary>The frame's width in pixels.</summary>
    public int? Width { get; init; }

    /// <summary>The frame's height in pixels.</summary>
    public int? Height { get; init; }

    /// <summary>The frame rate: "25", "25/1", "30000/1001" or "29.97" (within 0.001 of r_frame_rate).</summary>
    public string? FrameRate { get; init; }

    /// <summary>
    /// "progressive" or "interlaced", from field_order (interlaced: tt, bb, tb or bt). Which field comes first isn't read
    /// from it: FFmpeg sets those letters differently per format (a top-field-first ProRes in MOV is tb, an MPEG-2 in
    /// MXF tt), so <see cref="FieldOrder"/> names ffprobe's value itself.
    /// </summary>
    public string? Scan { get; init; }

    /// <summary>field_order exactly as ffprobe prints it for a good file of the delivery's kind (tt, bb, tb, bt, progressive).</summary>
    public string? FieldOrder { get; init; }

    /// <summary>pix_fmt (yuv422p10le, yuv420p, ...).</summary>
    public string? PixelFormat { get; init; }

    /// <summary>The video's bits per sample (bits_per_raw_sample).</summary>
    public int? BitDepth { get; init; }

    /// <summary>color_primaries (bt709, bt2020, ...).</summary>
    public string? ColourPrimaries { get; init; }

    /// <summary>color_transfer (bt709, smpte2084, arib-std-b67, ...).</summary>
    public string? Transfer { get; init; }

    /// <summary>color_space, the matrix (bt709, bt2020nc, ...).</summary>
    public string? Matrix { get; init; }

    /// <summary>color_range: "tv" (limited) or "pc" (full).</summary>
    public string? ColourRange { get; init; }

    /// <summary>display_aspect_ratio ("16:9", "4:3").</summary>
    public string? DisplayAspect { get; init; }

    /// <summary>How many audio tracks.</summary>
    public int? AudioTracks { get; init; }

    /// <summary>codec_name of every audio track (pcm_s24le, aac, ...).</summary>
    public string? AudioCodec { get; init; }

    /// <summary>Every audio track's sample rate (Hz).</summary>
    public int? SampleRate { get; init; }

    /// <summary>Every audio track's channels.</summary>
    public int? AudioChannels { get; init; }

    /// <summary>Every audio track's bits per sample.</summary>
    public int? AudioBitDepth { get; init; }

    /// <summary>The lowest bit rate every audio track may have (bits per second, ffprobe's bit_rate: 192000 for 192 kb/s).</summary>
    public int? MinAudioBitRate { get; init; }

    /// <summary>
    /// Whether the audio must be at a constant bit rate (true) or not (false): an MP3's frames each at the same bit rate
    /// (<see cref="Mp3Frames.Constant"/>); PCM is constant by nature; any other codec's isn't known, and fails.
    /// </summary>
    public bool? ConstantBitRate { get; init; }

    /// <summary>The shortest the file may be (seconds, format.duration).</summary>
    public double? MinDuration { get; init; }

    /// <summary>The longest the file may be (seconds, format.duration).</summary>
    public double? MaxDuration { get; init; }

    /// <summary>The start timecode ("10:00:00:00"; ";" before the frames for drop-frame).</summary>
    public string? StartTimecode { get; init; }

    /// <summary>Whether the start timecode is drop-frame (";" before the frames) or non-drop.</summary>
    public bool? DropFrame { get; init; }

    /// <summary>The scans <see cref="Scan"/> may name.</summary>
    public static IReadOnlyList<string> Scans { get; } = ["progressive", "interlaced"];

    /// <summary>Whether any video value is given.</summary>
    bool AsksForVideo =>
        (VideoCodec ?? VideoProfile ?? FrameRate ?? Scan ?? FieldOrder ?? PixelFormat ?? ColourPrimaries ?? Transfer ?? Matrix ?? ColourRange ?? DisplayAspect) is not null
        || (Width ?? Height ?? BitDepth) is not null;

    /// <summary>Whether no value is given (nothing to check).</summary>
    public bool IsEmpty => this == new FormatSpec();

    /// <summary>What <see cref="ConstantBitRate"/> is checked against.</summary>
    public const string BitRateModeFilter = "ffprobe -select_streams a:0 -show_packets -show_entries packet=size (one packet a frame, the Xing or Info frame left out: each frame's size its header's bit rate's, exact)";

    /// <summary>
    /// Every value given, judged against <paramref name="format"/>, the start timecode read from the file
    /// (<paramref name="startTimecode"/>, null when it has none) and an MP3's frames (<paramref name="mp3"/>, null when
    /// it isn't one): a pass or a failure each; the audio ones once per track.
    /// </summary>
    public IReadOnlyList<Finding> Judge(MediaFormat format, string? startTimecode, Mp3Frames? mp3 = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        var into = new List<Finding>();
        if (Container is { } container)
        {
            bool match = format.Container.Split(',').Contains(container, StringComparer.OrdinalIgnoreCase);
            Add("format.container", match, $"Container {format.Container}", container, "format_name");
        }

        if (format.Video is not { } video)
        {
            if (AsksForVideo)
            {
                Add("format.video-codec", false, "No video stream", VideoCodec ?? "a video stream", "codec_type=video");
            }
        }
        else
        {
            Text("format.video-codec", "Video codec", video.Codec, VideoCodec, "codec_name");
            Text("format.video-profile", "Video profile", video.Profile, VideoProfile, "profile");
            if (Width is not null || Height is not null)
            {
                string expected = $"{(Width is { } w ? w.ToString(CultureInfo.InvariantCulture) : "any")}x{(Height is { } h ? h.ToString(CultureInfo.InvariantCulture) : "any")}";
                bool match = (Width is null || Width == video.Width) && (Height is null || Height == video.Height);
                Add("format.frame-size", match, Invariant($"Frame size {video.Width}x{video.Height}"), expected, "width, height");
            }

            if (FrameRate is { } rate)
            {
                double? wanted = VideoFormat.Rational(rate);
                bool match = wanted is { } w && video.Rate is { } r && Math.Abs(w - r) < 0.001;
                string measured = video.Rate is { } got ? Invariant($"{got:0.###} fps ({video.FrameRate})") : "not stated";
                Add("format.frame-rate", match, $"Frame rate {measured}", rate, "r_frame_rate (within 0.001)");
            }

            if (Scan is { } scan)
            {
                string order = video.FieldOrder ?? "unknown";
                bool interlaced = order is "tt" or "bb" or "tb" or "bt";
                bool match = scan.ToLowerInvariant() switch
                {
                    "progressive" => order == "progressive",
                    "interlaced" => interlaced,
                    _ => false,
                };
                string said = order == "progressive" ? "progressive" : interlaced ? $"interlaced ({order})" : $"not stated ({order})";
                Add("format.scan", match, $"Scan {said}", scan, "field_order (interlaced: tt, bb, tb or bt)");
            }

            Text("format.field-order", "Field order", video.FieldOrder, FieldOrder, "field_order");

            Text("format.pixel-format", "Pixel format", video.PixelFormat, PixelFormat, "pix_fmt");
            Integer("format.bit-depth", "Video bit depth", video.BitDepth, BitDepth, "bits", "bits_per_raw_sample");
            Text("format.colour-primaries", "Colour primaries", video.ColourPrimaries, ColourPrimaries, "color_primaries");
            Text("format.transfer", "Transfer", video.Transfer, Transfer, "color_transfer");
            Text("format.matrix", "Matrix", video.Matrix, Matrix, "color_space");
            Text("format.colour-range", "Colour range", video.ColourRange, ColourRange, "color_range");
            Text("format.display-aspect", "Display aspect ratio", video.DisplayAspect, DisplayAspect, "display_aspect_ratio");
        }

        if (AudioTracks is { } tracks)
            Add("format.audio-tracks", format.Audio.Count == tracks, Invariant($"{format.Audio.Count} audio track{(format.Audio.Count == 1 ? "" : "s")}"), Invariant($"{tracks}"), "codec_type=audio streams");

        foreach (var audio in format.Audio)
        {
            Text("format.audio-codec", "Audio codec", audio.Codec, AudioCodec, "codec_name", audio.Number);
            Integer("format.sample-rate", "Sample rate", audio.SampleRate, SampleRate, "Hz", "sample_rate", audio.Number);
            Integer("format.audio-channels", "Channels", audio.Channels, AudioChannels, "", "channels", audio.Number);
            Integer("format.audio-bit-depth", "Audio bit depth", audio.BitDepth, AudioBitDepth, "bits", "bits_per_raw_sample, else bits_per_sample", audio.Number);
            if (MinAudioBitRate is { } least)
            {
                string said = audio.BitRate is { } rate ? Invariant($"{rate / 1000.0:0.###} kb/s") : "not stated";
                Add("format.audio-bit-rate", audio.BitRate >= least, $"Audio bit rate {said}", Invariant($"at least {least / 1000.0:0.###} kb/s"), "bit_rate", audio.Number);
            }

            if (ConstantBitRate is { } constant)
                BitRateMode(audio, constant);
        }

        if (MinDuration is not null || MaxDuration is not null)
        {
            double? d = format.Duration;
            bool match = d is { } got && (MinDuration is null || got >= MinDuration - 0.0005) && (MaxDuration is null || got <= MaxDuration + 0.0005);
            string expected = (MinDuration, MaxDuration) switch
            {
                ({ } min, { } max) => Invariant($"{min:0.###} to {max:0.###} s"),
                ({ } min, null) => Invariant($"at least {min:0.###} s"),
                _ => Invariant($"at most {MaxDuration:0.###} s"),
            };
            into.Add(new Finding
            {
                Check = "format.duration",
                Severity = match ? Severity.Pass : Severity.Failure,
                Message = d is { } length ? Invariant($"Duration {length:0.000} s (expected {expected})") : $"Duration not stated (expected {expected})",
                Value = d,
                Unit = "s",
                CheckedAgainst = $"{MediaFormat.CheckedAgainst}: format.duration (exact)",
            });
        }

        if (StartTimecode is { } timecode)
        {
            bool match = startTimecode is not null && string.Equals(startTimecode, timecode, StringComparison.Ordinal);
            Add("format.start-timecode", match, $"Start timecode {startTimecode ?? "none"}", timecode, $"the timecode tag ({TimecodeTag.CheckedAgainst})", raw: true);
        }

        if (DropFrame is { } drop)
        {
            bool? measured = startTimecode is null ? null : Timecode.IsDropFrame(startTimecode);
            string said = measured == true ? "drop-frame" : "non-drop";
            Add("format.drop-frame", measured == drop, startTimecode is null ? "No start timecode" : $"Start timecode {startTimecode}, {said}", drop ? "drop-frame" : "non-drop", $"the timecode tag's \";\" before the frames ({TimecodeTag.CheckedAgainst})", raw: true);
        }

        return into;

        // An MP3's frames say whether its bit rate is constant; PCM's is by nature; any other codec's isn't known here.
        void BitRateMode(AudioFormat audio, bool constant)
        {
            string expected = constant ? "constant" : "variable";
            bool? measured;
            string message;
            if (audio.Number == 1 && mp3 is { Count: > 0 } frames)
            {
                measured = frames.Constant;
                message = frames.Constant
                    ? Invariant($"Constant bit rate: {frames.Count} frames at {frames.Lowest / 1000.0:0.###} kb/s")
                    : frames.BitRates.ContainsKey(0)
                        ? Invariant($"Free-format bit rate in {frames.BitRates[0]} of {frames.Count} frames")
                        : Invariant($"Variable bit rate: {frames.Count} frames from {frames.Lowest / 1000.0:0.###} to {frames.Highest / 1000.0:0.###} kb/s");
            }
            else if (audio.Codec.StartsWith("pcm_", StringComparison.Ordinal))
            {
                measured = true;
                message = $"Constant bit rate: {audio.Codec} (PCM)";
            }
            else
            {
                measured = null;
                message = $"Bit rate mode not known: {audio.Codec} (only an MP3's frames are read)";
            }

            into.Add(new Finding
            {
                Check = "format.bit-rate-mode",
                Severity = measured == constant ? Severity.Pass : Severity.Failure,
                Message = Invariant($"Track {audio.Number}: {message} (expected {expected})"),
                Track = audio.Number,
                CheckedAgainst = BitRateModeFilter,
            });
        }

        void Text(string check, string what, string? measured, string? expected, string field, int? track = null)
        {
            if (expected is null)
                return;
            bool match = measured is not null && string.Equals(measured, expected, StringComparison.OrdinalIgnoreCase);
            Add(check, match, $"{what} {measured ?? "not stated"}", expected, field, track);
        }

        void Integer(string check, string what, int? measured, int? expected, string unit, string field, int? track = null)
        {
            if (expected is not { } e)
                return;
            string suffix = unit.Length == 0 ? "" : $" {unit}";
            string said = measured is { } m ? Invariant($"{m}{suffix}") : "not stated";
            Add(check, measured == e, $"{what} {said}", Invariant($"{e}{suffix}"), field, track);
        }

        void Add(string check, bool match, string message, string expected, string field, int? track = null, bool raw = false)
        {
            string text = $"{message} (expected {expected})";
            into.Add(new Finding
            {
                Check = check,
                Severity = match ? Severity.Pass : Severity.Failure,
                Message = track is { } n ? Invariant($"Track {n}: {text}") : text,
                Track = track,
                CheckedAgainst = raw ? field : track is { } t
                    ? Invariant($"{MediaFormat.CheckedAgainst}: the audio stream ffmpeg's 0:a:{t - 1} reads, {field} (exact)")
                    : $"{MediaFormat.CheckedAgainst}: {field} (exact)",
            });
        }
    }

    static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
