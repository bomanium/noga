using System.Buffers.Binary;
using System.Text;

namespace Noga.Ffmpeg;

/// <summary>
/// Reads what a WAV, RF64/BW64 or AIFF header lists (docs/decisions/2026-11-06-sound-headers.md): a WAV's data chunk
/// size over its block align, RF64 and BW64's ds64 data size, AIFF's COMM frame count; the sample rate from fmt or COMM.
/// FFmpeg's WAV demuxer goes by the bytes there are instead, so a WAV cut short reads as a shorter one.
/// </summary>
public static class SoundHeaders
{
    const uint Unset = 0xFFFFFFFF;

    /// <summary><paramref name="path"/>'s header; null when it isn't a WAV, RF64, BW64 or AIFF that reads.</summary>
    public static SoundHeader? Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
            byte[] head = Read(file, 0, 12);
            if (head.Length < 12)
                return null;
            string id = Encoding.ASCII.GetString(head, 0, 4), form = Encoding.ASCII.GetString(head, 8, 4);
            return (id, form) switch
            {
                ("RIFF" or "RF64" or "BW64", "WAVE") => Wav(file, id == "RIFF" ? "WAV" : id),
                ("FORM", "AIFF" or "AIFC") => Aiff(file, form == "AIFF" ? "AIFF" : "AIFF-C"),
                _ => null,
            };
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

    // RIFF chunks, little-endian: fmt (block align at 12, sample rate at 4), ds64 (the data size at 8), data.
    static SoundHeader? Wav(FileStream file, string kind)
    {
        int rate = 0, align = 0;
        long? ds64 = null;
        for (long at = 12; at + 8 <= file.Length;)
        {
            byte[] header = Read(file, at, 8);
            string id = Encoding.ASCII.GetString(header, 0, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
            if (id == "fmt ")
            {
                byte[] fmt = Read(file, at + 8, 16);
                if (fmt.Length < 16)
                    return null;
                (rate, align) = ((int)BinaryPrimitives.ReadUInt32LittleEndian(fmt.AsSpan(4)), BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(12)));
            }
            else if (id == "ds64")
            {
                byte[] sizes = Read(file, at + 8, 16);
                if (sizes.Length == 16)
                    ds64 = (long)BinaryPrimitives.ReadUInt64LittleEndian(sizes.AsSpan(8));
            }
            else if (id == "data")
            {
                if (align <= 0 || rate <= 0)
                    return null;
                long? bytes = kind != "WAV" && size == Unset ? ds64 : size is 0 or Unset ? null : size;
                return new SoundHeader(kind, bytes / align, rate);
            }

            at += 8 + size + (size & 1);
            if (size == Unset)
                return null;
        }

        return null;
    }

    // IFF chunks, big-endian: COMM (the frame count at 2, the sample rate an 80-bit extended at 8), SSND.
    static SoundHeader? Aiff(FileStream file, string kind)
    {
        for (long at = 12; at + 8 <= file.Length;)
        {
            byte[] header = Read(file, at, 8);
            uint size = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
            if (Encoding.ASCII.GetString(header, 0, 4) == "COMM")
            {
                byte[] comm = Read(file, at + 8, 18);
                if (comm.Length < 18)
                    return null;
                uint frames = BinaryPrimitives.ReadUInt32BigEndian(comm.AsSpan(2));
                int rate = (int)Math.Round(Extended(comm.AsSpan(8, 10)));
                bool sound = frames == 0 && file.Length > at + 8 + size;
                return rate <= 0 ? null : new SoundHeader(kind, sound ? null : frames, rate);
            }

            at += 8 + size + (size & 1);
        }

        return null;
    }

    // An IEEE 754 80-bit extended number: sign and 15-bit exponent, then a 64-bit mantissa with its integer bit.
    static double Extended(ReadOnlySpan<byte> bytes)
    {
        int exponent = ((bytes[0] & 0x7f) << 8) | bytes[1];
        ulong mantissa = BinaryPrimitives.ReadUInt64BigEndian(bytes[2..]);
        double value = mantissa * Math.Pow(2, exponent - 16383 - 63);
        return (bytes[0] & 0x80) != 0 ? -value : value;
    }

    static byte[] Read(FileStream file, long at, int count)
    {
        if (at < 0 || at >= file.Length)
            return [];
        var buffer = new byte[(int)Math.Min(count, file.Length - at)];
        file.Position = at;
        file.ReadExactly(buffer);
        return buffer;
    }
}
