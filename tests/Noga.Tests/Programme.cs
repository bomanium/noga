using FrameReader;
using Noga.Ffmpeg;

namespace Noga.Tests;

/// <summary>
/// A 14 s programme, generated once for the class: black under 1 kHz line-up tone at -18 dBFS (0-6 s), black and
/// silence (6-9 s), picture under 1 kHz tone too short to be line-up (9-11 s), black and silence to the end
/// (11-14 s); PCM audio in MOV, as a ProRes delivery has, and measured once.
/// </summary>
public sealed class Programme : IDisposable
{
    public Programme()
    {
        Folder = Directory.CreateTempSubdirectory("noga-").FullName;
        Path = System.IO.Path.Combine(Folder, "programme.mov");
        Measured = FfmpegMeasurer.Measure(Write(Path), Profile.EbuR128);
    }

    public string Folder { get; }

    public string Path { get; }

    public Measurements Measured { get; }

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
