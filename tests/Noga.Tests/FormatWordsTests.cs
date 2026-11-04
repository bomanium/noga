namespace Noga.Tests;

// The report's header says the format in words a client reads (docs/decisions/2026-11-04-format-words.md).
public class FormatWordsTests
{
    static readonly VideoFormat Hd = new("prores", "HQ", 1920, 1080, "25/1", "progressive", "yuv422p10le", 10, "bt709", "bt709", "bt709", "tv", "16:9");

    [Fact]
    public void VideoInWords()
    {
        Assert.Equal("Apple ProRes 422 HQ, 1920×1080, 25 fps progressive, 4:2:2 10-bit, BT.709, limited range, 16:9", FormatWords.Video(Hd));
        Assert.Equal(
            "Avid DNxHR SQ, 1920×1080, 29.97 fps interlaced, top field first, 4:2:2 8-bit, BT.709 matrix, primaries and transfer not tagged, limited range, 16:9",
            FormatWords.Video(Hd with { Codec = "dnxhd", Profile = "DNXHR SQ", FrameRate = "30000/1001", FieldOrder = "tt", PixelFormat = "yuv422p", BitDepth = 8, ColourPrimaries = null, Transfer = "unknown" }));
        Assert.Equal(
            "H.265 (HEVC) Main 10, 3840×2160, 50 fps, 4:2:0 10-bit, BT.2020 primaries, PQ transfer, BT.2020 matrix, full range, 16:9",
            FormatWords.Video(Hd with { Codec = "hevc", Profile = "Main 10", Width = 3840, Height = 2160, FrameRate = "50/1", FieldOrder = null, PixelFormat = "yuv420p10le", ColourPrimaries = "bt2020", Transfer = "smpte2084", Matrix = "bt2020nc", ColourRange = "pc" }));
        Assert.Contains("colour not tagged, range not tagged", FormatWords.Video(Hd with { ColourPrimaries = null, Transfer = null, Matrix = null, ColourRange = null }), StringComparison.Ordinal);
    }

    [Fact]
    public void AudioContainerAndDurationInWords()
    {
        var format = new MediaFormat("mov,mp4,m4a,3gp,3g2,mj2", 20, Hd, [new AudioFormat(1, "pcm_s24le", 48000, 2, "stereo", 24), new AudioFormat(2, "aac", 44100, 6, "5.1(side)", null)]);

        var rows = FormatWords.Rows(format, "ep01.mov");

        Assert.Equal(["Video", "Audio 1", "Audio 2", "Container", "Duration"], rows.Select(r => r.Label));
        Assert.Equal("PCM 24-bit, 48 kHz, stereo", rows[1].Text);
        Assert.Equal("AAC, 44.1 kHz, 5.1", rows[2].Text);
        Assert.Equal("QuickTime (MOV)", rows[3].Text);
        Assert.Equal("00:00:20:00 (20.00 s)", rows[4].Text);
        Assert.Equal("MPEG-4 (MP4)", FormatWords.Rows(format, "ep01.mp4")[3].Text);
        Assert.Equal("none", FormatWords.Rows(format with { Audio = [] }, "a.mxf")[1].Text);
    }
}
