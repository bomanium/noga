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
    readonly Func<string, CancellationToken, CheckResult> _check;
    readonly Task _worker;
    TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs checks with <paramref name="check"/> (<see cref="Checker.Run"/>'s, writing beside the file).</summary>
    public Jobs(Func<string, CancellationToken, CheckResult> check)
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

    /// <summary>Queues a check of <paramref name="file"/> (a full path).</summary>
    public Job Add(string file)
    {
        Job job;
        lock (_lock)
        {
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
                    var result = _check(job.File, _stopping.Token);
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
