using System.Diagnostics;
using FrameReader;
using MediaQc.Ffmpeg;

namespace MediaQc.Tests;

/// <summary>
/// A 14 s programme, generated once for the class: black under 1 kHz line-up tone at -18 dBFS (0-6 s), black and
/// silence (6-9 s), picture under 1 kHz tone too short to be line-up (9-11 s), black and silence to the end
/// (11-14 s); PCM audio in MOV, as a ProRes delivery has, and measured once.
/// </summary>
public sealed class Programme : IDisposable
{
    public Programme()
    {
        Folder = Directory.CreateTempSubdirectory("mediaqc-").FullName;
        Path = System.IO.Path.Combine(Folder, "programme.mov");
        Measured = FfmpegMeasurer.Measure(Write(Path), Profile.EbuR128);
    }

    public string Folder { get; }

    public string Path { get; }

    public Measurements Measured { get; }

    /// <summary>What the ffmpeg command line on PATH prints (its log) for <c>ffmpeg [INPUT] -i programme.mov ARGS -f null -</c>.</summary>
    public string Ffmpeg(string args, string input = "")
    {
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string arg in (string[])["-hide_banner", "-nostats", .. input.Split(' ', StringSplitOptions.RemoveEmptyEntries), "-i", Path, .. args.Split(' '), "-f", "null", "-"])
            start.ArgumentList.Add(arg);
        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new InvalidOperationException("These tests compare with the ffmpeg command line: put ffmpeg on PATH (CI installs it).", e);
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            string log = process.StandardError.ReadToEnd();
            process.WaitForExit();
            _ = output.Result;
            Assert.True(process.ExitCode == 0, $"ffmpeg {args} failed:\n{log}");
            return log;
        }
    }

    public void Dispose() => Directory.Delete(Folder, recursive: true);

    static string Write(string path)
    {
        Libraries.Load();
        TestMedia.Write(path, new TestMediaOptions
        {
            Video = ["color=c=black:d=9", "testsrc2=d=2", "color=c=black:d=3"],
            Audio = "sine=frequency=1000:duration=14,volume=volume=0:enable='between(t,6,9)+gte(t,11)'",
            AudioEncoder = "pcm_s16le",
        });
        return path;
    }
}
