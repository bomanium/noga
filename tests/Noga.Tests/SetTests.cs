using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Noga.Checking;
using Noga.Cli;

namespace Noga.Tests;

/// <summary>
/// A folder's files as one set (docs/decisions/2026-11-15-set-check.md): each file's report and the set's, one format
/// (a failure) and RMS levels near their median (a warning) where the profile has a set, and one exit code; each
/// file's level, and so the median, against astats.
/// </summary>
public sealed class SetTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("noga-set-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // White noise (anoisesrc's uniform samples, ±a: RMS a/√3) as an MP3 at 192 kb/s CBR, 44.1 kHz: 0.1732 is -20 dB RMS.
    string Chapter(string name, double amplitude, int channels = 1, string rate = "192k", string? folder = null)
    {
        string file = Path.Combine(folder ?? _folder, name);
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        string source = string.Create(CultureInfo.InvariantCulture, $"anoisesrc=d=4:r=44100:a={amplitude}:seed=7");
        string[] args = ["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-f", "lavfi", "-i", source, "-ar", "44100", "-ac", channels.ToString(CultureInfo.InvariantCulture), "-c:a", "libmp3lame", "-b:a", rate, file];
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"ffmpeg failed:\n{log}");
        return file;
    }

    // astats' Overall RMS level.
    static double Astats(string file)
    {
        string log = FfmpegCli.Run(file, "-map 0:a:0 -af astats=measure_perchannel=none:measure_overall=RMS_level");
        return double.Parse(Regex.Match(log, @"RMS level dB: (\S+)").Groups[1].Value, CultureInfo.InvariantCulture);
    }

    // A file's report as a set sees it: its format and its RMS level.
    static SetFile File(string name, double rms, int channels = 1, int rate = 44100, int bitRate = 192_000, Severity verdict = Severity.Pass) => new(name, new QcReport
    {
        Profile = "acx",
        StartTimecode = "00:00:00:00",
        DropFrame = false,
        Format = new MediaFormat("mp3", 60, null, [new AudioFormat(1, "mp3", rate, channels, channels == 1 ? "mono" : "stereo", null) { BitRate = bitRate }]),
        Findings = [new Finding { Check = "level.rms", Severity = verdict, Message = "RMS level", Value = rms, Unit = "dBFS", CheckedAgainst = Profile.RmsFilter }],
    });

    static SetReport Evaluate(Profile profile, params SetFile[] files) => SetReport.Evaluate(profile, "/book", files);

    [Fact]
    public void OneFormatAndLevelsNearTheMedianPass()
    {
        var report = Evaluate(Profiles.Acx, File("1.mp3", -20), File("2.mp3", -21.9), File("3.mp3", -18.1));

        var format = Assert.Single(report.Findings, f => f.Check == "set.format");
        var level = Assert.Single(report.Findings, f => f.Check == "set.level");
        Assert.Equal(Severity.Pass, format.Severity);
        Assert.Equal("The 3 files have one format: mp3, mp3, 44100 Hz, mono, 192 kb/s", format.Message);
        Assert.Equal(SetReport.FormatCheckedAgainst, format.CheckedAgainst);
        Assert.Equal(Severity.Pass, level.Severity);
        Assert.Equal("The 3 files' RMS levels are within 2 dB of their median (median -20.0 dBFS, -21.9 to -18.1 dBFS)", level.Message);
        Assert.Contains("astats", level.CheckedAgainst, StringComparison.Ordinal);
        Assert.Equal(Status.Passed, report.Status);
        Assert.Equal(QcReport.Passed, report.ExitCode());
        Assert.Equal("3 files: 3 passed", report.Counts);
    }

    // The set's format is each value's most common one; a file with another fails, named with each difference.
    [Fact]
    public void AFileOfAnotherFormatFailsTheSet()
    {
        var report = Evaluate(Profiles.Acx, File("1.mp3", -20), File("2.mp3", -20, channels: 2, bitRate: 256_000), File("3.mp3", -20), File("4.mp3", -20, rate: 48000));

        var failures = report.Findings.Where(f => f.Check == "set.format").ToList();
        Assert.Equal(2, failures.Count);
        Assert.All(failures, f => Assert.Equal(Severity.Failure, f.Severity));
        Assert.Equal("2.mp3: channels stereo (the set's: mono); bit rate 256 kb/s (the set's: 192 kb/s)", failures[0].Message);
        Assert.Equal("4.mp3: sample rate 48000 Hz (the set's: 44100 Hz)", failures[1].Message);
        Assert.Equal(QcReport.Failed, report.ExitCode());
    }

    // Further than the tolerance from the median, at the report's 0.1 dB: a warning; the median isn't moved by the odd file.
    [Fact]
    public void AFileFarFromTheMedianWarns()
    {
        var report = Evaluate(Profiles.Acx, File("1.mp3", -20), File("2.mp3", -20.4), File("3.mp3", -22.45), File("4.mp3", -19.6), File("5.mp3", -18));

        var warning = Assert.Single(report.Findings, f => f.Check == "set.level");
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal("3.mp3: RMS level -22.5 dBFS, 2.5 dB below the set's median, -20.0 dBFS (within 2 dB)", warning.Message);
        Assert.Equal(-2.45, Assert.NotNull(warning.Value), 1e-9);
        Assert.Equal(QcReport.Warnings, report.ExitCode());
        Assert.Equal(QcReport.Failed, report.ExitCode(warningsFail: true));
    }

    // An even count's median is the middle two's mean; exactly the tolerance away passes.
    [Fact]
    public void TheMedianOfAnEvenCountIsTheMiddleTwosMean()
    {
        var within = Evaluate(Profiles.Acx, File("1.mp3", -22), File("2.mp3", -20));
        var outside = Evaluate(Profiles.Acx with { Set = new SetSpec(LevelTolerance: 0.9) }, File("1.mp3", -22), File("2.mp3", -20));

        Assert.Equal(Severity.Pass, Assert.Single(within.Findings, f => f.Check == "set.level").Severity);
        Assert.Equal(2, outside.Findings.Count(f => f.Check == "set.level" && f.Severity == Severity.Warning));
        Assert.Contains("1.0 dB below the set's median, -21.0 dBFS (within 0.9 dB)", outside.Findings.First(f => f.Check == "set.level").Message, StringComparison.Ordinal);
    }

    // Without a set in the profile a folder's files are each their own: the worst verdict, nothing compared.
    [Fact]
    public void AProfileWithoutASetComparesNothing()
    {
        var report = Evaluate(Profile.EbuR128, File("1.mp3", -20), File("2.mp3", -30, channels: 2, verdict: Severity.Warning));

        Assert.Empty(report.Findings);
        Assert.Equal(Severity.Warning, report.Verdict);
        Assert.Equal(QcReport.Warnings, report.ExitCode());
        Assert.Equal("2 files: 1 passed, 1 passed with warnings", report.Counts);
        Assert.Null(Profile.EbuR128.Set);
        Assert.NotNull(Profiles.Acx.Set);
        Assert.NotNull(Profiles.AcxMaster.Set);
    }

    // A file that couldn't be read: the set wasn't checked whole, exit code 2 whatever the others say.
    [Fact]
    public void AFileThatCantBeReadIsExitCode2()
    {
        var report = Evaluate(Profiles.Acx, File("1.mp3", -20), File("2.mp3", -30, verdict: Severity.Failure), new SetFile("3.mp3", null, "can't read 3.mp3"));

        Assert.Equal(1, report.Unreadable);
        Assert.Equal(Status.Unreadable, report.Status);
        Assert.Equal(QcReport.Error, report.ExitCode());
        Assert.Equal("3 files: 1 passed, 1 failed, 1 couldn't read", report.Counts);
    }

    // A profile file's set: sameFormat and levelTolerance, from the profile it starts from when left out.
    [Fact]
    public void AProfileFileSetsTheSet()
    {
        var lenient = Profiles.Read("""{ "name": "x", "set": { "levelTolerance": 3 } }""");
        var formatOnly = Profiles.Read("""{ "name": "x", "basedOn": "acx", "set": { "sameFormat": true, "levelTolerance": null } }""");
        var asAcx = Profiles.Read("""{ "name": "x", "basedOn": "acx" }""");

        Assert.Equal(new SetSpec(LevelTolerance: 3), lenient.Set);
        Assert.True(lenient.Set!.SameFormat);
        Assert.Null(formatOnly.Set!.LevelTolerance);
        Assert.Equal(new SetSpec(), asAcx.Set);
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "set": { "levelTolerance": -1 } }"""));
        Assert.Throws<ArgumentException>(() => Profiles.Read("""{ "name": "x", "set": { "tolerance": 1 } }"""));

        var reported = Evaluate(formatOnly, File("1.mp3", -20), File("2.mp3", -26));
        var level = Assert.Single(reported.Findings, f => f.Check == "set.level");
        Assert.Equal(Severity.Info, level.Severity);
        Assert.Equal(6, Assert.NotNull(level.Value), 1e-9);
    }

    // A book: four chapters, one stereo and one quiet. Each file's level is astats', and so is the median the quiet
    // one is judged against; the reports are each file's and the set's, the subfolder's file and the notes left alone.
    [Fact]
    public void ABookIsCheckedAsOneSet()
    {
        string[] chapters = [Chapter("chapter 1.mp3", 0.1732), Chapter("chapter 2.mp3", 0.16), Chapter("chapter 3.mp3", 0.1), Chapter("chapter 4.mp3", 0.25, channels: 2)];
        Chapter("extra.mp3", 0.17, folder: Directory.CreateDirectory(Path.Combine(_folder, "sub")).FullName);
        System.IO.File.WriteAllText(Path.Combine(_folder, "notes.txt"), "not media");
        var seen = new List<string>();

        var (report, html, json) = SetChecker.Run(_folder, null, Profiles.Acx, checkedFile: f => seen.Add(f.Name));

        Assert.Equal(chapters.Select(Path.GetFileName), report.Files.Select(f => f.Name));
        Assert.Equal(report.Files.Select(f => f.Name), seen);
        var levels = chapters.Select(Astats).ToList();
        foreach (var (file, astats) in report.Files.Zip(levels))
            Assert.Equal(astats, Assert.NotNull(file.Rms), 0.05);
        var sorted = levels.Order().ToList();
        double median = (sorted[1] + sorted[2]) / 2;

        var quiet = Assert.Single(report.Findings, f => f.Check == "set.level");
        Assert.Equal(Severity.Warning, quiet.Severity);
        Assert.StartsWith("chapter 3.mp3: ", quiet.Message, StringComparison.Ordinal);
        Assert.Equal(levels[2] - median, Assert.NotNull(quiet.Value), 0.1);
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"the set's median, {median:0.0} dBFS"), quiet.Message, StringComparison.Ordinal);
        var stereo = Assert.Single(report.Findings, f => f.Check == "set.format");
        Assert.Equal("chapter 4.mp3: channels stereo (the set's: mono)", stereo.Message);
        Assert.Equal(QcReport.Failed, report.ExitCode());

        string name = Path.GetFileName(_folder);
        Assert.Equal(Path.Combine(_folder, name + ".set.qc.html"), html);
        Assert.Equal(Path.Combine(_folder, name + ".set.qc.json"), json);
        foreach (string chapter in chapters)
            Assert.True(System.IO.File.Exists(chapter + ".qc.html") && System.IO.File.Exists(chapter + ".qc.json"));
        Assert.False(System.IO.File.Exists(Path.Combine(_folder, "sub", "extra.mp3.qc.json")));
        string page = System.IO.File.ReadAllText(html);
        Assert.Contains("<a href=\"chapter%201.mp3.qc.html\">chapter 1.mp3</a>", page, StringComparison.Ordinal);
        Assert.Contains("<strong>Failed</strong><span>4 files: ", page, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(json));
        var root = document.RootElement;
        Assert.Equal("Failure", root.GetProperty("verdict").GetString());
        Assert.Equal(1, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(0, root.GetProperty("unreadable").GetInt32());
        Assert.Equal(4, root.GetProperty("files").GetArrayLength());
        Assert.Equal("chapter 1.mp3.qc.json", root.GetProperty("files")[0].GetProperty("report").GetString());
        Assert.Equal(levels[0], root.GetProperty("files")[0].GetProperty("rms").GetDouble(), 0.05);
        Assert.Contains(root.GetProperty("findings").EnumerateArray(), f => f.GetProperty("check").GetString() == "set.level");

        // A second run doesn't take the reports for files.
        Assert.Equal(4, SetChecker.Run(_folder, null, Profiles.Acx).Report.Files.Count);
    }

    // noga check FOLDER: a line a file, the set's findings, one exit code; a file that isn't media after all is 2.
    [Fact]
    public void TheCommandChecksAFolder()
    {
        Chapter("1.mp3", 0.1732);
        Chapter("2.mp3", 0.17);
        string reports = Path.Combine(_folder, "reports");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = CommandLine.Run(["check", _folder, "--profile", "acx", "--out", reports], output, error);

        Assert.Equal(QcReport.Passed, code);
        Assert.Empty(error.ToString());
        string[] lines = output.ToString().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("1.mp3: Passed", lines[0]);
        Assert.Equal("2.mp3: Passed", lines[1]);
        Assert.StartsWith("Pass     set.format: The 2 files have one format", lines[2], StringComparison.Ordinal);
        Assert.StartsWith("Pass     set.level: ", lines[3], StringComparison.Ordinal);
        Assert.Equal($"{Path.GetFileName(_folder)}: Passed (2 files: 2 passed; profile acx, exit code 0)", lines[4]);
        Assert.True(System.IO.File.Exists(Path.Combine(reports, "1.mp3.qc.json")));
        Assert.True(System.IO.File.Exists(Path.Combine(reports, Path.GetFileName(_folder) + ".set.qc.html")));

        System.IO.File.WriteAllText(Path.Combine(_folder, "3.mp3"), "not an MP3");
        Assert.Equal(QcReport.Error, CommandLine.Run(["check", _folder, "--profile", "acx"], output, error));
        Assert.Contains("3.mp3: Couldn't read: ", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Couldn't read 1 of 3 files", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderWithoutMediaIsAnError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(CommandLine.Usage, CommandLine.Run(["check", _folder], output, error));
        Assert.Contains("no media files in", error.ToString(), StringComparison.Ordinal);
    }

    // Under a profile with a set and no RMS window, the level is still measured for the set: reported, not judged.
    [Fact]
    public void TheLevelIsReportedForASetWithoutAWindow()
    {
        string file = Chapter("1.mp3", 0.1732);
        var profile = Profiles.Read("""{ "name": "x", "set": {} }""");

        var finding = Assert.Single(Checker.Run(file, null, profile).Report.Findings, f => f.Check == "level.rms");

        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Matches(@"^RMS level -\d+\.\d dBFS$", finding.Message);
        Assert.Equal(Astats(file), Assert.NotNull(finding.Value), 0.05);
        Assert.DoesNotContain(Checker.Run(file, null, Profile.EbuR128).Report.Findings, f => f.Check == "level.rms");
    }
}
