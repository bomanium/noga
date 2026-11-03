using Noga.Ffmpeg;

namespace Noga.Tests;

/// <summary>
/// Each figure on real pictures and sound: Blender's open films (tools/get-films.sh, pinned by SHA-256) measured on
/// the FFmpeg path, against the ffmpeg command line with the tolerances SampleTests use on generated files
/// (docs/decisions/2026-11-01-open-films.md). Neither film has AAC in MP4, so the managed path isn't among them; Big Buck Bunny's two audio tracks
/// (MP3 stereo, AC-3 5.1) are each checked.
/// </summary>
public sealed class FilmTests
{
    public static TheoryData<string> Films => ["bbb_sunflower_1080p_30fps_normal.mp4", "tears_of_steel_720p.mov"];

    static string Folder => Environment.GetEnvironmentVariable("NOGA_FILMS")
        ?? throw new InvalidOperationException("NOGA_FILMS names the films' folder (tools/get-films.sh).");

    [Theory]
    [MemberData(nameof(Films))]
    public void EachFigureIsFfmpegs(string film)
    {
        string file = Path.Combine(Folder, film);
        Assert.True(File.Exists(file), $"No {file}: run tools/get-films.sh {Folder}");

        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);

        FfmpegCli.AssertStartTimecode(file, measured);
        FfmpegCli.AssertLoudness(file, measured);
        FfmpegCli.AssertSilences(file, measured);
        if (measured.Tone.Count > 0)
            FfmpegCli.AssertTone(file, measured.Tone[0]);
        FfmpegCli.AssertBlack(file, measured);
        FfmpegCli.AssertWorstOutOfRange(file, measured);
        FfmpegCli.AssertCrop(file, measured);
        FfmpegCli.AssertPictureTypes(file, measured);
        FfmpegCli.AssertPackets(file, measured);
        foreach (var track in measured.OtherTracks)
            FfmpegCli.AssertTrack(file, track);
        Assert.Equal(measured.AudioTracks.Count - 1, measured.OtherTracks.Count);
        Assert.NotNull(measured.Shots);
        Assert.NotEmpty(measured.Shots);
    }
}
