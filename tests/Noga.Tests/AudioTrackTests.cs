using System.Diagnostics;
using Noga.Ffmpeg;
using Noga.Managed;

namespace Noga.Tests;

/// <summary>
/// A file with two audio tracks (docs/decisions/2026-11-01-every-audio-track.md): each measured as the first is, from its
/// own line-up, against ffmpeg -map 0:a:N, and each track's findings saying which. Track 1: line-up tone at -18 dBFS
/// 0-5 s, silence 5-7 s, noise to 15 s; track 2: quieter stereo noise with a silence 9-12 s and no tone. Made with the
/// ffmpeg command line on PATH (aevalsrc), not committed.
/// </summary>
public sealed class AudioTrackTests : IDisposable
{
    const string Track1 = "aevalsrc=if(lt(t\\,5)\\,0.1259*sin(2*PI*1000*t)\\,if(lt(t\\,7)\\,0\\,0.3*(random(0)*2-1))):s=48000:d=15";
    const string Track2 = "aevalsrc=if(between(t\\,9\\,12)\\,0\\,0.05*(random(0)*2-1))|if(between(t\\,9\\,12)\\,0\\,0.05*(random(1)*2-1)):s=48000:d=15";

    readonly string _folder = Directory.CreateTempSubdirectory("noga-tracks-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    string Make(string name, params string[] codec)
    {
        string path = Path.Combine(_folder, name);
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=25:d=15",
            "-f", "lavfi", "-i", Track1, "-f", "lavfi", "-i", Track2, "-map", "0", "-map", "1", "-map", "2", "-c:v", "mpeg4", .. codec, path])
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return path;
    }

    [Fact]
    public void EachTrackIsMeasuredFromItsOwnLineUpAsFfmpegMapsIt()
    {
        string file = Make("tracks.mov", "-c:a", "pcm_s16le");

        var measured = FfmpegMeasurer.Measure(file, Profile.EbuR128);

        Assert.Equal([1, 2], measured.AudioTracks.Select(t => t.Number));
        Assert.Equal([1, 2], measured.AudioTracks.Select(t => t.Channels));
        var tone = Assert.Single(measured.Tone);
        Assert.Equal(5, tone.End, 0.05);
        Assert.Equal(tone.End, measured.LoudnessFrom);
        FfmpegCli.AssertLoudness(file, measured);
        FfmpegCli.AssertSilences(file, measured);
        FfmpegCli.AssertTone(file, tone);

        var second = Assert.Single(measured.OtherTracks);
        Assert.Equal(2, second.Number);
        Assert.Empty(second.Tone);
        Assert.Equal(0, second.LoudnessFrom);
        var silence = Assert.Single(second.Silences);
        Assert.Equal((9, 12), (Math.Round(silence.Start, 1), Math.Round(silence.End, 1)));
        FfmpegCli.AssertTrack(file, second);
        Assert.NotEqual(Assert.NotNull(measured.Loudness).Integrated, second.Loudness.Integrated);
    }

    [Fact]
    public void EachTracksFindingsSayWhichTrack()
    {
        var measured = FfmpegMeasurer.Measure(Make("tracks.mov", "-c:a", "pcm_s16le"), Profile.EbuR128);

        var report = Profile.EbuR128.Evaluate(measured);

        var audio = report.Findings.Where(f => f.Check == "audio").ToList();
        Assert.Equal(["Audio track 1 of 2: pcm_s16le, 1 channel (mono)", "Audio track 2 of 2: pcm_s16le, 2 channels (stereo)"], audio.Select(f => f.Message));
        foreach (int track in (int[])[1, 2])
        {
            var integrated = Assert.Single(report.Findings, f => f.Check == "loudness.integrated" && f.Track == track);
            Assert.StartsWith($"Track {track}: Integrated loudness", integrated.Message, StringComparison.Ordinal);
            Assert.Contains($"-map 0:a:{track - 1}", integrated.CheckedAgainst, StringComparison.Ordinal);
            Assert.Single(report.Findings, f => f.Check == "loudness.true-peak" && f.Track == track);
        }

        Assert.Single(report.Findings, f => f.Check == "tone" && f.Track == 1);
        Assert.DoesNotContain(report.Findings, f => f.Check == "tone" && f.Track == 2);
        Assert.Contains(report.Findings, f => f.Check == "silence" && f.Track == 2 && f.Severity == Severity.Warning);
        Assert.DoesNotContain(report.Findings, f => f.Check is "tone" or "silence" or "loudness.integrated" && f.Track is null);
    }

    // ManagedCodecs' demuxer gives one audio track: a file with two goes to the FFmpeg path, which judges both.
    [Fact]
    public void AnMp4WithTwoAacTracksGoesToTheFfmpegPath()
    {
        Assert.False(ManagedMeasurer.CanMeasure(Make("tracks.mp4", "-c:a", "aac")));
    }
}
