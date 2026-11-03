using System.Numerics;
using System.Runtime.InteropServices;
using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// Frozen pictures (docs/decisions/2026-11-03-freeze-frames.md), FFmpeg's freezedetect done over the decoded planes of
/// either decoding path: each frame against a reference frame, the mean absolute difference over every plane's samples
/// divided by 2^depth; at most the noise (n) and the frame is frozen and the reference stays, else it becomes the
/// reference. A freeze starts at its reference frame once it has lasted the minimum (d) and ends at the first frame that
/// isn't frozen (one still running at the end, at the end).
/// </summary>
public sealed class FreezeMeter
{
    readonly double _noise;
    readonly double _minimum;
    readonly List<TimeRange> _freezes = [];
    byte[][]? _reference;
    double _referenceTime;
    bool _frozen;
    double? _start;
    double _last;
    double _frame;
    int _width;
    int _height;
    int _chromaWidth;
    int _chromaHeight;
    int _depth;

    /// <summary>A meter for freezes of <paramref name="minimum"/> seconds or more, at <paramref name="noise"/> (freezedetect's n and d).</summary>
    public FreezeMeter(double noise, double minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(noise);
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        (_noise, _minimum) = (noise, minimum);
    }

    /// <summary>
    /// Adds a frame of planar YUV at <paramref name="seconds"/>: its three planes and their strides, its size, chroma
    /// subsampling and depth (8 bits a sample, else 16 little-endian).
    /// </summary>
    public void Add(ReadOnlySpan<byte> y, int yStride, ReadOnlySpan<byte> u, int uStride, ReadOnlySpan<byte> v, int vStride, int width, int height, ChromaSubsampling chroma, int depth, double seconds)
    {
        if (_reference is null && _width == 0)
        {
            (_width, _height, _depth) = (width, height, depth);
            _chromaWidth = chroma == ChromaSubsampling.Yuv444 ? width : (width + 1) / 2;
            _chromaHeight = chroma == ChromaSubsampling.Yuv420 ? (height + 1) / 2 : height;
        }

        int bytes = _depth > 8 ? 2 : 1;
        bool frozen = false;
        if (_reference is { } reference)
        {
            // Frozen when sad / count / 2^depth <= noise: the sum stops once it is past that, which a moving picture is
            // within a few rows.
            ulong count = ((ulong)_width * (ulong)_height) + (2 * (ulong)_chromaWidth * (ulong)_chromaHeight);
            double limit = _noise * count * (1UL << _depth);
            ulong sad = 0;
            frozen = Within(ref sad, limit, y, yStride, reference[0], _width, _height, bytes)
                && Within(ref sad, limit, u, uStride, reference[1], _chromaWidth, _chromaHeight, bytes)
                && Within(ref sad, limit, v, vStride, reference[2], _chromaWidth, _chromaHeight, bytes);
            double duration = seconds - _referenceTime;
            if (duration >= _minimum - 1e-9)
            {
                if (!_frozen)
                    _start = _referenceTime;
                if (!frozen && _start is { } start)
                {
                    _freezes.Add(new TimeRange(start, seconds));
                    _start = null;
                }

                _frozen = frozen;
            }
        }

        if (!frozen)
        {
            _reference ??= [new byte[_width * bytes * _height], new byte[_chromaWidth * bytes * _chromaHeight], new byte[_chromaWidth * bytes * _chromaHeight]];
            Copy(y, yStride, _reference[0], _width, _height, bytes);
            Copy(u, uStride, _reference[1], _chromaWidth, _chromaHeight, bytes);
            Copy(v, vStride, _reference[2], _chromaWidth, _chromaHeight, bytes);
            _referenceTime = seconds;
        }

        if (seconds > _last)
            _frame = seconds - _last;
        _last = seconds;
    }

    /// <summary>The freezes found, a freeze still running at the end ending after the last frame.</summary>
    public IReadOnlyList<TimeRange> Finish()
    {
        if (_frozen && _start is { } start)
        {
            _freezes.Add(new TimeRange(start, _last + _frame));
            _start = null;
        }

        return _freezes;
    }

    static void Copy(ReadOnlySpan<byte> plane, int stride, byte[] into, int width, int height, int bytes)
    {
        int row = width * bytes;
        if (stride == row)
        {
            plane[..(row * height)].CopyTo(into);
            return;
        }

        for (int r = 0; r < height; r++)
            plane.Slice(r * stride, row).CopyTo(into.AsSpan(r * row, row));
    }

    // Adds the plane's sum of absolute differences to sad, row by row; false as soon as it is past the limit.
    static bool Within(ref ulong sad, double limit, ReadOnlySpan<byte> plane, int stride, byte[] reference, int width, int height, int bytes)
    {
        int row = width * bytes;
        for (int r = 0; r < height; r++)
        {
            var a = plane.Slice(r * stride, row);
            var b = reference.AsSpan(r * row, row);
            sad += bytes == 1 ? Row8(a, b) : Row16(MemoryMarshal.Cast<byte, ushort>(a), MemoryMarshal.Cast<byte, ushort>(b));
            if (sad > limit)
                return false;
        }

        return true;
    }

    static ulong Row8(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var sum = Vector<uint>.Zero;
        int i = 0;
        for (; i <= a.Length - Vector<byte>.Count; i += Vector<byte>.Count)
        {
            var (x, y) = (new Vector<byte>(a[i..]), new Vector<byte>(b[i..]));
            Vector.Widen(Vector.Max(x, y) - Vector.Min(x, y), out var low, out var high);
            Vector.Widen(low + high, out var l, out var h);
            sum += l + h;
        }

        ulong total = Vector.Sum(sum);
        for (; i < a.Length; i++)
            total += (ulong)Math.Abs(a[i] - b[i]);
        return total;
    }

    static ulong Row16(ReadOnlySpan<ushort> a, ReadOnlySpan<ushort> b)
    {
        // Each lane adds at most two differences of 65535 a step: a row of 65536 samples stays within a uint.
        var sum = Vector<uint>.Zero;
        int i = 0;
        for (; i <= a.Length - Vector<ushort>.Count; i += Vector<ushort>.Count)
        {
            var (x, y) = (new Vector<ushort>(a[i..]), new Vector<ushort>(b[i..]));
            Vector.Widen(Vector.Max(x, y) - Vector.Min(x, y), out var low, out var high);
            sum += low + high;
        }

        ulong total = Vector.Sum(sum);
        for (; i < a.Length; i++)
            total += (ulong)Math.Abs(a[i] - b[i]);
        return total;
    }
}
