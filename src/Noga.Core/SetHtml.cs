using System.Globalization;
using System.Text;

namespace Noga;

/// <summary>
/// A set's HTML report (<see cref="SetReport"/>): one self-contained page in the file report's look. The verdict with
/// a count of the files, what was found across them, then each file with its verdict, format, levels and the reason it
/// didn't simply pass, linked to its own report beside this one.
/// </summary>
public static class SetHtml
{
    const string Style = """
        col.sev { width: 11.5em; } col.file { width: 30%; }
        td .against { overflow-wrap: anywhere; }
        td.num { font-variant-numeric: tabular-nums; white-space: nowrap; }
        col.num { width: 8em; }
        @media (max-width: 40rem) { td.num[data-label]::before { content: attr(data-label) " "; color: var(--muted); } td.num:empty { display: none; } }
        """;

    /// <summary>The set's report as an HTML page, titled <paramref name="name"/> (the folder's).</summary>
    public static string Write(SetReport report, string name)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(name);
        var html = new StringBuilder();
        string label = report.Unreadable > 0
            ? Invariant($"Couldn't read {report.Unreadable} of {report.Files.Count} files")
            : StatusStyle.Label(report.Status);
        html.Append(CultureInfo.InvariantCulture, $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>QC: {Encode(name)}</title>
            <style>
            {ReportHtml.Style}
            {Style}
            {StatusStyle.Css}</style>
            </head>
            <body>
            <header>
            <h1>{Encode(name)}</h1>
            <p class="meta">Checked {Encode(CheckedFile.Time(report.Checked))} · Profile {Encode(report.Profile)} · {Encode(Product.Name)} {Encode(Product.Version)}</p>
            <p class="meta">{Encode(report.Folder)}</p>
            <div class="verdict {StatusStyle.Tone(report.Status)}"><strong>{Encode(label)}</strong><span>{Encode(report.Counts)}</span></div>
            </header>

            """);
        if (report.Findings.Count > 0)
        {
            html.Append("""
                <h2>The files together</h2>
                <table>
                <colgroup><col class="sev"><col class="check"><col></colgroup>
                <thead><tr><th>Result</th><th>Check</th><th>Finding</th></tr></thead>
                <tbody>

                """);

            // Failures, then warnings, then the rest, each in the files' order.
            foreach (var finding in report.Findings.OrderByDescending(f => f.Severity >= Severity.Warning ? f.Severity : Severity.Pass))
            {
                html.Append(CultureInfo.InvariantCulture, $"""
                    <tr><td class="sev"><span class="sev {StatusStyle.Tone(finding.Severity)}">{finding.Severity}</span></td><td class="check">{Encode(finding.Check)}</td><td>{Encode(finding.Message)}<span class="against">Checked against {Encode(finding.CheckedAgainst)}</span></td></tr>

                    """);
            }

            html.Append("""
                </tbody>
                </table>

                """);
        }

        html.Append("""
            <h2>The files</h2>
            <table>
            <colgroup><col class="sev"><col class="file"><col><col class="num"><col class="num"></colgroup>
            <thead><tr><th>Result</th><th>File</th><th>Finding</th><th>RMS</th><th>Peak</th></tr></thead>
            <tbody>

            """);
        foreach (var file in report.Files)
        {
            string finding = file.Report is { } own ? Encode(own.Reason ?? "") : Encode(file.Error ?? "");
            string link = file.Report is null ? Encode(file.Name) : $"<a href=\"{Uri.EscapeDataString(file.Name)}.qc.html\">{Encode(file.Name)}</a>";
            html.Append(CultureInfo.InvariantCulture, $"""
                <tr><td class="sev"><span class="sev {StatusStyle.Tone(file.Status)}">{Encode(StatusStyle.Label(file.Status))}</span></td><td class="check">{link}</td><td>{finding}<span class="against">{Encode(Words(file.Report?.Format, file.Name))}</span></td>{Level("RMS", file.Rms)}{Level("Peak", file.Peak)}</tr>

                """);
        }

        html.Append("""
            </tbody>
            </table>
            </body>
            </html>

            """);
        return html.ToString();
    }

    // "MP3, 44.1 kHz, mono · 12 min 34 s": the sound (and picture) in words and the duration.
    static string Words(MediaFormat? format, string file) => format is null
        ? ""
        : string.Join(" · ", FormatWords.Rows(format, file).Where(r => r.Label != "Container").Select(r => r.Text));

    static string Level(string label, double? level) => level is { } dB
        ? Invariant($"<td class=\"num\" data-label=\"{label}\">{dB:0.0} dBFS</td>")
        : "<td class=\"num\"></td>";

    static string Encode(string text) => ReportHtml.Encode(text);

    static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
