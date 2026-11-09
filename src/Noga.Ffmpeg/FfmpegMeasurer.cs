using System.Globalization;
using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// Measures a file on the FFmpeg path (FrameReader over FFmpeg's libraries, in this process): its duration and frame
/// rate, and from its audio the loudness (FFmpeg's own ebur128), the silences (FFmpeg's own silencedetect, with the
/// profile's settings) and the line-up tone (FrameReader's ReferenceTone), the loudness from the end of the line-up
/// (<see cref="LineUp"/>); from its picture black, bars, pixels outside broadcast range and the active picture
/// and each frame's picture type (<see cref="Pictures"/>); the keyframes, bitrate and steps between frames from the video packets
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
            if (measured.Format?.Container is "wav" or "aiff")
                measured = measured with { SoundHeader = SoundHeaders.Read(path) };
            else if (measured.Format?.Container == "mp3")
                measured = measured with { Mp3 = Mp3Headers.Read(path) };
            return MeasureSound(path, profile, measured, cancellationToken, stages.Together(1)[0]);
        }

        var freezes = new FreezeMeter(profile.FreezeNoise, profile.MinFreeze);
        var interlace = new InterlaceMeter();
        var colours = new ColourMeter(profile.ColourSpread, profile.MinConstantColour);
        var gamut = new GamutMeter(profile.GamutLow, profile.GamutHigh, measured.Format?.Video?.Matrix);
        var repeats = RepeatedFrames.Meter(profile, measured);
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
                pictures = Pictures.Measure(path, token, freezes, interlace, seconds => parts[0].Report(duration > 0 ? seconds / duration : 0), colours, gamut, repeats);
                packets = Packets.Read(path, video.FrameRate, token);
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
            Repeats = RepeatedFrames.From(repeats.Finish(), measured),
            ConstantColour = colours.Finish(),
            Interlace = interlace.Finish(),
            Packets = packets,
        };
        return Shots.Add(Pictures.AddGamut(measured, gamut, profile), shots!);
    }

    static Measurements MeasureSound(string path, Profile profile, Measurements measured, CancellationToken cancellationToken, IProgress<double> progress)
    {
        // Every audio track, each from the end of its own line-up (docs/decisions/2026-11-01-every-audio-track.md);
        // the first's figures are the file's, the others' each a TrackMeasures.
        int tracks = Math.Max(1, measured.AudioTrack?.Count ?? 1);
        var all = new List<TrackMeasures>();
        for (int stream = 0; stream < tracks; stream++)
        {
            all.Add(MeasureTrack(path, profile, stream, measured.Duration, Weights(measured, stream), cancellationToken));
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
    static TrackMeasures MeasureTrack(string path, Profile profile, int stream, double duration, ChannelWeights? weights, CancellationToken cancellationToken)
    {
        var options = new AudioReaderOptions { Stream = stream, CancellationToken = cancellationToken };
        var silences = FindSilences(path, profile, options, cancellationToken);
        var tone = FindTone(path, options, cancellationToken);

        // A tone to the end of the track's own sound is no line-up, though the picture runs on.
        var channels = MeasureChannels(path, profile, options, cancellationToken);
        double from = LineUp.End(tone, silences, channels?.Duration > 0 ? channels.Duration : duration);
        var loudness = MeasureLoudness(path, from, options, cancellationToken);
        var blocks = MeasureBlocks(path, from, weights, options, cancellationToken);
        loudness = loudness with
        {
            MaxMomentary = blocks.Momentary,
            MaxMomentaryAt = from + blocks.MomentaryAt,
            MaxShortTerm = blocks.ShortTerm,
            MaxShortTermAt = from + blocks.ShortTermAt,
        };
        return new TrackMeasures(stream + 1, from, loudness, silences, tone, channels);
    }

    // Each channel's weight in the loudness, as ebur128 weighs the track's layout (ffprobe's channel_layout); FFmpeg's
    // default layout for the count when it names none.
    static ChannelWeights? Weights(Measurements measured, int stream)
    {
        if (measured.Format?.Audio.FirstOrDefault(a => a.Number == stream + 1)?.Layout is not { } layout)
            return null;
        try
        {
            return ChannelWeights.ForLayout(layout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // The loudest momentary and short-term loudness from the end of the line-up, as ffmpeg -ss FROM's ebur128 M and S.
    static (double? Momentary, double MomentaryAt, double? ShortTerm, double ShortTermAt) MeasureBlocks(string path, double from, ChannelWeights? weights, AudioReaderOptions options, CancellationToken cancellationToken)
    {
        using var audio = new AudioReader(path, options);
        if (from > 0)
            audio.Seek(TimeSpan.FromSeconds(from));
        if (weights is not null && weights.Weights.Count != audio.Channels)
            weights = null;
        var meter = new LoudnessBlocks(audio.SampleRate, audio.Channels, weights);
        while (audio.TryRead(out var chunk))
        {
            cancellationToken.ThrowIfCancellationRequested();
            meter.Add(chunk.Samples);
        }

        return meter.Finish();
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
        return new LoudnessMeasure(integrated, loudness.Range, loudness.TruePeak) { SamplePeak = loudness.SamplePeak };
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
