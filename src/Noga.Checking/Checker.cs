using FrameReader;
using Noga.Ffmpeg;
using Noga.Managed;

namespace Noga.Checking;

/// <summary>
/// One file's check, for the command line and the web UI alike: measured on its path (docs/decisions/2026-10-31-decoding.md:
/// ManagedCodecs for H.264/AAC in MP4, FFmpeg for the rest), judged by the profile, both reports written.
/// </summary>
public static class Checker
{
    /// <summary>
    /// Checks <paramref name="file"/> by <paramref name="profile"/> (ebu-r128 when null) and writes FILE.qc.html and
    /// FILE.qc.json in <paramref name="folder"/> (else beside it).
    /// </summary>
    /// <exception cref="CheckException">The file isn't there or can't be read.</exception>
    public static CheckResult Run(string file, string? folder, Profile? profile = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!File.Exists(file))
            throw new CheckException($"no such file: {file}");

        profile ??= Profile.EbuR128;
        Measurements measured;
        try
        {
            measured = ManagedMeasurer.CanMeasure(file)
                ? ManagedMeasurer.Measure(file, profile, cancellationToken)
                : FfmpegMeasurer.Measure(file, profile, cancellationToken);
        }
        catch (Exception e) when (e is MediaException or InvalidDataException or NotSupportedException)
        {
            throw new CheckException($"can't read {file}: {e.Message}", e);
        }

        var report = profile.Evaluate(measured);
        string name = Path.GetFileName(file);
        folder ??= Path.GetDirectoryName(Path.GetFullPath(file))!;
        Directory.CreateDirectory(folder);
        string html = Path.Combine(folder, name + ".qc.html"), json = Path.Combine(folder, name + ".qc.json");
        File.WriteAllText(html, ReportHtml.Write(report, name));
        File.WriteAllText(json, ReportJson.Write(report, name));
        return new CheckResult(report, html, json);
    }
}

/// <summary>A check's report and where its HTML and JSON went.</summary>
public sealed record CheckResult(QcReport Report, string Html, string Json);

/// <summary>A file that couldn't be checked: not there, or not readable media.</summary>
public sealed class CheckException : Exception
{
    public CheckException()
    {
    }

    public CheckException(string message)
        : base(message)
    {
    }

    public CheckException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
