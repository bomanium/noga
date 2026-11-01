using FrameReader;
using MediaQc.Ffmpeg;
using MediaQc.Managed;

namespace MediaQc.Cli;

/// <summary>
/// The `mediaqc` command: <c>mediaqc check FILE</c> measures a file, judges it by the profile, writes an HTML and a
/// JSON report and exits with the report's code (docs/decisions/2026-10-31-exit-codes-head-tail-timecode.md).
/// </summary>
static class CommandLine
{
    /// <summary>Success, or a check that passed.</summary>
    public const int Ok = QcReport.Passed;

    /// <summary>The command line couldn't be understood, or the file couldn't be read.</summary>
    public const int Usage = QcReport.Error;

    const string Help = """
        Usage: mediaqc check FILE [--out FOLDER] [--warnings-fail]
               mediaqc --help | --version

        check writes FILE.qc.html and FILE.qc.json (next to FILE, or in FOLDER) and prints the findings.
        Profile: ebu-r128 (EBU R128 loudness, true peak, black, silence, bars and line-up tone).

        Exit codes: 0 passed, 1 failed, 2 usage or read error, 3 warnings (1 with --warnings-fail).
        """;

    /// <summary>Runs the command with these arguments and returns its exit code.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Count == 0 || args is ["-h" or "--help"])
        {
            output.WriteLine($"{Product.Name} {Product.Version}: file-based quality control of video deliveries.");
            output.WriteLine();
            output.Write(Help);
            return Ok;
        }

        if (args is ["--version"])
        {
            output.WriteLine(Product.Version);
            return Ok;
        }

        if (args is ["check", ..] && CheckArguments.Parse([.. args.Skip(1)]) is { } check)
            return Check(check, output, error);

        error.WriteLine($"mediaqc: can't understand: {string.Join(' ', args)}. See mediaqc --help.");
        return Usage;
    }

    static int Check(CheckArguments check, TextWriter output, TextWriter error)
    {
        if (!File.Exists(check.File))
        {
            error.WriteLine($"mediaqc: no such file: {check.File}");
            return Usage;
        }

        var profile = Profile.EbuR128;
        Measurements measured;
        try
        {
            // docs/decisions/2026-10-31-decoding.md: ManagedCodecs for H.264/AAC in MP4, FFmpeg for the rest.
            measured = ManagedMeasurer.CanMeasure(check.File)
                ? ManagedMeasurer.Measure(check.File, profile)
                : FfmpegMeasurer.Measure(check.File, profile);
        }
        catch (Exception e) when (e is FrameReaderException or MediaException or InvalidDataException or NotSupportedException)
        {
            error.WriteLine($"mediaqc: can't read {check.File}: {e.Message}");
            return Usage;
        }

        var report = profile.Evaluate(measured);
        string name = Path.GetFileName(check.File);
        string folder = check.Out ?? Path.GetDirectoryName(Path.GetFullPath(check.File))!;
        Directory.CreateDirectory(folder);
        string html = Path.Combine(folder, name + ".qc.html"), json = Path.Combine(folder, name + ".qc.json");
        File.WriteAllText(html, ReportHtml.Write(report, name));
        File.WriteAllText(json, ReportJson.Write(report, name));

        int code = report.ExitCode(check.WarningsFail);
        output.WriteLine($"{name}: {report.Verdict} (profile {report.Profile}, exit code {code})");
        foreach (var finding in report.Findings)
        {
            string where = (finding.Start, finding.End) switch
            {
                ({ } start, { } end) => $" [{start.Timecode}-{end.Timecode}]",
                ({ } start, null) => $" [{start.Timecode}]",
                _ => "",
            };
            output.WriteLine($"  {finding.Severity,-8} {finding.Check}: {finding.Message}{where}");
        }

        output.WriteLine($"Reports: {html}, {json}");
        return code;
    }

    /// <summary><c>check</c>'s arguments: the file, where the reports go, and whether warnings fail.</summary>
    sealed record CheckArguments(string File, string? Out, bool WarningsFail)
    {
        /// <summary>The arguments after <c>check</c>; null when they don't parse.</summary>
        public static CheckArguments? Parse(IReadOnlyList<string> args)
        {
            string? file = null, folder = null;
            bool warningsFail = false;
            for (int i = 0; i < args.Count; i++)
            {
                switch (args[i])
                {
                    case "--out" when i + 1 < args.Count:
                        folder = args[++i];
                        break;
                    case "--warnings-fail":
                        warningsFail = true;
                        break;
                    case var arg when !arg.StartsWith('-') && file is null:
                        file = arg;
                        break;
                    default:
                        return null;
                }
            }

            return file is null ? null : new CheckArguments(file, folder, warningsFail);
        }
    }
}
