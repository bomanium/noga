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
            report.Findings);
        return JsonSerializer.Serialize(document, ReportJsonContext.Default.ReportDocument);
    }
}

/// <summary>The JSON report's top level.</summary>
public sealed record ReportDocument(
    string Tool,
    string Version,
    string File,
    string Profile,
    Severity Verdict,
    int ExitCode,
    string StartTimecode,
    bool DropFrame,
    IReadOnlyList<Finding> Findings);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(ReportDocument))]
internal partial class ReportJsonContext : JsonSerializerContext;
