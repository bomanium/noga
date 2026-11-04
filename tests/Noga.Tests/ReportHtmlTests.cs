namespace Noga.Tests;

// The HTML report leads with the answer: the verdict and its counts, the failures and warnings, then the other checks;
// the format in the header and the shots folded away (docs/decisions/2026-11-04-report-layout.md).
public class ReportHtmlTests
{
    static string Html(params Finding[] findings) =>
        ReportHtml.Write(new QcReport { Profile = "ebu-r128", StartTimecode = "10:00:00:00", DropFrame = false, Findings = findings }, "programme.mov");

    static Finding Found(string check, Severity severity, string message) =>
        new() { Check = check, Severity = severity, Message = message, CheckedAgainst = "ffmpeg" };

    [Fact]
    public void PutsTheVerdictAndIssuesBeforeTheOtherChecks()
    {
        string html = Html(
            Found("format", Severity.Info, "Format: prores"),
            Found("bars", Severity.Info, "Colour bars for 5.00 s"),
            Found("black", Severity.Warning, "Black mid-programme"),
            Found("tone", Severity.Pass, "Line-up tone"),
            Found("loudness.integrated", Severity.Failure, "Integrated loudness -34.8 LUFS"));

        Assert.Contains("<strong>Failed</strong><span>1 failure · 1 warning · 1 passed · 2 for information</span>", html, StringComparison.Ordinal);
        int verdict = html.IndexOf("<strong>Failed", StringComparison.Ordinal);
        int failure = html.IndexOf("Integrated loudness", StringComparison.Ordinal);
        int warning = html.IndexOf("Black mid-programme", StringComparison.Ordinal);
        int others = html.IndexOf("<h2>The other checks</h2>", StringComparison.Ordinal);
        int bars = html.IndexOf("Colour bars", StringComparison.Ordinal);
        Assert.True(verdict < failure && failure < warning && warning < others && others < bars);
        Assert.Contains("<p class=\"format\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<td class=\"check\">format</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void FoldsTheShotsAway()
    {
        string html = Html(
            Found("tone", Severity.Pass, "Line-up tone"),
            Found("shots", Severity.Info, "2 shots, the shortest 3.00 s"),
            Found("shot", Severity.Info, "Shot 1, 5.00 s") with { Start = new Position(0, "10:00:00:00"), End = new Position(5, "10:00:05:00") },
            Found("shot", Severity.Info, "Shot 2, 3.00 s"));

        Assert.Contains("<summary>2 shots, the shortest 3.00 s", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("<details>", StringComparison.Ordinal) < html.IndexOf("Shot 1, 5.00 s", StringComparison.Ordinal));
        Assert.Contains("<td class=\"tc\" data-label=\"From\">10:00:00:00</td>", html, StringComparison.Ordinal);
        Assert.Contains("1 passed · 1 for information", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>Warnings</h2>", html, StringComparison.Ordinal);
    }

    // A report passed on says which file it covers and when it was checked (docs/decisions/2026-11-04-checked-file.md).
    [Fact]
    public void SaysWhichFileAndWhen()
    {
        var at = new DateTimeOffset(2026, 11, 4, 14, 32, 0, TimeSpan.FromHours(1));
        var report = new QcReport
        {
            Profile = "ebu-r128",
            StartTimecode = "10:00:00:00",
            DropFrame = false,
            Findings = [Found("tone", Severity.Pass, "Line-up tone")],
            Source = new CheckedFile("/deliveries/ep01.mov", 94_201_579, at.AddHours(-6), at),
        };

        string html = ReportHtml.Write(report, "ep01.mov");

        Assert.Contains("<p class=\"meta\">Checked 2026-11-04 14:32 +01:00 · Profile ebu-r128", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"meta\">/deliveries/ep01.mov · 94.2 MB (94,201,579 bytes) · modified 2026-11-04 08:32 +01:00</p>", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"meta\">Profile ebu-r128", Html(Found("tone", Severity.Pass, "Line-up tone")), StringComparison.Ordinal);
        Assert.Equal("512 bytes", (report.Source with { Size = 512 }).SizeText);
    }

    // In words in the header, a point "at" its timecode across From and To, and the shot list in print.
    [Fact]
    public void FormatInWordsAPointAndShotsInPrint()
    {
        var format = new MediaFormat("mxf", 20, new VideoFormat("dnxhd", "DNXHR SQ", 1920, 1080, "25/1", "progressive", "yuv422p", 8, "bt709", "bt709", "bt709", "tv", "16:9"), []);
        var report = new QcReport
        {
            Profile = "ebu-r128",
            StartTimecode = "10:00:00:00",
            DropFrame = false,
            Format = format,
            Findings =
            [
                Found("format", Severity.Info, "Format: dnxhd (DNXHR SQ) 1920x1080"),
                Found("range", Severity.Pass, "Pixels outside broadcast range") with { Start = new Position(12.4, "10:00:12:10") },
                Found("shots", Severity.Info, "1 shot"),
                Found("shot", Severity.Info, "Shot 1, 20.00 s"),
            ],
        };

        string html = ReportHtml.Write(report, "ep01.mxf");

        Assert.Contains("<dt>Video</dt><dd>Avid DNxHR SQ, 1920&#215;1080, 25 fps progressive, 4:2:2 8-bit, BT.709, limited range, 16:9</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Container</dt><dd>MXF</dd>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Format: dnxhd", html, StringComparison.Ordinal);
        Assert.Contains("<td class=\"tc\" colspan=\"2\">at 10:00:12:10</td>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"shots-print\"><h2>1 shot</h2>", html, StringComparison.Ordinal);
        Assert.Equal(2, html.Split("Shot 1, 20.00 s").Length - 1);
    }
}
