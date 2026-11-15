using System.Text.Json;
using System.Text.Json.Serialization;

namespace Noga;

/// <summary>A set's JSON report (<see cref="SetReport"/>), for pipelines: the set's verdict and exit code, what was found across the files, and each file's verdict with the name of its own JSON report.</summary>
public static class SetJson
{
    /// <summary>The set's report as indented JSON.</summary>
    public static string Write(SetReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var files = report.Files.Select(f => new SetFileDocument(
            f.Name,
            f.Report?.Verdict,
            f.Report?.ExitCode() ?? QcReport.Error,
            f.Report is null ? null : f.Name + ".qc.json",
            f.Error,
            f.Report?.Reason,
            f.Report?.Format?.Duration,
            f.Rms,
            f.Peak)).ToList();
        var document = new SetDocument(Product.Name, Product.Version, report.Folder, report.Profile, report.Verdict, report.ExitCode(), report.Unreadable, report.Checked, report.Findings, files);
        return JsonSerializer.Serialize(document, SetJsonContext.Default.SetDocument);
    }
}

/// <summary>
/// The set's JSON report's top level. <c>verdict</c> is the most severe finding of the files read and of the set;
/// <c>exitCode</c> is 2 when <c>unreadable</c> (how many files couldn't be read) isn't 0.
/// </summary>
public sealed record SetDocument(
    string Tool,
    string Version,
    string Folder,
    string Profile,
    Severity Verdict,
    int ExitCode,
    int Unreadable,
    DateTimeOffset Checked,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<SetFileDocument> Files);

/// <summary>
/// One file in the set's JSON report: its verdict and exit code as checked alone, its own JSON <c>report</c> (beside
/// this one), or the <c>error</c> that kept it from being read; why it didn't simply pass, its duration (seconds) and
/// its RMS level and sample peak (dBFS) where the profile measures them.
/// </summary>
public sealed record SetFileDocument(string File, Severity? Verdict, int ExitCode, string? Report, string? Error, string? Reason, double? Duration, double? Rms, double? Peak);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(SetDocument))]
internal partial class SetJsonContext : JsonSerializerContext;
