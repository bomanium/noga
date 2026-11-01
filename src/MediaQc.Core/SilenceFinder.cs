namespace MediaQc;

/// <summary>
/// FFmpeg's silencedetect (af_silencedetect.c, the default: all channels together) on samples the caller decodes, a
/// chunk at a time: the same spans for the same float samples. A sample is silent below the noise level's amplitude;
/// a silence is a run of silent samples, counted across the interleaved channels, at least the minimum length long;
/// it starts where that run's first instant is and ends at the first instant with a sample at or above the level.
/// </summary>
public sealed class SilenceFinder
{
    readonly int _sampleRate;
    readonly int _channels;
    readonly float _noise;
    readonly long _notify;
    readonly List<TimeRange> _found = [];
    long _silent;
    long? _start;
    long _end;

    /// <summary>Starts a search: <paramref name="noise"/> in dBFS (silencedetect's n), <paramref name="minDuration"/> in seconds (its d).</summary>
    public SilenceFinder(int sampleRate, int channels, double noise, double minDuration)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        _sampleRate = sampleRate;
        _channels = channels;
        _noise = (float)Math.Pow(10, noise / 20);

        // silencedetect: d is microseconds, rescaled to samples (rounded), counted over every channel's samples.
        _notify = (long)Math.Round(minDuration * sampleRate, MidpointRounding.AwayFromZero) * channels;
    }

    /// <summary>
    /// Searches the next samples, interleaved, starting at instant <paramref name="position"/> (samples per channel
    /// from the start); only whole instants.
    /// </summary>
    public void Add(ReadOnlySpan<float> samples, long position)
    {
        if (samples.Length % _channels != 0)
            throw new ArgumentException("Only whole instants (all channels).", nameof(samples));
        for (int i = 0; i < samples.Length; i++)
        {
            float sample = samples[i];
            if (sample < _noise && sample > -_noise)
            {
                if (_start is null && ++_silent >= _notify)
                    _start = position + (i / _channels) + 1 - (_notify / _channels);
            }
            else
            {
                if (_start is { } start)
                    _found.Add(new TimeRange(Seconds(start), Seconds(position + (i / _channels))));
                _silent = 0;
                _start = null;
            }
        }

        _end = position + (samples.Length / _channels);
    }

    /// <summary>The silences, in order; one still running at the end ends after the last sample added.</summary>
    public IReadOnlyList<TimeRange> Finish()
    {
        if (_start is { } start)
        {
            _found.Add(new TimeRange(Seconds(start), Seconds(_end)));
            _start = null;
        }

        return _found;
    }

    double Seconds(long samples) => (double)samples / _sampleRate;
}
