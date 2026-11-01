using System.Diagnostics;
using System.Security.Cryptography;

namespace MediaQc.Tests;

/// <summary>
/// tools/make-samples.sh's files, in the repository's samples/ folder (ignored by git): made once when they aren't
/// there or an older make-samples.sh made them (its SHA-256 in samples/.make-samples.sha256; bash and ffmpeg with
/// libx264 on PATH; ~45 s, ~480 MB), then kept. CI makes them in a step of its own first.
/// </summary>
public sealed class Samples
{
    /// <summary>The programme's files, by decoder path: ProRes/MOV, DNxHR/MXF, H.264/MP4.</summary>
    public static readonly string[] Programmes = ["programme_prores.mov", "programme_dnxhr.mxf", "programme_h264.mp4"];

    public Samples()
    {
        string root = Root();
        Folder = Path.Combine(root, "samples");
        string script = Path.Combine(root, "tools", "make-samples.sh");
        string stamp = Path.Combine(Folder, ".make-samples.sha256");
        string version = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(script)));
        if (Programmes.All(p => File.Exists(Path.Combine(Folder, p))) && File.Exists(stamp) && File.ReadAllText(stamp).Trim() == version)
            return;

        var start = new ProcessStartInfo("bash") { WorkingDirectory = root, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine("tools", "make-samples.sh"));
        start.ArgumentList.Add(Folder);
        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"tools/make-samples.sh failed:\n{log}");
        foreach (string programme in Programmes)
            Assert.True(File.Exists(Path.Combine(Folder, programme)), $"make-samples.sh made no {programme} (the H.264 file needs an ffmpeg with libx264).");
    }

    /// <summary>The samples folder.</summary>
    public string Folder { get; }

    static string Root()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "MediaQc.slnx")))
                return folder.FullName;
        }

        throw new InvalidOperationException($"No MediaQc.slnx above {AppContext.BaseDirectory}.");
    }
}
