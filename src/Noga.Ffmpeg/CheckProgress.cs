namespace Noga.Ffmpeg;

/// <summary>
/// How far a check has got, from 0 to 1, for a job's row (docs/decisions/2026-11-04-progress.md): each stage of the
/// measuring has a weight, its share of the time (docs/notes/performance.md: the picture pass most of it, the shots'
/// second decode a fifth, the sound almost nothing), and reports how far into the file it is. Reports go to
/// <paramref name="report"/> when they have moved by half a percent or more, so a row isn't redrawn each frame.
/// </summary>
public sealed class CheckProgress(IProgress<double>? report)
{
    double _done, _weight, _last = -1;

    /// <summary>Starts the next stage, <paramref name="weight"/> of the whole; the stage before counts as done.</summary>
    public void Begin(double weight)
    {
        _done += _weight;
        _weight = weight;
        At(0);
    }

    /// <summary>How far into the current stage the check is, from 0 to 1.</summary>
    public void At(double fraction)
    {
        if (report is null || double.IsNaN(fraction))
            return;
        double value = Math.Clamp(_done + (_weight * Math.Clamp(fraction, 0, 1)), 0, 1);
        if (value - _last < 0.005 && value < 1)
            return;
        _last = value;
        report.Report(value);
    }

    /// <summary>A stage's time in the file against its duration, as a fraction for <see cref="At"/>.</summary>
    public void AtSeconds(double seconds, double duration) => At(duration > 0 ? seconds / duration : 0);
}

/// <summary>An <see cref="IProgress{T}"/> that reports at once, on the thread that reports (unlike <see cref="Progress{T}"/>).</summary>
public sealed class ReportNow<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
