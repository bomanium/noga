using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// Whether the picture is interlaced, from the picture (docs/decisions/2026-11-03-interlacing.md): FFmpeg's idet on the
/// decoded planes of either decoding path, FrameReader's InterlaceDetector on Y, Cb and Cr (vf_idet.c: each frame
/// weighed against the one before and after it as top field first, bottom field first, progressive or undetermined,
/// alone and through a four-frame history); the totals idet prints at the end.
/// </summary>
public sealed class InterlaceMeter
{
    readonly IdetOptions _options;
    InterlaceDetector? _detector;

    /// <summary>A meter at idet's thresholds (its defaults: 1.04 and 1.5).</summary>
    public InterlaceMeter(float interlaceThreshold = 1.04f, float progressiveThreshold = 1.5f) =>
        _options = new IdetOptions { InterlaceThreshold = interlaceThreshold, ProgressiveThreshold = progressiveThreshold };

    /// <summary>Adds a frame of <paramref name="format"/>: its three planes and their strides.</summary>
    public void Add(PictureFormat format, ReadOnlySpan<byte> y, int yStride, ReadOnlySpan<byte> u, int uStride, ReadOnlySpan<byte> v, int vStride)
    {
        ArgumentNullException.ThrowIfNull(format);
        _detector ??= new InterlaceDetector(Planes.Of(format), format.BitDepth, _options);
        _detector.Add(y, yStride, u, uStride, v, vStride, default, 0);
    }

    /// <summary>The totals, as idet prints them at the end (the last frame weighed once more against itself as next).</summary>
    public InterlaceMeasures Finish()
    {
        if (_detector is null)
            return new InterlaceMeasures(0, 0, 0, 0, 0, 0, 0, 0);
        var t = _detector.Finish();
        return new InterlaceMeasures(t.SingleTff, t.SingleBff, t.SingleProgressive, t.SingleUndetermined, t.MultiTff, t.MultiBff, t.MultiProgressive, t.MultiUndetermined);
    }
}
