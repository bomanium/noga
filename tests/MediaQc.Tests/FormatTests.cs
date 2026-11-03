using System.Diagnostics;
using MediaQc.Ffmpeg;

namespace MediaQc.Tests;

/// <summary>
/// The delivery format (docs/decisions/2026-11-03-delivery-format.md): what ffprobe says a file is, read in-process
/// the same as the ffprobe command line; a profile's format judged against it; and the templates shipped.
/// </summary>
public sealed class FormatTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("mediaqc-format-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    static readonly VideoFormat Hd = new("prores", "HQ", 1920, 1080, "25/1", "tt", "yuv422p10le", 10, "bt709", "bt709", "bt709", "tv", "16:9");

    static MediaFormat Format(VideoFormat? video = null, params AudioFormat[] audio) =>
        new("mov,mp4,m4a,3gp,3g2,mj2", 60, video ?? Hd, audio.Length == 0 ? [new AudioFormat(1, "pcm_s24le", 48000, 2, "stereo", 24)] : audio);

    static FormatSpec Spec => Profiles.Get("hd-1080i25-prores-hq").Format!;

    // An interlaced ProRes with its colour tagged and two audio tracks (stereo, then 5.1): every value is the command
    // line's ffprobe's, read in-process through FrameReader.
    [Fact]
    public void TheFormatIsFfprobesOnBothCounts()
    {
        string file = Path.Combine(_folder, "interlaced.mov");
        Make(
            "-f",
            "lavfi",
            "-i",
            "testsrc2=size=1920x1080:rate=25:duration=1",
            "-f",
            "lavfi",
            "-i",
            "sine=duration=1",
            "-f",
            "lavfi",
            "-i",
            "sine=duration=1:frequency=500",
            "-map",
            "0:v",
            "-map",
            "1:a",
            "-map",
            "2:a",
            "-filter:a:0",
            "aformat=channel_layouts=stereo",
            "-filter:a:1",
            "aformat=channel_layouts=5.1(side)",
            "-c:v",
            "prores_ks",
            "-profile:v",
            "3",
            "-pix_fmt",
            "yuv422p10le",
            "-flags",
            "+ildct+ilme",
            "-top",
            "1",
            "-color_primaries",
            "bt709",
            "-color_trc",
            "bt709",
            "-colorspace",
            "bt709",
            "-color_range",
            "tv",
            "-c:a",
            "pcm_s24le",
            "-ar",
            "48000",
            "-timecode",
            "10:00:00:00",
            "-movflags",
            "+write_colr",
            file);

        var measured = StartTimecode.Add(new Measurements { Duration = 1, RateNumerator = 25, RateDenominator = 1 }, file);

        FfmpegCli.AssertFormat(file, measured);
        Assert.NotNull(measured.Format);
        Assert.NotNull(measured.Format.Video);
        var (format, video) = (measured.Format, measured.Format.Video);
        Assert.Equal(("prores", "HQ", 1920, 1080, "25/1"), (video.Codec, video.Profile, video.Width, video.Height, video.FrameRate));

        // The colour tags are the encoder's to write (CI's FFmpeg 8.0 left this file's primaries out, 6.1 writes them):
        // that they are read as ffprobe reads them is AssertFormat's.
        Assert.Equal(("yuv422p10le", 10, "tv", "16:9"), (video.PixelFormat, video.BitDepth, video.ColourRange, video.DisplayAspect));
        Assert.Equal("tb", video.FieldOrder);
        Assert.Equal([(1, 2, 24), (2, 6, 24)], format.Audio.Select(a => (a.Number, a.Channels, a.BitDepth ?? 0)));
        Assert.Equal(48000, format.Audio[0].SampleRate);
    }

    [Fact]
    public void AFormatThatIsTheSpecsPasses()
    {
        var findings = Spec.Judge(Format(), "10:00:00:00");

        Assert.All(findings, f => Assert.Equal(Severity.Pass, f.Severity));
        Assert.Equal(17, findings.Count);
        Assert.All(findings, f => Assert.StartsWith(f.Check == "format.start-timecode" ? "the timecode tag" : MediaFormat.CheckedAgainst, f.CheckedAgainst, StringComparison.Ordinal));
    }

    [Fact]
    public void EachDifferenceFailsAndSaysWhatWasExpected()
    {
        var video = Hd with { Profile = "Standard", Height = 720, FrameRate = "50/1", FieldOrder = "progressive", ColourPrimaries = null };
        var findings = Spec.Judge(Format(video), null);

        var failed = findings.Where(f => f.Severity == Severity.Failure).ToDictionary(f => f.Check, f => f.Message);
        Assert.Equal(["format.video-profile", "format.frame-size", "format.frame-rate", "format.scan", "format.colour-primaries", "format.start-timecode"], failed.Keys);
        Assert.Equal("Video profile Standard (expected HQ)", failed["format.video-profile"]);
        Assert.Equal("Frame size 1920x720 (expected 1920x1080)", failed["format.frame-size"]);
        Assert.Equal("Frame rate 50 fps (50/1) (expected 25)", failed["format.frame-rate"]);
        Assert.Equal("Scan progressive (expected interlaced)", failed["format.scan"]);
        Assert.Equal("Colour primaries not stated (expected bt709)", failed["format.colour-primaries"]);
        Assert.Equal("Start timecode none (expected 10:00:00:00)", failed["format.start-timecode"]);
    }

    // Interlaced is any of the four orders; unknown is neither interlaced nor progressive.
    [Theory]
    [InlineData("tt", "interlaced", true)]
    [InlineData("tb", "interlaced", true)]
    [InlineData("bb", "interlaced", true)]
    [InlineData("bt", "progressive", false)]
    [InlineData("progressive", "interlaced", false)]
    [InlineData(null, "progressive", false)]
    public void TheScanIsReadFromTheFieldOrder(string? order, string scan, bool passes)
    {
        var finding = Assert.Single(new FormatSpec { Scan = scan }.Judge(Format(Hd with { FieldOrder = order }), null));

        Assert.Equal(passes ? Severity.Pass : Severity.Failure, finding.Severity);
    }

    [Theory]
    [InlineData("30000/1001", "29.97", true)]
    [InlineData("30000/1001", "30000/1001", true)]
    [InlineData("30/1", "29.97", false)]
    [InlineData("25/1", "25", true)]
    [InlineData("0/0", "25", false)]
    public void FrameRatesCompareAsNumbers(string measured, string expected, bool passes)
    {
        var finding = Assert.Single(new FormatSpec { FrameRate = expected }.Judge(Format(Hd with { FrameRate = measured }), null));

        Assert.Equal(passes ? Severity.Pass : Severity.Failure, finding.Severity);
    }

    // The audio values hold for every track, each finding naming its track; the count is the file's.
    [Fact]
    public void EveryAudioTrackIsJudged()
    {
        var spec = new FormatSpec { AudioTracks = 2, AudioChannels = 2, SampleRate = 48000 };
        var format = Format(null, new AudioFormat(1, "pcm_s24le", 48000, 2, "stereo", 24), new AudioFormat(2, "pcm_s24le", 44100, 6, "5.1", 24));

        var failed = spec.Judge(format, null).Where(f => f.Severity == Severity.Failure).ToList();

        Assert.Equal([("format.sample-rate", 2), ("format.audio-channels", 2)], failed.Select(f => (f.Check, f.Track ?? 0)));
        Assert.Equal("Track 2: Sample rate 44100 Hz (expected 48000 Hz)", failed[0].Message);
        Assert.Contains("0:a:1", failed[0].CheckedAgainst, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainersDurationsAndAMissingVideoAreJudged()
    {
        var spec = new FormatSpec { Container = "mxf", MinDuration = 30, MaxDuration = 45, Width = 1920 };

        var findings = spec.Judge(new MediaFormat("mov,mp4,m4a,3gp,3g2,mj2", 60, null, []), null);

        Assert.Equal(
            [("format.container", Severity.Failure), ("format.video-codec", Severity.Failure), ("format.duration", Severity.Failure)],
            findings.Select(f => (f.Check, f.Severity)));
        Assert.Equal("Duration 60.000 s (expected 30 to 45 s)", findings[2].Message);
        Assert.Equal(Severity.Pass, Assert.Single(new FormatSpec { Container = "mp4" }.Judge(Format(), null)).Severity);
    }

    // A profile without a format reports it; one with a format fails a file whose format couldn't be read.
    [Fact]
    public void TheReportAlwaysSaysTheFormatAndAProfilesFormatIsJudged()
    {
        var measured = new Measurements { Duration = 60, RateNumerator = 25, RateDenominator = 1, Format = Format(), StartTimecode = "10:00:00:00" };

        var plain = Profile.EbuR128.Evaluate(measured);
        var judged = (Profile.EbuR128 with { Format = new FormatSpec { Height = 720 } }).Evaluate(measured);
        var unread = (Profile.EbuR128 with { Format = new FormatSpec { Height = 720 } }).Evaluate(measured with { Format = null });

        Assert.StartsWith("Format: prores (HQ) 1920x1080 25 fps tt", Assert.Single(plain.Findings, f => f.Check == "format").Message, StringComparison.Ordinal);
        Assert.DoesNotContain(plain.Findings, f => f.Check.StartsWith("format.", StringComparison.Ordinal));
        Assert.Equal(Severity.Failure, Assert.Single(judged.Findings, f => f.Check == "format.frame-size").Severity);
        Assert.Equal(Severity.Failure, Assert.Single(unread.Findings, f => f.Check == "format.container").Severity);
        Assert.Equal("0163W", Assert.Single(judged.Findings, f => f.Check == "format.frame-size").EbuQc?.Id);
    }

    [Fact]
    public void AProfileFileReadsItsFormatAndRefusesOneThatCouldNeverMatch()
    {
        var profile = Profiles.Read("""{ "name": "uk-hd", "basedOn": "ebu-r128", "format": { "frameRate": "25", "scan": "interlaced", "fieldOrder": "tb", "audioTracks": 4 } }""");

        Assert.Equal(new FormatSpec { FrameRate = "25", Scan = "interlaced", FieldOrder = "tb", AudioTracks = 4 }, profile.Format);
        Assert.Null(Profiles.Read("""{ "name": "plain" }""").Format);
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "format": { "scan": "tff" } }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "format": { "frameRate": "fast" } }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "format": { "width": 0 } }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "format": { "minDuration": 50, "maxDuration": 40 } }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "format": { "framerate": "25" } }"""));
    }

    // The templates beside the program: each by its name, each a profile with a format, none of them a shipped name.
    [Fact]
    public void TheTemplatesAreFoundByName()
    {
        Assert.Equal(["hd-1080i25-prores-hq", "hd-1080p25-prores-hq", "web-1080p-h264-aac"], Profiles.Templates);
        foreach (string name in Profiles.Templates)
        {
            var profile = Profiles.Get(name);
            Assert.Equal(name, profile.Name);
            Assert.NotNull(profile.Format);
            Assert.False(profile.Format.IsEmpty);
        }

        Assert.Contains("hd-1080i25-prores-hq", Assert.Throws<ArgumentException>(() => Profiles.Get("no-such-profile")).Message, StringComparison.Ordinal);
    }

    static void Make(params string[] args)
    {
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-loglevel", "error", "-nostdin", "-y", .. args])
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"ffmpeg failed:\n{log}");
    }
}
