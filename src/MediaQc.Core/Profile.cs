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

    /// <summary>The FFmpeg filter the loudness figures are checked against (exact: FrameReader runs ebur128 itself on the FFmpeg path).</summary>
    public static string LoudnessFilter => "ebur128=peak=true (integrated and range within 0.1 LU, true peak within 0.1 dB)";

    /// <summary>The FFmpeg filter black spans are checked against.</summary>
    public string BlackFilter => Invariant($"blackdetect=d={MinBlack}:pix_th={BlackPixelThreshold:0.00}:pic_th={BlackPictureThreshold:0.00} (each end within one frame)");

    /// <summary>The FFmpeg filter silences are checked against.</summary>
    public string SilenceFilter => Invariant($"silencedetect=n={SilenceNoise}dB:d={MinSilence} (each end within 10 ms)");

    /// <summary>What bars are checked against (signalstats on each bar's patch).</summary>
    public static string BarsFilter => "signalstats on each bar of smptebars/smptehdbars (each end within one frame)";

    /// <summary>What line-up tone is checked against.</summary>
    public static string ToneFilter => "astats per channel (RMS level within 0.1 dB) and a 1 kHz bandpass (each end within 20 ms)";

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
                Message = Invariant($"Not checked: this version doesn't measure {check} yet"),
                CheckedAgainst = check switch
                {
                    "black" => BlackFilter,
                    "bars" => BarsFilter,
                    "silence" => SilenceFilter,
                    "tone" => ToneFilter,
                    _ => "nothing yet",
                },
            });
        }

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

        foreach (var tone in measured.Tone)
        {
            findings.Add(new Finding
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

        Spans(findings, "black", "Black", measured.Black, MinBlack, BlackFilter);
        Spans(findings, "silence", "Silence", measured.Silences, MinSilence, SilenceFilter);
        Loudness(findings, measured.Loudness);

        return new QcReport { Profile = Name, StartTimecode = timecode.At(0), DropFrame = timecode.DropFrame, Findings = findings };

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

    static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
