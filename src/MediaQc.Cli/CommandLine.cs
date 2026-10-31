namespace MediaQc.Cli;

/// <summary>The `mediaqc` command. A stub while the project is parked: help and version only, no checks yet.</summary>
static class CommandLine
{
    /// <summary>Success.</summary>
    public const int Ok = 0;

    /// <summary>The command line couldn't be understood. The codes a QC run gives pipelines (pass, warnings, failures) come with the first checks.</summary>
    public const int Usage = 2;

    /// <summary>Runs the command with these arguments and returns its exit code.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 0 || args is ["-h" or "--help"])
        {
            output.WriteLine($"{Product.Name} {Product.Version}: file-based quality control of video deliveries (not working yet).");
            output.WriteLine();
            output.WriteLine("Usage: mediaqc [--help | --version]");
            return Ok;
        }

        if (args is ["--version"])
        {
            output.WriteLine(Product.Version);
            return Ok;
        }

        error.WriteLine($"mediaqc: unknown arguments: {string.Join(' ', args)}. There are no checks yet; see mediaqc --help.");
        return Usage;
    }
}
