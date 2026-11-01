using System.Globalization;
using System.Net;
using System.Text;

namespace MediaQc;

/// <summary>
/// The HTML report: one self-contained page (no scripts, no external files) for a person to read and pass on: the
/// verdict, then every finding with its timecodes, its value and what it is checked against.
/// </summary>
public static class ReportHtml
{
    const string Style = """
        body { font: 15px/1.45 system-ui, sans-serif; margin: 2rem auto; max-width: 72rem; padding: 0 1rem; color: #1b1b1b; }
        h1 { font-size: 1.4rem; margin-bottom: .2rem; }
        .meta { color: #555; margin: 0 0 1.2rem; }
        .verdict { display: inline-block; padding: .3rem .8rem; border-radius: .3rem; font-weight: 600; margin-bottom: 1.2rem; }
        table { border-collapse: collapse; width: 100%; }
        th, td { text-align: left; vertical-align: top; padding: .45rem .6rem; border-bottom: 1px solid #ddd; }
        th { font-size: .8rem; text-transform: uppercase; letter-spacing: .04em; color: #555; }
        td.tc { font-family: ui-monospace, monospace; white-space: nowrap; }
        td.against { color: #555; font-size: .85rem; }
        td.ebu { font-size: .85rem; white-space: nowrap; }
        .pass { background: #e3f4e6; color: #17602a; }
        .info { background: #e8eef7; color: #24466e; }
        .warning { background: #fdf1d6; color: #7a5200; }
        .failure { background: #fbe3e1; color: #8d1c12; }
        span.sev { padding: .1rem .45rem; border-radius: .25rem; font-size: .8rem; font-weight: 600; }
        """;

    /// <summary>The report as an HTML page, for <paramref name="file"/> (its name as shown).</summary>
    public static string Write(QcReport report, string file)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(file);
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
            </style>
            </head>
            <body>
            <h1>{Encode(file)}</h1>
            <p class="meta">Profile {Encode(report.Profile)} · starts at {Encode(report.StartTimecode)}{(report.DropFrame ? " (drop-frame)" : "")} · {Encode(Product.Name)} {Encode(Product.Version)}</p>
            <div class="verdict {Class(report.Verdict <= Severity.Info ? Severity.Pass : report.Verdict)}">{Verdict(report.Verdict)}</div>
            <table>
            <thead><tr><th>Result</th><th>Check</th><th>Finding</th><th>From</th><th>To</th><th>Checked against</th><th>EBU QC</th></tr></thead>
            <tbody>

            """);
        foreach (var finding in report.Findings)
        {
            html.Append(CultureInfo.InvariantCulture, $"""
                <tr><td><span class="sev {Class(finding.Severity)}">{finding.Severity}</span></td><td>{Encode(finding.Check)}</td><td>{Encode(finding.Message)}</td><td class="tc">{Encode(finding.Start?.Timecode ?? "")}</td><td class="tc">{Encode(finding.End?.Timecode ?? "")}</td><td class="against">{Encode(finding.CheckedAgainst)}</td><td class="ebu">{Ebu(finding.EbuQc)}</td></tr>

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

    static string Verdict(Severity verdict) => verdict switch
    {
        Severity.Failure => "Failed",
        Severity.Warning => "Passed with warnings",
        _ => "Passed",
    };

    static string Class(Severity severity) => severity.ToString().ToLowerInvariant();

    static string Encode(string text) => WebUtility.HtmlEncode(text);

    // The EBU QC item as a link to its page in the catalogue; nothing when the check has none.
    static string Ebu(EbuQcItem? item) =>
        item is null ? "" : $"<a href=\"{Encode(item.Url)}\" title=\"EBU QC {Encode(item.Id)} v{Encode(item.Version)}\">{Encode(item.Id)} {Encode(item.Name)}</a>";
}
