using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// The loudest momentary (400 ms) and short-term (3 s) loudness of samples fed to it, and when, with FFmpeg's ebur128
/// arithmetic (f_ebur128.c): each channel K-weighted (its pre-filter and RLB high-pass, in double), its squares summed
/// over the last 400 ms and 3 s, the weighted sum's loudness (-0.691 + 10 log10) every 100 ms once the window is full,
/// as ebur128's M and S (docs/decisions/2026-11-05-short-term-loudness.md). For both decoding paths' samples; FrameReader
/// gives only the programme's figures.
/// </summary>
public sealed class LoudnessBlocks
{
    readonly int _channels, _tick, _short, _momentary;
    readonly double[] _weights;
    readonly double[] _preB = new double[3], _preA = new double[3], _rlbA = new double[3];

    // Per channel: the input's last two samples, the pre-filter's last three outputs and the RLB filter's.
    readonly double[] _x, _y, _z;
    readonly double[] _sumMomentary, _sumShort;
    readonly double[][] _cacheMomentary, _cacheShort;
    int _positionMomentary, _positionShort, _sinceTick;
    bool _fullMomentary, _fullShort;
    long _samples;
    double? _maxMomentary, _maxShort;
    double _maxMomentaryAt, _maxShortAt;

    /// <summary>A meter of <paramref name="channels"/> interleaved channels at <paramref name="rate"/> Hz, each weighted as <paramref name="weights"/> says.</summary>
    public LoudnessBlocks(int rate, int channels, ChannelWeights? weights = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rate, 10);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        _channels = channels;
        _weights = [.. (weights ?? ChannelWeights.Default(channels)).Weights];
        if (_weights.Length != channels)
            throw new ArgumentException($"{_weights.Length} weights for {channels} channels.", nameof(weights));

        // ebur128's coefficients for the rate (init_filter's).
        double f0 = 1681.974450955533, gain = 3.999843853973347, q = 0.7071752369554196;
        double k = Math.Tan(Math.PI * f0 / rate);
        double vh = Math.Pow(10, gain / 20), vb = Math.Pow(vh, 0.4996667741545416);
        double a0 = 1 + (k / q) + (k * k);
        _preB[0] = (vh + (vb * k / q) + (k * k)) / a0;
        _preB[1] = 2 * ((k * k) - vh) / a0;
        _preB[2] = (vh - (vb * k / q) + (k * k)) / a0;
        _preA[1] = 2 * ((k * k) - 1) / a0;
        _preA[2] = (1 - (k / q) + (k * k)) / a0;
        f0 = 38.13547087602444;
        q = 0.5003270373238773;
        k = Math.Tan(Math.PI * f0 / rate);
        _rlbA[1] = 2 * ((k * k) - 1) / (1 + (k / q) + (k * k));
        _rlbA[2] = (1 - (k / q) + (k * k)) / (1 + (k / q) + (k * k));

        _tick = rate / 10;
        _momentary = rate * 4 / 10;
        _short = rate * 3;
        _x = new double[channels * 3];
        _y = new double[channels * 3];
        _z = new double[channels * 3];
        _sumMomentary = new double[channels];
        _sumShort = new double[channels];
        _cacheMomentary = [.. Enumerable.Range(0, channels).Select(_ => new double[_momentary])];
        _cacheShort = [.. Enumerable.Range(0, channels).Select(_ => new double[_short])];
        Rate = rate;
    }

    /// <summary>The samples' rate (Hz).</summary>
    public int Rate { get; }

    /// <summary>Adds the next samples: whole instants, interleaved, full scale ±1.</summary>
    public void Add(ReadOnlySpan<float> interleaved)
    {
        if (interleaved.Length % _channels != 0)
            throw new ArgumentException("Not a whole number of instants.", nameof(interleaved));
        for (int i = 0; i < interleaved.Length; i += _channels)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                if (_weights[ch] == 0)
                    continue;
                int s = ch * 3;
                _x[s] = interleaved[i + ch];

                // The pre-filter, then the RLB filter (b = 1, -2, 1), as ebur128's FILTER macro shifts and sums.
                _y[s + 2] = _y[s + 1];
                _y[s + 1] = _y[s];
                _y[s] = (_x[s] * _preB[0]) + (_x[s + 1] * _preB[1]) + (_x[s + 2] * _preB[2]) - (_y[s + 1] * _preA[1]) - (_y[s + 2] * _preA[2]);
                _x[s + 2] = _x[s + 1];
                _x[s + 1] = _x[s];
                _z[s + 2] = _z[s + 1];
                _z[s + 1] = _z[s];
                _z[s] = _y[s] - (_y[s + 1] * 2) + _y[s + 2] - (_z[s + 1] * _rlbA[1]) - (_z[s + 2] * _rlbA[2]);

                double bin = _z[s] * _z[s];
                _sumMomentary[ch] += bin - _cacheMomentary[ch][_positionMomentary];
                _sumShort[ch] += bin - _cacheShort[ch][_positionShort];
                _cacheMomentary[ch][_positionMomentary] = bin;
                _cacheShort[ch][_positionShort] = bin;
            }

            if (++_positionMomentary == _momentary)
                (_positionMomentary, _fullMomentary) = (0, true);
            if (++_positionShort == _short)
                (_positionShort, _fullShort) = (0, true);
            _samples++;
            if (++_sinceTick == _tick)
            {
                _sinceTick = 0;
                double at = (double)_samples / Rate;
                if (_fullMomentary && Loudness(_sumMomentary, _momentary) is var m && (_maxMomentary is null || m > _maxMomentary))
                    (_maxMomentary, _maxMomentaryAt) = (m, at);
                if (_fullShort && Loudness(_sumShort, _short) is var st && (_maxShort is null || st > _maxShort))
                    (_maxShort, _maxShortAt) = (st, at);
            }
        }
    }

    /// <summary>
    /// The loudest momentary and short-term loudness (LUFS), each with when its window ends (seconds from the first
    /// sample); null when the samples never filled the window.
    /// </summary>
    public (double? Momentary, double MomentaryAt, double? ShortTerm, double ShortTermAt) Finish() =>
        (_maxMomentary, _maxMomentaryAt, _maxShort, _maxShortAt);

    // ebur128's LOUDNESS of the window's weighted mean power.
    double Loudness(double[] sums, int bins)
    {
        double power = 0;
        for (int ch = 0; ch < _channels; ch++)
            power += _weights[ch] * sums[ch];
        power /= bins;
        return -0.691 + (10 * Math.Log10(Math.Max(power, 1e-12)));
    }
}
