using Noga.Checking;

namespace Noga.Tests;

// What a check's row in the web UI and the app says about it: why it didn't pass, where the file is, how long it took
// (docs/decisions/2026-11-04-job-rows.md).
public class JobRowTests
{
    static Finding Found(string check, Severity severity, string message) =>
        new() { Check = check, Severity = severity, Message = message, CheckedAgainst = "ffmpeg" };

    static QcReport Report(params Finding[] findings) =>
        new() { Profile = "ebu-r128", StartTimecode = "00:00:00:00", DropFrame = false, Findings = findings };

    [Fact]
    public void TheReasonIsTheFirstFailureThenHowManyMore()
    {
        var report = Report(
            Found("bars", Severity.Info, "Colour bars for 5.00 s"),
            Found("black", Severity.Warning, "Black for 3.00 s mid-programme"),
            Found("loudness.integrated", Severity.Failure, "Integrated loudness -34.8 LUFS"),
            Found("silence", Severity.Warning, "Silence for 3.00 s mid-programme"));

        Assert.Equal("Integrated loudness -34.8 LUFS (and 2 more)", report.Reason);
        Assert.Equal("Black for 3.00 s mid-programme", Report(report.Findings[1]).Reason);
        Assert.Null(Report(report.Findings[0], Found("tone", Severity.Pass, "Line-up tone")).Reason);
    }

    [Fact]
    public void TheErrorNamesTheFileNotItsPath()
    {
        string file = Path.Combine(Path.GetTempPath(), "deliveries", "broken.mov");
        var job = new Job(1, file, JobState.Failed, DateTimeOffset.UnixEpoch) { Error = $"can't read {file}: FFmpeg (open {file}): Invalid data" };

        Assert.Equal("can't read broken.mov: FFmpeg (open broken.mov): Invalid data", job.ShortError);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "deliveries"), job.Folder);
    }

    [Fact]
    public void TheTimeIsHowLongItHasMeasuredOrTook()
    {
        var started = new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
        var job = new Job(1, "a.mov", JobState.Measuring, started) { Started = started };

        Assert.Null((job with { Started = null }).Elapsed(started));
        Assert.Equal(TimeSpan.FromSeconds(12), job.Elapsed(started.AddSeconds(12)));
        Assert.Equal(TimeSpan.FromSeconds(185), (job with { Finished = started.AddSeconds(185) }).Elapsed(started.AddHours(1)));
        Assert.Equal("12 s", Job.Format(TimeSpan.FromSeconds(12.4)));
        Assert.Equal("3 min 05 s", Job.Format(TimeSpan.FromSeconds(185)));
    }

    // Check pressed twice, or Check all over a file already queued, checks it once; once done it can be checked again.
    [Fact]
    public async Task AFileQueuedOrMeasuringIsntQueuedTwice()
    {
        using var release = new ManualResetEventSlim();
        using var jobs = new Jobs((file, token) =>
        {
            release.Wait(token);
            throw new CheckException($"can't read {file}");
        });

        var first = jobs.Add("a.mov");
        var again = jobs.Add("a.mov");
        jobs.Add("b.mov");
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(2, jobs.All.Count);

        release.Set();

        // The next change's signal is taken before looking, so a change between the two isn't missed.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for (var changed = jobs.Changed; jobs.All.Any(j => j.State is JobState.Queued or JobState.Measuring); changed = jobs.Changed)
            await changed.WaitAsync(timeout.Token);
        Assert.NotEqual(first.Id, jobs.Add("a.mov").Id);
    }

    // Two checks at once by default on 4 cores or more (docs/decisions/2026-11-04-checks-side-by-side.md): each of these
    // finishes only once the other has started, so one at a time would time out; the third waits its turn.
    [Fact]
    public async Task TwoChecksRunAtOnce()
    {
        using var both = new Barrier(2);
        int running = 0, most = 0;
        using var jobs = new Jobs((file, token) =>
        {
            int now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);
            try
            {
                if (file != "c.mov" && !both.SignalAndWait(TimeSpan.FromSeconds(30), token))
                    throw new CheckException($"{file} ran alone");
                throw new CheckException($"checked {file}");
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        },
            atATime: 2);

        jobs.Add("a.mov");
        jobs.Add("b.mov");
        jobs.Add("c.mov");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        for (var changed = jobs.Changed; jobs.All.Any(j => j.State is JobState.Queued or JobState.Measuring); changed = jobs.Changed)
            await changed.WaitAsync(timeout.Token);

        Assert.Equal(2, jobs.AtATime);
        Assert.Equal(2, most);
        Assert.All(jobs.All, j => Assert.Equal($"checked {j.File}", j.Error));
        Assert.Equal(Environment.ProcessorCount >= 4 ? 2 : 1, Jobs.DefaultAtATime);
    }

    static void InterlockedMax(ref int target, int value)
    {
        for (int seen = Volatile.Read(ref target); value > seen; seen = Volatile.Read(ref target))
        {
            if (Interlocked.CompareExchange(ref target, value, seen) == seen)
                return;
        }
    }

    // A deep folder keeps its end, which tells folders apart, rather than a start they all share.
    [Fact]
    public void ADeepFolderKeepsItsEnd()
    {
        Assert.Equal("/deliveries/ep01", Job.Shorten("/deliveries/ep01", 48));
        Assert.Equal("…/season 2/ep01/masters", Job.Shorten("/mnt/server/projects/client/the series/season 2/ep01/masters", 26));
        Assert.Equal(@"…\ep01\masters", Job.Shorten(@"D:\work\clients\series\ep01\masters", 16));
        var job = new Job(1, "/mnt/server/projects/client/the long series name/season 2/ep01/masters/ep01.mov", JobState.Queued, DateTimeOffset.UnixEpoch);
        Assert.True(job.ShortFolder.Length <= 48);
        Assert.EndsWith("/ep01/masters", job.ShortFolder, StringComparison.Ordinal);
    }
}
