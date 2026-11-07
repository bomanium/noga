using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// Constant-colour frames (docs/decisions/2026-11-06-constant-colour.md), on the decoded planes of either decoding path:
/// FrameReader's ConstantColourDetector (each frame's signalstats YLOW, YHIGH and YAVG for Y, U and V; a frame flat when
/// every plane's HIGH less LOW is within the spread, a run of flat frames whose averages stay within it of its first's,
/// from its first frame to the frame after its last, every fourth luma row first), its runs in seconds with their colour
/// (<see cref="ColourSpan"/>).
/// </summary>
public sealed class ColourMeter
{
    readonly ConstantColourOptions _options;
    ConstantColourDetector? _detector;
    double _unit;
    bool _fullRange;
    bool _sd;

    /// <summary>A meter for runs of <paramref name="minimum"/> seconds or more of frames flat within <paramref name="spread"/> 8-bit codes.</summary>
    public ColourMeter(double spread, double minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spread);
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        _options = new ConstantColourOptions { Spread = spread, MinDuration = TimeSpan.FromSeconds(minimum) };
    }

    /// <summary>Each frame's figures, in the order added; null for a frame its luma's every fourth row shows isn't flat.</summary>
    public IReadOnlyList<SignalStats?> Frames => _detector?.Frames ?? [];

    /// <summary>Adds a frame of <paramref name="format"/> at <paramref name="pts"/>: its three planes and their strides.</summary>
    public void Add(PictureFormat format, ReadOnlySpan<byte> y, int yStride, ReadOnlySpan<byte> u, int uStride, ReadOnlySpan<byte> v, int vStride, long pts)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (_detector is null)
        {
            _detector = ConstantColourDetector.For(format, _options);
            _unit = (double)format.TimeBase.Num / format.TimeBase.Den;
            (_fullRange, _sd) = (format.FullRange, format.Height < 720);
        }

        _detector.Add(pts, y, yStride, u, uStride, v, vStride);
    }

    /// <summary>The runs found, a run still going at the end ending after the last frame.</summary>
    public IReadOnlyList<ColourSpan> Finish() =>
        _detector is null ? [] : [.. _detector.Finish().Select(s => ColourSpan.Of(s.StartPts * _unit, s.EndPts * _unit, s.Y, s.Cb, s.Cr, _fullRange, _sd))];
}
