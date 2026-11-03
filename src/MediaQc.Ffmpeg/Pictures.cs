using FrameReader;

namespace MediaQc.Ffmpeg;

/// <summary>
/// The picture's measures (FrameReader's PictureMeter: blackdetect's, signalstats' BRNG, cropdetect's and the colour
/// bars, exactly FFmpeg's figures) into <see cref="Measurements"/>, for both decoding paths: the FFmpeg path feeds it
/// FFmpeg's frames (<see cref="Measure"/>), the managed path ManagedCodecs' (docs/decisions/2026-11-01-range-and-crop.md).
/// </summary>
public static class Pictures
{
    /// <summary>
    /// Decodes <paramref name="path"/>'s video with FFmpeg's libraries and measures every frame, into
    /// <paramref name="freezes"/> too when given.
    /// </summary>
    /// <exception cref="InvalidDataException">The video isn't planar YUV of 8 to 16 bits.</exception>
    public static PictureMeasures? Measure(string path, CancellationToken cancellationToken = default, FreezeMeter? freezes = null)
    {
        Libraries.Load();
        using var decoder = new FrameDecoder(path, new FrameDecoderOptions { CancellationToken = cancellationToken });
        PictureMeter? meter = null;
        while (decoder.Next())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var frame = decoder.TakeFrame();
            meter ??= new PictureMeter(PictureFormat.Of(frame, decoder.TimeBase, 0)
                ?? throw new InvalidDataException($"{path}: {frame.PixelFormat} isn't planar YUV of 8 to 16 bits."));
            meter.Add(frame);
            if (freezes is not null)
            {
                var format = PictureFormat.Of(frame, decoder.TimeBase, 0)!;
                var y = frame.GetPlane(0, out int yStride);
                var u = frame.GetPlane(1, out int uStride);
                var v = frame.GetPlane(2, out int vStride);
                double unit = (double)decoder.TimeBase.Num / decoder.TimeBase.Den;
                freezes.Add(y, yStride, u, uStride, v, vStride, frame.Width, frame.Height, format.Chroma, format.BitDepth, frame.Pts * unit);
            }
        }

        return meter?.Measures;
    }

    /// <summary><paramref name="measured"/> with black, bars, pixels outside broadcast range, the active picture and each frame's picture type.</summary>
    public static Measurements Add(Measurements measured, PictureMeasures? pictures, Profile profile)
    {
        ArgumentNullException.ThrowIfNull(measured);
        ArgumentNullException.ThrowIfNull(profile);
        if (pictures is null || pictures.Count == 0)
            return measured;

        double unit = (double)pictures.TimeBase.Num / pictures.TimeBase.Den;
        double frame = (double)measured.RateDenominator / measured.RateNumerator;
        double At(int i) => pictures.Pts(i) * unit;
        double After(int i) => i + 1 < pictures.Count ? At(i + 1) : At(i) + frame;

        // Bars' PLUGE and the like are out of range on purpose: those frames aren't judged.
        var spans = new List<RangeSpan>();
        FrameShare? worst = null;
        int? start = null;
        double spanWorst = 0;
        for (int i = 0; i < pictures.Count; i++)
        {
            double share = pictures.IsBars(i) ? 0 : pictures.Brng(i);
            if (!pictures.IsBars(i) && (worst is null || share > worst.Value.Share))
                worst = new FrameShare(At(i), share);
            if (share > profile.MaxOutOfRange)
            {
                start ??= i;
                spanWorst = Math.Max(spanWorst, share);
            }
            else if (start is { } s)
            {
                spans.Add(new RangeSpan(At(s), At(i), spanWorst));
                (start, spanWorst) = (null, 0);
            }
        }

        if (start is { } open)
            spans.Add(new RangeSpan(At(open), After(pictures.Count - 1), spanWorst));

        // blackdetect ends a span still running at the end at the last frame's pts (a FIXME in FFmpeg); the span
        // covers that frame too, so it ends after it, as the tail it is.
        int last = pictures.FirstFrame + pictures.Count - 1;
        double BlackEnd(FrameSpan s) => s.EndFrame == last && pictures.IsBlack(pictures.Count - 1) ? After(pictures.Count - 1) : s.EndPts * unit;

        var crop = pictures.Crop();
        return measured with
        {
            Black = [.. pictures.BlackSpans(profile.MinBlack).Select(s => new TimeRange(s.StartPts * unit, BlackEnd(s)))],
            Bars = [.. pictures.BarsSpans().Select(s => new TimeRange(s.StartPts * unit, s.EndFrame < pictures.FirstFrame + pictures.Count ? s.EndPts * unit : After(s.EndFrame - pictures.FirstFrame - 1)))],
            OutOfRange = RangeSpan.Merge(spans, profile.RangeMergeGap),
            WorstOutOfRange = worst,
            Crop = crop is null ? null : new ActivePicture(pictures.Width, pictures.Height, crop.W, crop.H, crop.X, crop.Y),
            PictureTypes = pictures.Types(),
            NotMeasured = [.. measured.NotMeasured.Where(c => c is not ("black" or "bars"))],
        };
    }
}
