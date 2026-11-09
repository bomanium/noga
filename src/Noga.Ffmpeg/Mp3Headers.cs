namespace Noga.Ffmpeg;

/// <summary>
/// Reads an MP3's frame headers (docs/decisions/2026-11-09-acx-profile.md): each frame's bit rate, so a constant bit
/// rate can be told from a variable one, which ffprobe doesn't say. The frames are those FFmpeg's mp3 demuxer gives as
/// packets: after an ID3v2 tag, one MPEG audio Layer III frame after another (MPEG-1, 2 or 2.5), the first left out
/// when it is an encoder's Xing, Info or VBRI frame; anything between frames (an ID3v1 or APE tag) is skipped.
/// </summary>
public static class Mp3Headers
{
    // Layer III bit rates in kb/s by index: MPEG-1, then MPEG-2 and 2.5. Index 0 is free format, 15 invalid.
    static readonly int[] Mpeg1Rates = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
    static readonly int[] Mpeg2Rates = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];
    static readonly int[] SampleRates = [44100, 48000, 32000];

    /// <summary><paramref name="path"/>'s frames; null when it can't be read or has no Layer III frame.</summary>
    public static Mp3Frames? Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, FileOptions.SequentialScan);
            return Read(file);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The frames in an MP3's bytes, read once from where <paramref name="input"/> is; null when there is no Layer III frame.</summary>
    public static Mp3Frames? Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var bytes = new Window(input);
        long at = Id3v2Length(bytes.At(0, 10));
        var rates = new Dictionary<int, long>();
        long count = 0, total = 0;
        string? tag = null;
        bool first = true, chained = false;
        while (bytes.At(at, 4) is { Length: 4 } head)
        {
            if (Header(head) is not { } frame)
            {
                at++;
                chained = false;
                continue;
            }

            // A free-format frame has no length in its header: counted, and the rest of the file isn't read.
            if (frame.BitRate == 0)
            {
                rates[0] = rates.GetValueOrDefault(0) + 1;
                count++;
                break;
            }

            // Bytes that only look like a header: a frame found by searching counts when the next one follows it, or
            // the file ends there (FFmpeg's mp3 demuxer resyncs on consecutive frames too).
            var whole = bytes.At(at, frame.Length + 4);
            if (whole.Length < frame.Length)
                break;
            if (!chained && whole.Length == frame.Length + 4 && Header(whole[frame.Length..]) is not { BitRate: > 0 })
            {
                at++;
                continue;
            }

            whole = whole[..frame.Length];
            if (first && Tag(whole, frame) is { } found)
            {
                tag = found;
            }
            else
            {
                rates[frame.BitRate] = rates.GetValueOrDefault(frame.BitRate) + 1;
                count++;
                total += frame.Length;
            }

            first = false;
            chained = true;
            at += frame.Length;
        }

        return count == 0 ? null : new Mp3Frames(count, total, rates, tag);
    }

    // The stream's bytes through a buffer that moves forward only: a frame (at most 2,881 bytes) at a time.
    sealed class Window(Stream input)
    {
        readonly byte[] _buffer = new byte[1 << 16];
        long _start;
        int _length;
        bool _ended;

        // Up to count bytes from position at (at no earlier than the last call's); fewer at the end.
        public ReadOnlySpan<byte> At(long at, int count)
        {
            if (at + count > _start + _length && !_ended)
            {
                int keep = (int)Math.Max(0, _start + _length - at);
                if (keep > 0)
                    Array.Copy(_buffer, _length - keep, _buffer, 0, keep);
                else if (at > _start + _length)
                    Skip(at - (_start + _length));
                _start = at;
                _length = keep;
                while (_length < _buffer.Length)
                {
                    int read = input.Read(_buffer, _length, _buffer.Length - _length);
                    if (read == 0)
                    {
                        _ended = true;
                        break;
                    }

                    _length += read;
                }
            }

            int offset = (int)(at - _start);
            return offset >= _length ? [] : _buffer.AsSpan(offset, Math.Min(count, _length - offset));
        }

        void Skip(long bytes)
        {
            if (input.CanSeek)
            {
                input.Seek(bytes, SeekOrigin.Current);
                return;
            }

            for (long left = bytes; left > 0;)
            {
                int read = input.Read(_buffer, 0, (int)Math.Min(left, _buffer.Length));
                if (read == 0)
                {
                    _ended = true;
                    return;
                }

                left -= read;
            }
        }
    }

    // An ID3v2 tag's length: its 10-byte header, the size in 7-bit bytes, and a footer when flagged.
    static long Id3v2Length(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 10 || bytes[0] != 'I' || bytes[1] != 'D' || bytes[2] != '3')
            return 0;
        int size = (bytes[6] & 0x7F) << 21 | (bytes[7] & 0x7F) << 14 | (bytes[8] & 0x7F) << 7 | (bytes[9] & 0x7F);
        bool footer = (bytes[5] & 0x10) != 0;
        return 10 + size + (footer ? 10 : 0);
    }

    readonly record struct Frame(int BitRate, int Length, bool Mpeg1, bool Mono);

    // A Layer III frame header (ISO/IEC 11172-3, 13818-3): 11 sync bits, the version, the layer, the bit rate and sample
    // rate indexes, the padding bit and the channel mode.
    static Frame? Header(ReadOnlySpan<byte> b)
    {
        if (b[0] != 0xFF || (b[1] & 0xE0) != 0xE0)
            return null;
        int version = (b[1] >> 3) & 3; // 0: MPEG-2.5, 2: MPEG-2, 3: MPEG-1
        int layer = (b[1] >> 1) & 3; // 1: Layer III
        int rateIndex = b[2] >> 4, sampleIndex = (b[2] >> 2) & 3, padding = (b[2] >> 1) & 1;
        if (version == 1 || layer != 1 || rateIndex == 15 || sampleIndex == 3)
            return null;
        bool mpeg1 = version == 3;
        int halved = version switch { 3 => 0, 2 => 1, _ => 2 };
        int sampleRate = SampleRates[sampleIndex] >> halved;
        int kbps = (mpeg1 ? Mpeg1Rates : Mpeg2Rates)[rateIndex];
        int length = kbps == 0 ? 0 : (mpeg1 ? 144_000 : 72_000) * kbps / sampleRate + padding;
        bool mono = (b[3] >> 6) == 3;
        return new Frame(kbps * 1000, length, mpeg1, mono);
    }

    // An encoder's frame before the sound: Xing (VBR) or Info (CBR) after the side information, VBRI 32 bytes on.
    static string? Tag(ReadOnlySpan<byte> frame, Frame header)
    {
        int side = header.Mpeg1 ? (header.Mono ? 17 : 32) : (header.Mono ? 9 : 17);
        foreach (int at in (int[])[4 + side, 36])
        {
            if (at + 4 > frame.Length)
                continue;
            var id = frame.Slice(at, 4);
            if (id.SequenceEqual("Xing"u8))
                return "Xing";
            if (id.SequenceEqual("Info"u8))
                return "Info";
            if (at == 36 && id.SequenceEqual("VBRI"u8))
                return "VBRI";
        }

        return null;
    }
}
