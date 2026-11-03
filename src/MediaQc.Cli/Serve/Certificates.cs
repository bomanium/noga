using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MediaQc.Cli.Serve;

/// <summary>
/// A shared server's HTTPS certificate (docs/decisions/2026-11-03-shared-https.md): the one <c>--cert</c> names (a
/// .pfx, or a PEM certificate with its key), else a self-signed one made once and kept in the user's profile, so its
/// fingerprint, which the server prints, stays the same from run to run.
/// </summary>
static class Certificates
{
    /// <summary>The environment variable that holds a .pfx's password, when it has one.</summary>
    public const string PasswordVariable = "MEDIAQC_CERT_PASSWORD";

    /// <summary>A self-signed certificate is made again this long before it would expire.</summary>
    static readonly TimeSpan Renewal = TimeSpan.FromDays(30);

    /// <summary>Where the self-signed certificate is kept: the user's local application data, mediaqc's folder.</summary>
    public static string SelfSignedFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        "mediaqc",
        "serve-certificate.pfx");

    /// <summary>
    /// The certificate in <paramref name="file"/>: with <paramref name="key"/>, a PEM certificate and its PEM key;
    /// without, a .pfx (its password in <see cref="PasswordVariable"/>, if any) or a PEM file holding both.
    /// </summary>
    /// <exception cref="CryptographicException">Not a certificate, a wrong password, or no private key.</exception>
    /// <exception cref="IOException">The file can't be read.</exception>
    public static X509Certificate2 Load(string file, string? key)
    {
        ArgumentNullException.ThrowIfNull(file);
        X509Certificate2 certificate;
        if (key is not null || IsPem(file))
        {
            // A PEM key is ephemeral, which Windows' TLS can't use: the pair goes through PKCS#12 to get one it can.
            using var pem = X509Certificate2.CreateFromPemFile(file, key);
            certificate = X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), null);
        }
        else
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(file, Environment.GetEnvironmentVariable(PasswordVariable));
        }

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new CryptographicException($"{file} has no private key: give the .pfx, or the certificate's key with --key.");
        }

        return certificate;
    }

    /// <summary>
    /// The self-signed certificate kept in <paramref name="file"/> for <paramref name="machine"/>, made (and kept there,
    /// readable by this user only) when there is none, it is for another name, or it expires within 30 days of
    /// <paramref name="now"/>. ECDSA P-256, two years, for TLS servers; its names: the machine's, localhost and the
    /// loopback addresses.
    /// </summary>
    public static X509Certificate2 SelfSigned(string file, string machine, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrEmpty(machine);
        if (Kept(file, machine, now) is { } kept)
            return kept;

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(new X500DistinguishedName($"CN={machine}, O=mediaqc"), key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(machine);
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        names.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var made = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(2));
        byte[] pfx = made.Export(X509ContentType.Pkcs12);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var stream = new FileStream(file, options))
            stream.Write(pfx);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return X509CertificateLoader.LoadPkcs12(pfx, null);
    }

    /// <summary>The certificate's SHA-256 fingerprint as browsers show it: hex pairs joined by colons.</summary>
    public static string Fingerprint(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return string.Join(':', certificate.GetCertHash(HashAlgorithmName.SHA256).Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
    }

    // The certificate kept in the file, if it is one this would make now: for the machine, with its key, not expiring soon.
    static X509Certificate2? Kept(string file, string machine, DateTimeOffset now)
    {
        if (!File.Exists(file))
            return null;
        try
        {
            var kept = X509CertificateLoader.LoadPkcs12FromFile(file, null);
            if (kept.HasPrivateKey && kept.GetNameInfo(X509NameType.SimpleName, false) == machine && kept.NotAfter > (now + Renewal).LocalDateTime)
                return kept;
            kept.Dispose();
        }
        catch (CryptographicException)
        {
            // Not a certificate any more: made again.
        }

        return null;
    }

    static bool IsPem(string file)
    {
        using var reader = new StreamReader(file);
        char[] start = new char[64];
        int read = reader.ReadBlock(start);
        return new string(start, 0, read).Contains("-----BEGIN", StringComparison.Ordinal);
    }
}
