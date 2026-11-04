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
}
