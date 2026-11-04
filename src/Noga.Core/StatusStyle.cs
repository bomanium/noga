using System.Globalization;
using System.Text;

namespace Noga;

/// <summary>Where a file's check stands, as the report, the web UI and the app show it.</summary>
public enum Status
{
    /// <summary>Waiting for its turn.</summary>
    Queued,

    /// <summary>Being measured.</summary>
    Measuring,

    /// <summary>No warnings or failures (exit code 0).</summary>
    Passed,

    /// <summary>Warnings, no failures (exit code 3).</summary>
    PassedWithWarnings,

    /// <summary>At least one failure (exit code 1).</summary>
    Failed,

    /// <summary>The file couldn't be read (exit code 2): no verdict, so not a failure's red, nor a queued check's grey.</summary>
    Unreadable,
}

/// <summary>A badge's colours: text on a tinted background, as CSS hex (#rrggbb).</summary>
public readonly record struct BadgeColours(string Background, string Text);

/// <summary>
/// One look for statuses and severities across the HTML report, <c>noga serve</c>'s page and <c>noga-app</c>: the same
/// labels, and the same tinted badges (coloured text on a pale tint, or a light text on a dark tint in dark mode), each
/// at least WCAG AA (4.5:1) against its tint, which a test checks (docs/decisions/2026-11-04-status-style.md).
/// </summary>
public static class StatusStyle
{
    // The tones: light tint, light text, dark tint, dark text.
    static readonly Dictionary<string, (string LightBg, string LightText, string DarkBg, string DarkText)> Tones = new(StringComparer.Ordinal)
    {
        ["pass"] = ("#e3f4e6", "#17602a", "#173a22", "#9fdcae"),
        ["info"] = ("#e8eef7", "#24466e", "#1c2b40", "#a9c4e8"),
        ["warning"] = ("#fdf1d6", "#7a5200", "#3d2f0c", "#f2cf7c"),
        ["failure"] = ("#fbe3e1", "#8d1c12", "#4a1a16", "#f6aaa2"),
        ["neutral"] = ("#ececea", "#4a4a48", "#2e2e2c", "#cfcfca"),
        ["unreadable"] = ("#efe8f7", "#55307f", "#2f2440", "#d3bff0"),
    };

    /// <summary>The tone names, which are the CSS classes <see cref="Css"/> styles.</summary>
    public static IReadOnlyCollection<string> ToneNames => Tones.Keys;

    /// <summary>A report's verdict as a status.</summary>
    public static Status Of(Severity verdict) => verdict switch
    {
        Severity.Failure => Status.Failed,
        Severity.Warning => Status.PassedWithWarnings,
        _ => Status.Passed,
    };

    /// <summary>What a status reads as, everywhere.</summary>
    public static string Label(Status status) => status switch
    {
        Status.Queued => "Queued",
        Status.Measuring => "Measuring…",
        Status.Passed => "Passed",
        Status.PassedWithWarnings => "Passed with warnings",
        Status.Failed => "Failed",
        _ => "Couldn't read",
    };

    /// <summary>The tone (and CSS class) a status is drawn in.</summary>
    public static string Tone(Status status) => status switch
    {
        Status.Measuring => "info",
        Status.Passed => "pass",
        Status.PassedWithWarnings => "warning",
        Status.Failed => "failure",
        Status.Unreadable => "unreadable",
        _ => "neutral",
    };

    /// <summary>The tone (and CSS class) a finding's severity is drawn in.</summary>
    public static string Tone(Severity severity) => severity switch
    {
        Severity.Pass => "pass",
        Severity.Info => "info",
        Severity.Warning => "warning",
        _ => "failure",
    };

    /// <summary>A tone's colours, light or dark.</summary>
    public static BadgeColours Colours(string tone, bool dark)
    {
        var (lightBg, lightText, darkBg, darkText) = Tones[tone];
        return dark ? new BadgeColours(darkBg, darkText) : new BadgeColours(lightBg, lightText);
    }

    /// <summary>
    /// CSS for a page: each tone's custom properties (<c>--pass-bg</c>, <c>--pass</c>, ...) for light and, on screen with a
    /// dark system theme, dark, and a class per tone that draws a badge in it.
    /// </summary>
    public static string Css
    {
        get
        {
            var css = new StringBuilder();
            Properties(css, ":root", dark: false);
            css.Append("@media screen and (prefers-color-scheme: dark) {\n");
            Properties(css, ":root", dark: true);
            css.Append("}\n");
            foreach (string tone in Tones.Keys)
                css.Append(CultureInfo.InvariantCulture, $".{tone} {{ background: var(--{tone}-bg); color: var(--{tone}); }}\n");
            return css.ToString();
        }
    }

    static void Properties(StringBuilder css, string selector, bool dark)
    {
        css.Append(selector).Append(" {");
        foreach (string tone in Tones.Keys)
        {
            var colours = Colours(tone, dark);
            css.Append(CultureInfo.InvariantCulture, $" --{tone}-bg: {colours.Background}; --{tone}: {colours.Text};");
        }

        css.Append(" }\n");
    }

    /// <summary>WCAG 2's contrast ratio of two #rrggbb colours (1 to 21).</summary>
    public static double Contrast(string a, string b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    static double Luminance(string hex)
    {
        double Channel(int at)
        {
            double c = int.Parse(hex.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(1)) + (0.7152 * Channel(3)) + (0.0722 * Channel(5));
    }
}
