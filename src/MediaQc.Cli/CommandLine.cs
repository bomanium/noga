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
               mediaqc serve [--port PORT | --listen ADDRESS:PORT] [--root FOLDER]... [--watch FOLDER]
                             [--token TOKEN] [--no-open]
               mediaqc --help | --version

        check writes FILE.qc.html and FILE.qc.json (next to FILE, or in FOLDER) and prints the findings.
        Profile: ebu-r128 (EBU R128 loudness, true peak, black, silence, bars and line-up tone, pixels outside
        broadcast range; also reports crop, frame types, keyframes, bitrate and the shot list).

        serve runs the web UI on this machine only (http://localhost:8722 unless --port says another), opens it in
        the browser (not with --no-open) and checks the files picked there, writing their reports beside them. With
        --watch, it also checks FOLDER's media files (subfolders too) that have no report yet, new ones once copied.
        --listen 0.0.0.0:8722 shares it on the network: only the --root folders (and --watch's) can be seen, and only
        by who opens the link it prints, which carries a random token (or --token's, 16 characters or more). --root
        limits localhost too.

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

        if (args is ["serve", ..] && ServeArguments(args.Skip(1).ToList()) is var (options, open))
            return Serve.Server.Run(options, open, output, error);

        error.WriteLine($"mediaqc: can't understand: {string.Join(' ', args)}. See mediaqc --help.");
        return Usage;
    }

    static int Check(CheckArguments check, TextWriter output, TextWriter error)
    {
        CheckResult result;
        try
        {
            result = Checker.Run(check.File, check.Out);
        }
        catch (CheckException e)
        {
            error.WriteLine($"mediaqc: {e.Message}");
            return Usage;
        }

        var (report, html, json) = result;
        string name = Path.GetFileName(check.File);
        int code = report.ExitCode(check.WarningsFail);
        output.WriteLine($"{name}: {report.Verdict} (profile {report.Profile}, exit code {code})");

        // Each shot is in the reports; here only how many ("shots").
        foreach (var finding in report.Findings.Where(f => f.Check != "shot"))
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

    // serve's arguments: where it listens, its folders and token, and whether to open the browser; null when they don't
    // parse. A shared server (--listen beyond localhost) gets a random token unless --token gives one.
    static (Serve.ServeOptions Options, bool Open)? ServeArguments(IReadOnlyList<string> args)
    {
        var options = new Serve.ServeOptions();
        bool open = true, placed = false;
        List<string> roots = [];
        for (int i = 0; i < args.Count; i++)
        {
            string? value = i + 1 < args.Count ? args[i + 1] : null;
            switch (args[i])
            {
                case "--port" when !placed && int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out int port) && port is > 0 and < 65536:
                    options = options with { Port = port };
                    (placed, i) = (true, i + 1);
                    break;
                case "--listen" when !placed && System.Net.IPEndPoint.TryParse(value ?? "", out var at) && at.Port is > 0 and < 65536:
                    options = options with { Address = at.Address, Port = at.Port };
                    (placed, i) = (true, i + 1);
                    break;
                case "--no-open":
                    open = false;
                    break;
                case "--watch" when value is not null && options.Watch is null:
                    options = options with { Watch = value };
                    i++;
                    break;
                case "--root" when value is not null:
                    roots.Add(value);
                    i++;
                    break;
                case "--token" when value is { Length: >= 16 } && options.Token is null:
                    options = options with { Token = value };
                    i++;
                    break;
                default:
                    return null;
            }
        }

        options = options with { Roots = roots };
        if (options.Shared && options.Token is null)
            options = options with { Token = Serve.Server.NewToken() };
        return (options, open);
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
