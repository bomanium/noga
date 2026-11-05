namespace Noga.Ffmpeg;

/// <summary>
/// How far a check has got, from 0 to 1, for a job's row (docs/decisions/2026-11-04-progress.md): each stage of the
/// measuring has a weight, its share of the time (docs/notes/performance.md), and reports how far into the file it is.
/// A stage can be made of parts that run at the same time (<see cref="Together"/>: the picture pass, the shots and the
/// sound), each with its own weight. Reports go to <paramref name="report"/> when they have moved by half a percent or
/// more, so a row isn't redrawn each frame; parts may report from their own threads.
/// </summary>
public sealed class CheckProgress(IProgress<double>? report)
{
    readonly Lock _lock = new();
    double _done, _last = -1;
    int _stage;
    double[] _weights = [], _fractions = [];

    /// <summary>Starts the next stage, <paramref name="weight"/> of the whole; the stage before counts as done.</summary>
    public void Begin(double weight) => Together(weight);

    /// <summary>
    /// Starts the next stage, made of parts that run at the same time, each <paramref name="weights"/>' share of the
    /// whole; the stage before counts as done. Returns each part's reporter (0 to 1 into that part).
    /// </summary>
    public IProgress<double>[] Together(params double[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        lock (_lock)
        {
            _done += _weights.Sum();
            _weights = [.. weights];
            _fractions = new double[weights.Length];
            int stage = ++_stage;
            Report();
            return [.. Enumerable.Range(0, weights.Length).Select(i => new ReportNow<double>(fraction => At(stage, i, fraction)))];
        }
    }

    /// <summary>How far into the current stage the check is, from 0 to 1 (its first part's, when it has several).</summary>
    public void At(double fraction)
    {
        int stage;
        lock (_lock)
            stage = _stage;
        At(stage, 0, fraction);
    }

    /// <summary>A stage's time in the file against its duration, as a fraction for <see cref="At(double)"/>.</summary>
    public void AtSeconds(double seconds, double duration) => At(duration > 0 ? seconds / duration : 0);

    // A part of an earlier stage, reporting late, moves nothing.
    void At(int stage, int part, double fraction)
    {
        if (report is null || double.IsNaN(fraction))
            return;
        lock (_lock)
        {
            if (stage != _stage || part >= _fractions.Length)
                return;
            _fractions[part] = Math.Clamp(fraction, 0, 1);
            Report();
        }
    }

    // Under the lock.
    void Report()
    {
        if (report is null)
            return;
        double value = _done;
        for (int i = 0; i < _weights.Length; i++)
            value += _weights[i] * _fractions[i];
        value = Math.Clamp(value, 0, 1);
        if (value - _last < 0.005 && value < 1)
            return;
        _last = value;
        report.Report(value);
    }
}

/// <summary>An <see cref="IProgress{T}"/> that reports at once, on the thread that reports (unlike <see cref="Progress{T}"/>).</summary>
public sealed class ReportNow<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
