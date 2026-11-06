using System.Runtime.InteropServices;
using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// One frame's signalstats figures, as <c>signalstats</c> gives them at the frame's depth: each plane's YLOW and YHIGH
/// (the 10th and 90th percentiles of its samples) and YAVG (their mean), for Y, U and V.
/// </summary>
public readonly record struct ColourStats(ushort YLow, ushort YHigh, float YAvg, ushort ULow, ushort UHigh, float UAvg, ushort VLow, ushort VHigh, float VAvg);

/// <summary>
/// Constant-colour frames (docs/decisions/2026-11-06-constant-colour.md), over the decoded planes of either decoding
/// path: each frame's signalstats YLOW, YHIGH and YAVG for Y, U and V (vf_signalstats.c's histograms). A frame is flat
/// when every plane's HIGH less LOW is at most the spread (8-bit codes, scaled to the depth); a run is flat frames whose
/// averages stay within the spread of its first frame's, from its first frame to the frame after its last (one still
/// running at the end, to after the last frame), kept when it lasts the minimum. Every fourth row of the luma first: when
/// those rows alone put more than 10% of the frame's samples at or below one value and more than 10% at or above another
/// over the spread from it, its YLOW and YHIGH are that far apart at least and the frame isn't flat, without the rest
/// (docs/notes/performance.md).
/// </summary>
public sealed class ColourMeter
{
    readonly double _spread;
    readonly double _minimum;
    readonly List<ColourStats?> _frames = [];
    readonly List<ColourSpan> _spans = [];
    readonly int[] _histogram = new int[4 << 16];
    Run? _run;
    double _last;
    double _frame;
    int _depth = 8;
    bool _fullRange;
    bool _sd;

    /// <summary>A meter for runs of <paramref name="minimum"/> seconds or more of frames flat within <paramref name="spread"/> 8-bit codes.</summary>
    public ColourMeter(double spread, double minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spread);
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        (_spread, _minimum) = (spread, minimum);
    }

    /// <summary>Each frame's figures, in the order added; null for a frame its luma's every fourth row shows isn't flat.</summary>
    public IReadOnlyList<ColourStats?> Frames => _frames;

    /// <summary>
    /// Adds a frame of planar YUV at <paramref name="seconds"/>: its three planes and their strides, its size, chroma
    /// subsampling, depth (8 bits a sample, else 16 little-endian) and range.
    /// </summary>
    public void Add(ReadOnlySpan<byte> y, int yStride, ReadOnlySpan<byte> u, int uStride, ReadOnlySpan<byte> v, int vStride, int width, int height, ChromaSubsampling chroma, int depth, bool fullRange, double seconds)
    {
        (_depth, _fullRange, _sd) = (depth, fullRange, height < 720);
        int chromaWidth = chroma == ChromaSubsampling.Yuv444 ? width : (width + 1) / 2;
        int chromaHeight = chroma == ChromaSubsampling.Yuv420 ? (height + 1) / 2 : height;
        double spread = _spread * (1 << (depth - 8));
        if (Wide(y, yStride, width, height, depth, spread))
        {
            _frames.Add(null);
            End(seconds);
            Next(seconds);
            return;
        }

        var (yLow, yHigh, yAvg) = Plane(y, yStride, width, height, depth);
        var (uLow, uHigh, uAvg) = Plane(u, uStride, chromaWidth, chromaHeight, depth);
        var (vLow, vHigh, vAvg) = Plane(v, vStride, chromaWidth, chromaHeight, depth);
        var stats = new ColourStats(yLow, yHigh, (float)yAvg, uLow, uHigh, (float)uAvg, vLow, vHigh, (float)vAvg);
        _frames.Add(stats);

        bool flat = yHigh - yLow <= spread && uHigh - uLow <= spread && vHigh - vLow <= spread;
        if (_run is { } run && !(flat && run.Near(stats, spread)))
            End(seconds);
        if (flat)
            (_run ??= new Run(seconds, stats)).Add(stats);
        Next(seconds);
    }

    void Next(double seconds)
    {
        if (seconds > _last)
            _frame = seconds - _last;
        _last = seconds;
    }

    /// <summary>The runs found, a run still going at the end ending after the last frame.</summary>
    public IReadOnlyList<ColourSpan> Finish()
    {
        End(_last + _frame);
        return _spans;
    }

    void End(double seconds)
    {
        if (_run is not { } run)
            return;
        _run = null;
        if (seconds - run.Start < _minimum - 1e-9)
            return;
        double scale = 1.0 / (1 << (_depth - 8));
        _spans.Add(ColourSpan.Of(run.Start, seconds, run.Y / run.Count * scale, run.U / run.Count * scale, run.V / run.Count * scale, _fullRange, _sd));
    }

    // Whether every fourth row of the luma shows the frame's YHIGH less YLOW is over the spread: those rows' counts are
    // at most the frame's, so a value they reach the 10% mark at (lrint) is at least YLOW, and one with more than the
    // frame's last 10% at or above it at most YHIGH.
    bool Wide(ReadOnlySpan<byte> plane, int stride, int width, int height, int depth, double spread)
    {
        int size = 1 << depth;
        var histogram = _histogram.AsSpan(0, size);
        histogram.Clear();
        for (int r = 0; r < height; r += 4)
        {
            if (depth > 8)
            {
                foreach (ushort sample in MemoryMarshal.Cast<byte, ushort>(plane.Slice(r * stride, width * 2)))
                    histogram[sample & (size - 1)]++;
            }
            else
            {
                foreach (byte sample in plane.Slice(r * stride, width))
                    histogram[sample]++;
            }
        }

        var (low, high) = Marks((long)width * height);
        long above = (long)width * height - high + 1;
        int lowAt = -1, highAt = -1;
        long running = 0;
        for (int i = 0; i < size && lowAt < 0; i++)
        {
            running += histogram[i];
            if (running >= low)
                lowAt = i;
        }

        running = 0;
        for (int i = size - 1; i >= 0 && highAt < 0; i--)
        {
            running += histogram[i];
            if (running >= above)
                highAt = i;
        }

        return lowAt >= 0 && highAt >= 0 && highAt - lowAt > spread;
    }

    // signalstats' 10% and 90% marks of a plane's samples: lrint(count * 10 / 100.) and lrint(count * 90 / 100.).
    static (long Low, long High) Marks(long count) =>
        ((long)Math.Round(count * 10 / 100.0, MidpointRounding.ToEven), (long)Math.Round(count * 90 / 100.0, MidpointRounding.ToEven));

    // signalstats' LOW, HIGH and AVG of one plane: the first values at which the histogram's running count reaches
    // lrint(10% and 90% of the samples), and their mean.
    (ushort Low, ushort High, double Avg) Plane(ReadOnlySpan<byte> plane, int stride, int width, int height, int depth)
    {
        // Four histograms, a sample in each in turn, so a flat plane's runs of one value don't wait on each other's
        // counts; added together after.
        int size = 1 << depth;
        var counts = _histogram.AsSpan(0, 4 * size);
        counts.Clear();
        int mask = size - 1;
        for (int r = 0; r < height; r++)
        {
            if (depth > 8)
                Count(MemoryMarshal.Cast<byte, ushort>(plane.Slice(r * stride, width * 2)), counts, size, mask);
            else
                Count(plane.Slice(r * stride, width), counts, size);
        }

        var histogram = counts[..size];
        for (int i = 0; i < size; i++)
            histogram[i] += counts[size + i] + counts[(2 * size) + i] + counts[(3 * size) + i];

        long total = 0;
        long count = (long)width * height;
        var (low, high) = Marks(count);
        int lowAt = -1, highAt = -1;
        long running = 0;
        for (int i = 0; i < size; i++)
        {
            running += histogram[i];
            total += (long)histogram[i] * i;
            if (lowAt < 0 && running >= low)
                lowAt = i;
            if (highAt < 0 && running >= high)
                highAt = i;
        }

        return ((ushort)Math.Max(lowAt, 0), (ushort)Math.Max(highAt, 0), count == 0 ? 0 : (double)total / count);
    }

    static void Count(ReadOnlySpan<byte> row, Span<int> counts, int size)
    {
        int i = 0;
        for (; i <= row.Length - 4; i += 4)
        {
            counts[row[i]]++;
            counts[size + row[i + 1]]++;
            counts[(2 * size) + row[i + 2]]++;
            counts[(3 * size) + row[i + 3]]++;
        }

        for (; i < row.Length; i++)
            counts[row[i]]++;
    }

    static void Count(ReadOnlySpan<ushort> row, Span<int> counts, int size, int mask)
    {
        int i = 0;
        for (; i <= row.Length - 4; i += 4)
        {
            counts[row[i] & mask]++;
            counts[size + (row[i + 1] & mask)]++;
            counts[(2 * size) + (row[i + 2] & mask)]++;
            counts[(3 * size) + (row[i + 3] & mask)]++;
        }

        for (; i < row.Length; i++)
            counts[row[i] & mask]++;
    }

    // A run of flat frames: where it starts, its first frame's averages and the sums of its frames'.
    sealed class Run(double start, ColourStats first)
    {
        public double Start { get; } = start;

        public double Y { get; private set; }

        public double U { get; private set; }

        public double V { get; private set; }

        public int Count { get; private set; }

        public bool Near(ColourStats stats, double spread) =>
            Math.Abs(stats.YAvg - first.YAvg) <= spread && Math.Abs(stats.UAvg - first.UAvg) <= spread && Math.Abs(stats.VAvg - first.VAvg) <= spread;

        public void Add(ColourStats stats)
        {
            (Y, U, V) = (Y + stats.YAvg, U + stats.UAvg, V + stats.VAvg);
            Count++;
        }
    }
}
