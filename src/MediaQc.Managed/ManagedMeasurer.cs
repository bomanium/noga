using ManagedCodecs;
using ManagedCodecs.Aac;
using ManagedCodecs.Containers;
using ManagedCodecs.H264;
using MediaQc.Ffmpeg;
using ChromaSubsampling = FrameReader.ChromaSubsampling;
using LoudnessMeter = FrameReader.LoudnessMeter;
using PictureFormat = FrameReader.PictureFormat;
using PictureMeasures = FrameReader.PictureMeasures;
using PictureMeter = FrameReader.PictureMeter;
using ReferenceToneFinder = FrameReader.ReferenceToneFinder;
using ReferenceToneOptions = FrameReader.ReferenceToneOptions;
using SilenceFinder = FrameReader.SilenceFinder;

namespace MediaQc.Managed;

/// <summary>
/// Measures an H.264/AAC MP4 on the managed path (docs/decisions/2026-10-31-decoding.md): ManagedCodecs demuxes it and
/// decodes its H.264 and AAC, no native code (only the tags, start timecode and colour range, are read through
/// FFmpeg's libraries); the loudness is FrameReader's LoudnessMeter (FFmpeg's ebur128 arithmetic), the line-up tone
/// its ReferenceToneFinder, the silences its SilenceFinder (silencedetect's), the picture its PictureMeter
/// (blackdetect's, signalstats' BRNG, cropdetect's, the bars): the same measures and settings as the FFmpeg path, so
/// both give the same figures for the same file.
/// </summary>
public static class ManagedMeasurer
{
    /// <summary>The line-up tone's frequency searched for (Hz), as on the FFmpeg path.</summary>
    const double ToneFrequency = 1000;

    /// <summary>Whether this path reads <paramref name="path"/>: an MP4 with H.264 video and AAC audio (or none).</summary>
    public static bool CanMeasure(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            using var demuxer = Demuxer.Open(path) as Mp4Demuxer;
            return demuxer?.Video?.Codec == CodecIds.H264 && (demuxer.Audio is null || demuxer.Audio.Codec == CodecIds.Aac);
        }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or IOException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Measures <paramref name="path"/> with <paramref name="profile"/>'s measurement settings.</summary>
    /// <exception cref="InvalidDataException">The file isn't an MP4 with H.264 video (and AAC audio, if any).</exception>
    public static Measurements Measure(string path, Profile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(profile);
        VideoStreamInfo video;
        AudioStreamInfo? audio;
        using (var demuxer = Open(path))
            (video, audio) = (demuxer.Video!, demuxer.Audio);

        var (numerator, denominator) = Rate(video.FramesPerSecond);
        double videoDuration = video.FrameCount * (double)denominator / numerator;
        var measured = new Measurements
        {
            Duration = videoDuration,
            RateNumerator = numerator,
            RateDenominator = denominator,
        };
        measured = StartTimecode.Add(measured, path, cancellationToken);
        measured = Pictures.Add(measured, MeasurePictures(path, cancellationToken), profile);
        if (audio is null)
            return measured;

        // First the tone and the silences, which say where the line-up ends; then the loudness from there.
        var silences = new SilenceFinder(audio.SampleRate, audio.Channels, profile.SilenceNoise, TimeSpan.FromSeconds(profile.MinSilence));
        var tone = new ReferenceToneFinder(audio.SampleRate, audio.Channels, new ReferenceToneOptions { Frequency = ToneFrequency });
        long samples = Decode(path, cancellationToken, (chunk, position) =>
        {
            silences.Add(chunk);
            tone.Add(chunk, TimeSpan.FromSeconds((double)position / audio.SampleRate));
        });
        var silenceSpans = silences.Finish().Select(s => new TimeRange((double)s.StartSample / audio.SampleRate, (double)s.EndSample / audio.SampleRate)).ToList();
        var toneSpans = tone.Finish().Select(s => new ToneSpan(s.Start.TotalSeconds, s.End.TotalSeconds, ToneFrequency, s.Level)).ToList();
        double duration = Math.Max(measured.Duration, (double)samples / audio.SampleRate);
        double from = LineUp.End(toneSpans, silenceSpans, duration);

        // As ffmpeg -ss FROM: the first sample at or after it.
        long first = (long)Math.Ceiling(from * audio.SampleRate);
        var meter = new LoudnessMeter(audio.SampleRate, audio.Channels, null);
        Decode(path, cancellationToken, (chunk, position) =>
        {
            long skip = Math.Clamp(first - position, 0, chunk.Length / audio.Channels);
            meter.Add(chunk[(int)(skip * audio.Channels)..]);
        });
        var loudness = meter.Finish().Loudness;

        return measured with
        {
            Duration = duration,
            Silences = silenceSpans,
            Tone = toneSpans,
            LoudnessFrom = from,
            Loudness = new LoudnessMeasure(loudness.Integrated <= -70 ? null : loudness.Integrated, loudness.Range, loudness.TruePeak),
        };
    }

    // Every frame of the video through ManagedCodecs' H.264 decoder into FrameReader's PictureMeter, in display order.
    static PictureMeasures? MeasurePictures(string path, CancellationToken cancellationToken)
    {
        var (fullRange, rotation) = Pictures.VideoFormat(path, cancellationToken);
        using var demuxer = Open(path);
        var video = demuxer.Video!;
        var decoder = new H264Decoder(video) { Threads = Environment.ProcessorCount };
        var timeBase = new FrameReader.Rational(video.TimeBase.Numerator, video.TimeBase.Denominator);
        PictureMeter? meter = null;
        foreach (var packet in demuxer.ReadPackets())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (packet.StreamIndex == video.StreamIndex)
                Add(decoder.Decode(packet));
        }

        Add(decoder.Flush());
        return meter?.Measures;

        void Add(IReadOnlyList<VideoFrame> frames)
        {
            foreach (var frame in frames)
            {
                using (frame)
                {
                    var format = frame.Format;
                    if (format.PlaneCount() != 3)
                        throw new InvalidDataException($"{path}: monochrome H.264 isn't measured.");
                    meter ??= new PictureMeter(new PictureFormat(frame.Width, frame.Height, fullRange, timeBase, rotation, format.BitDepth(), Chroma(format)));
                    meter.Add(
                        Plane(frame, 0),
                        frame.Strides[0],
                        Plane(frame, 1),
                        frame.Strides[1],
                        Plane(frame, 2),
                        frame.Strides[2],
                        frame.Pts,
                        (FrameReader.PictureType)(int)frame.PictureType);
                }
            }
        }

        static ReadOnlySpan<byte> Plane(VideoFrame frame, int i) => frame.Planes[i].AsSpan(frame.Offsets[i]);

        static ChromaSubsampling Chroma(PixelFormat format) => format.ToString() switch
        {
            var name when name.StartsWith("Yuv420", StringComparison.Ordinal) => ChromaSubsampling.Yuv420,
            var name when name.StartsWith("Yuv422", StringComparison.Ordinal) => ChromaSubsampling.Yuv422,
            _ => ChromaSubsampling.Yuv444,
        };
    }

    delegate void ChunkHandler(ReadOnlySpan<float> interleaved, long position);

    static Mp4Demuxer Open(string path)
    {
        var demuxer = Demuxer.Open(path);
        if (demuxer is Mp4Demuxer mp4 && mp4.Video?.Codec == CodecIds.H264 && (mp4.Audio is null || mp4.Audio.Codec == CodecIds.Aac))
            return mp4;
        (demuxer as IDisposable)?.Dispose();
        throw new InvalidDataException($"{path} isn't an MP4 with H.264 video and AAC audio.");
    }

    // The audio decoded in file order, each frame interleaved; returns the samples per channel. ManagedCodecs drops the
    // encoder's priming as the edit list says, as FFmpeg does, so the first sample is the presentation's.
    static long Decode(string path, CancellationToken cancellationToken, ChunkHandler handle)
    {
        using var demuxer = Open(path);
        var audio = demuxer.Audio!;
        var decoder = new AacDecoder(audio);
        float[] interleaved = [];
        long position = 0;
        foreach (var packet in demuxer.ReadPackets())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (packet.StreamIndex != audio.StreamIndex || decoder.Decode(packet) is not { SampleCount: > 0 } frame)
                continue;
            int channels = frame.Channels, count = frame.SampleCount * channels;
            if (interleaved.Length < count)
                interleaved = new float[count];
            for (int c = 0; c < channels; c++)
            {
                float[] samples = frame.Samples[c];
                for (int i = 0; i < frame.SampleCount; i++)
                    interleaved[(i * channels) + c] = samples[i];
            }

            handle(interleaved.AsSpan(0, count), position);
            position += frame.SampleCount;
        }

        return position;
    }

    // The container gives frames per second as a double: 25, or 29.97... for 30000/1001.
    static (long Numerator, long Denominator) Rate(double fps)
    {
        double whole = Math.Round(fps);
        if (Math.Abs(fps - whole) < 1e-6)
            return ((long)whole, 1);
        double ntsc = Math.Round(fps * 1.001);
        return Math.Abs((ntsc * 1000 / 1001) - fps) < 1e-3 ? ((long)(ntsc * 1000), 1001) : ((long)Math.Round(fps * 1000), 1000);
    }
}
