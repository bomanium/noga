using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// Frozen pictures (docs/decisions/2026-11-03-freeze-frames.md), FFmpeg's freezedetect on the decoded planes of either
/// decoding path: FrameReader's FreezeDetector on Y, Cb and Cr (each frame against the picture's first, the mean
/// absolute difference over the planes' samples divided by 2^depth, at most n frozen; a freeze from its first frame once
/// it has lasted d, to the first frame that isn't), its freezes in seconds, one still running at the end ending after
/// the last frame.
/// </summary>
public sealed class FreezeMeter
{
    readonly FreezeOptions _options;
    FreezeDetector? _detector;
    double _unit;
    double _last;
    double _frame;

    /// <summary>A meter for freezes of <paramref name="minimum"/> seconds or more, at <paramref name="noise"/> (freezedetect's n and d).</summary>
    public FreezeMeter(double noise, double minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(noise);
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        _options = new FreezeOptions { Noise = noise, MinDuration = TimeSpan.FromSeconds(minimum) };
    }

    /// <summary>Adds a frame of <paramref name="format"/> at <paramref name="pts"/>: its three planes and their strides.</summary>
    public void Add(PictureFormat format, ReadOnlySpan<byte> y, int yStride, ReadOnlySpan<byte> u, int uStride, ReadOnlySpan<byte> v, int vStride, long pts)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (_detector is null)
        {
            _detector = new FreezeDetector(Planes.Of(format), format.BitDepth, format.TimeBase, _options);
            _unit = (double)format.TimeBase.Num / format.TimeBase.Den;
        }

        _detector.Add(pts, y, yStride, u, uStride, v, vStride, default, 0);
        double seconds = pts * _unit;
        if (seconds > _last)
            _frame = seconds - _last;
        _last = seconds;
    }

    /// <summary>The freezes found, a freeze still running at the end ending after the last frame.</summary>
    public IReadOnlyList<TimeRange> Finish() =>
        _detector is null ? [] : [.. _detector.Finish().Select(f => new TimeRange(f.StartPts * _unit, f.EndPts is { } end ? end * _unit : _last + _frame))];
}

/// <summary>The Y, Cb and Cr planes' sizes in samples, as FrameReader's detectors take them.</summary>
static class Planes
{
    public static (int, int)[] Of(PictureFormat format) =>
        [(format.Width, format.Height), (format.ChromaWidth, format.ChromaHeight), (format.ChromaWidth, format.ChromaHeight)];
}
