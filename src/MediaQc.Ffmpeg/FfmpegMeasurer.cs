using System.Globalization;
using FrameReader;

namespace MediaQc.Ffmpeg;

/// <summary>
/// Measures a file on the FFmpeg path (FrameReader over FFmpeg's libraries, in this process): its duration and frame
/// rate, and from its audio the loudness (FFmpeg's own ebur128), the silences (FFmpeg's own silencedetect, with the
/// profile's settings) and the line-up tone (FrameReader's ReferenceTone); the loudness from the end of the line-up (<see cref="LineUp"/>). Picture measures (black, bars) and the
/// start timecode aren't in FrameReader's public API yet: black and bars are listed as not measured, and timecodes
/// count from 00:00:00:00 (docs/backlog.md).
/// </summary>
public static class FfmpegMeasurer
{
    /// <summary>The line-up tone's frequency searched for (Hz): EBU's and SMPTE's.</summary>
    public const double ToneFrequency = 1000;

    /// <summary>
    /// What ebur128 gives as the integrated loudness when no block passes the absolute gate (LUFS); <see
    /// cref="LoudnessMeasure.Integrated"/> is null then.
    /// </summary>
    const double Ungated = -70;

    /// <summary>Measures <paramref name="path"/> with <paramref name="profile"/>'s measurement settings.</summary>
    /// <exception cref="InvalidDataException">The file has no video stream.</exception>
    /// <exception cref="FrameReaderException">FFmpeg's libraries didn't load, or the file can't be read.</exception>
    public static Measurements Measure(string path, Profile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(profile);
        Libraries.Load();

        var info = MediaProbe.Probe(path, new ProbeOptions { CancellationToken = cancellationToken });
        var video = info.Video ?? throw new InvalidDataException($"{path} has no video stream.");
        var measured = new Measurements
        {
            Duration = (info.Duration ?? video.Duration ?? TimeSpan.Zero).TotalSeconds,
            RateNumerator = video.FrameRate.Num,
            RateDenominator = video.FrameRate.Den,
            NotMeasured = ["bars", "black"],
        };
        if (!info.HasAudio)
            return measured;

        var silences = FindSilences(path, profile, cancellationToken);
        var tone = FindTone(path, cancellationToken);
        double from = LineUp.End(tone, silences);
        return measured with
        {
            Silences = silences,
            Tone = tone,
            LoudnessFrom = from,
            Loudness = MeasureLoudness(path, from, cancellationToken),
        };
    }

    /// <summary>The filter graph silences are found with: silencedetect with the profile's level and length.</summary>
    public static string SilenceGraph(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return string.Create(CultureInfo.InvariantCulture, $"silencedetect=n={profile.SilenceNoise}dB:d={profile.MinSilence}");
    }

    // From the end of the line-up: the reader seeks as ffmpeg's -ss does, so the figures are ffmpeg -ss FROM's.
    static LoudnessMeasure MeasureLoudness(string path, double from, CancellationToken cancellationToken)
    {
        using var audio = new AudioReader(path, new AudioReaderOptions { CancellationToken = cancellationToken });
        if (from > 0)
            audio.Seek(TimeSpan.FromSeconds(from));
        var loudness = Loudness.Measure(audio, cancellationToken);
        double? integrated = loudness.Integrated <= Ungated ? null : loudness.Integrated;
        return new LoudnessMeasure(integrated, loudness.Range, loudness.TruePeak);
    }

    // silencedetect attaches lavfi.silence_start and lavfi.silence_end to the frames where a silence starts (once it
    // has lasted d) and ends, as its log prints them; a silence still running at the end has no end, and ends there.
    static List<TimeRange> FindSilences(string path, Profile profile, CancellationToken cancellationToken)
    {
        using var audio = new AudioReader(path, new AudioReaderOptions { CancellationToken = cancellationToken });
        using var filter = new AudioFilter(audio, SilenceGraph(profile));
        var silences = new List<TimeRange>();
        double? start = null;
        double end = 0;
        while (filter.TryRead(out var chunk))
        {
            cancellationToken.ThrowIfCancellationRequested();
            end = chunk.Time.TotalSeconds + ((double)chunk.SampleCount / chunk.SampleRate);
            if (Seconds(filter, "lavfi.silence_start") is { } started)
                start = started;
            if (Seconds(filter, "lavfi.silence_end") is { } ended && start is { } from)
            {
                silences.Add(new TimeRange(from, ended));
                start = null;
            }
        }

        if (start is { } open)
            silences.Add(new TimeRange(open, Math.Max(open, end)));
        return silences;
    }

    static double? Seconds(AudioFilter filter, string key) =>
        filter.Metadata.TryGetValue(key, out string? value) ? double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture) : null;

    static List<ToneSpan> FindTone(string path, CancellationToken cancellationToken)
    {
        using var audio = new AudioReader(path, new AudioReaderOptions { CancellationToken = cancellationToken });
        var segments = ReferenceTone.Find(audio, new ReferenceToneOptions { Frequency = ToneFrequency }, cancellationToken);
        return [.. segments.Select(s => new ToneSpan(s.Start.TotalSeconds, s.End.TotalSeconds, ToneFrequency, s.Level))];
    }
}
