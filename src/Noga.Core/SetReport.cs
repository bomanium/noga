using System.Globalization;

namespace Noga;

/// <summary>
/// One file of a set: its <paramref name="Name"/> and its <paramref name="Report"/>, or why it couldn't be read
/// (<paramref name="Error"/>).
/// </summary>
public sealed record SetFile(string Name, QcReport? Report, string? Error = null)
{
    /// <summary>Where the file's check stands: its verdict, or unreadable.</summary>
    public Status Status => Report is null ? Status.Unreadable : StatusStyle.Of(Report.Verdict);

    /// <summary>The file's RMS level (dBFS: its level.rms finding's); null when silent or not measured.</summary>
    public double? Rms => Level("level.rms");

    /// <summary>The file's sample peak (dBFS: its level.peak finding's); null when silent or not judged.</summary>
    public double? Peak => Level("level.peak");

    double? Level(string check) => Report?.Findings.FirstOrDefault(f => f.Check == check && f.Track is null or 1)?.Value;
}

/// <summary>
/// A folder's files checked as one set (<c>noga check FOLDER</c>: a book's chapters; docs/decisions/2026-11-15-set-check.md):
/// each file's report, and what the profile's <see cref="SetSpec"/> asks of them together: one format (a failure for a
/// file that differs) and RMS levels near each other (a warning for one further from the median than the tolerance).
/// </summary>
public sealed record SetReport
{
    /// <summary>What the set's format is checked against.</summary>
    public const string FormatCheckedAgainst = "ffprobe -show_format -show_streams on each file: format_name; the first audio stream's codec_name, sample_rate, channels and bits per sample, and bit_rate to the kb/s for MP3 and PCM; the number of audio streams; the picture's codec_name, width, height and r_frame_rate (each exact)";

    /// <summary>What the set's levels are checked against.</summary>
    public static string LevelCheckedAgainst => $"each file's level.rms ({Noga.Profile.RmsFilter}), the median of them and each file's distance from it (arithmetic on those figures, at 0.1 dB)";

    /// <summary>The profile's name.</summary>
    public required string Profile { get; init; }

    /// <summary>The folder's full path.</summary>
    public required string Folder { get; init; }

    /// <summary>When the set's check ended.</summary>
    public required DateTimeOffset Checked { get; init; }

    /// <summary>The files, in the folder's order.</summary>
    public required IReadOnlyList<SetFile> Files { get; init; }

    /// <summary>What was found across the files (set.format, set.level); empty when the profile has no <see cref="SetSpec"/>.</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>How many files couldn't be read.</summary>
    public int Unreadable => Files.Count(f => f.Report is null);

    /// <summary>The most severe finding of any file's report and of the set's own.</summary>
    public Severity Verdict => Files.Where(f => f.Report is not null).Select(f => f.Report!.Verdict).Concat(Findings.Select(f => f.Severity)).DefaultIfEmpty(Severity.Pass).Max();

    /// <summary>The set's status: unreadable when a file couldn't be read (the set wasn't checked whole), else its verdict's.</summary>
    public Status Status => Unreadable > 0 ? Status.Unreadable : StatusStyle.Of(Verdict);

    /// <summary>"12 files: 10 passed, 1 passed with warnings, 1 failed".</summary>
    public string Counts
    {
        get
        {
            var parts = new List<string>();
            foreach (var status in (Status[])[Status.Passed, Status.PassedWithWarnings, Status.Failed, Status.Unreadable])
            {
                if (Files.Count(f => f.Status == status) is var count and > 0)
                    parts.Add(Invariant($"{count} {char.ToLowerInvariant(StatusStyle.Label(status)[0])}{StatusStyle.Label(status)[1..]}"));
            }

            return Invariant($"{Files.Count} {(Files.Count == 1 ? "file" : "files")}: {string.Join(", ", parts)}");
        }
    }

    /// <summary>
    /// One exit code for the set: 2 when a file couldn't be read, else 1 with a failure in any file or in the set,
    /// 3 with warnings only (1 with <paramref name="warningsFail"/>), 0 when everything passed.
    /// </summary>
    public int ExitCode(bool warningsFail = false) => Unreadable > 0 ? QcReport.Error : Verdict switch
    {
        Severity.Failure => QcReport.Failed,
        Severity.Warning => warningsFail ? QcReport.Failed : QcReport.Warnings,
        _ => QcReport.Passed,
    };

    /// <summary>The set's report: <paramref name="files"/> as checked by <paramref name="profile"/>, judged together as its <see cref="Profile.Set"/> says.</summary>
    public static SetReport Evaluate(Profile profile, string folder, IReadOnlyList<SetFile> files, DateTimeOffset? checkedAt = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(files);
        var findings = new List<Finding>();
        if (profile.Set is { } set)
        {
            if (set.SameFormat)
                Format(findings, files);
            Levels(findings, files, set.LevelTolerance);
        }

        return new SetReport { Profile = profile.Name, Folder = folder, Checked = checkedAt ?? DateTimeOffset.Now, Files = files, Findings = findings };
    }

    // What of a file's format the set compares, each with the words it is named by.
    static List<(string What, string Value)> Signature(MediaFormat format)
    {
        var parts = new List<(string, string)> { ("container", format.Container) };
        if (format.Video is { } video)
        {
            parts.Add(("video codec", video.Codec));
            parts.Add(("frame size", Invariant($"{video.Width}×{video.Height}")));
            parts.Add(("frame rate", video.Rate is { } rate ? Invariant($"{rate:0.###} fps") : video.FrameRate ?? "unknown"));
        }
        else
        {
            parts.Add(("picture", "none"));
        }

        parts.Add(("audio tracks", Invariant($"{format.Audio.Count}")));
        if (format.Audio is [var audio, ..])
        {
            parts.Add(("audio codec", audio.Codec));
            parts.Add(("sample rate", audio.SampleRate is { } hz ? Invariant($"{hz} Hz") : "unknown"));
            parts.Add(("channels", audio.Channels switch { 1 => "mono", 2 => "stereo", var n => Invariant($"{n}") }));
            if (audio.BitDepth is { } bits)
                parts.Add(("bit depth", Invariant($"{bits}-bit")));

            // A bit rate is the file's choice where it is constant: an MP3's (ACX's CBR) and PCM's. Other codecs' average.
            if (audio.BitRate is { } bitRate && (audio.Codec == "mp3" || audio.Codec.StartsWith("pcm_", StringComparison.Ordinal)))
                parts.Add(("bit rate", Invariant($"{Math.Round(bitRate / 1000.0):0} kb/s")));
        }

        return parts;
    }

    // One format throughout: each value's most common one (the earliest file's among equals) is the set's, and a file
    // with another fails, named with each difference.
    static void Format(List<Finding> into, IReadOnlyList<SetFile> files)
    {
        var known = files.Where(f => f.Report?.Format is not null).Select(f => (f.Name, Signature: Signature(f.Report!.Format!))).ToList();
        if (known.Count < 2)
        {
            into.Add(new Finding { Check = "set.format", Severity = Severity.Info, Message = "Fewer than two files with a format read: nothing to compare", CheckedAgainst = FormatCheckedAgainst });
            return;
        }

        var all = known.SelectMany(k => k.Signature).ToList();
        var common = all.Select(p => p.What).Distinct().ToDictionary(
            what => what,
            what => all.Where(p => p.What == what).GroupBy(p => p.Value).OrderByDescending(g => g.Count()).First().Key,
            StringComparer.Ordinal);
        int differing = 0;
        foreach (var (name, signature) in known)
        {
            var own = signature.ToDictionary(p => p.What, p => p.Value, StringComparer.Ordinal);
            var differences = common
                .Where(c => own.GetValueOrDefault(c.Key) != c.Value)
                .Select(c => $"{c.Key} {own.GetValueOrDefault(c.Key) ?? "none"} (the set's: {c.Value})")
                .ToList();
            if (differences.Count == 0)
                continue;
            differing++;
            into.Add(new Finding { Check = "set.format", Severity = Severity.Failure, Message = $"{name}: {string.Join("; ", differences)}", CheckedAgainst = FormatCheckedAgainst });
        }

        if (differing == 0)
        {
            string words = string.Join(", ", known[0].Signature.Where(p => p.What is not ("picture" or "audio tracks")).Select(p => p.Value));
            into.Add(new Finding { Check = "set.format", Severity = Severity.Pass, Message = Invariant($"The {known.Count} files have one format: {words}"), CheckedAgainst = FormatCheckedAgainst });
        }
    }

    // Levels that match: each file's RMS level against the median of the set's, at the report's 0.1 dB.
    static void Levels(List<Finding> into, IReadOnlyList<SetFile> files, double? tolerance)
    {
        var levels = files.Where(f => f.Rms is not null).Select(f => (f.Name, Rms: f.Rms!.Value)).ToList();
        if (levels.Count < 2)
            return;
        var sorted = levels.Select(l => l.Rms).Order().ToList();
        double median = sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;
        string range = Invariant($"median {median:0.0} dBFS, {sorted[0]:0.0} to {sorted[^1]:0.0} dBFS");
        if (tolerance is not { } within)
        {
            into.Add(new Finding { Check = "set.level", Severity = Severity.Info, Message = Invariant($"The {levels.Count} files' RMS levels: {range}"), Value = sorted[^1] - sorted[0], Unit = "dB", CheckedAgainst = LevelCheckedAgainst });
            return;
        }

        int outside = 0;
        foreach (var (name, rms) in levels)
        {
            // From the figures as shown, so the line's arithmetic holds.
            double away = Math.Round(Shown(rms) - Shown(median), 1);
            if (Math.Abs(away) <= within)
                continue;
            outside++;
            into.Add(new Finding
            {
                Check = "set.level",
                Severity = Severity.Warning,
                Message = Invariant($"{name}: RMS level {rms:0.0} dBFS, {Math.Abs(away):0.0} dB {(away < 0 ? "below" : "above")} the set's median, {median:0.0} dBFS (within {within:0.#} dB)"),
                Value = rms - median,
                Unit = "dB",
                CheckedAgainst = LevelCheckedAgainst,
            });
        }

        if (outside == 0)
        {
            into.Add(new Finding { Check = "set.level", Severity = Severity.Pass, Message = Invariant($"The {levels.Count} files' RMS levels are within {within:0.#} dB of their median ({range})"), Value = sorted[^1] - sorted[0], Unit = "dB", CheckedAgainst = LevelCheckedAgainst });
        }
    }

    static double Shown(double level) => Math.Round(level, 1, MidpointRounding.AwayFromZero);

    static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
