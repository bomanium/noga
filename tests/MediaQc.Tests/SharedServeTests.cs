using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediaQc.Cli;
using MediaQc.Cli.Serve;
using Microsoft.AspNetCore.Builder;

namespace MediaQc.Tests;

/// <summary>
/// mediaqc serve shared on the network (docs/decisions/2026-11-01-shared-machine.md), on a free port of every address:
/// only who has the token gets in, and only the root folders can be seen or checked.
/// </summary>
public sealed class SharedServeTests : IAsyncLifetime
{
    const string Token = "0123456789abcdef-token";

    readonly string _base = Directory.CreateTempSubdirectory("mediaqc-shared-").FullName;
    WebApplication _app = null!;
    Uri _address = null!;

    string Root => Path.Combine(_base, "deliveries");

    string Outside => Path.Combine(_base, "private");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(Root, "show"));
        Directory.CreateDirectory(Outside);
        File.WriteAllText(Path.Combine(Root, "show", "ep01.mov"), "frames");
        File.WriteAllText(Path.Combine(Outside, "secret.mov"), "frames");
        _app = Server.Build(new ServeOptions { Address = IPAddress.Any, Port = 0, Roots = [Root], Token = Token }, Check);
        await _app.StartAsync();
        _address = new Uri($"http://127.0.0.1:{new Uri(_app.Urls.First()).Port}/");
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        Directory.Delete(_base, recursive: true);
    }

    static CheckResult Check(string file, CancellationToken cancellationToken)
    {
        var report = new QcReport { Profile = "ebu-r128", StartTimecode = "00:00:00:00", DropFrame = false, Findings = [] };
        File.WriteAllText(file + ".qc.html", ReportHtml.Write(report, Path.GetFileName(file)));
        File.WriteAllText(file + ".qc.json", ReportJson.Write(report, Path.GetFileName(file)));
        return new CheckResult(report, file + ".qc.html", file + ".qc.json");
    }

    // A client that has opened the link: the cookie it set is sent with every request.
    async Task<HttpClient> SignedIn()
    {
        var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = _address };
        client.DefaultRequestHeaders.Add("X-MediaQc", "1");
        var response = await client.GetAsync(new Uri($"/?token={Token}", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task OnlyTheLinksTokenLetsIn()
    {
        using var stranger = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = _address };

        var bare = await stranger.GetAsync(new Uri("/api/jobs", UriKind.Relative));
        var wrong = await stranger.GetAsync(new Uri("/?token=0123456789abcdef-guess", UriKind.Relative));
        var link = await stranger.GetAsync(new Uri($"/?token={Token}", UriKind.Relative));
        using var client = await SignedIn();

        Assert.Equal(HttpStatusCode.Unauthorized, bare.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, link.StatusCode);
        Assert.Equal("/", link.Headers.Location?.OriginalString);
        Assert.Contains(link.Headers.GetValues("Set-Cookie"), c => c.Contains("httponly", StringComparison.OrdinalIgnoreCase) && c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/api/jobs", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task OnlyTheRootsCanBeSeen()
    {
        using var client = await SignedIn();

        using var top = JsonDocument.Parse(await client.GetStringAsync(new Uri("/api/folders", UriKind.Relative)));
        using var inside = JsonDocument.Parse(await client.GetStringAsync(new Uri($"/api/folders?path={Uri.EscapeDataString(Root)}", UriKind.Relative)));
        var outside = await client.GetAsync(new Uri($"/api/folders?path={Uri.EscapeDataString(Outside)}", UriKind.Relative));
        var above = await client.GetAsync(new Uri($"/api/folders?path={Uri.EscapeDataString(Path.Combine(Root, ".."))}", UriKind.Relative));

        Assert.Equal([Root], top.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("path").GetString()));
        Assert.Equal(["show"], inside.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()));
        Assert.Equal(Folders.Drives, inside.RootElement.GetProperty("parent").GetString());
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, above.StatusCode);
    }

    [Fact]
    public async Task OnlyFilesInTheRootsAreChecked()
    {
        using var client = await SignedIn();

        var inside = await client.PostAsJsonAsync(new Uri("/api/jobs", UriKind.Relative), new { path = Path.Combine(Root, "show", "ep01.mov") });
        var outside = await client.PostAsJsonAsync(new Uri("/api/jobs", UriKind.Relative), new { path = Path.Combine(Outside, "secret.mov") });
        var climbing = await client.PostAsJsonAsync(new Uri("/api/jobs", UriKind.Relative), new { path = Path.Combine(Root, "..", "private", "secret.mov") });

        Assert.Equal(HttpStatusCode.OK, inside.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outside.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, climbing.StatusCode);
    }

    // A link inside a root could lead anywhere: it isn't listed, and what it leads to can't be checked through it.
    [Fact]
    public async Task ALinkOutOfARootLeadsNowhere()
    {
        string link = Path.Combine(Root, "escape");
        Directory.CreateSymbolicLink(link, Outside);
        using var client = await SignedIn();

        using var inside = JsonDocument.Parse(await client.GetStringAsync(new Uri($"/api/folders?path={Uri.EscapeDataString(Root)}", UriKind.Relative)));
        var through = await client.GetAsync(new Uri($"/api/folders?path={Uri.EscapeDataString(link)}", UriKind.Relative));
        var check = await client.PostAsJsonAsync(new Uri("/api/jobs", UriKind.Relative), new { path = Path.Combine(link, "secret.mov") });

        Assert.DoesNotContain("escape", inside.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()));
        Assert.Equal(HttpStatusCode.NotFound, through.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, check.StatusCode);
    }

    [Fact]
    public void ASharedServerNeedsARootAndAToken()
    {
        Assert.Throws<ArgumentException>(() => Server.Build(new ServeOptions { Address = IPAddress.Any, Port = 0, Token = Token }, Check));
        Assert.Throws<ArgumentException>(() => Server.Build(new ServeOptions { Address = IPAddress.Any, Port = 0, Roots = [Root] }, Check));
    }
}
