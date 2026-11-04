using System.Globalization;
using System.Net;
using System.Text;

namespace Noga;

/// <summary>
/// The HTML report: one self-contained page (no scripts, no external files) for a person to read, print and pass on.
/// The answer first: the verdict with a count per severity, then the failures and warnings, then the other checks, each
/// finding with its timecodes, its value and, on a muted second line, what it is checked against and its EBU QC item.
/// The file's format is in the header, the shot list folded away at the end (docs/decisions/2026-11-04-report-layout.md).
/// Readable on a phone (rows stack), in dark mode, and printed.
/// </summary>
public static class ReportHtml
{
    const string Style = """
        :root { color-scheme: light dark;
          --fg: #1b1b1b; --muted: #5a5a5a; --line: #ddd; --bg: #fff; --head: #f6f6f4; --link: #1f5d99; }
        @media screen and (prefers-color-scheme: dark) { :root {
          --fg: #e6e6e3; --muted: #a6a6a1; --line: #3a3a38; --bg: #161615; --head: #1f1f1e; --link: #8cbcf0; } }
        * { box-sizing: border-box; }
        body { font: 15px/1.45 system-ui, sans-serif; margin: 2rem auto; max-width: 72rem; padding: 0 1rem; color: var(--fg); background: var(--bg); }
        h1 { font-size: 1.4rem; margin: 0 0 .2rem; overflow-wrap: anywhere; }
        h2 { font-size: 1rem; margin: 2rem 0 .4rem; }
        a { color: var(--link); }
        .meta, .format { color: var(--muted); margin: 0 0 .3rem; overflow-wrap: anywhere; }
        dl.format { display: grid; grid-template-columns: max-content 1fr; gap: .1rem .8rem; margin: .5rem 0 0; }
        dl.format dt { font-weight: 600; }
        dl.format dd { margin: 0; }
        .verdict { margin: 1.2rem 0 0; padding: .9rem 1.1rem; border-radius: .4rem; }
        .verdict strong { display: block; font-size: 1.5rem; line-height: 1.2; }
        .verdict span { font-variant-numeric: tabular-nums; }
        table { border-collapse: collapse; width: 100%; table-layout: fixed; }
        col.sev { width: 6.5em; } col.check { width: 11em; } col.tc { width: 7.5em; }
        th, td { text-align: left; vertical-align: top; padding: .45rem .6rem; border-bottom: 1px solid var(--line); overflow-wrap: anywhere; }
        th { font-size: .8rem; text-transform: uppercase; letter-spacing: .04em; color: var(--muted); }
        td.tc { font-family: ui-monospace, monospace; font-size: .9em; font-variant-numeric: tabular-nums; white-space: nowrap; }
        .against { display: block; color: var(--muted); font-size: .8rem; margin-top: .15rem; }
        .against a { white-space: nowrap; }
        span.sev { display: inline-block; padding: .1rem .45rem; border-radius: .25rem; font-size: .8rem; font-weight: 600; }
        details, .shots-print { margin-top: 2rem; }
        .shots-print { display: none; }
        summary { cursor: pointer; font-weight: 600; }
        summary .against { display: inline; margin-left: .4rem; font-weight: normal; }
        @media (max-width: 40rem) {
          body { margin: 1rem auto; }
          table, tbody, tr, td { display: block; width: 100%; }
          thead, colgroup { display: none; }
          tr { padding: .5rem 0; border-bottom: 1px solid var(--line); }
          td { border: 0; padding: .1rem 0; }
          td.check { display: inline-block; width: auto; margin-left: .4rem; font-weight: 600; }
          td.sev { display: inline-block; width: auto; }
          td.tc { display: inline-block; width: auto; margin-right: .6rem; }
          td.tc:empty { display: none; }
          td.tc[data-label]::before { content: attr(data-label) " "; font-family: system-ui, sans-serif; color: var(--muted); }
        }
        @media print {
          body { margin: 0; max-width: none; font-size: 10pt; }
          a { color: inherit; text-decoration: none; }
          tr { break-inside: avoid; }
          thead { display: table-header-group; }
          .verdict, span.sev { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
          details { display: none; }
          .shots-print { display: block; }
        }
        """;

    /// <summary>The report as an HTML page, for <paramref name="file"/> (its name as shown).</summary>
    public static string Write(QcReport report, string file)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(file);
        var findings = report.Findings;
        var format = findings.FirstOrDefault(f => f.Check == "format" && f.Severity == Severity.Info);
        var shots = findings.FirstOrDefault(f => f.Check == "shots");
        var shotList = findings.Where(f => f.Check == "shot").ToList();
        var rest = findings.Where(f => f != format && f != shots && f.Check != "shot").ToList();
        var issues = rest.Where(f => f.Severity >= Severity.Warning).OrderByDescending(f => f.Severity).ToList();
        var others = rest.Where(f => f.Severity < Severity.Warning).ToList();

        var html = new StringBuilder();
        html.Append(CultureInfo.InvariantCulture, $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>QC: {Encode(file)}</title>
            <style>
            {Style}
            {StatusStyle.Css}</style>
            </head>
            <body>
            <header>
            <h1>{Encode(file)}</h1>
            <p class="meta">{Checked(report.Source)}Profile {Encode(report.Profile)} · starts at {Encode(report.StartTimecode)}{(report.DropFrame ? " (drop-frame)" : "")} · {Encode(Product.Name)} {Encode(Product.Version)}</p>
            {Identity(report.Source)}

            """);
        if (report.Format is { } described)
        {
            // In words a client reads (FormatWords); ffprobe's exact values are in the JSON report.
            html.Append(CultureInfo.InvariantCulture, $"<dl class=\"format\" title=\"{Encode(MediaFormat.CheckedAgainst)}\">");
            foreach (var (label, text) in FormatWords.Rows(described, file))
                html.Append(CultureInfo.InvariantCulture, $"<dt>{Encode(label)}</dt><dd>{Encode(text)}</dd>");
            html.Append("</dl>\n");
        }
        else if (format is not null)
        {
            html.Append(CultureInfo.InvariantCulture, $"""
                <p class="format" title="{Encode(format.CheckedAgainst)}">{Encode(format.Message)}</p>

                """);
        }

        html.Append(CultureInfo.InvariantCulture, $"""
            <div class="verdict {StatusStyle.Tone(StatusStyle.Of(report.Verdict))}"><strong>{StatusStyle.Label(StatusStyle.Of(report.Verdict))}</strong><span>{Counts(findings)}</span></div>
            </header>

            """);
        if (issues.Count > 0)
        {
            Table(html, issues.Any(f => f.Severity == Severity.Failure) ? "Failures and warnings" : "Warnings", issues);
        }

        Table(html, issues.Count > 0 ? "The other checks" : "The checks", others);
        if (shots is not null || shotList.Count > 0)
        {
            html.Append(CultureInfo.InvariantCulture, $"""
                <details>
                <summary>{Encode(shots?.Message ?? $"{shotList.Count} shots")}{(shots is null ? "" : $"<span class=\"against\">{Encode(shots.CheckedAgainst)}</span>")}</summary>

                """);
            if (shotList.Count > 0)
            {
                Table(html, null, shotList);
            }

            html.Append("</details>\n");

            // Folded on screen; in print, a details element shows only its summary, so the list is printed from a copy.
            html.Append(CultureInfo.InvariantCulture, $"<div class=\"shots-print\"><h2>{Encode(shots?.Message ?? $"{shotList.Count} shots")}</h2>\n");
            if (shotList.Count > 0)
            {
                Table(html, null, shotList);
            }

            html.Append("</div>\n");
        }

        html.Append("""
            </body>
            </html>

            """);
        return html.ToString();
    }

    // One table of findings, under its heading (none for the shot list, whose summary is its heading).
    static void Table(StringBuilder html, string? heading, IReadOnlyList<Finding> findings)
    {
        if (findings.Count == 0)
        {
            return;
        }

        if (heading is not null)
        {
            html.Append(CultureInfo.InvariantCulture, $"<h2>{Encode(heading)}</h2>\n");
        }

        html.Append("""
            <table>
            <colgroup><col class="sev"><col class="check"><col><col class="tc"><col class="tc"></colgroup>
            <thead><tr><th>Result</th><th>Check</th><th>Finding</th><th>From</th><th>To</th></tr></thead>
            <tbody>

            """);
        foreach (var finding in findings)
        {
            string ebu = Ebu(finding.EbuQc);
            html.Append(CultureInfo.InvariantCulture, $"""
                <tr><td class="sev"><span class="sev {StatusStyle.Tone(finding.Severity)}">{finding.Severity}</span></td><td class="check">{Encode(finding.Check)}</td><td>{Encode(finding.Message)}<span class="against">Checked against {Encode(finding.CheckedAgainst)}{(ebu.Length == 0 ? "" : $" · EBU QC {ebu}")}</span></td>{Span(finding.Start, finding.End)}</tr>

                """);
        }

        html.Append("""
            </tbody>
            </table>

            """);
    }

    // When the file was checked, leading the line under its name.
    static string Checked(CheckedFile? source) => source is null ? "" : $"Checked {Encode(CheckedFile.Time(source.Checked))} · ";

    // Which file exactly: its full path, size and modified time, so a report passed on says which version it covers.
    static string Identity(CheckedFile? source) => source is null
        ? ""
        : $"<p class=\"meta\">{Encode(source.Path)} · {Encode(source.SizeText)} · modified {Encode(CheckedFile.Time(source.Modified))}</p>";

    // From and To; a point (a start without an end) spans both columns as "at", rather than leaving To empty.
    static string Span(Position? start, Position? end) => (start, end) switch
    {
        ({ } at, null) => $"<td class=\"tc\" colspan=\"2\">at {Encode(at.Timecode)}</td>",
        _ => Tc("From", start) + Tc("To", end),
    };

    static string Tc(string label, Position? at) =>
        at is { } position ? $"<td class=\"tc\" data-label=\"{label}\">{Encode(position.Timecode)}</td>" : "<td class=\"tc\"></td>";

    // How many findings of each severity, the most severe first: "1 failure · 2 warnings · 8 passed · 16 information".
    static string Counts(IReadOnlyList<Finding> findings)
    {
        int Count(Severity severity) => findings.Count(f => f.Severity == severity && f.Check != "shot");
        var parts = new List<string>();
        int failures = Count(Severity.Failure), warnings = Count(Severity.Warning), passed = Count(Severity.Pass), info = Count(Severity.Info);
        if (failures > 0)
        {
            parts.Add(failures == 1 ? "1 failure" : $"{failures} failures");
        }

        if (warnings > 0)
        {
            parts.Add(warnings == 1 ? "1 warning" : $"{warnings} warnings");
        }

        parts.Add($"{passed} passed");
        parts.Add($"{info} for information");
        return string.Join(" · ", parts);
    }

    static string Encode(string text) => WebUtility.HtmlEncode(text);

    // The EBU QC item as a link to its page in the catalogue; nothing when the check has none.
    static string Ebu(EbuQcItem? item) =>
        item is null ? "" : $"<a href=\"{Encode(item.Url)}\" title=\"EBU QC {Encode(item.Id)} v{Encode(item.Version)}\">{Encode(item.Id)} {Encode(item.Name)}</a>";
}
