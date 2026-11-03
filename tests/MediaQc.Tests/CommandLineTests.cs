using System.Text.Json;
using MediaQc.Cli;

namespace MediaQc.Tests;

public sealed class CommandLineTests : IClassFixture<Programme>
{
    readonly Programme _programme;

    public CommandLineTests(Programme programme) => _programme = programme;

    [Theory]
    [InlineData]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpSaysWhatItIs(params string[] args)
    {
        var (code, output, error) = Run(args);

        Assert.Equal(CommandLine.Ok, code);
        Assert.Contains("Usage: mediaqc", output, StringComparison.Ordinal);
        Assert.Empty(error);
    }

    [Fact]
    public void VersionIsTheProductsWithoutBuildMetadata()
    {
        var (code, output, _) = Run("--version");

        Assert.Equal(CommandLine.Ok, code);
        Assert.Equal(Product.Version, output.Trim());
        Assert.DoesNotContain("+", output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownArgumentsAreAUsageError()
    {
        var (code, output, error) = Run("input.mxf");

        Assert.Equal(CommandLine.Usage, code);
        Assert.Empty(output);
        Assert.Contains("input.mxf", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("check")]
    [InlineData("check", "a.mov", "b.mov")]
    [InlineData("check", "a.mov", "--out")]
    [InlineData("check", "a.mov", "--frobnicate")]
    public void CheckArgumentsThatDontParseAreAUsageError(params string[] args) =>
        Assert.Equal(CommandLine.Usage, Run(args).Code);

    [Theory]
    [InlineData("serve", "--port")]
    [InlineData("serve", "--port", "0")]
    [InlineData("serve", "--port", "http")]
    [InlineData("serve", "--frobnicate")]
    [InlineData("serve", "--watch")]
    [InlineData("serve", "--watch", "a", "--watch", "b")]
    [InlineData("serve", "--listen", "everywhere")]
    [InlineData("serve", "--listen", "0.0.0.0:0")]
    [InlineData("serve", "--port", "8000", "--listen", "0.0.0.0:8722")]
    [InlineData("serve", "--token", "short")]
    [InlineData("serve", "--http")]
    [InlineData("serve", "--cert", "server.pfx")]
    [InlineData("serve", "--listen", "0.0.0.0:8722", "--root", ".", "--http", "--cert", "server.pfx")]
    [InlineData("serve", "--listen", "0.0.0.0:8722", "--root", ".", "--key", "server.key")]
    [InlineData("serve", "--listen", "0.0.0.0:8722", "--root", ".", "--http", "--http")]
    public void ServeArgumentsThatDontParseAreAUsageError(params string[] args) =>
        Assert.Equal(CommandLine.Usage, Run(args).Code);

    // A certificate that can't be read stops serve before it listens, saying why.
    [Fact]
    public void ServeWithACertificateThatIsntThereIsAUsageError()
    {
        string missing = Path.Combine(_programme.Folder, "missing.pfx");

        var (code, output, error) = Run("serve", "--listen", "0.0.0.0:8722", "--root", _programme.Folder, "--cert", missing, "--no-open");

        Assert.Equal(CommandLine.Usage, code);
        Assert.Empty(output);
        Assert.Contains("missing.pfx", error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckJudgesByTheProfileGiven()
    {
        string folder = Path.Combine(_programme.Folder, "atsc");

        var (_, output, error) = Run("check", _programme.Path, "--out", folder, "--profile", "atsc-a85");

        Assert.Empty(error);
        Assert.Contains("(profile atsc-a85,", output, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "programme.mov.qc.json")));
        Assert.Equal("atsc-a85", json.RootElement.GetProperty("profile").GetString());
    }

    [Theory]
    [InlineData("check", "PROGRAMME", "--profile", "netflix")]
    [InlineData("serve", "--no-open", "--port", "1", "--profile", "netflix")]
    public void AnUnknownProfileIsAUsageError(params string[] args)
    {
        var (code, _, error) = Run([.. args.Select(a => a == "PROGRAMME" ? _programme.Path : a)]);

        Assert.Equal(CommandLine.Usage, code);
        Assert.Contains("No profile netflix", error, StringComparison.Ordinal);
    }

    [Fact]
    public void SharingWithoutARootIsAUsageError()
    {
        var (code, output, error) = Run("serve", "--no-open", "--listen", "0.0.0.0:1");

        Assert.Equal(CommandLine.Usage, code);
        Assert.Empty(output);
        Assert.Contains("--root", error, StringComparison.Ordinal);
    }

    [Fact]
    public void WatchingAFolderThatIsntThereIsAUsageError()
    {
        var (code, output, error) = Run("serve", "--no-open", "--port", "1", "--watch", Path.Combine(_programme.Folder, "missing"));

        Assert.Equal(CommandLine.Usage, code);
        Assert.Empty(output);
        Assert.Contains("No such folder", error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckingAFileThatIsntThereIsAUsageError()
    {
        var (code, _, error) = Run("check", Path.Combine(_programme.Folder, "missing.mov"));

        Assert.Equal(CommandLine.Usage, code);
        Assert.Contains("no such file", error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckingAFileThatIsntMediaIsAReadError()
    {
        string path = Path.Combine(_programme.Folder, "notes.mov");
        File.WriteAllText(path, "not a movie");

        var (code, _, error) = Run("check", path);

        Assert.Equal(CommandLine.Usage, code);
        Assert.Contains("can't read", error, StringComparison.Ordinal);
    }

    // After the line-up, the programme's audio is a short 1 kHz tone at -18 dBFS, well over -23 LUFS: a failure, so exit
    // code 1.
    [Fact]
    public void CheckWritesBothReportsAndExitsWithTheReportsCode()
    {
        string folder = Path.Combine(_programme.Folder, "reports");

        var (code, output, error) = Run("check", _programme.Path, "--out", folder);

        Assert.Empty(error);
        Assert.Equal(QcReport.Failed, code);
        Assert.Contains("programme.mov: Failure", output, StringComparison.Ordinal);
        string html = File.ReadAllText(Path.Combine(folder, "programme.mov.qc.html"));
        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("Failed", html, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "programme.mov.qc.json")));
        Assert.Equal("failure", json.RootElement.GetProperty("verdict").GetString(), ignoreCase: true);
        Assert.Equal(QcReport.Failed, json.RootElement.GetProperty("exitCode").GetInt32());
        var checks = json.RootElement.GetProperty("findings").EnumerateArray().Select(f => f.GetProperty("check").GetString()).ToList();
        Assert.Contains("tone", checks);
        Assert.Contains("silence", checks);
        Assert.Contains("black", checks);
    }

    static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = CommandLine.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
