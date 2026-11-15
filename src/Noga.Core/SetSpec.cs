namespace Noga;

/// <summary>
/// What a profile asks of a folder's files as one set (a profile file's "set"; a book's chapters under acx:
/// docs/decisions/2026-11-15-set-check.md). A profile without one checks a folder's files each on its own.
/// </summary>
/// <param name="SameFormat">Whether every file must have the same format (<see cref="SetReport.FormatCheckedAgainst"/>); a file that differs fails the set.</param>
/// <param name="LevelTolerance">
/// How far a file's RMS level (level.rms) may be from the median of the set's (dB); further is a warning. Null: the
/// levels are reported, not judged.
/// </param>
public sealed record SetSpec(bool SameFormat = true, double? LevelTolerance = 2);
