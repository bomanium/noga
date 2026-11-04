using Noga.Checking;

namespace Noga.Tests;

// One look for statuses in the report, the web UI and the app (docs/decisions/2026-11-04-status-style.md).
public class StatusStyleTests
{
    // Each badge's text against its tint is WCAG AA (4.5:1), light and dark.
    [Fact]
    public void EveryToneIsReadableLightAndDark()
    {
        foreach (string tone in StatusStyle.ToneNames)
        {
            foreach (bool dark in (bool[])[false, true])
            {
                var colours = StatusStyle.Colours(tone, dark);
                double contrast = StatusStyle.Contrast(colours.Text, colours.Background);
                Assert.True(contrast >= 4.5, $"{tone} ({(dark ? "dark" : "light")}): {contrast:0.00}:1");
            }
        }
    }

    [Fact]
    public void ContrastIsWcags()
    {
        Assert.Equal(21, StatusStyle.Contrast("#000000", "#ffffff"), 6);
        Assert.Equal(4.48, StatusStyle.Contrast("#777777", "#ffffff"), 2);
    }

    // A file with only information passed; "Couldn't read" isn't a failure's red.
    [Fact]
    public void VerdictsAndJobsReadTheSameEverywhere()
    {
        Assert.Equal(Status.Passed, StatusStyle.Of(Severity.Info));
        Assert.Equal("Passed with warnings", StatusStyle.Label(StatusStyle.Of(Severity.Warning)));
        Assert.Equal("Failed", StatusStyle.Label(StatusStyle.Of(Severity.Failure)));
        Assert.Equal("neutral", StatusStyle.Tone(Status.Unreadable));
        Assert.Equal("failure", StatusStyle.Tone(Status.Failed));

        var job = new Job(1, "a.mov", JobState.Done, DateTimeOffset.UnixEpoch) { Verdict = Severity.Warning };
        Assert.Equal(Status.PassedWithWarnings, job.Status);
        Assert.Equal(Status.Unreadable, (job with { State = JobState.Failed }).Status);
        Assert.Equal(Status.Queued, (job with { State = JobState.Queued }).Status);
    }

    [Fact]
    public void CssHasEveryToneLightAndDark()
    {
        string css = StatusStyle.Css;

        foreach (string tone in StatusStyle.ToneNames)
        {
            Assert.Contains($".{tone} {{ background: var(--{tone}-bg); color: var(--{tone}); }}", css, StringComparison.Ordinal);
            Assert.Contains($"--{tone}: {StatusStyle.Colours(tone, dark: true).Text};", css, StringComparison.Ordinal);
        }

        Assert.Contains("@media screen and (prefers-color-scheme: dark)", css, StringComparison.Ordinal);
    }
}
