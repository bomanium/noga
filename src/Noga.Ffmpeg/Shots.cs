using ShotDetector;

namespace Noga.Ffmpeg;

/// <summary>
/// The shot list: ShotDetector's port of PySceneDetect's adaptive detector with its defaults, trusted as checked in
/// ShotDetector's own CI (docs/decisions/2026-11-01-frames-bitrate-shots.md). The FFmpeg path detects on the file;
/// the managed path gives ShotDetector ManagedCodecs' frames with the same <see cref="Options"/> and a converter.
/// </summary>
public static class Shots
{
    /// <summary>The detection's options: PySceneDetect's detect-adaptive defaults.</summary>
    public static DetectionOptions Options => new() { Detector = DetectorKind.Adaptive };

    /// <summary>Detects <paramref name="path"/>'s shots, decoding it with FFmpeg's libraries, telling <paramref name="progress"/> how far (0 to 1).</summary>
    /// <exception cref="ShotDetectionException">The file can't be read.</exception>
    public static DetectionResult Detect(string path, CancellationToken cancellationToken = default, IProgress<double>? progress = null)
    {
        Libraries.Load();
        return ShotDetection.Detect(path, Options with { Progress = Fractions(progress) }, cancellationToken);
    }

    /// <summary>ShotDetector's progress as fractions (none while the frame count is unknown).</summary>
    public static IProgress<DetectionProgress>? Fractions(IProgress<double>? progress) =>
        progress is null ? null : new ReportNow<DetectionProgress>(p =>
        {
            if (p.Fraction is { } fraction)
                progress.Report(fraction);
        });

    /// <summary><paramref name="measured"/> with <paramref name="result"/>'s shots, in seconds.</summary>
    public static Measurements Add(Measurements measured, DetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(measured);
        ArgumentNullException.ThrowIfNull(result);
        return measured with { Shots = [.. result.Shots.Select(s => new TimeRange(s.Start.Seconds, s.End.Seconds))] };
    }
}
