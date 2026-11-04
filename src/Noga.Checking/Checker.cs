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
    /// FILE.qc.json in <paramref name="folder"/> (else beside it), telling <paramref name="progress"/> how far it has got
    /// (0 to 1).
    /// </summary>
    /// <exception cref="CheckException">The file isn't there or can't be read.</exception>
    public static CheckResult Run(string file, string? folder, Profile? profile = null, CancellationToken cancellationToken = default, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!File.Exists(file))
            throw new CheckException($"no such file: {file}");

        profile ??= Profile.EbuR128;

        // The file as the check finds it, before reading it: a report passed on says which version it covers.
        var source = CheckedFile.Of(file);
        Measurements measured;
        try
        {
            measured = Measure(file, profile, cancellationToken, progress);
        }
        catch (Exception e) when (e is MediaException or InvalidDataException or NotSupportedException)
        {
            throw new CheckException($"can't read {file}: {e.Message}", e);
        }
        catch (Exception e) when (e is not OperationCanceledException and not CheckException)
        {
            // A fault in Noga or a library under it, not in the file as far as anyone knows: still one file's error,
            // so the command exits 2 with what happened, and the web UI's and the app's queue go on to the next file.
            throw new CheckException($"can't check {file}: {e.GetType().Name}: {e.Message} (a fault in Noga: please report it)", e);
        }

        var report = profile.Evaluate(measured) with { Source = source };
        string name = Path.GetFileName(file);
        folder ??= Path.GetDirectoryName(Path.GetFullPath(file))!;
        Directory.CreateDirectory(folder);
        string html = Path.Combine(folder, name + ".qc.html"), json = Path.Combine(folder, name + ".qc.json");
        File.WriteAllText(html, ReportHtml.Write(report, name));
        File.WriteAllText(json, ReportJson.Write(report, name));
        return new CheckResult(report, html, json);
    }

    // The managed path for what it reads; a file it starts on but can't read to the end (a damaged or cut-off MP4: a
    // NAL unit that overruns its packet, a sample past the end of the file) goes to the FFmpeg path, which reads what
    // there is and reports the rest.
    static Measurements Measure(string file, Profile profile, CancellationToken cancellationToken, IProgress<double>? progress)
    {
        if (ManagedMeasurer.CanMeasure(file))
        {
            try
            {
                return ManagedMeasurer.Measure(file, profile, cancellationToken, progress);
            }
            catch (InvalidDataException)
            {
            }
        }

        return FfmpegMeasurer.Measure(file, profile, cancellationToken, progress);
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
