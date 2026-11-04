using Noga.Checking;
using Noga.Ffmpeg;

namespace Noga.Tests;

// How far a check has got, for a job's row: "measuring 42% · about 9 min left" (docs/decisions/2026-11-04-progress.md).
public class ProgressTests : IClassFixture<Samples>
{
    readonly Samples _samples;

    public ProgressTests(Samples samples) => _samples = samples;

    [Fact]
    public void StagesAddUpByTheirWeights()
    {
        var seen = new List<double>();
        var stages = new CheckProgress(new ReportNow<double>(seen.Add));

        stages.Begin(0.8);
        stages.AtSeconds(10, 20);
        stages.At(0.501);
        stages.Begin(0.2);
        stages.At(0.5);
        stages.At(1);

        Assert.Equal([0, 0.4, 0.8, 0.9, 1], seen.Select(v => Math.Round(v, 6)));
    }

    [Fact]
    public void ARowSaysThePercentAndTheTimeLeft()
    {
        var started = new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
        var job = new Job(1, "a.mov", JobState.Measuring, started) { Started = started };

        Assert.Equal("measuring 4 s", job.Time(started.AddSeconds(4)));
        Assert.Equal("measuring 1%", (job with { Progress = 0.01 }).Time(started.AddSeconds(30)));
        Assert.Equal("measuring 25% · about 3 min left", (job with { Progress = 0.25 }).Time(started.AddSeconds(60)));
        Assert.Equal("measuring 90% · under a minute left", (job with { Progress = 0.9 }).Time(started.AddSeconds(90)));
        Assert.Equal("took 2 min 00 s", (job with { State = JobState.Done, Finished = started.AddMinutes(2) }).Time(started.AddHours(1)));
        Assert.Equal("", (job with { State = JobState.Queued, Started = null }).Time(started));
    }

    [Fact]
    public async Task JobsKeepEachPercent()
    {
        using var release = new ManualResetEventSlim();
        using var reported = new ManualResetEventSlim();
        using var jobs = new Jobs((file, progress, token) =>
        {
            progress.Report(0.426);
            reported.Set();
            release.Wait(token);
            throw new CheckException($"can't read {file}");
        });

        jobs.Add("a.mov");
        Assert.True(reported.Wait(TimeSpan.FromSeconds(30)));
        Assert.Equal(0.42, jobs.All.Single().Progress);
        release.Set();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for (var changed = jobs.Changed; jobs.All.Any(j => j.State is JobState.Queued or JobState.Measuring); changed = jobs.Changed)
            await changed.WaitAsync(timeout.Token);
    }

    // On both decoding paths, a real check's progress only goes up, in many steps, and ends at 100%.
    [Theory]
    [InlineData("programme_prores.mov")]
    [InlineData("programme_h264.mp4")]
    public void ACheckClimbsToTheEnd(string name)
    {
        var seen = new List<double>();
        string folder = Directory.CreateTempSubdirectory("noga-progress-").FullName;
        try
        {
            Checker.Run(Path.Combine(_samples.Folder, name), folder, null, default, new ReportNow<double>(seen.Add));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }

        Assert.True(seen.Count >= 20, $"{seen.Count} reports");
        Assert.Equal(1, seen[^1]);
        Assert.All(seen.Zip(seen.Skip(1)), pair => Assert.True(pair.Second >= pair.First, $"{pair.First} then {pair.Second}"));
    }
}
