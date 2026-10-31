using MediaQc.Cli;

namespace MediaQc.Tests;

public class CommandLineTests
{
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

    static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = CommandLine.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
