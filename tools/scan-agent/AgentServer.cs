using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>The loopback HTTP server. Extracted from Program.cs so the same server can be hosted by
/// the tray UI or by the old headless console mode — the UI is a shell around this, not a rewrite of it.
///
///   GET  /status[?scanner=..]          -> { status, authorized, requiresToken, session{..}, scanners[],
///                                            printers[], mock, capabilities{flatbed,feeder,duplex},
///                                            dpiOptions[], colorModes[], paperSizes[], maxPages }
///   POST /scan  {format, scanner, dpi, color, paper, source, duplex, maxPages, preview}
///                                      -> binary body of the scan (application/pdf | image/jpeg),
///                                         page count in the X-Scan-Pages header
///   POST /print?printer=..&amp;ext=pdf     -> body = file bytes; prints to the queue -> { "status":"printed", pages }
///
/// Every /scan field is optional: an older caller posting only {format} gets the original
/// single-page flatbed behaviour.
///
/// <b>Remote Desktop.</b> Loopback is machine-wide (see <see cref="Session"/>), so on a Terminal
/// Server every session shares one port space and can reach every other session's agent. Two things
/// follow, and both are handled here:
///
///   * the port is taken from a range instead of being fixed, so the second and tenth user to sign
///     in can start an agent at all, and
///   * once the agent knows it is on a shared host it requires a per-user token, so one person's
///     browser cannot drive another person's scanner or printer.
///
/// On an ordinary single-user PC neither applies: the first port is free and no token is demanded,
/// which is exactly the behaviour older builds had.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class AgentServer : IDisposable
{
    public const int DefaultPort = 8765;

    /// <summary>How many ports past <see cref="DefaultPort"/> to try. One per concurrent session, so
    /// this is also the cap on simultaneous Remote Desktop users of the agent on one host.</summary>
    public const int PortRange = 20;

    private readonly AgentSettings _settings;
    private HttpListener _listener = new();
    private CancellationTokenSource? _cts;

    public AgentServer(AgentSettings settings, int? fixedPort = null)
    {
        _settings = settings;
        FixedPort = fixedPort;
        Port = fixedPort ?? DefaultPort;
    }

    /// <summary>Set when the user pinned a port on the command line; null means "take what is free".</summary>
    public int? FixedPort { get; }

    public int Port { get; private set; }
    public string Prefix => $"http://127.0.0.1:{Port}/";
    public bool IsRunning => _listener.IsListening;

    /// <summary>True once the agent has concluded it is sharing a machine with other sessions —
    /// either because it is itself a Remote Desktop session, or because something already held the
    /// default port. In that state every request must carry the user's token.</summary>
    public bool RequiresToken { get; private set; }

    public string Token => _settings.Token;

    /// <summary>Mock mode returns a sample page instead of touching a scanner. Mutable at runtime:
    /// it is read per request, so the tray UI can toggle it without restarting the listener.
    /// Named MockMode, not Mock, so it does not shadow the <see cref="Mock"/> helper class.</summary>
    public bool MockMode { get; set; }

    /// <summary>Raised for every handled request so the UI can show live activity. Fired on a
    /// worker thread — subscribers that touch controls must marshal to the UI thread.</summary>
    public event Action<string>? Logged;

    /// <summary>Set by the tray UI to review pages before they are returned. Receives the scanned
    /// pages, returns the approved set, or null if the user cancelled. Called on a worker thread,
    /// so the implementation must marshal to the UI thread and block until the user decides.
    /// Null when no UI is hosting the server (console mode), in which case `preview` is ignored —
    /// a headless agent has nowhere to show a preview and must not hang the request.</summary>
    public Func<List<byte[]>, ScanOptions, List<byte[]>?>? PreviewRequested { get; set; }

    public void Start()
    {
        if (_listener.IsListening) return;

        var first = FixedPort ?? DefaultPort;
        var last = FixedPort ?? DefaultPort + PortRange - 1;
        HttpListenerException? lastError = null;

        for (var port = first; port <= last; port++)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                lastError = ex;
                listener.Close();
                continue;   // taken by another session's agent, or by something else entirely
            }

            _listener = listener;
            Port = port;

            // Anything other than a clean run on the default port means we are not alone on this
            // machine: either another agent already holds 8765, or Windows says we are inside a
            // Remote Desktop session and therefore one of several.
            RequiresToken = Session.IsRemote || port != DefaultPort;

            _cts = new CancellationTokenSource();
            _ = AcceptLoopAsync(_cts.Token);
            Log($"يستمع على {Prefix} (وضع المحاكاة = {(MockMode ? "نعم" : "لا")})");
            Log($"الجلسة: {Session.Describe()}");
            if (RequiresToken)
                Log("جهاز مشترك — سيُطلب رمز الاقتران من المتصفح قبل المسح أو الطباعة.");
            return;
        }

        // Every port in the range refused us. On a Terminal Server that means the host is full;
        // anywhere else it is almost always a URL-reservation or firewall problem, and the two need
        // very different fixes, so they are worded apart.
        if (FixedPort is { } pinned)
            throw new AgentStartException(
                $"المنفذ {pinned} مستخدم بالفعل — على الأرجح أن نسخة أخرى من الوكيل تعمل حالياً.",
                lastError ?? new HttpListenerException());

        throw new AgentStartException(
            $"تعذّر الاستماع على أي منفذ بين {first} و{last}. إذا كان هذا خادم سطح مكتب بعيد فقد اكتمل عدد "
            + "الجلسات المسموح بها؛ وإلا فقد يمنع Windows حجز العنوان لهذا المستخدم.",
            lastError ?? new HttpListenerException());
    }

    public void Stop()
    {
        if (!_listener.IsListening) return;
        _cts?.Cancel();
        _listener.Stop();
        Log("تم إيقاف الخدمة");
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch (Exception) { break; }   // listener stopped or disposed
            _ = Task.Run(() => HandleAsync(ctx), ct);
        }
    }

    private void Log(string message) => Logged?.Invoke(message);

    /// <summary>Whether this request may touch a device.
    ///
    /// On a single-user PC every request passes, exactly as before. On a shared host the caller must
    /// present this user's token — without it, a page open in one Remote Desktop session could
    /// enumerate, scan from, and print to the devices of everyone else signed into the same server,
    /// because they all share 127.0.0.1.</summary>
    private bool Authorized(HttpListenerRequest req)
    {
        if (!RequiresToken) return true;

        var supplied = req.Headers["X-Agent-Token"] ?? req.QueryString["token"];
        if (string.IsNullOrEmpty(supplied)) return false;

        var a = Encoding.UTF8.GetBytes(supplied);
        var b = Encoding.UTF8.GetBytes(_settings.Token);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;

        // CORS — allow the cloud SPA origin (echo it back; loopback agents are not a CSRF target for
        // opaque scans). X-Agent-Token has to be listed or the browser refuses to send it, and
        // X-Scan-Pages has to be exposed or the caller cannot read the page count off the response.
        res.AddHeader("Access-Control-Allow-Origin", req.Headers["Origin"] ?? "*");
        res.AddHeader("Vary", "Origin");
        res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.AddHeader("Access-Control-Allow-Headers", "Content-Type, X-Agent-Token");
        res.AddHeader("Access-Control-Expose-Headers", "X-Scan-Pages");

        // Private Network Access.
        //
        // 127.0.0.1 is the most private address there is, so a page loaded from anywhere else — the
        // system's own server, say, on a public address — is reaching *inwards* when it calls this
        // agent. Chromium treats that as its own kind of cross-origin request: before the real call
        // it sends a preflight carrying Access-Control-Request-Private-Network, and unless the
        // answer opts in explicitly the request never arrives at all. Without this header the agent
        // is invisible to every page except one opened on the machine itself, which looks exactly
        // like "no scanner found" and sends people hunting for a driver that was never the problem.
        //
        // Opting in is safe for what this server is: it binds loopback only, so the caller is
        // already a page running on this very machine, and on a shared host the token still decides
        // whose devices it may touch.
        if (string.Equals(req.Headers["Access-Control-Request-Private-Network"], "true", StringComparison.OrdinalIgnoreCase))
            res.AddHeader("Access-Control-Allow-Private-Network", "true");

        var path = req.Url?.AbsolutePath ?? "/";
        try
        {
            if (req.HttpMethod == "OPTIONS")
            {
                // Discovery probes twenty ports, and every one of them costs a preflight on top of
                // the request itself. Ten minutes of caching turns the repeat visits back into one
                // round trip each.
                res.AddHeader("Access-Control-Max-Age", "600");
                res.StatusCode = 204;
                res.Close();
                return;
            }

            if (req.HttpMethod == "GET" && path == "/status")
            {
                // /status answers even unauthorized, and deliberately so: it is how a browser probes
                // the port range to find which agent is its own. What it will not do is list devices
                // to a caller that has not proved which session it belongs to.
                var session = new
                {
                    id = Session.Id,
                    user = Session.User,
                    machine = Session.Machine,
                    remote = Session.IsRemote,
                };

                if (!Authorized(req))
                {
                    await WriteJsonAsync(res, new
                    {
                        status = "ok",
                        authorized = false,
                        requiresToken = true,
                        session,
                        scanners = Array.Empty<string>(),
                        printers = Array.Empty<string>(),
                        mock = MockMode,
                    });
                    return;
                }

                var scanners = MockMode ? new[] { "Mock Scanner" } : SafeListScanners();
                var printers = MockMode ? new[] { "Mock Printer" } : SafeListPrinters();
                // Capabilities let the web app offer only what the device really has (no duplex
                // checkbox on a flatbed-only scanner) instead of a fixed menu it may reject.
                var caps = MockMode
                    ? new ScannerCapabilities { Flatbed = true, Feeder = true, Duplex = true }
                    : Wia.Capabilities(req.QueryString["scanner"]);
                await WriteJsonAsync(res, new
                {
                    status = "ok",
                    authorized = true,
                    requiresToken = RequiresToken,
                    session,
                    scanners,
                    printers,
                    mock = MockMode,
                    capabilities = new { flatbed = caps.Flatbed, feeder = caps.Feeder, duplex = caps.Duplex },
                    dpiOptions = new[] { 150, 200, 300, 600 },
                    colorModes = new[] { "blackwhite", "grayscale", "color" },
                    paperSizes = new[] { "a4", "a5", "letter", "legal" },
                    maxPages = Wia.PageCap,
                });
                return;
            }

            // Everything past this point touches a device, so it needs the token on a shared host.
            if (!Authorized(req))
            {
                res.StatusCode = 401;
                await WriteJsonAsync(res, new
                {
                    error = "هذا الوكيل يخصّ جلسة أخرى على هذا الخادم. افتح نافذة الوكيل واضغط «ربط المتصفح».",
                    requiresToken = true,
                });
                Log($"رُفض طلب {path} — رمز اقتران غير صالح");
                return;
            }

            if (req.HttpMethod == "POST" && path == "/print")
            {
                var printer = req.QueryString["printer"];
                var ext = (req.QueryString["ext"] ?? "pdf").Trim().ToLowerInvariant();

                using var ms = new MemoryStream();
                await req.InputStream.CopyToAsync(ms);
                var data = ms.ToArray();
                if (data.Length == 0)
                {
                    res.StatusCode = 400;
                    await WriteJsonAsync(res, new { error = "لا يوجد ملف للطباعة" });
                    Log("طباعة: فشل — لا يوجد ملف");
                    return;
                }

                var pages = MockMode ? 1 : Print.PrintFile(data, ext, printer);
                await WriteJsonAsync(res, new { status = "printed", printer = printer ?? "(افتراضية)", pages });
                Log($"طباعة: {pages} صفحة → {printer ?? "الطابعة الافتراضية"}");
                return;
            }

            if (req.HttpMethod == "POST" && path == "/scan")
            {
                var (format, opts) = await ReadScanRequestAsync(req);

                var pages = MockMode ? Mock.SamplePages(opts) : Wia.ScanPages(opts);
                Log($"مسح ضوئي: {pages.Count} صفحة · {opts.Dpi} dpi · {opts.Color} · " +
                    $"{(opts.Source == PaperSource.Feeder ? "وحدة التغذية" : "الزجاج")}{(opts.Duplex ? " · وجهان" : "")}");

                // Preview hands the pages to the agent's own window first: the user drops bad
                // pages or re-scans, and only the approved set is returned. The HTTP request is
                // deliberately held open meanwhile — the caller asked for a reviewed scan.
                if (opts.Preview && PreviewRequested is not null)
                {
                    var approved = PreviewRequested(pages, opts);
                    if (approved is null)
                    {
                        res.StatusCode = 499;                 // client-visible "user cancelled"
                        await WriteJsonAsync(res, new { error = "أُلغيت عملية المسح من نافذة المعاينة" });
                        Log("مسح ضوئي: أُلغي من نافذة المعاينة");
                        return;
                    }
                    pages = approved;
                    Log($"مسح ضوئي: اعتُمدت {pages.Count} صفحة بعد المعاينة");
                }

                // Pages are reported in a header so the caller can show "3 pages" without
                // parsing the PDF it just received.
                res.AddHeader("X-Scan-Pages", pages.Count.ToString());

                if (format == "jpeg")
                {
                    // JPEG cannot hold multiple pages; the first is returned and the header tells
                    // the caller what it is missing, rather than silently dropping the rest.
                    res.ContentType = "image/jpeg";
                    res.AddHeader("Content-Disposition", "attachment; filename=scan.jpg");
                    await res.OutputStream.WriteAsync(pages[0]);
                    Log($"مسح ضوئي: JPEG ({pages[0].Length / 1024} ك.ب)");
                }
                else // default: pdf
                {
                    var pdf = Pdf.FromJpegs(pages);
                    res.ContentType = "application/pdf";
                    res.AddHeader("Content-Disposition", "attachment; filename=scan.pdf");
                    await res.OutputStream.WriteAsync(pdf);
                    Log($"مسح ضوئي: PDF بـ {pages.Count} صفحة ({pdf.Length / 1024} ك.ب)");
                }
                res.Close();
                return;
            }

            res.StatusCode = 404;
            res.Close();
        }
        catch (Exception ex)
        {
            res.StatusCode = 500;
            var bytes = Encoding.UTF8.GetBytes(ex.Message);
            res.ContentType = "text/plain; charset=utf-8";
            try { await res.OutputStream.WriteAsync(bytes); res.Close(); } catch { /* client gone */ }
            Log($"خطأ في {path}: {ex.Message}");
        }
    }


    private static async Task WriteJsonAsync(HttpListenerResponse res, object payload)
    {
        res.ContentType = "application/json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }

    /// <summary>Parses the scan request. Every field is optional and unknown values fall back to the
    /// default, so an older caller that posts only <c>{format}</c> still behaves exactly as before.</summary>
    private static async Task<(string Format, ScanOptions Options)> ReadScanRequestAsync(HttpListenerRequest req)
    {
        var format = "pdf";
        var opts = new ScanOptions();

        if (req.HasEntityBody)
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("format", out var f) && f.GetString() is { } fs) format = fs.ToLowerInvariant();

                    opts = opts with
                    {
                        Scanner = Str(root, "scanner"),
                        Dpi = Int(root, "dpi") is { } d ? Math.Clamp(d, 50, 1200) : opts.Dpi,
                        Color = Str(root, "color")?.ToLowerInvariant() switch
                        {
                            "blackwhite" or "bw" => ColorMode.BlackWhite,
                            "grayscale" or "gray" or "grey" => ColorMode.Grayscale,
                            "color" or "colour" => ColorMode.Color,
                            _ => opts.Color,
                        },
                        Paper = Str(root, "paper")?.ToLowerInvariant() switch
                        {
                            "a4" => PaperSize.A4,
                            "a5" => PaperSize.A5,
                            "letter" => PaperSize.Letter,
                            "legal" => PaperSize.Legal,
                            "none" or "auto" => PaperSize.None,
                            _ => opts.Paper,
                        },
                        Source = Str(root, "source")?.ToLowerInvariant() switch
                        {
                            "feeder" or "adf" => PaperSource.Feeder,
                            "flatbed" or "glass" => PaperSource.Flatbed,
                            _ => opts.Source,
                        },
                        Duplex = Bool(root, "duplex") ?? opts.Duplex,
                        MaxPages = Int(root, "maxPages") ?? opts.MaxPages,
                        Preview = Bool(root, "preview") ?? opts.Preview,
                    };
                }
                catch { /* tolerate empty/invalid body — use defaults */ }
            }
        }
        return (format == "jpeg" ? "jpeg" : "pdf", opts);

        static string? Str(JsonElement o, string name) =>
            o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        static int? Int(JsonElement o, string name) =>
            o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

        static bool? Bool(JsonElement o, string name) =>
            o.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
    }

    public static string[] SafeListScanners()
    {
        try { return Wia.ListScanners(); }
        catch { return Array.Empty<string>(); }
    }

    public static string[] SafeListPrinters()
    {
        try { return Print.ListPrinters(); }
        catch { return Array.Empty<string>(); }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        if (_listener.IsListening) _listener.Stop();
        _listener.Close();
    }
}

/// <summary>A start failure already phrased for the user, so callers can show ex.Message directly.</summary>
internal sealed class AgentStartException(string message, Exception inner) : Exception(message, inner);
