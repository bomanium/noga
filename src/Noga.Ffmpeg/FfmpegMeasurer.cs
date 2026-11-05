using System.Globalization;
using FrameReader;

namespace Noga.Ffmpeg;

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

    /// <summary>The frame rate a file with sound only counts its timecodes at.</summary>
    const int SoundOnlyRate = 25;

    // Each part's share of a check's progress (docs/notes/performance.md): the picture pass, the shots' own decode,
    // and the sound, almost nothing. They run side by side, so the check is done when the slowest is.
    const double PictureShare = 0.78, ShotsShare = 0.2, SoundShare = 0.02;

    /// <summary>
    /// Measures <paramref name="path"/> with <paramref name="profile"/>'s measurement settings, telling
    /// <paramref name="progress"/> how far it has got (0 to 1).
    /// </summary>
    /// <exception cref="InvalidDataException">The file has neither a video nor an audio stream.</exception>
    /// <exception cref="FrameReaderException">FFmpeg's libraries didn't load, or the file can't be read.</exception>
    public static Measurements Measure(string path, Profile profile, CancellationToken cancellationToken = default, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(profile);
        Libraries.Load();

        var info = MediaProbe.Probe(path, new ProbeOptions { CancellationToken = cancellationToken });
        var video = info.Video;
        if (video is null && !info.HasAudio)
            throw new InvalidDataException($"{path} has neither picture nor sound.");
        var measured = new Measurements
        {
            Duration = (info.Duration ?? video?.Duration ?? TimeSpan.Zero).TotalSeconds,
            RateNumerator = video?.FrameRate.Num ?? SoundOnlyRate,
            RateDenominator = video?.FrameRate.Den ?? 1,
            NotMeasured = ["bars", "black"],
        };
        measured = StartTimecode.Add(measured, path, cancellationToken);
        var stages = new CheckProgress(progress);

        // Sound only (docs/decisions/2026-11-04-odd-files.md): a WAV or an M4A, whose cover art is a video stream to
        // FFmpeg but not a picture (MediaFormat leaves it out); its timecodes count at a nominal 25 fps.
        if (video is null || measured.Format is { Video: null })
        {
            measured = StartTimecode.Add(measured with { RateNumerator = SoundOnlyRate, RateDenominator = 1, NotMeasured = [], HasPicture = false }, path, cancellationToken);
            return MeasureSound(path, profile, measured, cancellationToken, stages.Together(1)[0]);
        }

        var freezes = new FreezeMeter(profile.FreezeNoise, profile.MinFreeze);
        var interlace = new InterlaceMeter();
        double duration = measured.Duration;
        var parts = stages.Together(PictureShare, ShotsShare, SoundShare);

        // The picture pass, the shots (ShotDetector decodes the video again: the frame source it would take the
        // picture pass's frames through has no 10-bit 4:2:2, ProRes HQ's) and the sound, side by side.
        PictureMeasures? pictures = null;
        VideoPackets? packets = null;
        ShotDetector.DetectionResult? shots = null;
        var sound = measured;
        SideBySide.Run(
            cancellationToken,
            token =>
            {
                pictures = Pictures.Measure(path, token, freezes, interlace, seconds => parts[0].Report(duration > 0 ? seconds / duration : 0));
                packets = Packets.Read(path, token);
            },
            token => shots = Shots.Detect(path, token, parts[1]),
            token =>
            {
                if (info.HasAudio)
                    sound = MeasureSound(path, profile, measured, token, parts[2]);
            });
        foreach (var part in parts)
            part.Report(1);

        measured = Pictures.Add(sound, pictures, profile) with
        {
            Freezes = freezes.Finish(),
            Interlace = interlace.Finish(),
            Packets = packets,
        };
        return Shots.Add(measured, shots!);
    }

    static Measurements MeasureSound(string path, Profile profile, Measurements measured, CancellationToken cancellationToken, IProgress<double> progress)
    {
        // Every audio track, each from the end of its own line-up (docs/decisions/2026-11-01-every-audio-track.md);
        // the first's figures are the file's, the others' each a TrackMeasures.
        int tracks = Math.Max(1, measured.AudioTrack?.Count ?? 1);
        var all = new List<TrackMeasures>();
        for (int stream = 0; stream < tracks; stream++)
        {
            all.Add(MeasureTrack(path, profile, stream, measured.Duration, cancellationToken));
            progress.Report((stream + 1.0) / tracks);
        }

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
