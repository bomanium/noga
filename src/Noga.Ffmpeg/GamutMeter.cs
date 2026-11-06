using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// Pixels outside RGB gamut (EBU R 103; docs/decisions/2026-11-06-rgb-gamut.md), over the decoded planes of either
/// decoding path: each pixel's R, G and B from its Y and the Cb and Cr samples it is sited on (the one beside it in
/// 4:2:2 and 4:2:0, not interpolated), by the matrix's Kr and Kb, each from 0 (black) to 1 (white); a pixel is out of
/// gamut when any of them is below the low limit or above the high one. Each frame's share of such pixels, its worst
/// frame, and the spans over the share allowed.
/// </summary>
/// <remarks>
/// Each of R, G and B rises with Y by the same step for a given Cb and Cr, so the pixels in gamut are one run of Y codes
/// for each pair: a table of those runs, its edges found by the arithmetic itself (<see cref="InGamut"/>, as the tests'
/// geq expression), makes a pixel one lookup.
/// </remarks>
public sealed class GamutMeter
{
    readonly double _low;
    readonly double _high;
    double _kr;
    double _kb;
    readonly List<double> _times = [];
    readonly List<double> _shares = [];
    uint[]? _runs;
    int _depth;
    bool _fullRange;

    /// <summary>
    /// A meter of R, G and B within <paramref name="low"/> to <paramref name="high"/> (-0.05 and 1.05: R 103's -5% and
    /// 105%) by the matrix <paramref name="matrix"/> (ffprobe's color_space: bt709, bt470bg or smpte170m, bt2020nc; else
    /// BT.709, BT.601 for a picture under 720 lines, decided by the first frame).
    /// </summary>
    public GamutMeter(double low, double high, string? matrix = null)
    {
        if (low >= high)
            throw new ArgumentOutOfRangeException(nameof(low), "The low limit is below the high one.");
        (_low, _high) = (low, high);
        (_kr, _kb) = Coefficients(matrix) ?? (double.NaN, double.NaN);
    }

    /// <summary>The Kr and Kb of ffprobe's color_space, or null when it isn't one of BT.709, BT.601 and BT.2020's.</summary>
    public static (double Kr, double Kb)? Coefficients(string? matrix) => matrix switch
    {
        "bt709" => (0.2126, 0.0722),
        "bt470bg" or "smpte170m" or "bt601" => (0.299, 0.114),
        "bt2020nc" or "bt2020c" => (0.2627, 0.0593),
        _ => null,
    };

    /// <summary>The Kr and Kb the frames are converted with (NaN until the first frame when no matrix was given).</summary>
    public (double Kr, double Kb) Matrix => (_kr, _kb);

    /// <summary>Each frame's share of pixels out of gamut (0 to 1), in the order added.</summary>
    public IReadOnlyList<double> Shares => _shares;

    /// <summary>Each frame's time (seconds), in the order added.</summary>
    public IReadOnlyList<double> Times => _times;

    /// <summary>
    /// Adds a frame of planar YUV at <paramref name="seconds"/>: its three planes and their strides, its size, chroma
    /// subsampling, depth (8 bits a sample, else 16 little-endian) and range.
    /// </summary>
    public void Add(ReadOnlySpan<byte> y, int yStride, ReadOnlySpan<byte> u, int uStride, ReadOnlySpan<byte> v, int vStride, int width, int height, ChromaSubsampling chroma, int depth, bool fullRange, double seconds)
    {
        if (_runs is null)
            Build(depth, fullRange, height);
        int hShift = chroma == ChromaSubsampling.Yuv444 ? 0 : 1;
        int vShift = chroma == ChromaSubsampling.Yuv420 ? 1 : 0;
        int chromaWidth = (width + hShift) >> hShift;
        var runs = _runs!;
        long outside = 0;
        for (int r = 0; r < height; r++)
        {
            int c = r >> vShift;
            if (depth > 8)
            {
                outside += Row(
                    MemoryMarshal.Cast<byte, ushort>(y.Slice(r * yStride, width * 2)),
                    MemoryMarshal.Cast<byte, ushort>(u.Slice(c * uStride, chromaWidth * 2)),
                    MemoryMarshal.Cast<byte, ushort>(v.Slice(c * vStride, chromaWidth * 2)),
                    runs,
                    hShift,
                    depth);
            }
            else
            {
                outside += Row(y.Slice(r * yStride, width), u.Slice(c * uStride, chromaWidth), v.Slice(c * vStride, chromaWidth), runs, hShift);
            }
        }

        _times.Add(seconds);
        _shares.Add(width * height == 0 ? 0 : (double)outside / ((long)width * height));
    }

    // The pixels of one row outside their chroma pair's run of Y codes (a run: its first code, and its last in the
    // high 16 bits), the pair looked up once for the luma samples sited on it.
    static long Row(ReadOnlySpan<byte> luma, ReadOnlySpan<byte> cb, ReadOnlySpan<byte> cr, uint[] runs, int hShift)
    {
        long outside = 0;
        if (hShift == 1)
        {
            // Two luma samples to each chroma pair: the pairs whose two samples both are there, then the last one alone.
            int pairs = luma.Length / 2;
            ref byte l = ref MemoryMarshal.GetReference(luma);
            ref byte b = ref MemoryMarshal.GetReference(cb);
            ref byte r = ref MemoryMarshal.GetReference(cr);
            ref uint table = ref MemoryMarshal.GetArrayDataReference(runs);
            for (int c = 0; c < pairs; c++)
            {
                uint run = Unsafe.Add(ref table, (Unsafe.Add(ref b, c) << 8) | Unsafe.Add(ref r, c));
                uint first = run & 0xFFFF, span = (run >> 16) - first;
                outside += ((uint)(Unsafe.Add(ref l, 2 * c) - first) > span ? 1 : 0) + ((uint)(Unsafe.Add(ref l, (2 * c) + 1) - first) > span ? 1 : 0);
            }

            if (luma.Length % 2 == 1)
            {
                uint run = runs[(cb[pairs] << 8) | cr[pairs]];
                uint first = run & 0xFFFF;
                outside += (uint)(luma[^1] - first) > (run >> 16) - first ? 1 : 0;
            }

            return outside;
        }

        int step = 1 << hShift;
        for (int c = 0, x = 0; c < cb.Length; c++)
        {
            uint run = runs[(cb[c] << 8) | cr[c]];
            uint first = run & 0xFFFF, span = (run >> 16) - first;
            for (int end = Math.Min(x + step, luma.Length); x < end; x++)
            {
                // Unsigned: a code below the first wraps past the span.
                if ((uint)(luma[x] - first) > span)
                    outside++;
            }
        }

        return outside;
    }

    static long Row(ReadOnlySpan<ushort> luma, ReadOnlySpan<ushort> cb, ReadOnlySpan<ushort> cr, uint[] runs, int hShift, int depth)
    {
        long outside = 0;
        int mask = (1 << depth) - 1;
        if (hShift == 1)
        {
            int pairs = luma.Length / 2;
            ref ushort l = ref MemoryMarshal.GetReference(luma);
            ref ushort b = ref MemoryMarshal.GetReference(cb);
            ref ushort r = ref MemoryMarshal.GetReference(cr);
            ref uint table = ref MemoryMarshal.GetArrayDataReference(runs);
            for (int c = 0; c < pairs; c++)
            {
                uint run = Unsafe.Add(ref table, ((Unsafe.Add(ref b, c) & mask) << depth) | (Unsafe.Add(ref r, c) & mask));
                uint first = run & 0xFFFF, span = (run >> 16) - first;
                outside += ((uint)((Unsafe.Add(ref l, 2 * c) & mask) - first) > span ? 1 : 0) + ((uint)((Unsafe.Add(ref l, (2 * c) + 1) & mask) - first) > span ? 1 : 0);
            }

            if (luma.Length % 2 == 1)
            {
                uint run = runs[((cb[pairs] & mask) << depth) | (cr[pairs] & mask)];
                uint first = run & 0xFFFF;
                outside += (uint)((luma[^1] & mask) - first) > (run >> 16) - first ? 1 : 0;
            }

            return outside;
        }

        int step = 1 << hShift;
        for (int c = 0, x = 0; c < cb.Length; c++)
        {
            uint run = runs[((cb[c] & mask) << depth) | (cr[c] & mask)];
            uint first = run & 0xFFFF, span = (run >> 16) - first;
            for (int end = Math.Min(x + step, luma.Length); x < end; x++)
            {
                if ((uint)((luma[x] & mask) - first) > span)
                    outside++;
            }
        }

        return outside;
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
        for (int i = 0; i < _shares.Count; i++)
        {
            bool skip = InBars(_times[i]);
            double share = skip ? 0 : _shares[i];
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
            spans.Add(new RangeSpan(_times[open], After(_shares.Count - 1), spanWorst));
        return (RangeSpan.Merge(spans, mergeGap), worst);
    }

    /// <summary>
    /// Whether the pixel of codes <paramref name="y"/>, <paramref name="cb"/>, <paramref name="cr"/> is in gamut: the
    /// arithmetic the tests' geq expression does, in the same order.
    /// </summary>
    public bool InGamut(int y, int cb, int cr)
    {
        var (yn, pb, pr) = Normalised(y, cb, cr);
        double r = yn + (2 * (1 - _kr) * pr);
        double b = yn + (2 * (1 - _kb) * pb);
        double g = (yn - (_kr * r) - (_kb * b)) / (1 - _kr - _kb);
        return r >= _low && r <= _high && g >= _low && g <= _high && b >= _low && b <= _high;
    }

    // Y from 0 (black) to 1 (white), Cb and Cr from -0.5 to 0.5: limited range 16-235 and 16-240 scaled to the depth,
    // full range 0 to 2^depth - 1 about 2^(depth - 1).
    (double Y, double Cb, double Cr) Normalised(int y, int cb, int cr)
    {
        double s = 1 << (_depth - 8);
        if (_fullRange)
        {
            double max = (1 << _depth) - 1, mid = 1 << (_depth - 1);
            return (y / max, (cb - mid) / max, (cr - mid) / max);
        }

        return ((y - (16 * s)) / (219 * s), (cb - (128 * s)) / (224 * s), (cr - (128 * s)) / (224 * s));
    }

    // The run of Y codes in gamut for every Cb and Cr: estimated from the limits, then moved to where InGamut changes.
    void Build(int depth, bool fullRange, int height)
    {
        (_depth, _fullRange) = (depth, fullRange);
        if (double.IsNaN(_kr))
        {
            (_kr, _kb) = height < 720 ? (0.299, 0.114) : (0.2126, 0.0722);
        }

        int size = 1 << depth, max = size - 1;
        _runs = new uint[size * size];
        double kg = 1 - _kr - _kb;
        double yScale = fullRange ? max : 219.0 * (1 << (depth - 8));
        double yOffset = fullRange ? 0 : 16.0 * (1 << (depth - 8));
        for (int cb = 0; cb < size; cb++)
        {
            for (int cr = 0; cr < size; cr++)
            {
                var (_, pb, pr) = Normalised(0, cb, cr);
                double dr = 2 * (1 - _kr) * pr, db = 2 * (1 - _kb) * pb, dg = -((_kr * dr) + (_kb * db)) / kg;
                double low = _low - Math.Min(dr, Math.Min(dg, db)), high = _high - Math.Max(dr, Math.Max(dg, db));
                int first = (int)Math.Clamp(Math.Ceiling((low * yScale) + yOffset), 0, max);
                int last = (int)Math.Clamp(Math.Floor((high * yScale) + yOffset), 0, max);

                // The estimate is off by a code at most: find one code in gamut near it (none when it is empty by more
                // than that), then the run's two ends.
                int? inside = null;
                for (int at = Math.Max(0, first - 1); at <= Math.Min(max, Math.Min(last, first + 1) + 1) && inside is null; at++)
                {
                    if (InGamut(at, cb, cr))
                        inside = at;
                }

                if (inside is { } found)
                {
                    (first, last) = (found, Math.Max(found, last));
                    while (first > 0 && InGamut(first - 1, cb, cr))
                        first--;
                    while (last > first && !InGamut(last, cb, cr))
                        last--;
                    while (last < max && InGamut(last + 1, cb, cr))
                        last++;
                }
                else
                {
                    // None in gamut: a run of the one code no sample has (65535), which every code is outside.
                    (first, last) = (ushort.MaxValue, ushort.MaxValue);
                }

                _runs[(cb << depth) | cr] = (uint)first | ((uint)last << 16);
            }
        }
    }
}
