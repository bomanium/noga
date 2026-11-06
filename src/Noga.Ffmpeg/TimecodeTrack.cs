using System.Buffers.Binary;
using System.Text;

namespace Noga.Ffmpeg;

/// <summary>
/// A start timecode flagged drop-frame at a rate that has none (25, 23.976), which FFmpeg drops: its MOV and MXF
/// demuxers log "Drop frame is only allowed with multiples of 30000/1001 FPS" and give no timecode tag
/// (docs/decisions/2026-11-06-drop-frame.md). Read from the file itself: a MOV or MP4's tmcd track (its sample
/// description's flags, bit 0 drop-frame, and its first sample's frame number) or an MXF's first timecode component
/// (DropFrame, StartTimecode and RoundedTimecodeBase).
/// </summary>
public static class TimecodeTrack
{
    // An MXF timecode component's key (SMPTE 377-1), bytes 5 and 7 (registry designator, version) left out.
    static readonly byte[] TimecodeComponent = [0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x14, 0x00];

    // How far into an MXF its header metadata is looked for.
    const int MxfHeader = 4 << 20;

    /// <summary>
    /// <paramref name="path"/>'s start timecode when it is flagged drop-frame, written with ";" before the frames
    /// (counted non-drop at the track's base), else null: no timecode track, one not flagged drop-frame, or not a MOV,
    /// MP4 or MXF that reads.
    /// </summary>
    public static string? DropFrameStart(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
            Span<byte> head = stackalloc byte[16];
            if (file.ReadAtLeast(head, 16, throwOnEndOfStream: false) < 16)
                return null;
            file.Position = 0;
            return Matches(head, TimecodeComponent.AsSpan(0, 4)) ? Mxf(file) : Mov(file);
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

    // The frame's label counted non-drop at the rate, written drop-frame: ";" before the frames.
    static string? Label(long frame, int rate)
    {
        if (rate is <= 0 or >= 100 || frame < 0)
            return null;
        string label = new Timecode(rate, 1).Label(frame);
        return string.Concat(label.AsSpan(0, label.Length - 3), ";", label.AsSpan(label.Length - 2));
    }

    // MOV and MP4: moov, then each trak's mdia/minf/stbl, for a tmcd sample description and the first chunk's offset.
    static string? Mov(FileStream file)
    {
        foreach (var (type, start, size) in Boxes(file, 0, file.Length))
        {
            if (type != "moov")
                continue;
            foreach (var trak in Boxes(file, start, size).Where(b => b.Type == "trak"))
            {
                var stbl = Path(file, trak, "mdia", "minf", "stbl");
                if (stbl is not { } table)
                    continue;
                var boxes = Boxes(file, table.Start, table.Size).ToList();
                if (boxes.FirstOrDefault(b => b.Type == "stsd") is not { Type: not null } stsd)
                    continue;
                byte[] description = Read(file, stsd.Start, (int)Math.Min(stsd.Size, 256));

                // version and flags (4), entries (4); the entry: size (4), format (4), reserved (6), data reference (2),
                // reserved (4), flags (4), time scale (4), frame duration (4), frames (1).
                if (description.Length < 41 || Encoding.ASCII.GetString(description, 12, 4) != "tmcd")
                    continue;
                bool drop = (BinaryPrimitives.ReadUInt32BigEndian(description.AsSpan(28)) & 1) != 0;
                int rate = description[40];
                if (!drop)
                    return null;
                long? chunk = boxes.FirstOrDefault(b => b.Type is "stco" or "co64") is { Type: not null } offsets
                    ? FirstChunk(Read(file, offsets.Start, 16), offsets.Type == "co64")
                    : null;
                if (chunk is not { } at)
                    return null;
                byte[] sample = Read(file, at, 4);
                return sample.Length == 4 ? Label(BinaryPrimitives.ReadUInt32BigEndian(sample), rate) : null;
            }
        }

        return null;
    }

    static long? FirstChunk(byte[] box, bool wide) =>
        box.Length < (wide ? 16 : 12) || BinaryPrimitives.ReadUInt32BigEndian(box.AsSpan(4)) == 0 ? null
        : wide ? (long)BinaryPrimitives.ReadUInt64BigEndian(box.AsSpan(8)) : BinaryPrimitives.ReadUInt32BigEndian(box.AsSpan(8));

    static (string Type, long Start, long Size)? Path(FileStream file, (string Type, long Start, long Size) box, params string[] types)
    {
        var at = box;
        foreach (string type in types)
        {
            var next = Boxes(file, at.Start, at.Size).FirstOrDefault(b => b.Type == type);
            if (next.Type is null)
                return null;
            at = next;
        }

        return at;
    }

    // The boxes from start for size bytes: each one's type and where its contents start, and their size.
    static IEnumerable<(string Type, long Start, long Size)> Boxes(FileStream file, long start, long size)
    {
        long at = start, end = Math.Min(start + size, file.Length);
        while (at + 8 <= end)
        {
            byte[] header = Read(file, at, 16);
            if (header.Length < 8)
                yield break;
            long length = BinaryPrimitives.ReadUInt32BigEndian(header);
            int headerSize = 8;
            if (length == 1 && header.Length == 16)
                (length, headerSize) = ((long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8)), 16);
            else if (length == 0)
                length = end - at;
            if (length < headerSize || at + length > end)
                yield break;
            yield return (Encoding.ASCII.GetString(header, 4, 4), at + headerSize, length - headerSize);
            at += length;
        }
    }

    // MXF: the first timecode component in the header's first megabytes, its local tags 0x1501 StartTimecode (8 bytes),
    // 0x1502 RoundedTimecodeBase (2) and 0x1503 DropFrame (1).
    static string? Mxf(FileStream file)
    {
        byte[] data = Read(file, 0, (int)Math.Min(file.Length, MxfHeader));
        for (int at = 0; at + 17 <= data.Length; at++)
        {
            if (data[at] != 0x06 || !IsTimecodeComponent(data.AsSpan(at, 16)))
                continue;
            int p = at + 16;
            if (!Length(data, ref p, out long length) || p + length > data.Length)
                return null;
            long start = -1;
            int rate = 0;
            bool drop = false;
            for (long end = p + length; p + 4 <= end;)
            {
                int tag = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p)), size = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 2));
                if (p + 4 + size > end)
                    return null;
                var value = data.AsSpan(p + 4, size);
                if (tag == 0x1501 && size == 8)
                    start = BinaryPrimitives.ReadInt64BigEndian(value);
                else if (tag == 0x1502 && size == 2)
                    rate = BinaryPrimitives.ReadUInt16BigEndian(value);
                else if (tag == 0x1503 && size == 1)
                    drop = value[0] != 0;
                p += 4 + size;
            }

            return drop ? Label(start, rate) : null;
        }

        return null;
    }

    static bool IsTimecodeComponent(ReadOnlySpan<byte> key)
    {
        for (int i = 0; i < 16; i++)
        {
            if (i is not (5 or 7) && key[i] != TimecodeComponent[i])
                return false;
        }

        return true;
    }

    // A KLV's BER length.
    static bool Length(byte[] data, ref int at, out long length)
    {
        length = 0;
        int first = data[at++];
        if (first < 0x80)
        {
            length = first;
            return true;
        }

        int count = first & 0x7f;
        if (count is 0 or > 8 || at + count > data.Length)
            return false;
        for (int i = 0; i < count; i++)
            length = (length << 8) | data[at++];
        return length >= 0;
    }

    static bool Matches(ReadOnlySpan<byte> data, ReadOnlySpan<byte> prefix) => data.StartsWith(prefix);

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
