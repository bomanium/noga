namespace Noga.Ffmpeg;

/// <summary>
/// An audio track's channels one by one (docs/decisions/2026-11-03-channel-faults.md), from its decoded samples
/// (interleaved floats, as FrameReader's AudioReader and ManagedCodecs' AAC decoder give them), for both decoding paths:
/// FrameReader's ChannelMeter (each channel's silences, silencedetect's; its RMS level, astats'; its samples at full
/// scale and the runs of them; the first two channels' phase, aphasemeter's), its figures in seconds.
/// </summary>
public sealed class ChannelMeter
{
    readonly int _sampleRate;
    readonly FrameReader.ChannelMeter _meter;

    /// <summary>
    /// A meter for <paramref name="channels"/> channels at <paramref name="sampleRate"/>: silences below
    /// <paramref name="silenceNoise"/> dBFS for <paramref name="minSilence"/> or more, and clipped runs of
    /// <paramref name="clipRun"/> samples or more.
    /// </summary>
    public ChannelMeter(int sampleRate, int channels, double silenceNoise, TimeSpan minSilence, int clipRun)
    {
        _sampleRate = sampleRate;
        _meter = new FrameReader.ChannelMeter(sampleRate, channels, new FrameReader.ChannelMeterOptions { SilenceLevel = silenceNoise, SilenceDuration = minSilence, ClipRun = clipRun });
    }

    /// <summary>Adds interleaved samples (a whole number of frames, every channel each).</summary>
    public void Add(ReadOnlySpan<float> samples) => _meter.Add(samples);

    /// <summary>What was measured.</summary>
    public ChannelMeasures Finish()
    {
        var measured = _meter.Finish();
        var channels = measured.Channels;
        return new ChannelMeasures(
            channels.Count,
            (double)measured.Samples / _sampleRate,
            [.. channels.Select(c => (IReadOnlyList<TimeRange>)[.. c.Silences.Select(s => new TimeRange((double)s.StartSample / _sampleRate, (double)s.EndSample / _sampleRate))])],
            [.. channels.Select(c => c.RmsLevel)],
            [.. channels.Select(c => c.ClippedRuns)],
            [.. channels.Select(c => c.FullScaleSamples)],
            measured.Phase);
    }
}
