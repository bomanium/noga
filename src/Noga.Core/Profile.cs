using System.Globalization;

namespace Noga;

/// <summary>
/// A check profile: the thresholds a file is held to, and the measurement settings the measures are taken with. The
/// first one, <see cref="EbuR128"/>, is docs/decisions/2026-10-31-v1-thresholds.md.
/// </summary>
public sealed record Profile
{
    /// <summary>EBU R128, strict: -23 LUFS within ±0.5 LU (warning to ±1 LU), true peak at most -1 dBTP; black and silences from 2 s.</summary>
    public static Profile EbuR128 { get; } = new()
    {
        Name = "ebu-r128",
        TargetLoudness = -23,
        LoudnessPass = 0.5,
        LoudnessWarning = 1,
        MaxTruePeak = -1,
        MinBlack = 2,
        BlackPixelThreshold = 0.10,
        BlackPictureThreshold = 0.98,
        MinSilence = 2,
        SilenceNoise = -60,
        MaxOutOfRange = 0.01,
        RangeMergeGap = 1,
        MinPhase = -0.3,
        ClipRun = 3,
        FreezeNoise = 0.001,
        MinFreeze = 2,
        RepeatNoise = 0.0001,
        RepeatCadence = 0.1,
        ColourSpread = 4,
        MaxOutOfGamut = 0.01,
        GamutLow = -0.05,
        GamutHigh = 1.05,
        MinConstantColour = 2,
        ToneLevel = -18,
    };

    /// <summary>The profile's name in reports and on the command line.</summary>
    public required string Name { get; init; }

    /// <summary>The integrated loudness aimed at (LUFS).</summary>
    public required double TargetLoudness { get; init; }

    /// <summary>How far from the target still passes (LU).</summary>
    public required double LoudnessPass { get; init; }

    /// <summary>How far from the target is a warning rather than a failure (LU).</summary>
    public required double LoudnessWarning { get; init; }

    /// <summary>The highest true peak that passes (dBTP).</summary>
    public required double MaxTruePeak { get; init; }

    /// <summary>The shortest black span reported (seconds; blackdetect's d).</summary>
    public required double MinBlack { get; init; }

    /// <summary>blackdetect's pix_th: how dark a pixel is black, as a fraction of the luma range.</summary>
    public required double BlackPixelThreshold { get; init; }

    /// <summary>blackdetect's pic_th: the share of black pixels that makes a picture black.</summary>
    public required double BlackPictureThreshold { get; init; }

    /// <summary>The shortest silence reported (seconds; silencedetect's d).</summary>
    public required double MinSilence { get; init; }

    /// <summary>The level below which audio is silent (dBFS; silencedetect's n).</summary>
    public required double SilenceNoise { get; init; }

    /// <summary>
    /// The largest share of a frame's pixels outside broadcast range that passes (0 to 1): EBU R 103's 1%
    /// (docs/decisions/2026-11-01-range-and-crop.md).
    /// </summary>
    public required double MaxOutOfRange { get; init; }

    /// <summary>
    /// Spans of pixels outside broadcast range less than this many seconds apart are one warning
    /// (docs/decisions/2026-11-01-open-films.md); 0 keeps every span.
    /// </summary>
    public double RangeMergeGap { get; init; }

    /// <summary>
    /// The lowest mean phase correlation of a track's first two channels that passes (1 in phase, 0 unrelated, -1
    /// inverted): below it the pair is out of phase, a warning (docs/decisions/2026-11-03-channel-faults.md).
    /// </summary>
    public double MinPhase { get; init; } = -0.3;

    /// <summary>A run of this many samples or more at full scale on a channel is clipping, a warning.</summary>
    public int ClipRun { get; init; } = 3;

    /// <summary>freezedetect's n: how little a frame may differ from the one before it was frozen (mean absolute difference, 0 to 1).</summary>
    public double FreezeNoise { get; init; } = 0.001;

    /// <summary>The shortest freeze reported (seconds; freezedetect's d).</summary>
    public double MinFreeze { get; init; } = 2;

    /// <summary>
    /// How little a frame may differ from the one before to be a repeat of it (freezedetect's n; -80 dB): tighter than
    /// <see cref="FreezeNoise"/> (docs/decisions/2026-11-06-repeated-frames.md).
    /// </summary>
    public double RepeatNoise { get; init; } = 0.0001;

    /// <summary>The share of the moving picture's frames above which repeats are a cadence (pulldown, animation), summed up.</summary>
    public double RepeatCadence { get; init; } = 0.1;

    /// <summary>Whether a repeated frame is a warning rather than information.</summary>
    public bool RepeatsWarn { get; init; }

    /// <summary>
    /// How flat a frame of one colour is: each of Y, U and V's signalstats HIGH less LOW (90th less 10th percentile) at
    /// most this many 8-bit codes (scaled to the depth), and the run's averages within it
    /// (docs/decisions/2026-11-06-constant-colour.md).
    /// </summary>
    public double ColourSpread { get; init; } = 4;

    /// <summary>The shortest run of one flat colour reported (seconds), black and bars left out.</summary>
    public double MinConstantColour { get; init; } = 2;

    /// <summary>
    /// The share of a frame's pixels (0 to 1) that may be outside RGB gamut: EBU R 103's 1% of the picture
    /// (docs/decisions/2026-11-06-rgb-gamut.md).
    /// </summary>
    public double MaxOutOfGamut { get; init; } = 0.01;

    /// <summary>The lowest R, G or B in gamut, black 0 and white 1 (R 103's -5%).</summary>
    public double GamutLow { get; init; } = -0.05;

    /// <summary>The highest R, G or B in gamut (R 103's 105%).</summary>
    public double GamutHigh { get; init; } = 1.05;

    /// <summary>
    /// The line-up tone's level (dBFS, a sine's peak as AES17 has it): EBU R 68's -18 for ebu-r128; null, not judged
    /// (docs/decisions/2026-11-03-line-up-and-head-tail.md). A tone further from it than <see cref="ToneTolerance"/> warns.
    /// </summary>
    public double? ToneLevel { get; init; }

    /// <summary>How far from <see cref="ToneLevel"/> the tone may be (dB).</summary>
    public double ToneTolerance { get; init; } = 0.5;

    /// <summary>The bars the head must start with (seconds at least); null: not required. Short or missing fails.</summary>
    public double? HeadBars { get; init; }

    /// <summary>The line-up tone the head must start with (seconds at least); null: not required.</summary>
    public double? HeadTone { get; init; }

    /// <summary>The black, with silence, before the programme (seconds at least: after the head's bars, or from the start); null: not required.</summary>
    public double? HeadBlack { get; init; }

    /// <summary>The black, with silence, the file must end with (seconds at least); null: not required.</summary>
    public double? TailBlack { get; init; }

    /// <summary>
    /// How many frames the sound's length or start may differ from the picture's before it warns (EBU QC 0082W's
    /// default tolerance, one frame; docs/decisions/2026-11-05-av-lengths.md).
    /// </summary>
    public int SyncFrames { get; init; } = 1;

    /// <summary>How far (seconds) the sound's length or start may differ from the picture's before it fails.</summary>
    public double SyncFailure { get; init; } = 1;

    /// <summary>
    /// How many frames may be missing from the picture (gaps in its timestamps) before it fails; 0, any gap fails
    /// (docs/decisions/2026-11-05-dropped-frames.md).
    /// </summary>
    public int MaxDroppedFrames { get; init; }

    /// <summary>
    /// The loudest momentary loudness allowed (LUFS, ebur128's M); null: reported, not judged. Louder fails
    /// (docs/decisions/2026-11-05-short-term-loudness.md).
    /// </summary>
    public double? MaxMomentary { get; init; }

    /// <summary>The loudest short-term loudness allowed (LUFS, ebur128's S); null: reported, not judged. EBU R 128 s1's -18 for ebu-r128-s1.</summary>
    public double? MaxShortTerm { get; init; }

    /// <summary>The loudness range's least (LU); null: no least. Outside the window fails.</summary>
    public double? MinRange { get; init; }

    /// <summary>The loudness range's most (LU); null: no most.</summary>
    public double? MaxRange { get; init; }

    /// <summary>What the sound's length is checked against, as the picture's.</summary>
    public static string AvLengthFilter => "ffprobe -select_streams v:0 -count_frames (nb_read_frames, and the frames missing in its gaps, times the frame's duration) against -map 0:a:N -af astats's Number of samples (within one AAC frame, 1024 samples)";

    /// <summary>What the gaps in the picture's timestamps are checked against.</summary>
    public static string DroppedFilter => "ffprobe -select_streams v:0 -show_packets -show_entries packet=pts,dts (each packet's, exact): a step over 1.5 frames is a gap of its frames less one";

    /// <summary>What a variable frame rate is checked against.</summary>
    public static string VariableRateFilter => "ffprobe -select_streams v:0 -show_packets -show_entries packet=pts,dts (each packet's, exact): over 1% of the steps (and more than one) not one frame, within half a frame, at r_frame_rate";

    /// <summary>What the sound's start is checked against, as the picture's.</summary>
    public static string AvStartFilter => "ffprobe -show_entries stream=start_time (each stream's, exact)";

    /// <summary>What the picture's interlacing is checked against.</summary>
    public static string InterlaceFilter => "idet with its defaults (intl_thres=1.04, prog_thres=1.5): its Single and Multi frame detection totals (exact)";

    /// <summary>What the pixels outside RGB gamut are checked against.</summary>
    public string GamutFilter => Invariant($"geq per pixel: R, G and B from Y, Cb and Cr (the samples it is sited on) by the matrix tag's Kr and Kb (else BT.709, BT.601 under 720 lines), each within {GamutLow * 100:0.#}% to {GamutHigh * 100:0.#}%; signalstats YAVG of that mask of 0 and the depth's maximum (each frame's share exact), colour bars left out, spans under {RangeMergeGap:0.#} s apart joined");

    /// <summary>What constant-colour frames are checked against.</summary>
    public string ColourFilter => Invariant($"signalstats per frame: YHIGH-YLOW, UHIGH-ULOW and VHIGH-VLOW at most {ColourSpread} 8-bit codes, YAVG, UAVG and VAVG within that of the run's first frame (exact; each end exact), runs of {MinConstantColour} s or more, black and bars left out");

    /// <summary>What repeated frames are checked against.</summary>
    public string RepeatFilter => Invariant($"freezedetect=n={RepeatNoise}:d=1.5 frames' seconds, its freezes of two frames exactly (one repeated frame), each within half a frame");

    /// <summary>What freezes are checked against.</summary>
    public string FreezeFilter => Invariant($"freezedetect=n={FreezeNoise}:d={MinFreeze} (each end within one frame), black and bars left out");

    /// <summary>What a channel's silences are checked against (channel N, from 1).</summary>
    public string ChannelSilenceFilter(int channel) => Invariant($"-map 0:a:0 -af pan=mono|c0=c{channel - 1},silencedetect=n={SilenceNoise}dB:d={MinSilence} (each end within 10 ms)");

    /// <summary>What the phase is checked against.</summary>
    public static string PhaseFilter => "-map 0:a:0 -af pan=stereo|c0=c0|c1=c1,asetnsamples=n=4800,aphasemeter=video=0 (the mean of lavfi.aphasemeter.phase, within 0.01)";

    /// <summary>What clipping is checked against.</summary>
    public string ClippingFilter => Invariant($"-map 0:a:0 -af astats per channel: Peak count, its samples at full scale when it reaches it up and down (exact; runs of {ClipRun} or more of them are clipping)");

    /// <summary>What dual mono is checked against.</summary>
    public static string DualMonoFilter => "-map 0:a:0 -af astats per channel (RMS level within 0.1 dB) and aphasemeter (mean phase above 0.999)";

    /// <summary>
    /// The format the delivery asks for (a profile file's "format": docs/decisions/2026-11-03-delivery-format.md); null:
    /// the format is reported, not judged.
    /// </summary>
    public FormatSpec? Format { get; init; }

    /// <summary>What pixels outside broadcast range are checked against.</summary>
    public string RangeFilter => Invariant($"signalstats BRNG on each frame (luma 16-235, chroma 16-240, scaled to the depth; exact), colour bars left out, spans under {RangeMergeGap:0.#} s apart joined");

    /// <summary>What the active picture is checked against.</summary>
    public static string CropFilter => "cropdetect=limit=24/255:round=16:skip=2:reset=0 (exact)";

    /// <summary>The FFmpeg filter the loudness figures are checked against (exact: FrameReader runs ebur128 itself on the FFmpeg path).</summary>
    public static string LoudnessFilter => "-map 0:a:0 -af ebur128=peak=true (integrated and range within 0.1 LU, true peak within 0.1 dB)";

    /// <summary>What the loudest momentary loudness is checked against.</summary>
    public static string MomentaryFilter => "-map 0:a:0 -af ebur128=framelog=verbose: the highest M of its 100 ms readings (within 0.1 LU)";

    /// <summary>What the loudest short-term loudness is checked against.</summary>
    public static string ShortTermFilter => "-map 0:a:0 -af ebur128=framelog=verbose: the highest S of its 100 ms readings (within 0.1 LU)";

    /// <summary>The FFmpeg filter black spans are checked against.</summary>
    public string BlackFilter => Invariant($"blackdetect=d={MinBlack}:pix_th={BlackPixelThreshold:0.00}:pic_th={BlackPictureThreshold:0.00} (each end within one frame)");

    /// <summary>The FFmpeg filter silences are checked against.</summary>
    public string SilenceFilter => Invariant($"-map 0:a:0 -af silencedetect=n={SilenceNoise}dB:d={MinSilence} (each end within 10 ms)");

    /// <summary>What bars are checked against (signalstats on each bar's patch).</summary>
    public static string BarsFilter => "signalstats on each bar of smptebars/smptehdbars (each end within one frame)";

    /// <summary>What line-up tone is checked against.</summary>
    public static string ToneFilter => "-map 0:a:0, astats per channel (RMS level within 0.1 dB) and a 1 kHz bandpass (each end within 20 ms)";

    /// <summary>What picture types are checked against.</summary>
    public static string PictureTypesFilter => "ffprobe -select_streams v:0 -show_frames -show_entries frame=pict_type (each frame's, exact)";

    /// <summary>What the keyframes are checked against.</summary>
    public static string KeyframesFilter => "ffprobe -select_streams v:0 -show_packets -show_entries packet=pts_time,flags (the K packets, exact)";

    /// <summary>What the bitrate is checked against.</summary>
    public static string BitrateFilter => "ffprobe -select_streams v:0 -show_packets -show_entries packet=pts_time,size (summed per second, exact)";

    /// <summary>What the frames decoded are checked against: the frames the container lists, the packets read and the frames decoded.</summary>
    public static string DecodeFilter => "ffprobe -select_streams v:0 -count_packets -count_frames -show_entries stream=nb_frames,nb_read_packets,nb_read_frames (exact; an MXF's listed frames its duration times r_frame_rate)";

    /// <summary>What the sound decoded is checked against: the stream's duration and the samples ffmpeg decodes.</summary>
    public static string SoundDecodeFilter => "ffprobe -show_entries stream=duration, against -map 0:a:N -af astats's Number of samples (within one AAC frame, 1024 samples)";

    /// <summary>What a start timecode flagged drop-frame at a rate without it is checked against.</summary>
    public static string TimecodeDropFrameFilter => "ffprobe, which gives no timecode tag for it and warns \"Drop frame is only allowed with multiples of 30000/1001 FPS\"; the flag and start read from the MOV/MP4 tmcd track or the MXF timecode component";

    /// <summary>What a WAV, RF64, BW64 or AIFF header's length is checked against.</summary>
    public static string SoundHeaderFilter => "the header's length (WAV data size, RF64/BW64 ds64 data size, AIFF COMM frames: ffprobe -show_entries stream=duration on files ffmpeg wrote whole, and astats' Number of samples, exactly), against -map 0:a:0 -af astats's Number of samples";

    /// <summary>What the shot list is checked against: not FFmpeg, which has no detector that gives these shots.</summary>
    public static string ShotsFilter => "PySceneDetect detect-adaptive with its defaults, through ShotDetector (which matches it exactly in its own CI)";

    /// <summary>Judges <paramref name="measured"/> by this profile.</summary>
    public QcReport Evaluate(Measurements measured)
    {
        ArgumentNullException.ThrowIfNull(measured);
        var timecode = new Timecode(measured.RateNumerator, measured.RateDenominator, measured.StartTimecode);
        double frame = (double)measured.RateDenominator / measured.RateNumerator;
        Position At(double seconds) => new(seconds, timecode.At(seconds));

        var findings = new List<Finding>();
        foreach (string check in measured.NotMeasured)
        {
            findings.Add(new Finding
            {
                Check = check,
                Severity = Severity.Info,
                Message = check switch
                {
                    "timecode" => "Start timecode not read (FFmpeg's libraries didn't load): timecodes count from 00:00:00:00",
                    "format" => "Format not read (FFmpeg's libraries didn't load)",
                    _ => Invariant($"Not checked: this version doesn't measure {check} yet"),
                },
                CheckedAgainst = check switch
                {
                    "black" => BlackFilter,
                    "bars" => BarsFilter,
                    "silence" => SilenceFilter,
                    "tone" => ToneFilter,
                    "range" => RangeFilter,
                    "crop" => CropFilter,
                    "frames" => PictureTypesFilter,
                    "keyframes" => KeyframesFilter,
                    "bitrate" => BitrateFilter,
                    "shots" => ShotsFilter,
                    "timecode" => TimecodeTag.CheckedAgainst,
                    "format" => MediaFormat.CheckedAgainst,
                    _ => "nothing yet",
                },
            });
        }

        // A start timecode flagged drop-frame at a rate that has none: counted non-drop (docs/decisions/2026-11-06-drop-frame.md).
        if (measured.StartTimecode is { } start && Timecode.IsDropFrame(start) && !Timecode.HasDropFrame(measured.RateNumerator, measured.RateDenominator))
        {
            findings.Add(new Finding
            {
                Check = "timecode.drop-frame",
                Severity = Severity.Warning,
                Message = Invariant($"Start timecode {start} is drop-frame, which {(double)measured.RateNumerator / measured.RateDenominator:0.###} fps doesn't have: counted non-drop"),
                CheckedAgainst = TimecodeDropFrameFilter,
            });
        }

        if (!measured.HasPicture)
        {
            findings.Add(new Finding
            {
                Check = "picture",
                Severity = Severity.Info,
                Message = "Sound only: the picture's checks (black, bars, constant colour, freezes, interlacing, range, gamut, crop, frames, keyframes, bitrate, shots, the head's bars and black) aren't made; timecodes count at 25 fps",
                CheckedAgainst = "ffprobe -show_streams (no video stream but cover art)",
            });
        }

        FormatFindings(findings, measured);

        foreach (var bars in measured.Bars)
        {
            findings.Add(new Finding
            {
                Check = "bars",
                Severity = Severity.Info,
                Message = Invariant($"Colour bars for {bars.Duration:0.00} s"),
                Value = bars.Duration,
                Unit = "s",
                Start = At(bars.Start),
                End = At(bars.End),
                CheckedAgainst = BarsFilter,
            });
        }

        Tone(findings, measured.Tone);

        Spans(findings, "black", "Black", measured.Black, MinBlack, BlackFilter);
        HeadAndTail(findings, measured, frame, At);
        Interlace(findings, measured);

        // One flat colour other than black, outside the bars (docs/decisions/2026-11-06-constant-colour.md): each part
        // of a run outside them, from MinConstantColour on.
        var blackAndBars = measured.Black.Concat(measured.Bars).OrderBy(s => s.Start).ToList();
        foreach (var run in measured.ConstantColour)
        {
            foreach (var piece in Without(run.Range, blackAndBars))
                Spans(findings, "colour", Invariant($"One colour, {run.Rgb} (Y {run.Y:0}, Cb {run.Cb:0}, Cr {run.Cr:0}),"), [piece], MinConstantColour, ColourFilter);
        }

        // A black picture, bars or one flat colour are still by nature (they are reported as such): a freeze counts
        // outside them.
        var still = blackAndBars.Concat(measured.ConstantColour.Select(c => c.Range)).OrderBy(s => s.Start).ToList();
        Spans(findings, "freeze", "Picture frozen", [.. measured.Freezes.SelectMany(f => Without(f, still))], MinFreeze, FreezeFilter);
        if (measured.HasPicture && !measured.NotMeasured.Contains("repeat"))
            Repeats(findings, measured, still, frame, At);
        Range(findings, measured, At);
        Gamut(findings, measured, At);
        Crop(findings, measured.Crop);
        PictureTypes(findings, measured.PictureTypes);
        Decode(findings, measured, frame, At);
        long dropped = Dropped(findings, measured, frame, At);
        AudioVideo(findings, measured, frame, dropped, At);
        Keyframes(findings, measured, At);
        Bitrate(findings, measured, At);
        Shots(findings, measured.Shots, At);
        Spans(findings, "silence", "Silence", measured.Silences, MinSilence, SilenceFilter);
        Channels(findings, measured.Channels, measured.Silences, At);
        From(findings, measured.LoudnessFrom, measured.Loudness);

        if (measured.OtherTracks.Count > 0)
        {
            foreach (var each in measured.AudioTracks)
            {
                findings.Add(new Finding
                {
                    Check = "audio",
                    Severity = Severity.Info,
                    Message = Invariant($"Audio {each}"),
                    Track = each.Number,
                    CheckedAgainst = Invariant($"ffprobe -show_streams (the audio stream ffmpeg's 0:a:{each.Number - 1} reads)"),
                });
            }
        }
        else if (measured.AudioTrack is { } track)
        {
            findings.Add(new Finding
            {
                Check = "audio",
                Severity = Severity.Info,
                Message = track.Count == 1
                    ? Invariant($"Audio measured: {track}")
                    : track.Count == 2
                        ? Invariant($"Audio measured: {track}; the other track isn't judged")
                        : Invariant($"Audio measured: {track}; the other {track.Count - 1} tracks aren't judged"),
                Value = track.Count,
                Unit = "tracks",
                CheckedAgainst = "ffprobe -show_streams (the first audio stream: ffmpeg's 0:a:0)",
            });
        }

        Loudness(findings, measured.Loudness, At);

        // Every track's findings say which track (docs/decisions/2026-11-01-every-audio-track.md): the first's above,
        // then each other's in turn, each compared with its own -map 0:a:N.
        if (measured.OtherTracks.Count > 0)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                if (IsTrackCheck(findings[i].Check) && findings[i].Track is null)
                    findings[i] = OnTrack(findings[i], 1);
            }

            foreach (var other in measured.OtherTracks)
            {
                var own = new List<Finding>();
                Tone(own, other.Tone);
                Spans(own, "silence", "Silence", other.Silences, MinSilence, SilenceFilter);
                Channels(own, other.Channels, other.Silences, At);
                From(own, other.LoudnessFrom, other.Loudness);
                Loudness(own, other.Loudness, At);
                findings.AddRange(own.Select(f => OnTrack(f, other.Number)));
            }
        }

        return new QcReport
        {
            Profile = Name,
            StartTimecode = timecode.At(0),
            DropFrame = timecode.DropFrame,
            DropFrameRate = Timecode.HasDropFrame(measured.RateNumerator, measured.RateDenominator),
            Findings = findings,
            Format = measured.Format,
        };

        void Tone(List<Finding> into, IReadOnlyList<ToneSpan> tones)
        {
            foreach (var tone in tones)
            {
                // Judged at the report's precision (0.1 dB), as loudness is.
                bool off = ToneLevel is { } level && Math.Round(Math.Abs(tone.Level - level), 1) > ToneTolerance;
                into.Add(new Finding
                {
                    Check = "tone",
                    Severity = ToneLevel is null ? Severity.Info : off ? Severity.Warning : Severity.Pass,
                    Message = ToneLevel is { } expected
                        ? Invariant($"Line-up tone, {tone.Frequency:0} Hz at {tone.Level:0.0} dBFS (expected {expected:0.#} dBFS within {ToneTolerance:0.0#} dB), for {tone.End - tone.Start:0.00} s")
                        : Invariant($"Line-up tone, {tone.Frequency:0} Hz at {tone.Level:0.0} dBFS, for {tone.End - tone.Start:0.00} s"),
                    Value = tone.Level,
                    Unit = "dBFS",
                    Start = At(tone.Start),
                    End = At(tone.End),
                    CheckedAgainst = ToneFilter,
                });
            }
        }

        void From(List<Finding> into, double from, LoudnessMeasure? loudness)
        {
            if (from <= 0 || loudness is null)
                return;
            into.Add(new Finding
            {
                Check = "loudness.from",
                Severity = Severity.Info,
                Message = "Loudness is measured after the line-up tone",
                Start = At(from),
                End = At(measured.Duration),
                CheckedAgainst = Invariant($"ffmpeg -ss {from:0.###} -i FILE -map 0:a:0 -af ebur128=peak=true (the same figures)"),
            });
        }

        // A span that touches the first or the last frame is the programme's head or tail (reported); any other is a
        // warning (docs/decisions/2026-10-31-exit-codes-head-tail-timecode.md).
        void Spans(List<Finding> into, string check, string what, IReadOnlyList<TimeRange> spans, double minimum, string filter)
        {
            foreach (var span in spans)
            {
                if (span.Duration < minimum)
                    continue;
                bool head = span.Start < frame / 2, tail = span.End > measured.Duration - (frame / 2);
                string where = head ? " at the head" : tail ? " at the tail" : " mid-programme";
                into.Add(new Finding
                {
                    Check = check,
                    Severity = head || tail ? Severity.Info : Severity.Warning,
                    Message = Invariant($"{what} for {span.Duration:0.00} s{where}"),
                    Value = span.Duration,
                    Unit = "s",
                    Start = At(span.Start),
                    End = At(span.End),
                    CheckedAgainst = filter,
                });
            }
        }
    }

    // The format, always (information), then what the profile's format asks for, each a pass or a failure; a profile
    // that asks for a format fails a file whose format couldn't be read.
    void FormatFindings(List<Finding> into, Measurements measured)
    {
        if (measured.Format is { } format)
        {
            into.Add(new Finding { Check = "format", Severity = Severity.Info, Message = $"Format: {format}", CheckedAgainst = $"{MediaFormat.CheckedAgainst} (exact)" });
            if (Format is { IsEmpty: false } spec)
                into.AddRange(spec.Judge(format, measured.StartTimecode));
        }
        else if (Format is { IsEmpty: false })
        {
            into.Add(new Finding
            {
                Check = "format.container",
                Severity = Severity.Failure,
                Message = Invariant($"The format couldn't be read, so profile {Name}'s format can't be checked"),
                CheckedAgainst = MediaFormat.CheckedAgainst,
            });
        }
    }

    void Loudness(List<Finding> into, LoudnessMeasure? loudness, Func<double, Position> at)
    {
        if (loudness is not { } l)
        {
            into.Add(new Finding { Check = "loudness.integrated", Severity = Severity.Failure, Message = "No audio to measure", CheckedAgainst = LoudnessFilter });
            return;
        }

        if (l.Integrated is not { } integrated)
        {
            into.Add(new Finding
            {
                Check = "loudness.integrated",
                Severity = Severity.Failure,
                Message = "Silent: nothing above the absolute gate (-70 LUFS)",
                CheckedAgainst = LoudnessFilter,
            });
        }
        else
        {
            // Judged at the report's precision (0.1 LU), so a figure the report shows as passing passes.
            double off = Math.Round(integrated - TargetLoudness, 1);
            var severity = Math.Abs(off) <= LoudnessPass ? Severity.Pass : Math.Abs(off) <= LoudnessWarning ? Severity.Warning : Severity.Failure;
            into.Add(new Finding
            {
                Check = "loudness.integrated",
                Severity = severity,
                Message = Invariant($"Integrated loudness {integrated:0.0} LUFS ({off:+0.0;-0.0;0.0} LU from {TargetLoudness:0} LUFS)"),
                Value = integrated,
                Unit = "LUFS",
                CheckedAgainst = LoudnessFilter,
            });
        }

        // The range judged at the report's precision when the profile sets a window (docs/decisions/2026-11-05-short-term-loudness.md).
        double range = Math.Round(l.Range, 1);
        bool windowed = MinRange is not null || MaxRange is not null;
        bool outside = range < MinRange || range > MaxRange;
        string window = (MinRange, MaxRange) switch
        {
            ({ } low, { } high) => Invariant($" ({low:0.#} to {high:0.#} LU)"),
            ({ } low, null) => Invariant($" (at least {low:0.#} LU)"),
            (null, { } high) => Invariant($" (at most {high:0.#} LU)"),
            _ => "",
        };
        into.Add(new Finding
        {
            Check = "loudness.range",
            Severity = !windowed ? Severity.Info : outside ? Severity.Failure : Severity.Pass,
            Message = Invariant($"Loudness range {l.Range:0.0} LU{window}"),
            Value = l.Range,
            Unit = "LU",
            CheckedAgainst = LoudnessFilter,
        });

        Loudest(into, "loudness.momentary", "momentary", l.MaxMomentary, l.MaxMomentaryAt, 0.4, MaxMomentary, at);
        Loudest(into, "loudness.short-term", "short-term", l.MaxShortTerm, l.MaxShortTermAt, 3, MaxShortTerm, at);

        double peak = Math.Round(l.TruePeak, 1);
        into.Add(new Finding
        {
            Check = "loudness.true-peak",
            Severity = peak > MaxTruePeak ? Severity.Failure : Severity.Pass,
            // Silence has no peak: ebur128 gives -inf, which the report says in words rather than as "-Infinity".
            Message = double.IsNegativeInfinity(l.TruePeak)
                ? Invariant($"True peak: none, the sound is silent (at most {MaxTruePeak:0.0} dBTP)")
                : Invariant($"True peak {l.TruePeak:0.0} dBTP (at most {MaxTruePeak:0.0} dBTP)"),
            Value = l.TruePeak,
            Unit = "dBTP",
            CheckedAgainst = LoudnessFilter,
        });
    }

    // The loudest momentary or short-term window: reported, and judged at the report's precision when the profile sets
    // a maximum; its timecodes the window's.
    static void Loudest(List<Finding> into, string check, string name, double? loudest, double end, double window, double? max, Func<double, Position> at)
    {
        if (loudest is not { } value)
            return;
        bool over = Math.Round(value, 1) > max;
        into.Add(new Finding
        {
            Check = check,
            Severity = max is null ? Severity.Info : over ? Severity.Failure : Severity.Pass,
            Message = max is { } limit
                ? Invariant($"Loudest {name} loudness {value:0.0} LUFS (at most {limit:0.0} LUFS)")
                : Invariant($"Loudest {name} loudness {value:0.0} LUFS"),
            Value = value,
            Unit = "LUFS",
            Start = at(Math.Max(0, end - window)),
            End = at(end),
            CheckedAgainst = check == "loudness.momentary" ? MomentaryFilter : ShortTermFilter,
        });
    }

    void Range(List<Finding> into, Measurements measured, Func<double, Position> at)
    {
        foreach (var span in measured.OutOfRange)
        {
            into.Add(new Finding
            {
                Check = "range",
                Severity = Severity.Warning,
                Message = Invariant($"Pixels outside broadcast range: up to {span.WorstShare * 100:0.0}% of the picture (over {MaxOutOfRange * 100:0.#}%) for {span.End - span.Start:0.00} s"),
                Value = span.WorstShare * 100,
                Unit = "%",
                Start = at(span.Start),
                End = at(span.End),
                CheckedAgainst = RangeFilter,
            });
        }

        if (measured.OutOfRange.Count == 0 && measured.WorstOutOfRange is { } worst)
        {
            into.Add(new Finding
            {
                Check = "range",
                Severity = Severity.Pass,
                Message = Invariant($"Pixels outside broadcast range: at most {worst.Share * 100:0.00}% of the picture (at most {MaxOutOfRange * 100:0.#}%)"),
                Value = worst.Share * 100,
                Unit = "%",
                Start = at(worst.Seconds),
                CheckedAgainst = RangeFilter,
            });
        }
    }

    // R 103's gamut, as broadcast range (docs/decisions/2026-11-06-rgb-gamut.md): each span over the share a warning,
    // else a pass with the worst frame.
    void Gamut(List<Finding> into, Measurements measured, Func<double, Position> at)
    {
        string limits = Invariant($"{GamutLow * 100:0.#}% to {GamutHigh * 100:0.#}%");
        foreach (var span in measured.OutOfGamut)
        {
            into.Add(new Finding
            {
                Check = "gamut",
                Severity = Severity.Warning,
                Message = Invariant($"Pixels outside RGB gamut ({limits}): up to {span.WorstShare * 100:0.0}% of the picture (over {MaxOutOfGamut * 100:0.#}%) for {span.End - span.Start:0.00} s"),
                Value = span.WorstShare * 100,
                Unit = "%",
                Start = at(span.Start),
                End = at(span.End),
                CheckedAgainst = GamutFilter,
            });
        }

        if (measured.OutOfGamut.Count == 0 && measured.WorstOutOfGamut is { } worst)
        {
            into.Add(new Finding
            {
                Check = "gamut",
                Severity = Severity.Pass,
                Message = Invariant($"Pixels outside RGB gamut ({limits}): at most {worst.Share * 100:0.00}% of the picture (at most {MaxOutOfGamut * 100:0.#}%)"),
                Value = worst.Share * 100,
                Unit = "%",
                Start = at(worst.Seconds),
                CheckedAgainst = GamutFilter,
            });
        }
    }

    static void Crop(List<Finding> into, ActivePicture? crop)
    {
        if (crop is not { } c)
            return;
        string message = c.FillsFrame
            ? Invariant($"The picture fills the {c.FrameWidth}x{c.FrameHeight} frame")
            : Invariant($"Active picture {c.Width}x{c.Height} ({(double)c.Width / c.Height:0.00}:1) at {c.X},{c.Y} in the {c.FrameWidth}x{c.FrameHeight} frame");
        into.Add(new Finding { Check = "crop", Severity = Severity.Info, Message = message, CheckedAgainst = CropFilter });
    }

    // Information only (docs/decisions/2026-11-01-frames-bitrate-shots.md): how many of each type, and the GOP's
    // longest run from one I-frame to the next.
    static void PictureTypes(List<Finding> into, string types)
    {
        if (types.Length == 0)
            return;
        string message;
        if (types.All(t => t == 'I'))
        {
            message = Invariant($"All {types.Length} frames intra-coded (I)");
        }
        else
        {
            var counts = types.GroupBy(t => t).OrderBy(g => "IPB".IndexOf(g.Key, StringComparison.Ordinal) is var i and >= 0 ? i : 3).Select(g => Invariant($"{g.Count()} {g.Key}"));
            var starts = types.Select((t, i) => (t, i)).Where(x => x.t == 'I').Select(x => x.i).Append(types.Length).ToList();
            int gop = starts.Count < 2 ? types.Length : starts.Zip(starts.Skip(1), (a, b) => b - a).Prepend(starts[0]).Max();
            message = Invariant($"{types.Length} frames: {string.Join(", ", counts)}; at most {gop} frames from one I-frame to the next");
        }

        into.Add(new Finding { Check = "frames", Severity = Severity.Info, Message = message, Value = types.Length, Unit = "frames", CheckedAgainst = PictureTypesFilter });
    }

    // Single repeated frames (docs/decisions/2026-11-06-repeated-frames.md): each one information (a warning when the
    // profile says so), or one line when there are more than RepeatCadence of the moving picture's frames, a cadence
    // (pulldown, animation on twos). The moving picture is the frames decoded less those black, bars, one colour or
    // frozen.
    void Repeats(List<Finding> into, Measurements measured, List<TimeRange> still, double frame, Func<double, Position> at)
    {
        var stills = still.Concat(measured.Freezes).OrderBy(s => s.Start).ToList();
        var repeats = measured.Repeats.Where(t => !stills.Any(s => t >= s.Start && t < s.End)).ToList();
        double stillSeconds = 0, reach = 0;
        foreach (var s in stills)
        {
            double from = Math.Max(s.Start, reach);
            if (s.End > from)
                stillSeconds += s.End - from;
            reach = Math.Max(reach, s.End);
        }

        long frames = measured.PictureTypes.Length > 0 ? measured.PictureTypes.Length : (long)Math.Round(measured.Duration / frame);
        long moving = Math.Max(0, frames - (long)Math.Round(stillSeconds / frame));
        double share = moving > 0 ? (double)repeats.Count / moving : 0;
        if (repeats.Count == 0 || share > RepeatCadence)
        {
            into.Add(new Finding
            {
                Check = "repeat",
                Severity = Severity.Info,
                Message = repeats.Count == 0
                    ? "No repeated frames (a single frame identical to the one before, in moving picture)"
                    : Invariant($"Frames repeat in a cadence: {repeats.Count} of {moving} moving frames ({share * 100:0.#}%), pulldown or animation; not listed"),
                Value = repeats.Count,
                Unit = "frames",
                CheckedAgainst = RepeatFilter,
            });
            return;
        }

        foreach (double t in repeats)
        {
            into.Add(new Finding
            {
                Check = "repeat",
                Severity = RepeatsWarn ? Severity.Warning : Severity.Info,
                Message = "Frame repeated: identical to the one before, in moving picture (a dropped frame filled?)",
                Start = at(t),
                CheckedAgainst = RepeatFilter,
            });
        }
    }

    // What was decoded against what the file lists (docs/decisions/2026-11-04-odd-files.md): a picture that ends before
    // the frames the container lists (cut short), frames read that don't decode (damaged) and a track whose sound ends
    // more than a frame before its stream's duration each fail. Judged where the container indexes its streams (MOV,
    // MP4 and their kin, MXF: one sample a frame, durations that are the essence's); elsewhere (MKV, MPEG, AVI) they
    // are estimates or absent, and the frames decoded are reported.
    static void Decode(List<Finding> into, Measurements measured, double frame, Func<double, Position> at)
    {
        if (measured.Format is not { } format)
            return;
        string container = format.Container;
        bool mxf = container == "mxf";
        if (!mxf && !container.StartsWith("mov,", StringComparison.Ordinal) && container is not ("wav" or "aiff"))
            return;
        if (measured.Packets is { } packets && format.Video is { } video)
            DecodePicture(into, measured, packets, video, mxf, frame, at);

        var tracks = new List<(int Number, ChannelMeasures? Channels)> { (1, measured.Channels) };
        tracks.AddRange(measured.OtherTracks.Select(t => (t.Number, t.Channels)));
        foreach (var (number, channels) in tracks)
        {
            if (channels is not { } c)
                continue;
            var header = number == 1 ? measured.SoundHeader : null;
            if (header is { Frames: null })
            {
                into.Add(new Finding
                {
                    Check = "decode.sound",
                    Severity = Severity.Warning,
                    Message = Invariant($"The {header.Kind} header was never finished (it lists no length): {c.Duration:0.00} s decoded, whether that is all of it unknown"),
                    Value = c.Duration,
                    Unit = "s",
                    CheckedAgainst = SoundHeaderFilter,
                });
                continue;
            }

            // A WAV's header can list more than FFmpeg's stream duration, which goes by the bytes there are.
            double? listed = format.Audio.FirstOrDefault(a => a.Number == number)?.StreamDuration;
            string lists = "the track lists";
            if (header?.Seconds is { } seconds && (listed is null || seconds > listed + frame))
                (listed, lists) = (seconds, $"the {header.Kind} header lists");
            if (listed is not { } length)
                continue;
            bool whole = c.Duration >= length - frame;
            into.Add(new Finding
            {
                Check = "decode.sound",
                Severity = whole ? Severity.Pass : Severity.Failure,
                Message = whole
                    ? Invariant($"The sound decoded to the end: {c.Duration:0.00} s of the {length:0.00} s {lists}")
                    : Invariant($"The sound ends {length - c.Duration:0.00} s early: {c.Duration:0.00} s of the {length:0.00} s {lists} decoded"),
                Value = c.Duration,
                Unit = "s",
                Start = whole ? null : at(c.Duration),
                End = whole ? null : at(length),
                Track = measured.OtherTracks.Count > 0 ? number : null,
                CheckedAgainst = header is null ? SoundDecodeFilter : SoundHeaderFilter,
            });
        }
    }

    static void DecodePicture(List<Finding> into, Measurements measured, VideoPackets packets, VideoFormat video, bool mxf, double frame, Func<double, Position> at)
    {
        long decoded = measured.PictureTypes.Length, read = packets.Count;
        long? listed = video.ListedFrames ?? (mxf && video.StreamDuration is { } seconds && video.Rate is { } rate ? (long)Math.Round(seconds * rate) : (long?)null);
        if (listed is { } frames && read < frames)
        {
            into.Add(new Finding
            {
                Check = "decode.frames",
                Severity = Severity.Failure,
                Message = Invariant($"The picture ends early: {decoded} of the {frames} frames the file lists decoded ({read} in the file), {(frames - decoded) * frame:0.00} s missing"),
                Value = decoded,
                Unit = "frames",
                Start = at(decoded * frame),
                End = at(frames * frame),
                CheckedAgainst = DecodeFilter,
            });
        }
        else if (decoded < read)
        {
            into.Add(new Finding
            {
                Check = "decode.errors",
                Severity = Severity.Failure,
                Message = Invariant($"{read - decoded} of the {read} frames in the file don't decode"),
                Value = read - decoded,
                Unit = "frames",
                CheckedAgainst = DecodeFilter,
            });
        }
        else if (listed is { } all)
        {
            into.Add(new Finding
            {
                Check = "decode.frames",
                Severity = Severity.Pass,
                Message = Invariant($"Every frame the file lists decoded: {decoded} of {all}"),
                Value = decoded,
                Unit = "frames",
                CheckedAgainst = DecodeFilter,
            });
        }
    }

    // Each audio track's length and start against the picture's (docs/decisions/2026-11-05-av-lengths.md): what decoded
    // (the picture's frames, the track's samples) and where the streams start (ffprobe's start_time).
    // The picture's length counts the frames missing from it (Dropped): the picture runs on through a gap.
    void AudioVideo(List<Finding> into, Measurements measured, double frame, long dropped, Func<double, Position> at)
    {
        if (!measured.HasPicture || measured.PictureTypes.Length == 0)
            return;
        double picture = (measured.PictureTypes.Length + dropped) * frame;
        var tracks = new List<(int Number, ChannelMeasures? Channels)> { (1, measured.Channels) };
        tracks.AddRange(measured.OtherTracks.Select(t => (t.Number, t.Channels)));
        int? Track(int number) => measured.OtherTracks.Count > 0 ? number : null;
        double? Offset(int number) => measured.Format is { Video.StartTime: { } video } format
            && format.Audio.FirstOrDefault(a => a.Number == number)?.StartTime is { } audio ? audio - video : null;
        foreach (var (number, channels) in tracks)
        {
            if (channels is not { } c || c.Duration <= 0)
                continue;
            double sound = c.Duration, off = sound - picture;

            // Where the sound is missing or runs on: at its start when it starts late or early, else at its end.
            double starts = Offset(number) ?? 0, ends = starts + sound;
            (double From, double To) gap = Math.Abs(starts) > Math.Abs(ends - picture)
                ? (Math.Min(0, starts), Math.Max(0, starts))
                : (Math.Min(ends, picture), Math.Max(ends, picture));
            var severity = Judge(off, frame);
            into.Add(new Finding
            {
                Check = "av.length",
                Severity = severity,
                Message = severity == Severity.Pass
                    ? Invariant($"The sound is as long as the picture: {sound:0.00} s against {picture:0.00} s (within {Frames(SyncFrames)})")
                    : Invariant($"The sound is {Math.Abs(off):0.00} s ({Frames(Math.Abs(off) / frame)}) {(off < 0 ? "shorter" : "longer")} than the picture: {sound:0.00} s against {picture:0.00} s"),
                Value = off,
                Unit = "s",
                Start = severity == Severity.Pass ? null : at(gap.From),
                End = severity == Severity.Pass ? null : at(gap.To),
                Track = Track(number),
                CheckedAgainst = AvLengthFilter,
            });
        }

        foreach (var (number, _) in tracks)
        {
            if (Offset(number) is not { } off)
                continue;
            var severity = Judge(off, frame);
            into.Add(new Finding
            {
                Check = "av.start",
                Severity = severity,
                Message = severity == Severity.Pass
                    ? Invariant($"The sound starts with the picture (within {Frames(SyncFrames)})")
                    : Invariant($"The sound starts {Math.Abs(off):0.00} s ({Frames(Math.Abs(off) / frame)}) {(off > 0 ? "after" : "before")} the picture"),
                Value = off,
                Unit = "s",
                Track = Track(number),
                CheckedAgainst = AvStartFilter,
            });
        }
    }

    // The steps between the picture's frames, in display order (docs/decisions/2026-11-05-dropped-frames.md): one frame,
    // within half a frame, is regular; over 1.5 frames is a gap, its frames less one missing. Over 1% of the steps (and
    // more than one) not regular is a variable frame rate, a warning, and its gaps aren't judged: a phone's or a screen
    // recording's steps are irregular by design. Missing frames over MaxDroppedFrames fail, each gap a finding (the
    // first MaxGaps; the rest in one); within it, they are reported. Gives the frames missing where judged, else 0.
    long Dropped(List<Finding> into, Measurements measured, double frame, Func<double, Position> at)
    {
        if (!measured.HasPicture || measured.Packets is not { Times.Count: > 1 } packets || !(frame > 0))
            return 0;
        var times = packets.Times;
        int steps = times.Count - 1, irregular = 0;
        var gaps = new List<(double After, double Next, long Missing)>();
        for (int i = 1; i < times.Count; i++)
        {
            double step = (times[i] - times[i - 1]) / frame;
            if (Math.Abs(step - 1) <= 0.5)
                continue;
            irregular++;
            if (step > 1.5)
                gaps.Add((times[i - 1], times[i], (long)Math.Round(step) - 1));
        }

        if (irregular > 1 && irregular > steps * 0.01)
        {
            into.Add(new Finding
            {
                Check = "frames.variable-rate",
                Severity = Severity.Warning,
                Message = Invariant($"Variable frame rate: {irregular} of the {steps} steps between frames aren't one frame at {1 / frame:0.###} fps; gaps (dropped frames) not judged"),
                Value = irregular,
                Unit = "steps",
                CheckedAgainst = VariableRateFilter,
            });
            return 0;
        }

        long missing = gaps.Sum(g => g.Missing);
        if (gaps.Count == 0)
        {
            into.Add(new Finding
            {
                Check = "frames.dropped",
                Severity = Severity.Pass,
                Message = Invariant($"No frames missing: each of the {times.Count} frames one frame after the last"),
                Value = 0,
                Unit = "frames",
                CheckedAgainst = DroppedFilter,
            });
            return 0;
        }

        var severity = missing > MaxDroppedFrames ? Severity.Failure : Severity.Info;
        string allowed = severity == Severity.Info ? Invariant($" (the profile allows {MaxDroppedFrames})") : "";
        foreach (var (after, next, count) in gaps.Take(MaxGaps))
        {
            into.Add(new Finding
            {
                Check = "frames.dropped",
                Severity = severity,
                Message = Invariant($"{count} frame{(count == 1 ? "" : "s")} missing: the picture jumps {next - after:0.000} s{allowed}"),
                Value = count,
                Unit = "frames",
                Start = at(after + frame),
                End = at(next),
                CheckedAgainst = DroppedFilter,
            });
        }

        if (gaps.Count > MaxGaps)
        {
            var rest = gaps.Skip(MaxGaps).ToList();
            into.Add(new Finding
            {
                Check = "frames.dropped",
                Severity = severity,
                Message = Invariant($"{rest.Sum(g => g.Missing)} frames missing in {rest.Count} more gaps, {missing} in all{allowed}"),
                Value = rest.Sum(g => g.Missing),
                Unit = "frames",
                Start = at(rest[0].After + frame),
                End = at(rest[^1].Next),
                CheckedAgainst = DroppedFilter,
            });
        }

        return missing;
    }

    // The gaps reported one by one; the rest in one finding.
    const int MaxGaps = 20;

    // Within SyncFrames frames passes (a hair over, from rounding, too); over SyncFailure seconds fails; between warns.
    Severity Judge(double off, double frame) =>
        Math.Abs(off) <= (SyncFrames * frame) + 1e-6 ? Severity.Pass
        : Math.Abs(off) > SyncFailure ? Severity.Failure
        : Severity.Warning;

    static string Frames(double frames) =>
        Math.Abs(frames - Math.Round(frames)) < 0.05
            ? Invariant($"{Math.Round(frames):0} frame{(Math.Round(frames) == 1 ? "" : "s")}")
            : Invariant($"{frames:0.0} frames");

    // The longest interval between two keyframes (a single keyframe's: the file's length).
    static void Keyframes(List<Finding> into, Measurements measured, Func<double, Position> at)
    {
        if (measured.Packets is not { } packets)
            return;
        var keys = packets.Keyframes;
        if (keys.Count == 0)
        {
            into.Add(new Finding { Check = "keyframes", Severity = Severity.Info, Message = "No keyframes", CheckedAgainst = KeyframesFilter });
            return;
        }

        if (keys.Count == measured.PictureTypes.Length && keys.Count > 1)
        {
            into.Add(new Finding { Check = "keyframes", Severity = Severity.Info, Message = Invariant($"Every frame is a keyframe ({keys.Count})"), CheckedAgainst = KeyframesFilter });
            return;
        }

        var (from, longest) = keys.Count == 1
            ? (keys[0], measured.Duration - keys[0])
            : keys.Zip(keys.Skip(1), (a, b) => (a, b - a)).MaxBy(x => x.Item2);
        string message = keys.Count == 1 ? "1 keyframe" : Invariant($"{keys.Count} keyframes, at most {longest:0.00} s apart");
        into.Add(new Finding
        {
            Check = "keyframes",
            Severity = Severity.Info,
            Message = message,
            Value = longest,
            Unit = "s",
            Start = at(from),
            End = at(from + longest),
            CheckedAgainst = KeyframesFilter,
        });
    }

    // The average over the video's duration, and the second with the most bytes.
    static void Bitrate(List<Finding> into, Measurements measured, Func<double, Position> at)
    {
        if (measured.Packets is not { BytesPerSecond.Count: > 0 } packets || measured.Duration <= 0)
            return;
        double average = packets.Bytes * 8 / measured.Duration / 1e6;
        int second = packets.BytesPerSecond.Index().MaxBy(x => x.Item).Index;
        double peak = packets.BytesPerSecond[second] * 8 / 1e6;
        into.Add(new Finding
        {
            Check = "bitrate",
            Severity = Severity.Info,
            Message = Invariant($"Video bitrate {average:0.0} Mb/s on average, at most {peak:0.0} Mb/s in a second"),
            Value = average,
            Unit = "Mb/s",
            Start = at(second),
            End = at(Math.Min(second + 1, measured.Duration)),
            CheckedAgainst = BitrateFilter,
        });
    }

    // Information only: how many and the shortest, then each shot ("shot", which the command line doesn't print).
    static void Shots(List<Finding> into, IReadOnlyList<TimeRange>? shots, Func<double, Position> at)
    {
        if (shots is not { Count: > 0 })
            return;
        var shortest = shots.MinBy(s => s.Duration);
        into.Add(new Finding
        {
            Check = "shots",
            Severity = Severity.Info,
            Message = shots.Count == 1 ? "1 shot" : Invariant($"{shots.Count} shots, the shortest {shortest.Duration:0.00} s"),
            Value = shots.Count,
            Unit = "shots",
            CheckedAgainst = ShotsFilter,
        });
        foreach (var (shot, i) in shots.Select((s, i) => (s, i + 1)))
        {
            into.Add(new Finding
            {
                Check = "shot",
                Severity = Severity.Info,
                Message = Invariant($"Shot {i}, {shot.Duration:0.00} s"),
                Value = shot.Duration,
                Unit = "s",
                Start = at(shot.Start),
                End = at(shot.End),
                CheckedAgainst = ShotsFilter,
            });
        }
    }

    // What the profile asks for at the head and the tail (docs/decisions/2026-11-03-line-up-and-head-tail.md), each a
    // pass or a failure: bars and tone from the start, black with silence before the programme (after the head's bars,
    // or from the start) and at the end.
    void HeadAndTail(List<Finding> into, Measurements measured, double frame, Func<double, Position> at)
    {
        var bars = measured.Bars.FirstOrDefault(b => b.Start < frame / 2);
        bool picture = measured.HasPicture;
        if (HeadBars is { } headBars && picture)
        {
            double got = bars.Duration;
            into.Add(Need("head.bars", got, headBars, got > 0 ? Invariant($"Bars at the head for {got:0.00} s") : "No bars at the head", BarsFilter, bars));
        }

        if (HeadTone is { } headTone)
        {
            var tone = measured.Tone.FirstOrDefault(t => t.Start < 0.5);
            double got = tone.End - tone.Start;
            into.Add(Need("head.tone", got, headTone, got > 0 ? Invariant($"Line-up tone at the head for {got:0.00} s") : "No line-up tone at the head", ToneFilter, new TimeRange(tone.Start, tone.End)));
        }

        if (HeadBlack is { } headBlack && picture)
        {
            double from = bars.Duration > 0 ? bars.End : 0;
            var black = measured.Black.FirstOrDefault(b => b.Start <= from + frame && b.End > from);
            double got = Silent(black);
            into.Add(Need("head.black", got, headBlack, got > 0 ? Invariant($"Black with silence before the programme for {got:0.00} s") : "No black with silence before the programme", BlackFilter + "; " + SilenceFilter, black));
        }

        if (TailBlack is { } tailBlack && picture)
        {
            var black = measured.Black.FirstOrDefault(b => b.End > measured.Duration - (frame / 2));
            double got = Silent(black);
            into.Add(Need("tail.black", got, tailBlack, got > 0 ? Invariant($"Black with silence at the tail for {got:0.00} s") : "No black with silence at the tail", BlackFilter + "; " + SilenceFilter, black));
        }

        // How much of a black span is silent too.
        double Silent(TimeRange black) => black.Duration <= 0 ? 0 : measured.Silences.Sum(s => Math.Max(0, Math.Min(s.End, black.End) - Math.Max(s.Start, black.Start)));

        Finding Need(string check, double got, double needed, string what, string filter, TimeRange span) => new()
        {
            Check = check,
            Severity = Math.Round(got, 2) >= needed ? Severity.Pass : Severity.Failure,
            Message = Invariant($"{what} (at least {needed:0.##} s)"),
            Value = got,
            Unit = "s",
            Start = span.Duration > 0 ? at(span.Start) : null,
            End = span.Duration > 0 ? at(span.End) : null,
            CheckedAgainst = filter,
        };
    }

    // Whether the picture looks interlaced (idet's multiple-frame totals), reported, and against the stream's own flag
    // (field_order) a pass, or a warning when they disagree (docs/decisions/2026-11-03-interlacing.md).
    static void Interlace(List<Finding> into, Measurements measured)
    {
        if (measured.Interlace is not { } i)
            return;
        long judged = i.MultiTff + i.MultiBff + i.MultiProgressive;
        long interlaced = i.MultiTff + i.MultiBff;
        string looks = judged == 0 ? "says nothing either way"
            : i.LooksInterlaced ? Invariant($"looks interlaced, {(i.TopFieldFirst ? "top" : "bottom")} field first")
            : "looks progressive";
        into.Add(new Finding
        {
            Check = "interlace",
            Severity = Severity.Info,
            Message = Invariant($"The picture {looks}: {i.MultiTff} frames top field first, {i.MultiBff} bottom field first, {i.MultiProgressive} progressive, {i.MultiUndetermined} undetermined"),
            Value = interlaced,
            Unit = "frames",
            CheckedAgainst = InterlaceFilter,
        });

        string? order = measured.Format?.Video?.FieldOrder;
        bool? flagged = order switch
        {
            "progressive" => false,
            "tt" or "bb" or "tb" or "bt" => true,
            _ => null,
        };
        if (judged == 0 || flagged is not { } says)
            return;
        bool agrees = says == i.LooksInterlaced;
        into.Add(new Finding
        {
            Check = "interlace.flag",
            Severity = agrees ? Severity.Pass : Severity.Warning,
            Message = agrees
                ? Invariant($"The stream says {(says ? "interlaced" : "progressive")} ({order}), as the picture looks")
                : Invariant($"The stream says {(says ? "interlaced" : "progressive")} ({order}), but the picture looks {(i.LooksInterlaced ? "interlaced" : "progressive")} ({interlaced} of {judged} frames interlaced)"),
            CheckedAgainst = $"{InterlaceFilter}, against ffprobe's field_order",
        });
    }

    // The checks each audio track has.
    static bool IsTrackCheck(string check) => check is "tone" or "silence" || check.StartsWith("loudness.", StringComparison.Ordinal) || check.StartsWith("channel.", StringComparison.Ordinal);

    // A track's channels one by one (docs/decisions/2026-11-03-channel-faults.md): a channel silent throughout while
    // another has sound fails; one silent for MinSilence or more while another has sound, a pair out of phase, and
    // clipping warn; dual mono is reported. Silences of every channel at once are the track's own (silence).
    void Channels(List<Finding> into, ChannelMeasures? channels, IReadOnlyList<TimeRange> together, Func<double, Position> at)
    {
        if (channels is not { Channels: > 1 } c)
        {
            if (channels is { } mono)
                Clipping(into, mono);
            return;
        }

        bool Dead(int i) => c.Silences[i].Any(s => s.Start <= 0.01 && s.End >= c.Duration - 0.05);
        var dead = Enumerable.Range(0, c.Channels).Where(Dead).ToList();
        bool someSound = dead.Count < c.Channels;
        if (someSound)
        {
            foreach (int i in dead)
            {
                into.Add(new Finding
                {
                    Check = "channel.dead",
                    Severity = Severity.Failure,
                    Message = Invariant($"Channel {i + 1} of {c.Channels} is silent throughout while the others have sound"),
                    Start = at(0),
                    End = at(c.Duration),
                    CheckedAgainst = ChannelSilenceFilter(i + 1),
                });
            }

            for (int i = 0; i < c.Channels; i++)
            {
                if (dead.Contains(i))
                    continue;
                foreach (var piece in c.Silences[i].SelectMany(s => Without(s, together)).Where(p => p.Duration >= MinSilence))
                {
                    into.Add(new Finding
                    {
                        Check = "channel.silence",
                        Severity = Severity.Warning,
                        Message = Invariant($"Channel {i + 1} of {c.Channels} silent for {piece.Duration:0.00} s while the others have sound"),
                        Value = piece.Duration,
                        Unit = "s",
                        Start = at(piece.Start),
                        End = at(piece.End),
                        CheckedAgainst = ChannelSilenceFilter(i + 1),
                    });
                }
            }
        }

        if (c.Phase is { } phase && !Dead(0) && !Dead(1))
        {
            double shown = Math.Round(phase, 2);
            into.Add(new Finding
            {
                Check = "channel.phase",
                Severity = shown < MinPhase ? Severity.Warning : Severity.Pass,
                Message = shown < MinPhase
                    ? Invariant($"Channels 1 and 2 out of phase: phase correlation {phase:0.00} (at least {MinPhase:0.0#})")
                    : Invariant($"Phase correlation of channels 1 and 2: {phase:0.00} (at least {MinPhase:0.0#})"),
                Value = phase,
                CheckedAgainst = PhaseFilter,
            });
            if (phase > 0.999 && Math.Abs(c.RmsLevels[0] - c.RmsLevels[1]) < 0.1 && c.RmsLevels[0] > SilenceNoise)
            {
                into.Add(new Finding
                {
                    Check = "channel.dual-mono",
                    Severity = Severity.Info,
                    Message = "Channels 1 and 2 are the same (dual mono)",
                    CheckedAgainst = DualMonoFilter,
                });
            }
        }

        Clipping(into, c);
    }

    void Clipping(List<Finding> into, ChannelMeasures c)
    {
        bool any = false;
        for (int i = 0; i < c.Channels; i++)
        {
            if (c.ClippedRuns[i] == 0)
                continue;
            any = true;
            into.Add(new Finding
            {
                Check = "channel.clipping",
                Severity = Severity.Warning,
                Message = Invariant($"Channel {i + 1} of {c.Channels} clips: {c.ClippedRuns[i]} run{(c.ClippedRuns[i] == 1 ? "" : "s")} of {ClipRun} or more samples at full scale"),
                Value = c.ClippedRuns[i],
                Unit = "runs",
                CheckedAgainst = ClippingFilter,
            });
        }

        if (!any)
        {
            into.Add(new Finding
            {
                Check = "channel.clipping",
                Severity = Severity.Pass,
                Message = Invariant($"No clipping: no run of {ClipRun} or more samples at full scale on any channel"),
                CheckedAgainst = ClippingFilter,
            });
        }
    }

    // The parts of span outside every one of the others.
    static IEnumerable<TimeRange> Without(TimeRange span, IReadOnlyList<TimeRange> others)
    {
        double start = span.Start;
        foreach (var other in others.Where(o => o.End > span.Start && o.Start < span.End).OrderBy(o => o.Start))
        {
            if (other.Start > start)
                yield return new TimeRange(start, other.Start);
            start = Math.Max(start, other.End);
        }

        if (start < span.End)
            yield return new TimeRange(start, span.End);
    }

    // A finding of audio track number (from 1): it says so, and is compared with that track's -map.
    static Finding OnTrack(Finding finding, int number) => finding with
    {
        Track = number,
        Message = Invariant($"Track {number}: {finding.Message}"),
        CheckedAgainst = finding.CheckedAgainst.Replace("0:a:0", Invariant($"0:a:{number - 1}"), StringComparison.Ordinal),
    };

    static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
