using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// The first video stream's packets through FFmpeg's demuxer (FrameReader's MediaProbe, as ffprobe -show_packets reads
/// them): each one's pts, size and key flag, for the keyframes and the bitrate
/// (docs/decisions/2026-11-01-frames-bitrate-shots.md), and the steps between them (<see cref="Steps"/>).
/// </summary>
public static class Packets
{
    /// <summary>Reads <paramref name="path"/>'s video packets, their steps counted at <paramref name="rate"/> (the stream's r_frame_rate when null).</summary>
    /// <exception cref="FrameReaderException">The file can't be opened or read, or has no video stream.</exception>
    public static VideoPackets Read(string path, Rational? rate = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        Libraries.Load();
        var info = MediaProbe.Probe(path, new ProbeOptions { PacketTimestamps = true, PacketKeyframes = true, CancellationToken = cancellationToken });
        if (info.Video is not { } video || info.PacketTimestamps is not { } pts || info.PacketDecodeTimestamps is not { } dts || info.PacketSizes is not { } sizes || info.PacketKeyframes is not { } keys)
            throw new FrameReaderException(FrameReaderError.InvalidInput, $"{path} has no video stream.", null);

        double unit = (double)video.TimeBase.Num / video.TimeBase.Den;
        var times = Enumerable.Range(0, pts.Count).Select(i => pts[i] ?? dts[i]).ToList();
        var packets = Enumerable.Range(0, times.Count).Where(i => times[i] is not null).Select(i => (times[i]!.Value * unit, (long)sizes[i], keys[i]));
        return VideoPackets.From(packets) with { Steps = Steps(times, video.TimeBase, rate ?? video.FrameRate) };
    }

    /// <summary>
    /// The steps between frames whose pts (in <paramref name="timeBase"/>, any order; null ones left out) are
    /// <paramref name="pts"/>, at <paramref name="rate"/>: FrameReader's FrameSteps, in seconds.
    /// </summary>
    public static FrameStepCount Steps(IEnumerable<long?> pts, Rational timeBase, Rational rate)
    {
        var steps = FrameSteps.Of(pts, timeBase, rate);
        double unit = (double)timeBase.Num / timeBase.Den;
        return new FrameStepCount(steps.Steps, steps.Irregular, [.. steps.Gaps.Select(g => new FrameGapSpan(g.BeforePts * unit, g.AfterPts * unit, g.Missing))]);
    }
}
