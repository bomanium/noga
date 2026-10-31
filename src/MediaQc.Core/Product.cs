using System.Reflection;

namespace MediaQc;

/// <summary>What the product is called and which version this is (a working name until it gets a real one).</summary>
public static class Product
{
    /// <summary>The working name.</summary>
    public const string Name = "media-qc";

    /// <summary>The informational version, without the build metadata the SDK appends (+commit).</summary>
    public static string Version { get; } = ReadVersion();

    static string ReadVersion()
    {
        string? version = typeof(Product).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (version is null)
            return "0.0.0";
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}
