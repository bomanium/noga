using FrameReader;

namespace MediaQc.Ffmpeg;

/// <summary>
/// An audio track's channels one by one (docs/decisions/2026-11-03-channel-faults.md), from its decoded samples
/// (interleaved floats, as FrameReader's AudioReader and ManagedCodecs' AAC decoder give them): each channel's silences
/// (FrameReader's SilenceFinder on that channel alone: silencedetect's), its RMS level (astats' RMS level), its runs of
/// samples at full scale, and the first two channels' phase (aphasemeter's: the mean over the samples of
/// 2·L·R / (L² + R²), 1 where both are 0). Both decoding paths feed it.
/// </summary>
public sealed class ChannelMeter
{
    /// <summary>A sample at or beyond this is at full scale: a 16-bit sample's largest (32767/32768), and up.</summary>
    public const float FullScale = 32767f / 32768f;

    readonly int _channels;
    readonly int _sampleRate;
    readonly int _clipRun;
    readonly SilenceFinder[] _silences;
    readonly double[] _squares;
    readonly int[] _run;
    readonly int[] _clipped;
    readonly long[] _full;
    readonly float[] _one;
    double _phase;
    long _samples;

    /// <summary>
    /// A meter for <paramref name="channels"/> channels at <paramref name="sampleRate"/>: silences below
    /// <paramref name="silenceNoise"/> dBFS for <paramref name="minSilence"/> or more, and clipped runs of
    /// <paramref name="clipRun"/> samples or more.
    /// </summary>
    public ChannelMeter(int sampleRate, int channels, double silenceNoise, TimeSpan minSilence, int clipRun)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(clipRun, 1);
        (_sampleRate, _channels, _clipRun) = (sampleRate, channels, clipRun);
        _silences = [.. Enumerable.Range(0, channels).Select(_ => new SilenceFinder(sampleRate, 1, silenceNoise, minSilence))];
        _squares = new double[channels];
        _run = new int[channels];
        _clipped = new int[channels];
        _full = new long[channels];
        _one = new float[8192];
    }

    /// <summary>Adds interleaved samples (a whole number of frames, every channel each).</summary>
    public void Add(ReadOnlySpan<float> samples)
    {
        int frames = samples.Length / _channels;
        for (int c = 0; c < _channels; c++)
        {
            for (int start = 0; start < frames; start += _one.Length)
            {
                int count = Math.Min(_one.Length, frames - start);
                for (int i = 0; i < count; i++)
                    _one[i] = samples[((start + i) * _channels) + c];
                _silences[c].Add(_one.AsSpan(0, count));
            }
        }

        for (int i = 0; i < frames; i++)
        {
            var frame = samples.Slice(i * _channels, _channels);
            for (int c = 0; c < _channels; c++)
            {
                float x = frame[c];
                _squares[c] += (double)x * x;
                if (Math.Abs(x) >= FullScale)
                {
                    _full[c]++;
                    if (++_run[c] == _clipRun)
                        _clipped[c]++;
                }
                else
                {
                    _run[c] = 0;
                }
            }

            if (_channels >= 2)
            {
                // aphasemeter's own arithmetic, in float as it does it.
                float l = frame[0], r = frame[1];
                float f = l * r / ((l * l) + (r * r)) * 2;
                _phase += float.IsNaN(f) ? 1 : f;
            }
        }

        _samples += frames;
    }

    /// <summary>What was measured.</summary>
    public ChannelMeasures Finish()
    {
        var silences = _silences.Select(s => (IReadOnlyList<TimeRange>)[.. s.Finish().Select(span => new TimeRange((double)span.StartSample / _sampleRate, (double)span.EndSample / _sampleRate))]).ToList();
        var rms = _squares.Select(sum => _samples == 0 || sum == 0 ? double.NegativeInfinity : 10 * Math.Log10(sum / _samples)).ToList();
        double? phase = _channels >= 2 && _samples > 0 ? _phase / _samples : null;
        return new ChannelMeasures(_channels, (double)_samples / _sampleRate, silences, rms, [.. _clipped], [.. _full], phase);
    }
}
