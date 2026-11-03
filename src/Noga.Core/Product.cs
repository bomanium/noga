using System.Reflection;

namespace Noga;

/// <summary>What the product is called and which version this is (Noga: Swedish for careful, thorough).</summary>
public static class Product
{
    /// <summary>The product's name.</summary>
    public const string Name = "Noga";

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
