using System.Numerics;
using System.Runtime.InteropServices;
using FrameReader;

namespace MediaQc.Ffmpeg;

/// <summary>
/// Whether the picture is interlaced, from the picture (docs/decisions/2026-11-03-interlacing.md): FFmpeg's idet (FFmpeg
/// 8.0's vf_idet.c, its defaults: intl_thres 1.04, prog_thres 1.5) over the decoded planes of either decoding path. Each
/// frame is weighed against the one before and after it, line by line, as top field first, bottom field first,
/// progressive or undetermined (single frame), and through a four-frame history (multiple frames); the totals are
/// idet's, which it prints at the end.
/// </summary>
public sealed class InterlaceMeter
{
    const int Tff = 0, Bff = 1, Progressive = 2, Undetermined = 3;

    readonly float _interlaceThreshold;
    readonly float _progressiveThreshold;
    readonly long[] _single = new long[4];
    readonly long[] _multi = new long[4];
    readonly int[] _history = [Undetermined, Undetermined, Undetermined, Undetermined];
    int _lastType = Undetermined;
    byte[][]? _prev;
    byte[][]? _cur;
    byte[][]? _next;
    int[] _widths = [];
    int[] _heights = [];
    int _bytes;
    int _depth;

    /// <summary>A meter at idet's thresholds (its defaults: 1.04 and 1.5).</summary>
    public InterlaceMeter(float interlaceThreshold = 1.04f, float progressiveThreshold = 1.5f) =>
        (_interlaceThreshold, _progressiveThreshold) = (interlaceThreshold, progressiveThreshold);

    /// <summary>Adds a frame of planar YUV: its three planes and their strides, its size, chroma subsampling and depth.</summary>
    public void Add(ReadOnlySpan<byte> y, int yStride, ReadOnlySpan<byte> u, int uStride, ReadOnlySpan<byte> v, int vStride, int width, int height, ChromaSubsampling chroma, int depth)
    {
        if (_widths.Length == 0)
        {
            int cw = chroma == ChromaSubsampling.Yuv444 ? width : (width + 1) / 2;
            int ch = chroma == ChromaSubsampling.Yuv420 ? (height + 1) / 2 : height;
            (_widths, _heights, _bytes, _depth) = ([width, cw, cw], [height, ch, ch], depth > 8 ? 2 : 1, depth);
        }

        // idet's frames: prev ← cur ← next ← this one; the first is its own cur, and nothing is weighed until there is a prev.
        var frame = _prev ?? NewFrame();
        Copy(y, yStride, frame[0], 0);
        Copy(u, uStride, frame[1], 1);
        Copy(v, vStride, frame[2], 2);
        Step(frame);
    }

    /// <summary>The totals, as idet prints them at the end (the last frame weighed once more against itself as next).</summary>
    public InterlaceMeasures Finish()
    {
        if (_cur is not null && _next is not null)
        {
            var again = NewFrame();
            for (int p = 0; p < 3; p++)
                _next[p].CopyTo(again[p], 0);
            Step(again);
            _cur = null;
        }

        return new InterlaceMeasures(_single[Tff], _single[Bff], _single[Progressive], _single[Undetermined], _multi[Tff], _multi[Bff], _multi[Progressive], _multi[Undetermined]);
    }

    void Step(byte[][] frame)
    {
        (_prev, _cur, _next) = (_cur, _next, frame);
        if (_cur is null)
        {
            _cur = NewFrame();
            for (int p = 0; p < 3; p++)
                frame[p].CopyTo(_cur[p], 0);
        }

        if (_prev is null)
            return;
        Filter(_prev, _cur, _next);
    }

    void Filter(byte[][] prev, byte[][] cur, byte[][] next)
    {
        // Whole-number sums, so the rows can be added up in any order: in bands, in parallel.
        long alpha0 = 0, alpha1 = 0, delta = 0;
        var gate = new object();
        for (int p = 0; p < 3; p++)
        {
            int h = _heights[p], row = _widths[p] * _bytes, plane = p;
            int bands = Math.Max(1, Math.Min(Environment.ProcessorCount, (h - 4) / 32));
            Parallel.For(0, bands, band =>
            {
                long a0 = 0, a1 = 0, d = 0;
                int from = 2 + (int)((long)(h - 4) * band / bands), to = 2 + (int)((long)(h - 4) * (band + 1) / bands);
                for (int y = from; y < to; y++)
                {
                    var above = cur[plane].AsSpan((y - 1) * row, row);
                    var below = cur[plane].AsSpan((y + 1) * row, row);
                    long withPrev = Line(above, prev[plane].AsSpan(y * row, row), below);
                    long withNext = Line(above, next[plane].AsSpan(y * row, row), below);
                    if ((y & 1) == 0)
                        (a0, a1) = (a0 + withPrev, a1 + withNext);
                    else
                        (a0, a1) = (a0 + withNext, a1 + withPrev);
                    d += Line(above, cur[plane].AsSpan(y * row, row), below);
                }

                lock (gate)
                    (alpha0, alpha1, delta) = (alpha0 + a0, alpha1 + a1, delta + d);
            });
        }

        // As C does it: the int64 sums made floats, against float thresholds.
        int type = (float)alpha0 > _interlaceThreshold * alpha1 ? Tff
            : (float)alpha1 > _interlaceThreshold * alpha0 ? Bff
            : (float)alpha1 > _progressiveThreshold * delta ? Progressive
            : Undetermined;

        for (int i = _history.Length - 1; i > 0; i--)
            _history[i] = _history[i - 1];
        _history[0] = type;
        int best = Undetermined, match = 0;
        foreach (int seen in _history)
        {
            if (seen == Undetermined)
                continue;
            if (best == Undetermined)
                best = seen;
            if (seen == best)
            {
                match++;
            }
            else
            {
                match = 0;
                break;
            }
        }

        if (_lastType == Undetermined ? match > 0 : match > 2)
            _lastType = best;
        _single[type]++;
        _multi[_lastType]++;
    }

    // idet's filter_line: the sum over the row of |a + c - 2b| (an int, as idet's).
    long Line(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ReadOnlySpan<byte> c) =>
        _bytes == 1 ? Line8(a, b, c)
        : _depth <= 14 ? LineShort(MemoryMarshal.Cast<byte, short>(a), MemoryMarshal.Cast<byte, short>(b), MemoryMarshal.Cast<byte, short>(c))
        : Line16(MemoryMarshal.Cast<byte, ushort>(a), MemoryMarshal.Cast<byte, ushort>(b), MemoryMarshal.Cast<byte, ushort>(c));

    // Up to 14 bits a sample, |a + c - 2b| fits a short: a lane of shorts, widened once to add up.
    static int LineShort(ReadOnlySpan<short> a, ReadOnlySpan<short> b, ReadOnlySpan<short> c)
    {
        var sum = Vector<int>.Zero;
        int x = 0;
        for (; x <= a.Length - Vector<short>.Count; x += Vector<short>.Count)
        {
            var v = Vector.Abs(new Vector<short>(a[x..]) + new Vector<short>(c[x..]) - (new Vector<short>(b[x..]) * 2));
            Vector.Widen(v, out var low, out var high);
            sum += low + high;
        }

        int total = Vector.Sum(sum);
        for (; x < a.Length; x++)
            total += Math.Abs(a[x] + c[x] - (2 * b[x]));
        return total;
    }

    static int Line8(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ReadOnlySpan<byte> c)
    {
        var sum = Vector<int>.Zero;
        int x = 0;
        for (; x <= a.Length - Vector<byte>.Count; x += Vector<byte>.Count)
        {
            Vector.Widen(new Vector<byte>(a[x..]), out var a0, out var a1);
            Vector.Widen(new Vector<byte>(b[x..]), out var b0, out var b1);
            Vector.Widen(new Vector<byte>(c[x..]), out var c0, out var c1);
            sum += Abs(a0, b0, c0) + Abs(a1, b1, c1);
        }

        int total = Vector.Sum(sum);
        for (; x < a.Length; x++)
            total += Math.Abs(a[x] + c[x] - (2 * b[x]));
        return total;

        static Vector<int> Abs(Vector<ushort> a, Vector<ushort> b, Vector<ushort> c)
        {
            var v = Vector.Abs(Vector.AsVectorInt16(a) + Vector.AsVectorInt16(c) - (Vector.AsVectorInt16(b) * 2));
            Vector.Widen(v, out var low, out var high);
            return low + high;
        }
    }

    static int Line16(ReadOnlySpan<ushort> a, ReadOnlySpan<ushort> b, ReadOnlySpan<ushort> c)
    {
        var sum = Vector<int>.Zero;
        int x = 0;
        for (; x <= a.Length - Vector<ushort>.Count; x += Vector<ushort>.Count)
        {
            Vector.Widen(new Vector<ushort>(a[x..]), out var a0, out var a1);
            Vector.Widen(new Vector<ushort>(b[x..]), out var b0, out var b1);
            Vector.Widen(new Vector<ushort>(c[x..]), out var c0, out var c1);
            sum += Vector.Abs(Vector.AsVectorInt32(a0) + Vector.AsVectorInt32(c0) - (Vector.AsVectorInt32(b0) * 2))
                + Vector.Abs(Vector.AsVectorInt32(a1) + Vector.AsVectorInt32(c1) - (Vector.AsVectorInt32(b1) * 2));
        }

        int total = Vector.Sum(sum);
        for (; x < a.Length; x++)
            total += Math.Abs(a[x] + c[x] - (2 * b[x]));
        return total;
    }

    byte[][] NewFrame() => [new byte[_widths[0] * _bytes * _heights[0]], new byte[_widths[1] * _bytes * _heights[1]], new byte[_widths[2] * _bytes * _heights[2]]];

    void Copy(ReadOnlySpan<byte> plane, int stride, byte[] into, int p)
    {
        int row = _widths[p] * _bytes;
        if (stride == row)
        {
            plane[..(row * _heights[p])].CopyTo(into);
            return;
        }

        for (int r = 0; r < _heights[p]; r++)
            plane.Slice(r * stride, row).CopyTo(into.AsSpan(r * row, row));
    }
}
