using System.Text.Json;
using System.Text.Json.Serialization;

namespace Noga;

/// <summary>The JSON report: <see cref="QcReport"/> with camelCase names and severities as strings, for pipelines.</summary>
public static class ReportJson
{
    /// <summary>The report as indented JSON, with the tool, its version and the file's name.</summary>
    public static string Write(QcReport report, string file)
    {
        ArgumentNullException.ThrowIfNull(report);
        var document = new ReportDocument(Product.Name,
            Product.Version,
            file,
            report.Profile,
            report.Verdict,
            report.ExitCode(),
            report.StartTimecode,
            report.DropFrame,
            report.Source?.Path,
            report.Source?.Size,
            report.Source?.Modified,
            report.Source?.Checked,
            report.Findings);
        return JsonSerializer.Serialize(document, ReportJsonContext.Default.ReportDocument);
    }
}

/// <summary>
/// The JSON report's top level; <c>path</c>, <c>size</c>, <c>modified</c> and <see langword="checked"/> say which file was checked
/// and when (<see cref="CheckedFile"/>), left out for a report made from measures alone.
/// </summary>
public sealed record ReportDocument(
    string Tool,
    string Version,
    string File,
    string Profile,
    Severity Verdict,
    int ExitCode,
    string StartTimecode,
    bool DropFrame,
    string? Path,
    long? Size,
    DateTimeOffset? Modified,
    DateTimeOffset? Checked,
    IReadOnlyList<Finding> Findings);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(ReportDocument))]
internal partial class ReportJsonContext : JsonSerializerContext;
