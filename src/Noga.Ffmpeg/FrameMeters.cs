using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>A decoded frame as the meters read it, from either decoder: disposed once every meter is done with it.</summary>
public interface IMeteredFrame : IDisposable
{
    /// <summary>The frame's width in pixels.</summary>
    int Width { get; }

    /// <summary>The frame's height in pixels.</summary>
    int Height { get; }

    /// <summary>Its presentation time, in the video's time base.</summary>
    long Pts { get; }

    /// <summary>The decoder's picture type.</summary>
    PictureType Type { get; }

    /// <summary>Plane <paramref name="index"/> (Y, Cb, Cr) and the bytes from one of its rows to the next.</summary>
    ReadOnlySpan<byte> Plane(int index, out int stride);
}

/// <summary>
/// The picture's meters over every frame of a video, for both decoding paths: FrameReader's PictureMeter (black, bars,
/// BRNG, crop, picture types), and the <see cref="FreezeMeter"/>, <see cref="InterlaceMeter"/> and
/// <see cref="ColourMeter"/> when given. Each
/// meter takes the frames on a thread of its own (<see cref="Lanes{T}"/>) while the next ones are decoded, PictureMeter,
/// the slowest, on <see cref="PictureLanes"/> threads: each frame measured on its own, and the frames' measures joined,
/// which gives the whole run's byte for byte (docs/notes/performance.md).
/// </summary>
public sealed class FrameMeters : IDisposable
{
    /// <summary>
    /// The threads PictureMeter measures on: two, which with the decoder's, the freeze meter's and the interlace
    /// meter's keep a 4-core machine busy (docs/notes/performance.md).
    /// </summary>
    public const int PictureLanes = 2;

    readonly List<PictureMeasures>[] _measured = [.. Enumerable.Range(0, PictureLanes).Select(_ => new List<PictureMeasures>())];
    readonly Lanes<Numbered> _lanes;
    int _count;

    /// <summary>
    /// Meters of frames in <paramref name="format"/>, telling <paramref name="at"/> each frame's time (seconds) as it
    /// is measured.
    /// </summary>
    public FrameMeters(PictureFormat format, FreezeMeter? freezes = null, InterlaceMeter? interlace = null, Action<double>? at = null, CancellationToken cancellationToken = default, ColourMeter? colours = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        double unit = (double)format.TimeBase.Num / format.TimeBase.Den;
        var consumers = new List<Action<Numbered>>();

        // Lane k measures the frames whose index is k modulo PictureLanes.
        for (int lane = 0; lane < PictureLanes; lane++)
        {
            var mine = _measured[lane];
            int which = lane;
            consumers.Add(numbered =>
            {
                if (numbered.Index % PictureLanes != which)
                    return;
                var frame = numbered.Frame;
                var meter = new PictureMeter(format, numbered.Index, numbered.Index + 1);
                var y = frame.Plane(0, out int yStride);
                var u = frame.Plane(1, out int uStride);
                var v = frame.Plane(2, out int vStride);
                meter.Add(y, yStride, u, uStride, v, vStride, frame.Pts, frame.Type);
                mine.Add(meter.Measures);
                if (which == 0)
                    at?.Invoke(frame.Pts * unit);
            });
        }

        if (freezes is not null)
        {
            consumers.Add(numbered =>
            {
                var frame = numbered.Frame;
                var y = frame.Plane(0, out int yStride);
                var u = frame.Plane(1, out int uStride);
                var v = frame.Plane(2, out int vStride);
                freezes.Add(y, yStride, u, uStride, v, vStride, frame.Width, frame.Height, format.Chroma, format.BitDepth, frame.Pts * unit);
            });
        }

        if (interlace is not null)
        {
            consumers.Add(numbered =>
            {
                var frame = numbered.Frame;
                var y = frame.Plane(0, out int yStride);
                var u = frame.Plane(1, out int uStride);
                var v = frame.Plane(2, out int vStride);
                interlace.Add(y, yStride, u, uStride, v, vStride, frame.Width, frame.Height, format.Chroma, format.BitDepth);
            });
        }

        if (colours is not null)
        {
            consumers.Add(numbered =>
            {
                var frame = numbered.Frame;
                var y = frame.Plane(0, out int yStride);
                var u = frame.Plane(1, out int uStride);
                var v = frame.Plane(2, out int vStride);
                colours.Add(y, yStride, u, uStride, v, vStride, frame.Width, frame.Height, format.Chroma, format.BitDepth, format.FullRange, frame.Pts * unit);
            });
        }

        _lanes = new Lanes<Numbered>(consumers, cancellationToken: cancellationToken);
    }

    /// <summary>Measures the next frame, in display order; the meters own it from here.</summary>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public void Post(IMeteredFrame frame) => _lanes.Post(new Numbered(frame, _count++));

    /// <summary>As <see cref="Post"/>, the frame kept until the handle returned is disposed too.</summary>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public IDisposable PostHeld(IMeteredFrame frame) => _lanes.PostHeld(new Numbered(frame, _count++));

    /// <summary>Waits for the meters; the frames' picture measures, joined (null when none was posted).</summary>
    public PictureMeasures? Finish()
    {
        _lanes.Finish();
        return _count == 0 ? null : PictureMeasures.Join(Enumerable.Range(0, _count).Select(i => _measured[i % PictureLanes][i / PictureLanes]));
    }

    public void Dispose() => _lanes.Dispose();

    // A frame and its index in display order, as the lanes take it.
    sealed record Numbered(IMeteredFrame Frame, int Index) : IDisposable
    {
        public void Dispose() => Frame.Dispose();
    }
}
