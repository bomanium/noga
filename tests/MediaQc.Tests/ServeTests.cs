using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediaQc.Cli;
using MediaQc.Cli.Serve;
using Microsoft.AspNetCore.Builder;

namespace MediaQc.Tests;

/// <summary>
/// mediaqc serve on a free port of 127.0.0.1, with a stand-in check (the real one is CommandLineTests' and
/// SampleTests'): the page, the folders, the queue and the guards that keep it to this machine's browser.
/// </summary>
public sealed class ServeTests : IAsyncLifetime
{
    readonly string _folder = Directory.CreateTempSubdirectory("mediaqc-serve-").FullName;
    WebApplication _app = null!;
    HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _app = Server.Build(0, Check);
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
        _client.DefaultRequestHeaders.Add("X-MediaQc", "1");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        Directory.Delete(_folder, recursive: true);
    }

    // Passes "good.mov", fails the loudness of anything else; can't read "bad.mov".
    static CheckResult Check(string file, CancellationToken cancellationToken)
    {
        if (Path.GetFileName(file) == "bad.mov")
            throw new CheckException($"can't read {file}: not media");
        var finding = Path.GetFileName(file) == "good.mov"
            ? new Finding { Check = "loudness.integrated", Severity = Severity.Pass, Message = "ok", CheckedAgainst = "ebur128" }
            : new Finding { Check = "loudness.integrated", Severity = Severity.Failure, Message = "loud", CheckedAgainst = "ebur128" };
        var report = new QcReport { Profile = "ebu-r128", StartTimecode = "00:00:00:00", DropFrame = false, Findings = [finding] };
        string html = file + ".qc.html", json = file + ".qc.json";
        File.WriteAllText(html, ReportHtml.Write(report, Path.GetFileName(file)));
        File.WriteAllText(json, ReportJson.Write(report, Path.GetFileName(file)));
        return new CheckResult(report, html, json);
    }

    string Touch(string name)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, "frames");
        return path;
    }

    static Uri At(string path) => new(path, UriKind.Relative);

    [Fact]
    public async Task ThePageIsServed()
    {
        string page = await _client.GetStringAsync(At("/"));

        Assert.StartsWith("<!DOCTYPE html>", page, StringComparison.Ordinal);
        Assert.Contains(Product.Version, page, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFolderListsItsFoldersThenItsFilesWithoutReportsOrHiddenOnes()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "masters"));
        Touch("programme.mov");
        Touch("programme.mov.qc.html");
        Touch(".hidden.mov");
        Touch("notes.txt");

        using var json = JsonDocument.Parse(await _client.GetStringAsync(At($"/api/folders?path={Uri.EscapeDataString(_folder)}")));

        var entries = json.RootElement.GetProperty("entries").EnumerateArray()
            .Select(e => (e.GetProperty("name").GetString(), e.GetProperty("isFolder").GetBoolean(), e.GetProperty("isMedia").GetBoolean()))
            .ToList();
        Assert.Equal([("masters", true, false), ("notes.txt", false, false), ("programme.mov", false, true)], entries);
        Assert.Equal(Directory.GetParent(_folder)!.FullName, json.RootElement.GetProperty("parent").GetString());
    }

    [Fact]
    public async Task AFolderThatIsntThereIsNotFound()
    {
        var response = await _client.GetAsync(At($"/api/folders?path={Uri.EscapeDataString(Path.Combine(_folder, "missing"))}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChecksRunInTurnAndTheirReportsAreServed()
    {
        string good = Touch("good.mov"), loud = Touch("loud.mov"), bad = Touch("bad.mov");

        foreach (string file in (string[])[good, loud, bad])
            (await _client.PostAsJsonAsync(At("/api/jobs"), new { path = file })).EnsureSuccessStatusCode();
        var jobs = await Until(all => all.All(j => j.GetProperty("state").GetString() is "Done" or "Failed"));

        Assert.Equal([3, 2, 1], jobs.Select(j => j.GetProperty("id").GetInt32()));
        Assert.Equal("Failed", jobs[0].GetProperty("state").GetString());
        Assert.Contains("not media", jobs[0].GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal("Failure", jobs[1].GetProperty("verdict").GetString());
        Assert.Equal(QcReport.Failed, jobs[1].GetProperty("exitCode").GetInt32());
        Assert.Equal("Pass", jobs[2].GetProperty("verdict").GetString());
        string html = await _client.GetStringAsync(At("/api/jobs/1/report.html"));
        Assert.Contains("good.mov", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(At("/api/jobs/3/report.html"))).StatusCode);
    }

    [Fact]
    public async Task AFileThatIsntThereIsntQueued()
    {
        var response = await _client.PostAsJsonAsync(At("/api/jobs"), new { path = Path.Combine(_folder, "missing.mov") });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheEventsStreamSendsTheJobs()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stream = await _client.GetStreamAsync(At("/api/events"), cancel.Token);
        using var reader = new StreamReader(stream);

        string? first = await reader.ReadLineAsync(cancel.Token);
        (await _client.PostAsJsonAsync(At("/api/jobs"), new { path = Touch("good.mov") }, cancel.Token)).EnsureSuccessStatusCode();
        string? line;
        do
            line = await reader.ReadLineAsync(cancel.Token);
        while (line is not null && !line.Contains("good.mov", StringComparison.Ordinal));

        Assert.Equal("data: []", first);
        Assert.NotNull(line);
    }

    // Only this machine's browser: another Host (DNS rebinding), a change without the page's header, or one from
    // another origin is refused.
    [Fact]
    public async Task OtherHostsHeaderlessChangesAndOtherOriginsAreRefused()
    {
        using var otherHost = new HttpRequestMessage(HttpMethod.Get, At("/api/jobs"));
        otherHost.Headers.Host = "attacker.example";
        using var bare = new HttpClient { BaseAddress = _client.BaseAddress };
        using var otherOrigin = new HttpRequestMessage(HttpMethod.Post, At("/api/jobs")) { Content = JsonContent.Create(new { path = Touch("good.mov") }) };
        otherOrigin.Headers.Add("Origin", "http://attacker.example");

        Assert.Equal(HttpStatusCode.MisdirectedRequest, (await _client.SendAsync(otherHost)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bare.PostAsJsonAsync(At("/api/jobs"), new { path = Touch("good.mov") })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(otherOrigin)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bare.GetAsync(At("/api/jobs"))).StatusCode);
    }

    async Task<List<JsonElement>> Until(Func<List<JsonElement>, bool> done)
    {
        for (int i = 0; i < 100; i++)
        {
            using var json = JsonDocument.Parse(await _client.GetStringAsync(At("/api/jobs")));
            var jobs = json.RootElement.EnumerateArray().Select(j => j.Clone()).ToList();
            if (jobs.Count > 0 && done(jobs))
                return jobs;
            await Task.Delay(50);
        }

        throw new TimeoutException("The checks didn't finish.");
    }
}
