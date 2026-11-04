using Noga.Checking;

namespace Noga.App;

/// <summary>
/// What the window shows, apart from the window: the checks asked for, run two at a time (<see cref="Jobs"/>) with the command line's
/// Checker (reports beside each file), and the profile they are judged by. A check takes the profile chosen when it
/// starts, so changing it applies to the checks still waiting.
/// </summary>
sealed class AppModel : IDisposable
{
    readonly Jobs _jobs;

    /// <summary>Checks with <paramref name="check"/> (<see cref="Checker.Run"/> but for tests).</summary>
    public AppModel(Func<string, Profile, CancellationToken, CheckResult> check)
        : this((file, profile, _, token) => check(file, profile, token))
    {
    }

    /// <summary>Checks with <paramref name="check"/>, which tells its progress (0 to 1) to the check's row.</summary>
    public AppModel(Func<string, Profile, IProgress<double>, CancellationToken, CheckResult> check)
    {
        ArgumentNullException.ThrowIfNull(check);
        _jobs = new Jobs((file, progress, token) => check(file, Profile, progress, token));
    }

    /// <summary>The profile the checks still to start are judged by.</summary>
    public Profile Profile { get; set; } = Profile.EbuR128;

    /// <summary>Every check, the newest first.</summary>
    public IReadOnlyList<Job> Jobs => _jobs.All;

    /// <summary>Completes at the next change to any check.</summary>
    public Task Changed => _jobs.Changed;

    /// <summary>
    /// Queues a check of each file <paramref name="paths"/> name, and of the media files in each folder they name
    /// (subfolders too); returns how many were queued.
    /// </summary>
    public int Add(IEnumerable<string> paths)
    {
        int added = 0;
        foreach (string file in MediaFiles.In(paths))
        {
            _jobs.Add(file);
            added++;
        }

        return added;
    }

    public void Dispose() => _jobs.Dispose();
}
