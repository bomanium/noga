namespace Noga;

/// <summary>How a finding counts: <see cref="Info"/> is reported only; <see cref="Warning"/> and <see cref="Failure"/> set the exit code.</summary>
public enum Severity
{
    /// <summary>The check passed.</summary>
    Pass,

    /// <summary>Reported, not judged (where the bars are, the loudness range, the head's black).</summary>
    Info,

    /// <summary>Worth a look before delivery.</summary>
    Warning,

    /// <summary>Outside the profile.</summary>
    Failure,
}

/// <summary>
/// One figure of the report: which check, how it came out, where in the file (seconds and timecode), its value, and how
/// it is checked against FFmpeg (CLAUDE.md: every figure says so).
/// </summary>
public sealed record Finding
{
    /// <summary>The check's id, e.g. "loudness.integrated", "black".</summary>
    public required string Check { get; init; }

    /// <summary>How it came out.</summary>
    public required Severity Severity { get; init; }

    /// <summary>One line for the reader.</summary>
    public required string Message { get; init; }

    /// <summary>The measured value, when the check has one (LUFS, dBTP, LU, Hz, dBFS, seconds).</summary>
    public double? Value { get; init; }

    /// <summary>The value's unit.</summary>
    public string? Unit { get; init; }

    /// <summary>Where it starts, when it is a span.</summary>
    public Position? Start { get; init; }

    /// <summary>Where it ends, when it is a span.</summary>
    public Position? End { get; init; }

    /// <summary>The audio track it is about (from 1), when the file has several and every one is judged; null otherwise.</summary>
    public int? Track { get; init; }

    /// <summary>The EBU QC catalogue's item this check is (<see cref="EbuQcItem.For"/>); null when it has none.</summary>
    public EbuQcItem? EbuQc => EbuQcItem.For(Check);

    /// <summary>The FFmpeg filter, options and tolerance this figure is checked against.</summary>
    public required string CheckedAgainst { get; init; }
}

/// <summary>A place in the file: seconds from the first frame and its SMPTE timecode.</summary>
public readonly record struct Position(double Seconds, string Timecode);

/// <summary>A file's QC report: the profile it was judged by, and its findings in order.</summary>
public sealed record QcReport
{
    /// <summary>Exit code: no warnings or failures.</summary>
    public const int Passed = 0;

    /// <summary>Exit code: at least one failure (or a warning with warningsFail).</summary>
    public const int Failed = 1;

    /// <summary>Exit code: the command line couldn't be understood or the file couldn't be read.</summary>
    public const int Error = 2;

    /// <summary>Exit code: warnings, no failures.</summary>
    public const int Warnings = 3;

    /// <summary>The profile's name.</summary>
    public required string Profile { get; init; }

    /// <summary>The timecode the file starts at.</summary>
    public required string StartTimecode { get; init; }

    /// <summary>Whether timecodes are drop-frame.</summary>
    public required bool DropFrame { get; init; }

    /// <summary>The findings, in the profile's order.</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>
    /// Why it didn't simply pass, in one line for a list of checks: the first failure's message (else the first
    /// warning's), and how many more failures and warnings there are; null when there are none.
    /// </summary>
    public string? Reason
    {
        get
        {
            var issues = Findings.Where(f => f.Severity >= Severity.Warning).OrderByDescending(f => f.Severity).ToList();
            return issues.Count switch
            {
                0 => null,
                1 => issues[0].Message,
                _ => $"{issues[0].Message} (and {issues.Count - 1} more)",
            };
        }
    }

    /// <summary>The most severe finding's severity.</summary>
    public Severity Verdict => Findings.Count == 0 ? Severity.Pass : Findings.Max(f => f.Severity);

    /// <summary>The exit code for pipelines (docs/decisions/2026-10-31-exit-codes-head-tail-timecode.md): 0 pass, 1 failure, 3 warnings; with <paramref name="warningsFail"/>, warnings are 1.</summary>
    public int ExitCode(bool warningsFail = false) => Verdict switch
    {
        Severity.Failure => Failed,
        Severity.Warning => warningsFail ? Failed : Warnings,
        _ => Passed,
    };
}
