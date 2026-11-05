using ManagedCodecs;
using ManagedCodecs.Aac;
using ManagedCodecs.Containers;
using ManagedCodecs.H264;
using Noga.Ffmpeg;
using ChromaSubsampling = FrameReader.ChromaSubsampling;
using LoudnessMeter = FrameReader.LoudnessMeter;
using PictureFormat = FrameReader.PictureFormat;
using PictureMeasures = FrameReader.PictureMeasures;
using ReferenceToneFinder = FrameReader.ReferenceToneFinder;
using ReferenceToneOptions = FrameReader.ReferenceToneOptions;
using ShotDetection = ShotDetector.ShotDetection;
using SilenceFinder = FrameReader.SilenceFinder;
using SwscaleYuv420 = ShotDetector.FastYuv.SwscaleYuv420;
using YuvFrame = ShotDetector.YuvFrame;
using YuvVideo = ShotDetector.YuvVideo;

namespace Noga.Managed;

/// <summary>
/// Measures an H.264/AAC MP4 on the managed path (docs/decisions/2026-10-31-decoding.md): ManagedCodecs demuxes it and
/// decodes its H.264 and AAC, no native code (only the tags, start timecode and colour range, are read through
/// FFmpeg's libraries); the loudness is FrameReader's LoudnessMeter (FFmpeg's ebur128 arithmetic), the line-up tone
/// its ReferenceToneFinder, the silences its SilenceFinder (silencedetect's), the picture its PictureMeter
/// (blackdetect's, signalstats' BRNG, cropdetect's, the bars), the shots ShotDetector's on the same frames (converted by
/// ShotDetector.FastYuv): the same measures and settings as the FFmpeg path, so both give the same figures for the same
/// file.
/// </summary>
public static class ManagedMeasurer
{
    /// <summary>The line-up tone's frequency searched for (Hz), as on the FFmpeg path.</summary>
    const double ToneFrequency = 1000;

    // The pixel formats ShotDetector takes decoded frames in (its VideoReader); H.264 in others (High 4:2:2 at 10 bits,
    // High 4:4:4) goes to the FFmpeg path, whose shots convert any format.
    static readonly HashSet<string> ShotDetectorFormats = ["yuv420p", "yuvj420p", "yuv422p", "yuvj422p", "yuv420p10le"];

    /// <summary>
    /// Whether this path reads <paramref name="path"/>: an MP4 with H.264 video and one AAC audio track (or none). A file
    /// with several audio tracks goes to the FFmpeg path, which judges every one (ManagedCodecs' demuxer gives one:
    /// docs/decisions/2026-11-01-every-audio-track.md), and so does H.264 in a pixel format ShotDetector doesn't take.
    /// </summary>
    public static bool CanMeasure(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            // The video track's headers alone, and the audio's codecs from FFmpeg's: a QuickTime file's PCM track has one
            // sample an audio sample, and a demuxer of every track builds its whole table (29 bytes a sample) only to
            // answer no: 1.6 GB for 10 minutes of ProRes with stereo PCM (docs/notes/performance.md).
            using var file = File.OpenRead(path);
            using (var video = new Mp4Demuxer(file, MediaTypes.Video, leaveOpen: true))
            {
                if (video.Video?.Codec != CodecIds.H264)
                    return false;
            }

            if (Tags.Headers(path) is { } headers)
                return (headers.PixelFormat is null || ShotDetectorFormats.Contains(headers.PixelFormat)) && headers.AudioCodecs is [] or ["aac"];
            file.Position = 0;
            using var audio = new Mp4Demuxer(file, MediaTypes.Audio, leaveOpen: true);
            return audio.Audio is null || audio.Audio.Codec == CodecIds.Aac;
        }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or IOException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Measures <paramref name="path"/> with <paramref name="profile"/>'s measurement settings.</summary>
    /// <exception cref="InvalidDataException">The file isn't an MP4 with H.264 video (and AAC audio, if any).</exception>
    public static Measurements Measure(string path, Profile profile, CancellationToken cancellationToken = default, IProgress<double>? progress = null)
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
        var (packets, timestamps) = ReadPackets(path, cancellationToken);
        var freezes = new FreezeMeter(profile.FreezeNoise, profile.MinFreeze);
        var interlace = new InterlaceMeter();

        // The picture pass is nearly all of the time (it measures and detects the shots on the same frames); the sound
        // passes almost none, side by side with it (docs/notes/performance.md).
        var parts = new CheckProgress(progress).Together(audio is null ? 1 : 0.97, audio is null ? 0 : 0.03);
        PictureMeasures? pictures = null;
        ShotDetector.DetectionResult? shots = null;
        var sound = measured;
        SideBySide.Run(
            cancellationToken,
            token => (pictures, shots) = MeasurePictures(path, timestamps, freezes, interlace, token, parts[0]),
            token =>
            {
                if (audio is not null)
                    sound = MeasureSound(path, profile, audio, measured, token);
            });
        foreach (var part in parts)
            part.Report(1);
        return Shots.Add(Pictures.Add(sound, pictures, profile) with { Packets = packets, Freezes = freezes.Finish(), Interlace = interlace.Finish() }, shots!);
    }

    static Measurements MeasureSound(string path, Profile profile, AudioStreamInfo audio, Measurements measured, CancellationToken cancellationToken)
    {
        // First the tone and the silences, which say where the line-up ends; then the loudness from there.
        var silences = new SilenceFinder(audio.SampleRate, audio.Channels, profile.SilenceNoise, TimeSpan.FromSeconds(profile.MinSilence));
        var tone = new ReferenceToneFinder(audio.SampleRate, audio.Channels, new ReferenceToneOptions { Frequency = ToneFrequency });
        var channels = new ChannelMeter(audio.SampleRate, audio.Channels, profile.SilenceNoise, TimeSpan.FromSeconds(profile.MinSilence), profile.ClipRun);
        long samples = Decode(path, cancellationToken, (chunk, position) =>
        {
            silences.Add(chunk);
            channels.Add(chunk);
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
            Channels = channels.Finish(),
            Tone = toneSpans,
            LoudnessFrom = from,
            Loudness = new LoudnessMeasure(loudness.Integrated <= -70 ? null : loudness.Integrated, loudness.Range, loudness.TruePeak),
        };
    }

    // Every frame of the video through ManagedCodecs' H.264 decoder, in display order, into the picture's meters (on
    // threads of their own: FrameMeters) and ShotDetector, which pulls the frames one at a time on this thread. A frame
    // is released once the meters are done with it and ShotDetector has pulled the next.
    static (PictureMeasures? Pictures, ShotDetector.DetectionResult Shots) MeasurePictures(string path, IReadOnlyList<long> timestamps, FreezeMeter freezes, InterlaceMeter interlace, CancellationToken cancellationToken, IProgress<double> progress)
    {
        var tags = Tags.Read(path, cancellationToken);
        using var demuxer = Open(path);
        var video = demuxer.Video!;
        var decoder = new H264Decoder(video) { Threads = Environment.ProcessorCount };
        var timeBase = new FrameReader.Rational(video.TimeBase.Numerator, video.TimeBase.Denominator);
        var shotVideo = new YuvVideo
        {
            Width = video.Width,
            Height = video.Height,
            FrameRate = tags.AverageFrameRate,
            PixelFormat = tags.PixelFormat,
            ColorSpace = tags.ColorSpace,
            FullRange = tags.FullRange,
            ChromaLocation = tags.ChromaLocation,
            TimeBase = timeBase,
            FrameTimestamps = timestamps,
            StartPts = tags.StartPts,
            FrameCount = tags.FrameCount,
            Duration = tags.Duration,
            Rotation = tags.Rotation,
            FieldOrder = tags.FieldOrder,
            Codec = "h264",
            Container = tags.Container,
        };
        FrameMeters? meters = null;
        try
        {
            var options = Shots.Options with { Yuv420Converter = new SwscaleYuv420(), Progress = Shots.Fractions(progress) };
            var shots = ShotDetection.Detect(shotVideo, Frames(), options, cancellationToken);
            return (meters?.Finish(), shots);
        }
        finally
        {
            meters?.Dispose();
        }

        IEnumerable<YuvFrame> Frames()
        {
            foreach (var packet in demuxer.ReadPackets())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (packet.StreamIndex != video.StreamIndex)
                    continue;
                foreach (var frame in decoder.Decode(packet))
                {
                    using (Measure(frame))
                        yield return Shot(frame);
                }
            }

            foreach (var frame in decoder.Flush())
            {
                using (Measure(frame))
                    yield return Shot(frame);
            }
        }

        // The frame given to the meters, which own it from here; the handle keeps it for ShotDetector.
        IDisposable Measure(VideoFrame frame)
        {
            var format = frame.Format;
            if (format.PlaneCount() != 3)
            {
                frame.Dispose();
                throw new InvalidDataException($"{path}: monochrome H.264 isn't measured.");
            }

            meters ??= new FrameMeters(new PictureFormat(frame.Width, frame.Height, tags.FullRange, timeBase, tags.Rotation, format.BitDepth(), Chroma(format)), freezes, interlace, cancellationToken: cancellationToken);
            return meters.PostHeld(new Decoded(frame));
        }

        // The frame as ShotDetector's: its planes stay valid until the next is pulled.
        static YuvFrame Shot(VideoFrame frame) =>
            new(Plane(frame, 0), frame.Strides[0], Plane(frame, 1), frame.Strides[1], Plane(frame, 2), frame.Strides[2])
            {
                Pts = frame.Pts,
            };

        static ReadOnlyMemory<byte> Plane(VideoFrame frame, int i) => frame.Planes[i].AsMemory(frame.Offsets[i]);

        static ChromaSubsampling Chroma(PixelFormat format) => format.ToString() switch
        {
            var name when name.StartsWith("Yuv420", StringComparison.Ordinal) => ChromaSubsampling.Yuv420,
            var name when name.StartsWith("Yuv422", StringComparison.Ordinal) => ChromaSubsampling.Yuv422,
            _ => ChromaSubsampling.Yuv444,
        };
    }

    // ManagedCodecs' frame, as the meters read it.
    sealed class Decoded(VideoFrame frame) : IMeteredFrame
    {
        public int Width => frame.Width;

        public int Height => frame.Height;

        public long Pts => frame.Pts;

        public FrameReader.PictureType Type => (FrameReader.PictureType)(int)frame.PictureType;

        public ReadOnlySpan<byte> Plane(int index, out int stride)
        {
            stride = frame.Strides[index];
            return frame.Planes[index].AsSpan(frame.Offsets[index]);
        }

        public void Dispose() => frame.Dispose();
    }

    // The video packets as the demuxer gives them, as FFmpeg's (ffprobe -show_packets'): pts, size and key flag; and
    // their pts sorted, each frame's timestamp as ShotDetector's file path labels them.
    static (VideoPackets Packets, IReadOnlyList<long> Timestamps) ReadPackets(string path, CancellationToken cancellationToken)
    {
        using var demuxer = Open(path);
        var video = demuxer.Video!;
        double unit = (double)video.TimeBase.Numerator / video.TimeBase.Denominator;
        var packets = new List<(double, long, bool)>();
        var timestamps = new List<long>();
        foreach (var packet in demuxer.ReadPackets())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (packet.StreamIndex != video.StreamIndex)
                continue;
            packets.Add((packet.Pts * unit, packet.Data.Length, packet.IsKeyframe));
            timestamps.Add(packet.Pts);
        }

        timestamps.Sort();
        return (VideoPackets.From(packets), timestamps);
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
