using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediaQc.Checking;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaQc.Cli.Serve;

/// <summary>
/// <c>mediaqc serve</c>: the web UI (docs/decisions/2026-11-01-web-ui.md), on this machine only (127.0.0.1), or shared on
/// the network (<see cref="ServeOptions.Shared"/>: docs/decisions/2026-11-01-shared-machine.md), then only its roots'
/// folders and only for those with its token. One page (<c>index.html</c>, no scripts from anywhere else) picks files in
/// the server's folders, queues their checks and shows them as they go (server-sent events); each report is the command
/// line's, written beside its file and shown as is.
/// </summary>
static class Server
{
    /// <summary>The port it listens on unless told another.</summary>
    public const int DefaultPort = 8722;

    /// <summary>The header every request that changes something carries (the page's fetches set it).</summary>
    const string Header = "X-MediaQc";

    /// <summary>The cookie a shared server's token is kept in, once its link was opened.</summary>
    const string Cookie = "mediaqc";

    /// <summary>
    /// Builds the server on 127.0.0.1:<paramref name="port"/> (0: any free port), checking files with
    /// <paramref name="check"/>, and those arriving in <paramref name="watch"/> when given (<see cref="Watcher"/>).
    /// Start it with <c>StartAsync</c>; its address is then in <c>Urls</c>.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException"><paramref name="watch"/> isn't a folder.</exception>
    public static WebApplication Build(int port, Func<string, CancellationToken, CheckResult> check, string? watch = null) =>
        Build(new ServeOptions { Port = port, Watch = watch }, check);

    /// <summary>Builds the server <paramref name="options"/> describe, checking files with <paramref name="check"/>.</summary>
    /// <exception cref="DirectoryNotFoundException">The watched folder or a root isn't a folder.</exception>
    /// <exception cref="ArgumentException">A shared server without a root or a token.</exception>
    public static WebApplication Build(ServeOptions options, Func<string, CancellationToken, CheckResult> check)
    {
        ArgumentNullException.ThrowIfNull(options);
        var roots = new Roots(options.Watch is null ? options.Roots : [.. options.Roots, options.Watch]);
        if (options.Shared && (!roots.Any || string.IsNullOrEmpty(options.Token)))
            throw new ArgumentException("A server shared on the network needs a --root (or --watch) folder and a token.", nameof(options));
        var jobs = new Jobs(check);
        var watcher = options.Watch is null ? null : new Watcher(options.Watch, file => jobs.Add(file));
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(options.Address, options.Port, listen =>
        {
            if (options.Certificate is { } certificate)
                listen.UseHttps(certificate);
        }));
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, ServeJsonContext.Default));
        builder.Services.AddSingleton(jobs);
        var app = builder.Build();
        app.Use((context, next) => Guard(context, next, options));
        string page = Page(watcher?.Folder, options.Profile.Name);
        app.Lifetime.ApplicationStarted.Register(() => watcher?.Start());
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            watcher?.Dispose();
            jobs.Dispose();
        });

        app.MapGet("/", () => Results.Text(page, "text/html; charset=utf-8"));
        app.MapGet("/api/folders", (string? path) =>
        {
            try
            {
                return Results.Ok(Folders.List(string.IsNullOrEmpty(path) && !roots.Any ? Folders.Home : path, roots));
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
                : !roots.Contains(request.Path)
                    ? Results.Problem($"Not in a shared folder: {request.Path}", statusCode: StatusCodes.Status403Forbidden)
                    : Results.Ok(jobs.Add(Path.GetFullPath(request.Path))));
        app.MapGet("/api/jobs/{id:int}/report.html", (int id, Jobs jobs) => Report(jobs.Get(id)?.Html, "text/html; charset=utf-8"));
        app.MapGet("/api/jobs/{id:int}/report.json", (int id, Jobs jobs) => Report(jobs.Get(id)?.Json, "application/json"));
        app.MapGet("/api/events", Events);
        return app;
    }

    /// <summary>Runs the server until Ctrl+C, telling <paramref name="output"/> where it is.</summary>
    public static int Run(ServeOptions options, bool open, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(options);
        WebApplication app;
        try
        {
            options = WithCertificate(options);
            app = Build(options, (file, token) => Checker.Run(file, null, options.Profile, token));
        }
        catch (Exception e) when (e is DirectoryNotFoundException or ArgumentException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            error.WriteLine($"mediaqc: {e.Message}");
            return CommandLine.Usage;
        }

        using (app)
        {
            app.StartAsync().GetAwaiter().GetResult();
            int port = new Uri(app.Urls.First()).Port;
            string url;
            if (options.Shared)
            {
                string link = Invariant($"?token={Uri.EscapeDataString(options.Token!)}");
                string scheme = options.Certificate is null ? "http" : "https";
                url = Invariant($"{scheme}://localhost:{port}/{link}");
                output.WriteLine($"{Product.Name} {Product.Version}: the web UI is shared on the network at {scheme}://{Environment.MachineName}:{port}/{link}");
                output.WriteLine($"Only who has that link gets in. Shared folders: {string.Join(", ", new Roots(options.Watch is null ? options.Roots : [.. options.Roots, options.Watch]).Folders)}. Ctrl+C stops it.");
                if (options.Certificate is null)
                    output.WriteLine("It is plain HTTP (--http): the link and the files' names cross the network unencrypted.");
                else if (options.CertificateFile is null)
                    output.WriteLine($"Its certificate is self-signed ({Certificates.SelfSignedFile}), so browsers warn once: accept it if they show this SHA-256 fingerprint: {Certificates.Fingerprint(options.Certificate)}");
            }
            else
            {
                url = app.Urls.First().Replace("127.0.0.1", "localhost", StringComparison.Ordinal);
                output.WriteLine($"{Product.Name} {Product.Version}: the web UI is at {url} (this machine only). Ctrl+C stops it.");
            }

            if (options.Watch is not null)
                output.WriteLine($"Watching {Path.GetFullPath(options.Watch)}: its media files without a report are checked, new ones once copied.");
            if (open)
                Open(url);
            app.WaitForShutdownAsync().GetAwaiter().GetResult();
        }

        return CommandLine.Ok;
    }

    /// <summary>
    /// A shared server's options with its certificate (docs/decisions/2026-11-03-shared-https.md): --cert's, else the
    /// self-signed one kept in the user's profile; none on this machine only or with --http.
    /// </summary>
    static ServeOptions WithCertificate(ServeOptions options)
    {
        if (!options.Shared || options.Http || options.Certificate is not null)
            return options;
        return options with
        {
            Certificate = options.CertificateFile is { } file
                ? Certificates.Load(file, options.KeyFile)
                : Certificates.SelfSigned(Certificates.SelfSignedFile, Environment.MachineName, DateTimeOffset.Now),
        };
    }

    /// <summary>A token for a shared server's link: 128 random bits, URL-safe.</summary>
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // On this machine only: a Host that isn't localhost or 127.0.0.1 (DNS rebinding: another site's name pointed here) is
    // refused. Shared: every request needs the token, from the link (then kept in a cookie: the link takes the token out
    // of the address) or the cookie; another site's page has neither. Both: a change without the page's header or from
    // another origin (a cross-site form or fetch; a custom header can't be sent cross-origin without CORS) is refused.
    static async Task Guard(HttpContext context, Func<Task> next, ServeOptions options)
    {
        var request = context.Request;
        var response = context.Response;
        if (options.Shared)
        {
            string? linked = request.Query["token"];
            if (linked is not null && Same(linked, options.Token!))
            {
                response.Cookies.Append(Cookie, options.Token!, new CookieOptions { HttpOnly = true, Secure = request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromDays(30) });
                response.Redirect(request.Path);
                return;
            }

            if (!request.Cookies.TryGetValue(Cookie, out string? kept) || !Same(kept, options.Token!))
            {
                response.StatusCode = StatusCodes.Status401Unauthorized;
                response.ContentType = "text/plain; charset=utf-8";
                await response.WriteAsync("Open the link mediaqc printed when it started (it ends in ?token=...).").ConfigureAwait(false);
                return;
            }
        }
        else if (request.Host.Host is not ("localhost" or "127.0.0.1"))
        {
            response.StatusCode = StatusCodes.Status421MisdirectedRequest;
            return;
        }

        bool changes = !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method);
        string? origin = request.Headers.Origin;
        bool sameOrigin = origin is null || origin == $"{request.Scheme}://{request.Host}";
        if ((changes && !request.Headers.ContainsKey(Header)) || !sameOrigin)
        {
            response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; frame-ancestors 'self'";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        await next().ConfigureAwait(false);
    }

    // Compared in constant time, so the answer's timing doesn't tell how much of a guess was right.
    static bool Same(string given, string token) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(token));

    static string Invariant(FormattableString text) => text.ToString(System.Globalization.CultureInfo.InvariantCulture);

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

    static string Template { get; } = ReadTemplate();

    static string ReadTemplate()
    {
        using var stream = typeof(Server).Assembly.GetManifestResourceStream("MediaQc.Cli.Serve.index.html")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // The page, with the folder watched (the picker opens there, and a line says it is watched) and the profile.
    static string Page(string? watched, string profile) => Template
        .Replace("{{profile}}", WebUtility.HtmlEncode(profile), StringComparison.Ordinal)
        .Replace("{{product}}", WebUtility.HtmlEncode(Product.Name), StringComparison.Ordinal)
        .Replace("{{version}}", WebUtility.HtmlEncode(Product.Version), StringComparison.Ordinal)
        .Replace("{{watched}}", watched is null ? "" : WebUtility.HtmlEncode(watched), StringComparison.Ordinal);
}

/// <summary>How <c>mediaqc serve</c> runs: where it listens, the folder it watches, the folders it shares, its token and certificate.</summary>
sealed record ServeOptions
{
    /// <summary>The address listened on: 127.0.0.1 (this machine only) unless shared.</summary>
    public IPAddress Address { get; init; } = IPAddress.Loopback;

    /// <summary>The port (0: any free one).</summary>
    public int Port { get; init; } = Server.DefaultPort;

    /// <summary>The folder watched; null: none.</summary>
    public string? Watch { get; init; }

    /// <summary>The folders users may see and check (with <see cref="Watch"/>); empty: the whole machine (localhost only).</summary>
    public IReadOnlyList<string> Roots { get; init; } = [];

    /// <summary>A shared server's token, which its link carries.</summary>
    public string? Token { get; init; }

    /// <summary>The profile every check is judged by.</summary>
    public Profile Profile { get; init; } = Profile.EbuR128;

    /// <summary>The profile's name or file as given on the command line, before it is read into <see cref="Profile"/>.</summary>
    public string? ProfileName { get; init; }

    /// <summary>A shared server without HTTPS (--http).</summary>
    public bool Http { get; init; }

    /// <summary>The certificate file --cert names (a .pfx, or PEM); null: a self-signed one.</summary>
    public string? CertificateFile { get; init; }

    /// <summary>The PEM key file --key names, for a PEM <see cref="CertificateFile"/>.</summary>
    public string? KeyFile { get; init; }

    /// <summary>The certificate it serves HTTPS with; null: plain HTTP (this machine only, or --http).</summary>
    public X509Certificate2? Certificate { get; init; }

    /// <summary>Whether it listens beyond this machine.</summary>
    public bool Shared => !IPAddress.IsLoopback(Address);
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
