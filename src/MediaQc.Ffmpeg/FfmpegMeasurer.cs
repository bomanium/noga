using System.Globalization;
using FrameReader;

namespace MediaQc.Ffmpeg;

/// <summary>
/// Measures a file on the FFmpeg path (FrameReader over FFmpeg's libraries, in this process): its duration and frame
/// rate, and from its audio the loudness (FFmpeg's own ebur128), the silences (FFmpeg's own silencedetect, with the
/// profile's settings) and the line-up tone (FrameReader's ReferenceTone), the loudness from the end of the line-up
/// (<see cref="LineUp"/>); from its picture black, bars, pixels outside broadcast range and the active picture
/// and each frame's picture type (<see cref="Pictures"/>); the keyframes and bitrate from the video packets
/// (<see cref="Packets"/>); the shot list (<see cref="Shots"/>); the start timecode the tag ffprobe's JSON gives (<see cref="StartTimecode"/>).
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
        measured = StartTimecode.Add(measured, path, cancellationToken);
        var freezes = new FreezeMeter(profile.FreezeNoise, profile.MinFreeze);
        measured = Pictures.Add(measured, Pictures.Measure(path, cancellationToken, freezes), profile) with
        {
            Freezes = freezes.Finish(),
            Packets = Packets.Read(path, cancellationToken),
        };
        measured = Shots.Add(measured, Shots.Detect(path, cancellationToken));
        if (!info.HasAudio)
            return measured;

        // Every audio track, each from the end of its own line-up (docs/decisions/2026-11-01-every-audio-track.md);
        // the first's figures are the file's, the others' each a TrackMeasures.
        int tracks = Math.Max(1, measured.AudioTrack?.Count ?? 1);
        var all = Enumerable.Range(0, tracks).Select(stream => MeasureTrack(path, profile, stream, measured.Duration, cancellationToken)).ToList();
        var first = all[0];
        return measured with
        {
            Silences = first.Silences,
            Channels = first.Channels,
            Tone = first.Tone,
            LoudnessFrom = first.LoudnessFrom,
            Loudness = first.Loudness,
            OtherTracks = [.. all.Skip(1)],
        };
    }

    // One audio track (ffmpeg's 0:a:STREAM): its silences and tone, which say where its line-up ends, then its loudness
    // from there.
    static TrackMeasures MeasureTrack(string path, Profile profile, int stream, double duration, CancellationToken cancellationToken)
    {
        var options = new AudioReaderOptions { Stream = stream, CancellationToken = cancellationToken };
        var silences = FindSilences(path, profile, options, cancellationToken);
        var tone = FindTone(path, options, cancellationToken);
        double from = LineUp.End(tone, silences, duration);
        return new TrackMeasures(stream + 1, from, MeasureLoudness(path, from, options, cancellationToken), silences, tone, MeasureChannels(path, profile, options, cancellationToken));
    }

    // Each channel on its own (ChannelMeter), from the decoded samples.
    static ChannelMeasures MeasureChannels(string path, Profile profile, AudioReaderOptions options, CancellationToken cancellationToken)
    {
        using var audio = new AudioReader(path, options);
        var meter = new ChannelMeter(audio.SampleRate, audio.Channels, profile.SilenceNoise, TimeSpan.FromSeconds(profile.MinSilence), profile.ClipRun);
        while (audio.TryRead(out var chunk))
        {
            cancellationToken.ThrowIfCancellationRequested();
            meter.Add(chunk.Samples);
        }

        return meter.Finish();
    }

    /// <summary>The filter graph silences are found with: silencedetect with the profile's level and length.</summary>
    public static string SilenceGraph(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return string.Create(CultureInfo.InvariantCulture, $"silencedetect=n={profile.SilenceNoise}dB:d={profile.MinSilence}");
    }

    // From the end of the line-up: the reader seeks as ffmpeg's -ss does, so the figures are ffmpeg -ss FROM's.
    static LoudnessMeasure MeasureLoudness(string path, double from, AudioReaderOptions options, CancellationToken cancellationToken)
    {
        using var audio = new AudioReader(path, options);
        if (from > 0)
            audio.Seek(TimeSpan.FromSeconds(from));
        var loudness = Loudness.Measure(audio, cancellationToken);
        double? integrated = loudness.Integrated <= Ungated ? null : loudness.Integrated;
        return new LoudnessMeasure(integrated, loudness.Range, loudness.TruePeak);
    }

    // silencedetect attaches lavfi.silence_start and lavfi.silence_end to the frames where a silence starts (once it
    // has lasted d) and ends, as its log prints them; a silence still running at the end has no end, and ends there.
    static List<TimeRange> FindSilences(string path, Profile profile, AudioReaderOptions options, CancellationToken cancellationToken)
    {
        using var audio = new AudioReader(path, options);
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

    static List<ToneSpan> FindTone(string path, AudioReaderOptions options, CancellationToken cancellationToken)
    {
        using var audio = new AudioReader(path, options);
        var segments = ReferenceTone.Find(audio, new ReferenceToneOptions { Frequency = ToneFrequency }, cancellationToken);
        return [.. segments.Select(s => new ToneSpan(s.Start.TotalSeconds, s.End.TotalSeconds, ToneFrequency, s.Level))];
    }
}
