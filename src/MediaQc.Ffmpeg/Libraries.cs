using System.Runtime.InteropServices;
using FrameReader;

namespace MediaQc.Ffmpeg;

/// <summary>Loads FFmpeg's shared libraries, the FrameReader.Native ones next to the app.</summary>
static class Libraries
{
    /// <summary>
    /// Loads them, once per process: where FrameReader looks (next to the app under the runtime's own RID, then the
    /// system's places), else under the portable RID (linux-x64, win-x64, ...), where the FrameReader.Native packages
    /// put them in a build's output. A distribution's own .NET has a RID of its own (ubuntu.24.04-x64), so the first
    /// misses them there.
    /// </summary>
    public static void Load()
    {
        if (FFmpegLibraries.CanLoad(null))
            return;

        string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string folder = Path.Combine(AppContext.BaseDirectory, "runtimes", $"{os}-{arch}", "native");
        FFmpegLibraries.Load(Directory.Exists(folder) ? folder : null);
    }
}
