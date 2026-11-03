using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MediaQc.Checking;
using MediaQc.Cli.Serve;
using Microsoft.AspNetCore.Builder;

namespace MediaQc.Tests;

/// <summary>
/// A shared server over HTTPS (docs/decisions/2026-11-03-shared-https.md): the certificates it serves with, and the
/// server itself on a self-signed one.
/// </summary>
public sealed class HttpsServeTests : IDisposable
{
    const string Token = "0123456789abcdef-token";

    readonly string _folder = Directory.CreateTempSubdirectory("mediaqc-https-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    string Kept => Path.Combine(_folder, "profile", "serve-certificate.pfx");

    [Fact]
    public void ASelfSignedCertificateIsMadeOnceAndKept()
    {
        var now = DateTimeOffset.UtcNow;
        using var made = Certificates.SelfSigned(Kept, "edit-suite", now);
        using var again = Certificates.SelfSigned(Kept, "edit-suite", now.AddDays(100));

        Assert.True(made.HasPrivateKey);
        Assert.Equal("edit-suite", made.GetNameInfo(X509NameType.SimpleName, false));
        Assert.Equal(made.Thumbprint, again.Thumbprint);
        string names = made.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single().Format(false);
        Assert.Contains("edit-suite", names, StringComparison.Ordinal);
        Assert.Contains("localhost", names, StringComparison.Ordinal);
        Assert.Contains("1.3.6.1.5.5.7.3.1", made.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value));
        Assert.InRange(made.NotAfter, now.AddYears(2).AddDays(-2).LocalDateTime, now.AddYears(2).AddDays(1).LocalDateTime);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Kept));
    }

    // Made again for another machine's name, within 30 days of expiring, and when the file isn't a certificate.
    [Fact]
    public void ASelfSignedCertificateIsMadeAgainWhenItWouldntDo()
    {
        var now = DateTimeOffset.UtcNow;
        using var first = Certificates.SelfSigned(Kept, "edit-suite", now);
        using var renamed = Certificates.SelfSigned(Kept, "grading", now);
        using var expiring = Certificates.SelfSigned(Kept, "grading", now.AddYears(2).AddDays(-20));
        File.WriteAllText(Kept, "not a certificate");
        using var broken = Certificates.SelfSigned(Kept, "grading", now);

        Assert.NotEqual(first.Thumbprint, renamed.Thumbprint);
        Assert.NotEqual(renamed.Thumbprint, expiring.Thumbprint);
        Assert.True(broken.HasPrivateKey);
    }

    [Fact]
    public void AGivenCertificateIsReadAsPfxOrPem()
    {
        using var made = Certificates.SelfSigned(Kept, "edit-suite", DateTimeOffset.UtcNow);
        string pfx = Path.Combine(_folder, "company.pfx"), cert = Path.Combine(_folder, "company.crt"), key = Path.Combine(_folder, "company.key");
        string both = Path.Combine(_folder, "both.pem"), bare = Path.Combine(_folder, "bare.crt");
        File.WriteAllBytes(pfx, made.Export(X509ContentType.Pkcs12));
        File.WriteAllText(cert, made.ExportCertificatePem());
        File.WriteAllText(key, made.GetECDsaPrivateKey()!.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(both, made.ExportCertificatePem() + "\n" + made.GetECDsaPrivateKey()!.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(bare, made.ExportCertificatePem());

        foreach (var loaded in (X509Certificate2[])[Certificates.Load(pfx, null), Certificates.Load(cert, key), Certificates.Load(both, null)])
        {
            using (loaded)
            {
                Assert.Equal(made.Thumbprint, loaded.Thumbprint);
                Assert.True(loaded.HasPrivateKey);
            }
        }

        Assert.ThrowsAny<CryptographicException>(() => Certificates.Load(bare, null));
        Assert.ThrowsAny<CryptographicException>(() => Certificates.Load(key, null));
    }

    [Fact]
    public void TheFingerprintIsTheSha256AsBrowsersShowIt()
    {
        using var made = Certificates.SelfSigned(Kept, "edit-suite", DateTimeOffset.UtcNow);

        string fingerprint = Certificates.Fingerprint(made);

        Assert.Matches("^([0-9A-F]{2}:){31}[0-9A-F]{2}$", fingerprint);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(made.RawData)), fingerprint.Replace(":", "", StringComparison.Ordinal));
    }

    // The shared server on the certificate: a client that trusts it (by its fingerprint, as a user would) gets in with
    // the link, its cookie marked Secure; plain HTTP to the same port gets no page (Kestrel answers 400 or closes).
    [Fact]
    public async Task ASharedServerServesHttpsWithItsCertificate()
    {
        string root = Directory.CreateDirectory(Path.Combine(_folder, "deliveries")).FullName;
        File.WriteAllText(Path.Combine(root, "ep01.mov"), "frames");
        using var certificate = Certificates.SelfSigned(Kept, "edit-suite", DateTimeOffset.UtcNow);
        await using var app = Server.Build(new ServeOptions { Address = IPAddress.Any, Port = 0, Roots = [root], Token = Token, Certificate = certificate }, Check);
        await app.StartAsync();
        int port = new Uri(app.Urls.First()).Port;
        string? seen = null;
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = (_, cert, _, _) => (seen = cert is null ? null : Certificates.Fingerprint(cert)) == Certificates.Fingerprint(certificate),
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri($"https://127.0.0.1:{port}/") };
        client.DefaultRequestHeaders.Add("X-MediaQc", "1");
        using var plain = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };

        var link = await client.GetAsync(new Uri($"/?token={Token}", UriKind.Relative));
        var check = await client.PostAsJsonAsync(new Uri("/api/jobs", UriKind.Relative), new { path = Path.Combine(root, "ep01.mov") });
        HttpStatusCode? overHttp = null;
        try
        {
            overHttp = (await plain.GetAsync(new Uri($"/?token={Token}", UriKind.Relative))).StatusCode;
        }
        catch (HttpRequestException)
        {
            // The connection closed: no answer at all.
        }

        Assert.Equal(Certificates.Fingerprint(certificate), seen);
        Assert.Equal(HttpStatusCode.Redirect, link.StatusCode);
        Assert.Contains(link.Headers.GetValues("Set-Cookie"), c => c.Contains("secure", StringComparison.OrdinalIgnoreCase) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.True(overHttp is null or HttpStatusCode.BadRequest, $"plain HTTP got {overHttp}");
        await app.StopAsync();
    }

    static CheckResult Check(string file, CancellationToken cancellationToken)
    {
        var report = new QcReport { Profile = "ebu-r128", StartTimecode = "00:00:00:00", DropFrame = false, Findings = [] };
        File.WriteAllText(file + ".qc.html", ReportHtml.Write(report, Path.GetFileName(file)));
        File.WriteAllText(file + ".qc.json", ReportJson.Write(report, Path.GetFileName(file)));
        return new CheckResult(report, file + ".qc.html", file + ".qc.json");
    }
}
