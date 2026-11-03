using System.Globalization;

namespace MediaQc;

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

        // A black picture or bars are still by nature (black and bars report them): a freeze counts outside them.
        var still = measured.Black.Concat(measured.Bars).OrderBy(s => s.Start).ToList();
        Spans(findings, "freeze", "Picture frozen", [.. measured.Freezes.SelectMany(f => Without(f, still))], MinFreeze, FreezeFilter);
        Range(findings, measured, At);
        Crop(findings, measured.Crop);
        PictureTypes(findings, measured.PictureTypes);
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

        Loudness(findings, measured.Loudness);

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
                Loudness(own, other.Loudness);
                findings.AddRange(own.Select(f => OnTrack(f, other.Number)));
            }
        }

        return new QcReport { Profile = Name, StartTimecode = timecode.At(0), DropFrame = timecode.DropFrame, Findings = findings };

        void Tone(List<Finding> into, IReadOnlyList<ToneSpan> tones)
        {
            foreach (var tone in tones)
            {
                into.Add(new Finding
                {
                    Check = "tone",
                    Severity = Severity.Info,
                    Message = Invariant($"Line-up tone, {tone.Frequency:0} Hz at {tone.Level:0.0} dBFS, for {tone.End - tone.Start:0.00} s"),
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

    void Loudness(List<Finding> into, LoudnessMeasure? loudness)
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

        into.Add(new Finding
        {
            Check = "loudness.range",
            Severity = Severity.Info,
            Message = Invariant($"Loudness range {l.Range:0.0} LU"),
            Value = l.Range,
            Unit = "LU",
            CheckedAgainst = LoudnessFilter,
        });

        double peak = Math.Round(l.TruePeak, 1);
        into.Add(new Finding
        {
            Check = "loudness.true-peak",
            Severity = peak > MaxTruePeak ? Severity.Failure : Severity.Pass,
            Message = Invariant($"True peak {l.TruePeak:0.0} dBTP (at most {MaxTruePeak:0.0} dBTP)"),
            Value = l.TruePeak,
            Unit = "dBTP",
            CheckedAgainst = LoudnessFilter,
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
