using FFmpeg.AutoGen;
using FrameReader;

namespace Noga.Ffmpeg;

/// <summary>
/// The first video stream's packets through FFmpeg's demuxer (av_read_frame, as ffprobe -show_packets reads them):
/// each one's pts, size and key flag, for the keyframes and the bitrate (docs/decisions/2026-11-01-frames-bitrate-shots.md).
/// </summary>
public static class Packets
{
    /// <summary>Reads <paramref name="path"/>'s video packets.</summary>
    /// <exception cref="FrameReaderException">The file can't be opened or read.</exception>
    public static unsafe VideoPackets Read(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        Libraries.Load();
        AVFormatContext* format = null;
        int error = ffmpeg.avformat_open_input(&format, path, null, null);
        if (error < 0)
            throw new FrameReaderException(FrameReaderError.InvalidInput, $"{path}: can't open it ({Message(error)}).", null);
        var packet = ffmpeg.av_packet_alloc();
        try
        {
            error = ffmpeg.avformat_find_stream_info(format, null);
            if (error < 0)
                throw new FrameReaderException(FrameReaderError.InvalidInput, $"{path}: can't read its streams ({Message(error)}).", null);
            int video = ffmpeg.av_find_best_stream(format, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (video < 0)
                throw new FrameReaderException(FrameReaderError.InvalidInput, $"{path} has no video stream.", null);
            for (int i = 0; i < format->nb_streams; i++)
            {
                if (i != video)
                    format->streams[i]->discard = AVDiscard.AVDISCARD_ALL;
            }

            var timeBase = format->streams[video]->time_base;
            double unit = (double)timeBase.num / timeBase.den;
            var packets = new List<(double, long, bool)>();
            while ((error = ffmpeg.av_read_frame(format, packet)) >= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (packet->stream_index == video)
                {
                    long pts = packet->pts != ffmpeg.AV_NOPTS_VALUE ? packet->pts : packet->dts;
                    packets.Add((pts * unit, packet->size, (packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0));
                }

                ffmpeg.av_packet_unref(packet);
            }

            if (error != ffmpeg.AVERROR_EOF)
                throw new FrameReaderException(FrameReaderError.InvalidInput, $"{path}: reading it failed ({Message(error)}).", null);
            return VideoPackets.From(packets);
        }
        finally
        {
            ffmpeg.av_packet_free(&packet);
            ffmpeg.avformat_close_input(&format);
        }
    }

    static unsafe string Message(int error)
    {
        const int size = 256;
        byte* buffer = stackalloc byte[size];
        ffmpeg.av_strerror(error, buffer, size);
        return System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)buffer) ?? error.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
