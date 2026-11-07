using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// Pixels outside RGB gamut (EBU R 103; docs/decisions/2026-11-06-rgb-gamut.md), on the decoded planes of either
/// decoding path: FrameReader's GamutMeter (each pixel's R, G and B from its Y and the Cb and Cr samples it is sited on,
/// by the matrix's Kr and Kb, each from 0 to 1; out of gamut when any is below the low limit or above the high one), each
/// frame's share of such pixels in seconds, and the spans over the share allowed.
/// </summary>
public sealed class GamutMeter
{
    /// <summary>The deepest picture FrameReader's GamutMeter takes: deeper ones aren't measured (their shares stay empty).</summary>
    public const int MaxDepth = 12;

    readonly GamutOptions _options;
    readonly List<double> _times = [];
    FrameReader.GamutMeter? _meter;
    bool _deep;
    double _unit;

    /// <summary>
    /// A meter of R, G and B within <paramref name="low"/> to <paramref name="high"/> (-0.05 and 1.05: R 103's -5% and
    /// 105%) by the matrix <paramref name="matrix"/> (ffprobe's color_space: bt709, bt470bg or smpte170m, bt2020nc; else
    /// BT.709, BT.601 for a picture under 720 lines, decided by the first frame).
    /// </summary>
    public GamutMeter(double low, double high, string? matrix = null)
    {
        if (low >= high)
            throw new ArgumentOutOfRangeException(nameof(low), "The low limit is below the high one.");
        _options = new GamutOptions { Low = low, High = high, Matrix = FrameReader.GamutMeter.Coefficients(matrix) };
    }

    /// <summary>The Kr and Kb the frames are converted with (NaN until the first frame when no matrix was given).</summary>
    public (double Kr, double Kb) Matrix => _meter?.Matrix ?? _options.Matrix ?? (double.NaN, double.NaN);

    /// <summary>Each frame's share of pixels out of gamut (0 to 1), in the order added.</summary>
    public IReadOnlyList<double> Shares => _meter?.Shares ?? [];

    /// <summary>Each frame's time (seconds), in the order added.</summary>
    public IReadOnlyList<double> Times => _times;

    /// <summary>
    /// Adds a frame of <paramref name="format"/> at <paramref name="pts"/>: its three planes and their strides; a picture
    /// deeper than <see cref="MaxDepth"/> isn't measured.
    /// </summary>
    public void Add(PictureFormat format, ReadOnlySpan<byte> y, int yStride, ReadOnlySpan<byte> u, int uStride, ReadOnlySpan<byte> v, int vStride, long pts)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (_meter is null && !_deep)
        {
            _deep = format.BitDepth > MaxDepth;
            if (_deep)
                return;
            _meter = FrameReader.GamutMeter.For(format, _options);
            _unit = (double)format.TimeBase.Num / format.TimeBase.Den;
        }

        if (_meter is null)
            return;
        _meter.Add(pts, y, yStride, u, uStride, v, vStride);
        _times.Add(pts * _unit);
    }

    /// <summary>
    /// The spans where more than <paramref name="maxShare"/> of a frame's pixels are out of gamut, frames in
    /// <paramref name="bars"/> left out (their colours are the 100% ones, and a PLUGE below black), those under
    /// <paramref name="mergeGap"/> seconds apart joined, and the frame with the most (bars left out), a frame lasting
    /// <paramref name="frame"/> seconds after the last.
    /// </summary>
    public (IReadOnlyList<RangeSpan> Spans, FrameShare? Worst) Finish(IReadOnlyList<TimeRange> bars, double maxShare, double mergeGap, double frame)
    {
        ArgumentNullException.ThrowIfNull(bars);
        var spans = new List<RangeSpan>();
        FrameShare? worst = null;
        int? start = null;
        double spanWorst = 0;
        bool InBars(double t) => bars.Any(b => t >= b.Start - 1e-9 && t < b.End - 1e-9);
        double After(int i) => i + 1 < _times.Count ? _times[i + 1] : _times[i] + frame;
        for (int i = 0; i < Shares.Count; i++)
        {
            bool skip = InBars(_times[i]);
            double share = skip ? 0 : Shares[i];
            if (!skip && (worst is null || share > worst.Value.Share))
                worst = new FrameShare(_times[i], share);
            if (share > maxShare)
            {
                start ??= i;
                spanWorst = Math.Max(spanWorst, share);
            }
            else if (start is { } s)
            {
                spans.Add(new RangeSpan(_times[s], _times[i], spanWorst));
                (start, spanWorst) = (null, 0);
            }
        }

        if (start is { } open)
            spans.Add(new RangeSpan(_times[open], After(Shares.Count - 1), spanWorst));
        return (RangeSpan.Merge(spans, mergeGap), worst);
    }

    /// <summary>
    /// Whether the pixel of codes <paramref name="y"/>, <paramref name="cb"/>, <paramref name="cr"/> is in gamut: the
    /// arithmetic the tests' geq expression does, in the same order (once a frame has been added).
    /// </summary>
    public bool InGamut(int y, int cb, int cr) => _meter?.InGamut(y, cb, cr) ?? throw new InvalidOperationException("No frame has been added.");
}
