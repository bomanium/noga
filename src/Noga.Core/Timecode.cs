using System.Globalization;

namespace Noga;

/// <summary>
/// SMPTE timecode (ST 12-1) at a frame rate, counted from a start timecode: HH:MM:SS:FF, or HH:MM:SS;FF drop-frame at
/// 29.97 and 59.94 (docs/decisions/2026-10-31-exit-codes-head-tail-timecode.md). At those rates labels are drop-frame
/// when the start timecode is (";" before the frames) or there is none, else non-drop; at any other rate non-drop, a
/// start timecode's ";" counted as ":" (docs/decisions/2026-11-06-drop-frame.md).
/// </summary>
public sealed class Timecode
{
    readonly long _rateNumerator;
    readonly long _rateDenominator;
    readonly int _nominal;
    readonly int _drop;
    readonly long _startFrame;

    /// <summary>
    /// A timecode at <paramref name="rateNumerator"/>/<paramref name="rateDenominator"/> frames a second, starting at
    /// <paramref name="start"/> (e.g. "10:00:00:00", or "10:00:00;00" drop-frame; null for 00:00:00:00, drop-frame where
    /// the rate has it).
    /// </summary>
    /// <exception cref="ArgumentException">The rate isn't positive, or <paramref name="start"/> isn't a timecode at this rate.</exception>
    public Timecode(long rateNumerator, long rateDenominator, string? start = null)
    {
        if (rateNumerator <= 0 || rateDenominator <= 0)
            throw new ArgumentException($"Not a frame rate: {rateNumerator}/{rateDenominator}.", nameof(rateNumerator));
        _rateNumerator = rateNumerator;
        _rateDenominator = rateDenominator;
        _nominal = (int)Math.Round((double)rateNumerator / rateDenominator, MidpointRounding.AwayFromZero);

        // Drop-frame only at the NTSC rates 30000/1001 and 60000/1001: 2 or 4 labels dropped a minute, but not every tenth.
        bool drop = HasDropFrame(rateNumerator, rateDenominator) && (start is null || IsDropFrame(start));
        _drop = drop ? (int)(rateNumerator / 15000) : 0;
        _startFrame = start is null ? 0 : Parse(start);
    }

    /// <summary>Whether labels are drop-frame.</summary>
    public bool DropFrame => _drop > 0;

    /// <summary>Whether the rate has drop-frame labels: multiples of 30000/1001 (29.97, 59.94), as FFmpeg allows them.</summary>
    public static bool HasDropFrame(long rateNumerator, long rateDenominator) =>
        rateDenominator == 1001 && rateNumerator > 0 && rateNumerator % 30000 == 0;

    /// <summary>Whether <paramref name="timecode"/> is written drop-frame: ";" before its frames, as FFmpeg writes it.</summary>
    public static bool IsDropFrame(string timecode)
    {
        ArgumentNullException.ThrowIfNull(timecode);
        int at = timecode.LastIndexOfAny([':', ';', '.', ',']);
        return at >= 0 && timecode[at] == ';';
    }

    /// <summary>The frame number at <paramref name="seconds"/> from the first frame (to the nearest frame).</summary>
    public long FrameAt(double seconds) => (long)Math.Round(seconds * _rateNumerator / _rateDenominator, MidpointRounding.AwayFromZero);

    /// <summary>The timecode of the frame at <paramref name="seconds"/> from the first frame.</summary>
    public string At(double seconds) => Label(_startFrame + FrameAt(seconds));

    /// <summary>The label of frame <paramref name="frame"/> counted from 00:00:00:00, wrapping at 24 hours.</summary>
    public string Label(long frame)
    {
        long perTenMinutes = ((long)_nominal * 600) - (_drop * 9);
        long perDay = perTenMinutes * 6 * 24;
        frame %= perDay;
        if (frame < 0)
            frame += perDay;
        if (_drop > 0)
        {
            long perMinute = ((long)_nominal * 60) - _drop;
            long tens = frame / perTenMinutes, rest = frame % perTenMinutes;
            frame += _drop * 9 * tens + (rest > _drop ? _drop * ((rest - _drop) / perMinute) : 0);
        }

        long ff = frame % _nominal, totalSeconds = frame / _nominal;
        char separator = _drop > 0 ? ';' : ':';
        return string.Create(CultureInfo.InvariantCulture, $"{totalSeconds / 3600:00}:{totalSeconds / 60 % 60:00}:{totalSeconds % 60:00}{separator}{ff:00}");
    }

    long Parse(string text)
    {
        string[] parts = text.Split(':', ';', '.', ',');
        if (parts.Length != 4 || !parts.All(p => p.Length is 2 or 3 && p.All(char.IsAsciiDigit)))
            throw new ArgumentException($"Not a timecode: '{text}'.", nameof(text));
        int h = int.Parse(parts[0], CultureInfo.InvariantCulture), m = int.Parse(parts[1], CultureInfo.InvariantCulture);
        int s = int.Parse(parts[2], CultureInfo.InvariantCulture), f = int.Parse(parts[3], CultureInfo.InvariantCulture);
        if (h > 23 || m > 59 || s > 59 || f >= _nominal || (_drop > 0 && s == 0 && f < _drop && m % 10 != 0))
            throw new ArgumentException($"Not a timecode at {_rateNumerator}/{_rateDenominator}: '{text}'.", nameof(text));
        long totalMinutes = 60L * h + m;
        return ((3600L * h) + (60L * m) + s) * _nominal + f - (_drop * (totalMinutes - (totalMinutes / 10)));
    }
}
