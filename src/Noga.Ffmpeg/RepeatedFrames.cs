namespace Noga.Ffmpeg;

/// <summary>
/// Single repeated frames (docs/decisions/2026-11-06-repeated-frames.md): a second <see cref="FreezeMeter"/> at
/// <c>Profile.RepeatNoise</c> whose minimum is a frame and a half, so a freeze is reported from two frames on; those of two
/// frames exactly (within half a frame) are one frame repeated, the second of the two.
/// </summary>
public static class RepeatedFrames
{
    /// <summary>The meter for <paramref name="measured"/>'s frame rate.</summary>
    public static FreezeMeter Meter(Profile profile, Measurements measured)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(measured);
        return new FreezeMeter(profile.RepeatNoise, 1.5 * Frame(measured));
    }

    /// <summary>When each repeated frame is: the second frame of each freeze two frames long.</summary>
    public static IReadOnlyList<double> From(IReadOnlyList<TimeRange> freezes, Measurements measured)
    {
        ArgumentNullException.ThrowIfNull(freezes);
        ArgumentNullException.ThrowIfNull(measured);
        double frame = Frame(measured);
        return [.. freezes.Where(f => Math.Abs(f.End - f.Start - (2 * frame)) < frame / 2).Select(f => f.Start + frame)];
    }

    static double Frame(Measurements measured) => (double)measured.RateDenominator / measured.RateNumerator;
}
