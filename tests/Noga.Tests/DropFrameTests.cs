using System.Buffers.Binary;
using System.Diagnostics;
using Noga.Ffmpeg;

namespace Noga.Tests;

/// <summary>
/// The start timecode against the frame rate (docs/decisions/2026-11-06-drop-frame.md): at 29.97 and 59.94 labels
/// follow the timecode's ";" or ":"; a timecode flagged drop-frame at a rate without it, which FFmpeg drops, is read
/// from the file, counted non-drop and warned about; a profile's <c>dropFrame</c> judged.
/// </summary>
public sealed class DropFrameTests : IDisposable
{
    const string Warning = "Drop frame is only allowed with multiples of 30000/1001 FPS";

    readonly string _folder = Directory.CreateTempSubdirectory("noga-drop-frame-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void At2997ANonDropTimecodeCountsNonDrop()
    {
        var timecode = new Timecode(30000, 1001, "10:00:00:00");

        Assert.False(timecode.DropFrame);
        Assert.Equal("10:01:00:00", timecode.At(1800 * 1.001 / 30));
    }

    [Fact]
    public void At2997ADropFrameTimecodeOrNoneCountsDropFrame()
    {
        Assert.True(new Timecode(30000, 1001, "10:00:00;00").DropFrame);
        Assert.Equal("10:01:00;02", new Timecode(30000, 1001, "10:00:00;00").At(1800 * 1.001 / 30));
        Assert.True(new Timecode(60000, 1001).DropFrame);
        Assert.False(new Timecode(60000, 1001, "00:00:00:00").DropFrame);
    }

    [Fact]
    public void ADropFrameTimecodeAt25CountsNonDrop()
    {
        var timecode = new Timecode(25, 1, "10:00:00;00");

        Assert.False(timecode.DropFrame);
        Assert.Equal("10:00:01:00", timecode.At(1));
    }

    // The ffprobe command line's tag, ":" or ";", is the start; the report says non-drop or drop-frame.
    [Theory]
    [InlineData("mov", "10:00:00:00")]
    [InlineData("mxf", "10:00:00:00")]
    [InlineData("mov", "10:00:00;00")]
    [InlineData("mp4", "10:00:00;00")]
    public void At2997TheReportFollowsTheTag(string extension, string start)
    {
        string file = Make(extension, "30000/1001", start);
        var measured = Measure(file, 30000, 1001);

        Assert.Equal(Tag(file), measured.StartTimecode);
        var report = Profile.EbuR128.Evaluate(measured);
        Assert.Equal(Timecode.IsDropFrame(start), report.DropFrame);
        Assert.True(report.DropFrameRate);
        Assert.DoesNotContain(report.Findings, f => f.Check == "timecode.drop-frame");
        Assert.Contains(report.DropFrame ? "(drop-frame)" : "(non-drop)", ReportHtml.Write(report, file), StringComparison.Ordinal);
    }

    // A tmcd track or MXF timecode component flagged drop-frame at 25 fps: ffprobe gives no tag and warns; Noga reads
    // the start the file had before the flag was set, written drop-frame, warns and counts non-drop.
    [Theory]
    [InlineData("mov")]
    [InlineData("mp4")]
    [InlineData("mxf")]
    public void ADropFrameFlagAt25IsReadAndWarned(string extension)
    {
        string file = Make(extension, "25", "10:00:01:12");
        Assert.Equal("10:00:01:12", Tag(file));
        Assert.Null(TimecodeTrack.DropFrameStart(file));

        Flag(file);
        Assert.Null(Tag(file));
        Assert.Contains(Warning, Ffprobe(file, "-v", "warning", "-show_entries", "format_tags=timecode:stream_tags=timecode").Log, StringComparison.Ordinal);

        var measured = Measure(file, 25, 1);
        Assert.Equal("10:00:01;12", measured.StartTimecode);
        var report = Profile.EbuR128.Evaluate(measured);
        Assert.Equal("10:00:01:12", report.StartTimecode);
        Assert.False(report.DropFrame);
        var finding = Assert.Single(report.Findings, f => f.Check == "timecode.drop-frame");
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal("Start timecode 10:00:01;12 is drop-frame, which 25 fps doesn't have: counted non-drop", finding.Message);
        Assert.Contains(Warning, finding.CheckedAgainst, StringComparison.Ordinal);
        Assert.Equal("0026W", finding.EbuQc?.Id);
    }

    [Fact]
    public void ADropFrameFlagAt23976IsWarned()
    {
        string file = Make("mov", "24000/1001", "01:00:00:00");
        Flag(file);

        var report = Profile.EbuR128.Evaluate(Measure(file, 24000, 1001));

        Assert.Contains(report.Findings, f => f.Check == "timecode.drop-frame" && f.Message.Contains("23.976 fps", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("10:00:00;00", true, Severity.Pass)]
    [InlineData("10:00:00:00", true, Severity.Failure)]
    [InlineData("10:00:00:00", false, Severity.Pass)]
    [InlineData(null, false, Severity.Failure)]
    public void AProfileCanAskForDropFrame(string? start, bool drop, Severity expected)
    {
        var format = new MediaFormat("mov,mp4,m4a,3gp,3g2,mj2", 60, null, []);

        var finding = Assert.Single(new FormatSpec { DropFrame = drop }.Judge(format, start));

        Assert.Equal("format.drop-frame", finding.Check);
        Assert.Equal(expected, finding.Severity);
        Assert.EndsWith(drop ? "(expected drop-frame)" : "(expected non-drop)", finding.Message, StringComparison.Ordinal);
        Assert.Equal("0026W", finding.EbuQc?.Id);
    }

    [Fact]
    public void AProfileFileCanAskForDropFrame() =>
        Assert.True(Profiles.Read("""{"name":"us","format":{"dropFrame":true}}""").Format?.DropFrame);

    static Measurements Measure(string file, long numerator, long denominator) =>
        StartTimecode.Add(new Measurements { Duration = 1, RateNumerator = numerator, RateDenominator = denominator }, file);

    string Make(string extension, string rate, string start)
    {
        string file = Path.Combine(_folder, $"{rate.Replace('/', '_')}_{start.Replace(':', '_').Replace(';', 'd')}.{extension}");
        Ffmpeg("-f", "lavfi", "-i", $"testsrc2=s=64x64:r={rate}:d=0.4", "-c:v", "mpeg2video", "-timecode", start, file);
        return file;
    }

    // Sets the drop-frame flag: bit 0 of the tmcd sample description's flags (MOV, MP4), every MXF timecode
    // component's DropFrame (local tag 0x1503).
    static void Flag(string file)
    {
        byte[] data = File.ReadAllBytes(file);
        int set = 0;
        if (file.EndsWith(".mxf", StringComparison.Ordinal))
        {
            byte[] key = [0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x14, 0x00];
            for (int at = data.AsSpan().IndexOf(key); at >= 0; at = Next(data, key, at))
            {
                int p = at + 17 + (data[at + 16] >= 0x80 ? data[at + 16] & 0x7f : 0);
                long end = p + (data[at + 16] >= 0x80 ? BerLength(data.AsSpan(at + 17, data[at + 16] & 0x7f)) : data[at + 16]);
                for (; p + 4 <= end; p += 4 + BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 2)))
                {
                    if (BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p)) == 0x1503)
                        (data[p + 4], set) = (1, set + 1);
                }
            }
        }
        else
        {
            for (int at = data.AsSpan().IndexOf("stsd"u8); at >= 0; at = Next(data, "stsd"u8.ToArray(), at))
            {
                if (data.AsSpan(at + 16, 4).SequenceEqual("tmcd"u8))
                    (data[at + 4 + 28 + 3], set) = ((byte)(data[at + 4 + 28 + 3] | 1), set + 1);
            }
        }

        Assert.True(set > 0, $"no timecode flag in {file}");
        File.WriteAllBytes(file, data);

        static int Next(byte[] data, byte[] key, int at)
        {
            int next = data.AsSpan(at + 1).IndexOf(key);
            return next < 0 ? -1 : at + 1 + next;
        }

        static long BerLength(ReadOnlySpan<byte> bytes)
        {
            long length = 0;
            foreach (byte b in bytes)
                length = (length << 8) | b;
            return length;
        }
    }

    // The tag the ffprobe command line gives: the video stream's, else the container's (TimecodeTag.Pick's order).
    static string? Tag(string file)
    {
        string[] tags = Ffprobe(file, "-v", "error", "-show_entries", "stream_tags=timecode:format_tags=timecode", "-of", "default=nw=1:nk=1").Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tags.FirstOrDefault();
    }

    static (string Output, string Log) Ffprobe(string file, params string[] args) => Tool("ffprobe", [.. args, file]);

    static void Ffmpeg(params string[] args) => Tool("ffmpeg", ["-v", "error", "-y", .. args]);

    static (string Output, string Log) Tool(string name, string[] args)
    {
        var start = new ProcessStartInfo(name) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var log = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"{name} {string.Join(' ', args)} failed:\n{log.Result}");
        return (output, log.Result);
    }
}
