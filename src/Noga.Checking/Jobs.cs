using System.Threading.Channels;

namespace Noga.Checking;

/// <summary>Where a check is: waiting its turn, being measured, done (with its report) or failed (with why).</summary>
public enum JobState
{
    /// <summary>Waiting for the checks before it.</summary>
    Queued,

    /// <summary>Being measured.</summary>
    Measuring,

    /// <summary>Checked: its verdict and reports are there.</summary>
    Done,

    /// <summary>Couldn't be checked.</summary>
    Failed,
}

/// <summary>One file's check in the web UI.</summary>
public sealed record Job(int Id, string File, JobState State, DateTimeOffset Added)
{
    /// <summary>How far its measuring has got, from 0 to 1, while measuring; null before the first report.</summary>
    public double? Progress { get; init; }

    /// <summary>When its measuring started.</summary>
    public DateTimeOffset? Started { get; init; }

    /// <summary>When it ended.</summary>
    public DateTimeOffset? Finished { get; init; }

    /// <summary>The report's verdict, when done.</summary>
    public Severity? Verdict { get; init; }

    /// <summary>The command line's exit code for it, when done.</summary>
    public int? ExitCode { get; init; }

    /// <summary>Why it failed.</summary>
    public string? Error { get; init; }

    /// <summary>Why it failed, the file named rather than its whole path, which the row already shows.</summary>
    public string? ShortError => Error?.Replace(File, Path.GetFileName(File), StringComparison.Ordinal);

    /// <summary>When done with failures or warnings, the first of them and how many more (<see cref="QcReport.Reason"/>).</summary>
    public string? Reason { get; init; }

    /// <summary>The folder the file is in.</summary>
    public string Folder => Path.GetDirectoryName(File) ?? "";

    /// <summary>How long it has been measuring, or took; null before it starts.</summary>
    public TimeSpan? Elapsed(DateTimeOffset now) => Started is { } started ? (Finished ?? now) - started : null;

    /// <summary>
    /// How long its measuring will take still, from how far it has got in the time so far; null until it is 2% in and
    /// 5 s along, when the guess would mean little.
    /// </summary>
    public TimeSpan? Remaining(DateTimeOffset now) =>
        State == JobState.Measuring && Progress is >= 0.02 and < 1 and { } done && Elapsed(now) is { TotalSeconds: >= 5 } elapsed
            ? elapsed * ((1 - done) / done)
            : null;

    /// <summary>How long is left as a row says it: "under a minute left", "about 9 min left".</summary>
    public static string FormatRemaining(TimeSpan remaining) =>
        remaining < TimeSpan.FromMinutes(1) ? "under a minute left" : $"about {(int)Math.Ceiling(remaining.TotalMinutes)} min left";

    /// <summary>
    /// Where it stands, for the line under its name: "measuring 42% · about 9 min left", "measuring 12 s" before the
    /// first report, "took 3 min 05 s" once done; empty while queued.
    /// </summary>
    public string Time(DateTimeOffset now)
    {
        if (Elapsed(now) is not { } elapsed)
            return "";
        if (State != JobState.Measuring)
            return $"took {Format(elapsed)}";
        if (Progress is not { } done)
            return $"measuring {Format(elapsed)}";
        string percent = $"measuring {Math.Floor(done * 100).ToString(System.Globalization.CultureInfo.InvariantCulture)}%";
        return Remaining(now) is { } left ? $"{percent} · {FormatRemaining(left)}" : percent;
    }

    /// <summary>A duration as a row shows it: "12 s", "3 min 05 s".</summary>
    public static string Format(TimeSpan duration)
    {
        int seconds = Math.Max(0, (int)Math.Round(duration.TotalSeconds));
        return seconds < 60 ? $"{seconds} s" : $"{seconds / 60} min {seconds % 60:00} s";
    }

    /// <summary>The HTML report's path, when done.</summary>
    public string? Html { get; init; }

    /// <summary>The JSON report's path, when done.</summary>
    public string? Json { get; init; }

    /// <summary>Where it stands, as the web UI and the app show it (<see cref="StatusStyle"/>).</summary>
    public Status Status => State switch
    {
        JobState.Queued => Status.Queued,
        JobState.Measuring => Status.Measuring,
        JobState.Failed => Status.Unreadable,
        _ => StatusStyle.Of(Verdict ?? Severity.Pass),
    };
}

/// <summary>
/// The checks asked for, run one at a time in the order asked (a check already keeps the cores busy), each with its
/// reports beside its file as the command line writes them. Anyone waiting on <see cref="Changed"/> hears of each change.
/// </summary>
public sealed class Jobs : IDisposable
{
    readonly Lock _lock = new();
    readonly List<Job> _jobs = [];
    readonly Channel<int> _queue = Channel.CreateUnbounded<int>();
    readonly CancellationTokenSource _stopping = new();
    readonly Func<string, IProgress<double>, CancellationToken, CheckResult> _check;
    readonly Task _worker;
    TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs checks with <paramref name="check"/> (<see cref="Checker.Run"/>'s, writing beside the file).</summary>
    public Jobs(Func<string, CancellationToken, CheckResult> check)
        : this((file, _, token) => check(file, token))
    {
    }

    /// <summary>Runs checks with <paramref name="check"/>, which tells its progress (0 to 1) to the job's row.</summary>
    public Jobs(Func<string, IProgress<double>, CancellationToken, CheckResult> check)
    {
        _check = check;
        _worker = Task.Run(Work);
    }

    /// <summary>Completes at the next change to any job (then ask again).</summary>
    public Task Changed
    {
        get
        {
            lock (_lock)
                return _changed.Task;
        }
    }

    /// <summary>Every job, the newest first.</summary>
    public IReadOnlyList<Job> All
    {
        get
        {
            lock (_lock)
                return [.. Enumerable.Reverse(_jobs)];
        }
    }

    /// <summary>The job with this id; null if there is none.</summary>
    public Job? Get(int id)
    {
        lock (_lock)
            return id >= 1 && id <= _jobs.Count ? _jobs[id - 1] : null;
    }

    /// <summary>
    /// Queues a check of <paramref name="file"/> (a full path); when one of it is already queued or measuring, returns
    /// that one rather than checking it twice.
    /// </summary>
    public Job Add(string file)
    {
        Job job;
        lock (_lock)
        {
            if (_jobs.FindLast(j => j.File == file && j.State is JobState.Queued or JobState.Measuring) is { } waiting)
                return waiting;
            job = new Job(_jobs.Count + 1, file, JobState.Queued, DateTimeOffset.Now);
            _jobs.Add(job);
            Signal();
        }

        _queue.Writer.TryWrite(job.Id);
        return job;
    }

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _stopping.Cancel();
        try
        {
            _worker.Wait(TimeSpan.FromSeconds(10));
        }
        catch (AggregateException)
        {
        }

        _stopping.Dispose();
    }

    async Task Work()
    {
        try
        {
            await foreach (int id in _queue.Reader.ReadAllAsync(_stopping.Token).ConfigureAwait(false))
            {
                var job = Update(id, j => j with { State = JobState.Measuring, Started = DateTimeOffset.Now });
                try
                {
                    // Each whole percent, so the rows and the events aren't redrawn more than a hundred times a check.
                    var progress = new ReportNow(fraction =>
                    {
                        double percent = Math.Floor(fraction * 100) / 100;
                        if (Get(id)?.Progress != percent)
                            Update(id, j => j.State == JobState.Measuring ? j with { Progress = percent } : j);
                    });
                    var result = _check(job.File, progress, _stopping.Token);
                    Update(id, j => j with
                    {
                        State = JobState.Done,
                        Finished = DateTimeOffset.Now,
                        Verdict = result.Report.Verdict,
                        ExitCode = result.Report.ExitCode(),
                        Reason = result.Report.Reason,
                        Html = result.Html,
                        Json = result.Json,
                    });
                }
                catch (CheckException e)
                {
                    Update(id, j => j with { State = JobState.Failed, Finished = DateTimeOffset.Now, Error = e.Message });
                }
                catch (IOException e)
                {
                    Update(id, j => j with { State = JobState.Failed, Finished = DateTimeOffset.Now, Error = e.Message });
                }
                catch (UnauthorizedAccessException e)
                {
                    Update(id, j => j with { State = JobState.Failed, Finished = DateTimeOffset.Now, Error = e.Message });
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // Anything else is still this one file's: the queue goes on to the next.
                    Update(id, j => j with { State = JobState.Failed, Finished = DateTimeOffset.Now, Error = $"{e.GetType().Name}: {e.Message}" });
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    Job Update(int id, Func<Job, Job> change)
    {
        lock (_lock)
        {
            var job = _jobs[id - 1] = change(_jobs[id - 1]);
            Signal();
            return job;
        }
    }

    // Under the lock: wakes everyone waiting and starts a new wait.
    void Signal()
    {
        var changed = _changed;
        _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        changed.SetResult();
    }
}

// Reports at once, on the reporting thread (Progress<T> would post them to the thread pool, out of order).
file sealed class ReportNow(Action<double> report) : IProgress<double>
{
    public void Report(double value) => report(value);
}
