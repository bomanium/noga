using System.Diagnostics;

namespace Noga.Tests;

/// <summary>
/// Odd and broken files, small and generated once for the class with the ffmpeg command line (lavfi sources), and
/// copies of them cut short or overwritten in the middle (docs/decisions/2026-11-04-odd-files.md): what a delivery can
/// be, rather than what it should be.
/// </summary>
public sealed class OddFiles : IDisposable
{
    const string Picture = "testsrc2=s=320x180:r=25:d=2";
    const string Tone = "sine=frequency=1000:duration=2:sample_rate=48000";
    const string H264 = "-c:v libx264 -preset ultrafast -pix_fmt yuv420p";
    const string ProRes = "-c:v prores_ks -profile:v 3";

    /// <summary>The files a check reports on, each with how ffmpeg makes it (its inputs and options, before the output).</summary>
    public static readonly IReadOnlyDictionary<string, string> Reported = new Dictionary<string, string>
    {
        // H.264 in pixel formats ShotDetector doesn't take decoded frames in: the FFmpeg path
        ["h264_444.mp4"] = $"-f lavfi -i {Picture} -f lavfi -i {Tone} -c:v libx264 -preset ultrafast -pix_fmt yuv444p -c:a aac",
        ["h264_422_10.mp4"] = $"-f lavfi -i {Picture} -f lavfi -i {Tone} -c:v libx264 -preset ultrafast -pix_fmt yuv422p10le -c:a aac",
        ["prores_4444.mov"] = $"-f lavfi -i {Picture} -f lavfi -i {Tone} -c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le -c:a pcm_s24le",

        // Timing
        ["vfr.mp4"] = $"-f lavfi -i testsrc2=s=320x180:r=30:d=2,setpts='PTS+0.02*random(0)/TB' -f lavfi -i {Tone} {H264} -fps_mode vfr -c:a aac",
        ["vfr.mov"] = $"-f lavfi -i testsrc2=s=320x180:r=30:d=2,setpts='PTS+0.02*random(0)/TB' -f lavfi -i {Tone} {ProRes} -fps_mode vfr -c:a pcm_s24le",
        ["edit_list.mp4"] = $"-f lavfi -i testsrc2=s=320x180:r=25:d=4 -f lavfi -i sine=duration=4 -ss 1.3 -t 2 {H264} -c:a aac",
        ["one_frame.mp4"] = $"-f lavfi -i testsrc2=s=320x180:r=25:d=0.04 -f lavfi -i sine=duration=0.04 {H264} -c:a aac",
        ["short_audio.mp4"] = $"-f lavfi -i {Picture} -f lavfi -i sine=duration=0.5 {H264} -c:a aac",
        ["drop_frame.mov"] = $"-f lavfi -i testsrc2=s=320x180:r=30000/1001:d=2 -f lavfi -i {Tone} {ProRes} -c:a pcm_s24le -timecode 01:00:00;00",

        // Sound only: a stem, an M4A with cover art (a video stream to FFmpeg, not a picture)
        ["sound_only.wav"] = $"-f lavfi -i {Tone} -c:a pcm_s24le",
        ["sound_only.mov"] = $"-f lavfi -i {Tone} -c:a pcm_s24le",
        ["sound_only.aiff"] = $"-f lavfi -i {Tone} -c:a pcm_s16be",
        ["cover_art.m4a"] = $"-f lavfi -i {Tone} -f lavfi -i color=red:s=64x64:d=0.04 -map 0 -map 1 -c:a aac -c:v mjpeg -disposition:v attached_pic",

        // Streams
        ["video_only.mp4"] = $"-f lavfi -i {Picture} {H264}",
        ["video_only.mov"] = $"-f lavfi -i {Picture} {ProRes}",
        ["two_video.mov"] = $"-f lavfi -i {Picture} -f lavfi -i testsrc=s=160x90:r=25:d=2 -f lavfi -i {Tone} -map 0 -map 1 -map 2 {ProRes} -c:a pcm_s24le",
        ["two_audio.mp4"] = $"-f lavfi -i {Picture} -f lavfi -i {Tone} -f lavfi -i anullsrc=r=48000:cl=mono -map 0 -map 1 -map 2 -t 2 {H264} -c:a aac",
        ["surround.mov"] = $"-f lavfi -i {Picture} -f lavfi -i {Tone} -filter_complex [1]pan=5.1|c0=c0|c1=c0|c2=c0|c3=c0|c4=c0|c5=c0[a] -map 0 -map [a] {ProRes} -c:a pcm_s24le",
        ["h264.mkv"] = $"-f lavfi -i {Picture} -f lavfi -i {Tone} {H264} -c:a aac",

        // Pictures
        ["odd_size.mp4"] = $"-f lavfi -i testsrc2=s=322x182:r=25:d=2 -f lavfi -i {Tone} {H264} -c:a aac",
        ["odd_size.mov"] = $"-f lavfi -i testsrc2=s=321x181:r=25:d=2 -f lavfi -i {Tone} -c:v ffv1 -pix_fmt yuv444p -c:a pcm_s16le",
        ["tiny.mp4"] = $"-f lavfi -i testsrc2=s=32x18:r=25:d=2 -f lavfi -i {Tone} {H264} -c:a aac",
        ["hlg.mov"] = $"-f lavfi -i {Picture} -f lavfi -i {Tone} {ProRes} -pix_fmt yuv422p10le -color_primaries bt2020 -color_trc arib-std-b67 -colorspace bt2020nc -c:a pcm_s24le",
        ["rotated.mp4"] = $"-f lavfi -i {Picture} -f lavfi -i {Tone} {H264} -metadata:s:v rotate=90 -c:a aac",
        ["mpeg2.mpg"] = $"-f lavfi -i testsrc2=s=720x576:r=25:d=2,setfield=tff -f lavfi -i {Tone} -c:v mpeg2video -flags +ilme+ildct -c:a mp2",
        ["mjpeg.avi"] = $"-f lavfi -i {Picture} -f lavfi -i {Tone} -c:v mjpeg -pix_fmt yuvj422p -c:a pcm_s16le",
    };

    /// <summary>Files cut short or overwritten in the middle that a check still reports on: the frames they have.</summary>
    public static readonly IReadOnlyList<string> Damaged = ["cut_moov_first.mov", "cut_moov_first.mp4", "garbled.mov", "garbled.mp4"];

    /// <summary>Files a check can't read: a read error (exit 2) that says so.</summary>
    public static readonly IReadOnlyList<string> Unreadable = ["empty.mov", "cut_header.mov", "cut_moov_last.mp4", "random.mp4", "text.mxf"];

    public OddFiles()
    {
        Folder = Directory.CreateTempSubdirectory("noga-odd-").FullName;
        foreach (var (name, args) in Reported)
            Make(args, name);

        // The same file with its moov first (faststart) and last, as ffmpeg writes MOV and MP4 by default
        Make($"-f lavfi -i {Picture} -f lavfi -i {Tone} {ProRes} -c:a pcm_s24le -movflags +faststart", "whole_moov_first.mov");
        Make($"-f lavfi -i {Picture} -f lavfi -i {Tone} {H264} -c:a aac -movflags +faststart", "whole_moov_first.mp4");
        Make($"-f lavfi -i {Picture} -f lavfi -i {Tone} {H264} -c:a aac", "whole_moov_last.mp4");
        Cut("whole_moov_first.mov", "cut_moov_first.mov", 0.5);
        Cut("whole_moov_first.mp4", "cut_moov_first.mp4", 0.5);
        Cut("whole_moov_last.mp4", "cut_moov_last.mp4", 0.5);
        Cut("whole_moov_first.mov", "cut_header.mov", 1000);
        Garble("whole_moov_first.mov", "garbled.mov", 100_000);
        Garble("whole_moov_first.mp4", "garbled.mp4", 10_000);
        File.WriteAllBytes(Path("empty.mov"), []);
        File.WriteAllBytes(Path("random.mp4"), Bytes(100_000, 1));
        File.WriteAllText(Path("text.mxf"), "not a movie");
    }

    public string Folder { get; }

    public string Path(string name) => System.IO.Path.Combine(Folder, name);

    public void Dispose() => Directory.Delete(Folder, recursive: true);

    void Make(string args, string name)
    {
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-nostdin", "-hide_banner", "-loglevel", "error", "-y", .. args.Split(' '), Path(name)])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"ffmpeg {args} {name} failed:\n{log}");
    }

    // The first part of a file: a fraction of it, or a number of bytes.
    void Cut(string from, string to, double part)
    {
        byte[] bytes = File.ReadAllBytes(Path(from));
        File.WriteAllBytes(Path(to), bytes[..(int)(part < 1 ? bytes.Length * part : part)]);
    }

    // A file with a run of random bytes over its middle, as a bad sector or a broken transfer leaves it.
    void Garble(string from, string to, int count)
    {
        byte[] bytes = File.ReadAllBytes(Path(from));
        Bytes(count, 2).CopyTo(bytes, bytes.Length / 2);
        File.WriteAllBytes(Path(to), bytes);
    }

    static byte[] Bytes(int count, int seed)
    {
        byte[] bytes = new byte[count];
#pragma warning disable CA5394 // Not for security: the same garbage on every run.
        new Random(seed).NextBytes(bytes);
#pragma warning restore CA5394
        return bytes;
    }
}
