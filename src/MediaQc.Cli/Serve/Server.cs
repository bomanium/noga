using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaQc.Cli.Serve;

/// <summary>
/// <c>mediaqc serve</c>: the web UI (docs/decisions/2026-11-01-web-ui.md), on this machine only (127.0.0.1). One page
/// (<c>index.html</c>, no scripts from anywhere else) picks files in the server's folders, queues their checks and shows
/// them as they go (server-sent events); each report is the command line's, written beside its file and shown as is.
/// </summary>
static class Server
{
    /// <summary>The port it listens on unless told another.</summary>
    public const int DefaultPort = 8722;

    /// <summary>The header every request that changes something carries (the page's fetches set it).</summary>
    const string Header = "X-MediaQc";

    /// <summary>
    /// Builds the server on 127.0.0.1:<paramref name="port"/> (0: any free port), checking files with
    /// <paramref name="check"/>. Start it with <c>StartAsync</c>; its address is then in <c>Urls</c>.
    /// </summary>
    public static WebApplication Build(int port, Func<string, CancellationToken, CheckResult> check)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, ServeJsonContext.Default));
        builder.Services.AddSingleton(new Jobs(check));
        var app = builder.Build();
        app.Use(Guard);

        app.MapGet("/", () => Results.Text(Page, "text/html; charset=utf-8"));
        app.MapGet("/api/folders", (string? path) =>
        {
            try
            {
                return Results.Ok(Folders.List(string.IsNullOrEmpty(path) ? Folders.Home : path));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return Results.Problem(e.Message, statusCode: StatusCodes.Status404NotFound);
            }
        });
        app.MapGet("/api/jobs", (Jobs jobs) => Results.Ok(jobs.All));
        app.MapPost("/api/jobs", (CheckRequest request, Jobs jobs) =>
            string.IsNullOrWhiteSpace(request.Path) || !File.Exists(request.Path)
                ? Results.Problem($"No such file: {request.Path}", statusCode: StatusCodes.Status400BadRequest)
                : Results.Ok(jobs.Add(Path.GetFullPath(request.Path))));
        app.MapGet("/api/jobs/{id:int}/report.html", (int id, Jobs jobs) => Report(jobs.Get(id)?.Html, "text/html; charset=utf-8"));
        app.MapGet("/api/jobs/{id:int}/report.json", (int id, Jobs jobs) => Report(jobs.Get(id)?.Json, "application/json"));
        app.MapGet("/api/events", Events);
        return app;
    }

    /// <summary>Runs the server until Ctrl+C, telling <paramref name="output"/> where it is.</summary>
    public static int Run(int port, bool open, TextWriter output)
    {
        using var app = Build(port, (file, token) => Checker.Run(file, null, token));
        app.StartAsync().GetAwaiter().GetResult();
        string url = app.Urls.First().Replace("127.0.0.1", "localhost", StringComparison.Ordinal);
        output.WriteLine($"{Product.Name} {Product.Version}: the web UI is at {url} (this machine only). Ctrl+C stops it.");
        if (open)
            Open(url);
        app.WaitForShutdownAsync().GetAwaiter().GetResult();
        app.Services.GetRequiredService<Jobs>().Dispose();
        return CommandLine.Ok;
    }

    // Only this machine's browser, at this address: a Host that isn't localhost or 127.0.0.1 (DNS rebinding: another
    // site's name pointed here) is refused, and so is a change without the page's header or from another origin (a
    // cross-site form or fetch; a custom header can't be sent cross-origin without CORS, which isn't allowed).
    static async Task Guard(HttpContext context, Func<Task> next)
    {
        var request = context.Request;
        string host = request.Host.Host;
        if (host is not ("localhost" or "127.0.0.1"))
        {
            context.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
            return;
        }

        bool changes = !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method);
        string? origin = request.Headers.Origin;
        bool sameOrigin = origin is null || origin == $"{request.Scheme}://{request.Host}";
        if ((changes && !request.Headers.ContainsKey(Header)) || !sameOrigin)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; frame-ancestors 'self'";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        await next().ConfigureAwait(false);
    }

    static IResult Report(string? path, string type) =>
        path is not null && File.Exists(path) ? Results.File(path, type) : Results.NotFound();

    // Every job, now and after each change, as server-sent events, until the page goes away.
    static async Task Events(HttpContext context, Jobs jobs)
    {
        var response = context.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-store";
        var token = context.RequestAborted;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var changed = jobs.Changed;
                string json = JsonSerializer.Serialize(jobs.All, ServeJsonContext.Default.IReadOnlyListJob);
                await response.WriteAsync($"data: {json}\n\n", token).ConfigureAwait(false);
                await response.Body.FlushAsync(token).ConfigureAwait(false);
                await changed.WaitAsync(TimeSpan.FromSeconds(15), token).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    static void Open(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No browser to open (a server without a desktop): the address is printed.
        }
    }

    static string Page { get; } = ReadPage();

    static string ReadPage()
    {
        using var stream = typeof(Server).Assembly.GetManifestResourceStream("MediaQc.Cli.Serve.index.html")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd()
            .Replace("{{product}}", WebUtility.HtmlEncode(Product.Name), StringComparison.Ordinal)
            .Replace("{{version}}", WebUtility.HtmlEncode(Product.Version), StringComparison.Ordinal);
    }
}

/// <summary>A check asked for: the file's path on this machine.</summary>
sealed record CheckRequest(string? Path);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(IReadOnlyList<Job>))]
[JsonSerializable(typeof(Job))]
[JsonSerializable(typeof(Listing))]
[JsonSerializable(typeof(CheckRequest))]
[JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
internal partial class ServeJsonContext : JsonSerializerContext;
