using System.Net;
using System.Text;
using System.Text.Json;

// Archiving — local scan agent.
//
// Runs on the USER's PC (where the scanner is plugged in) and exposes scanning over loopback so the
// cloud-hosted web app can acquire scans. Browsers allow an HTTPS page to call http://127.0.0.1, so
// this works even though the system itself is in the cloud.
//
//   GET  /status                       -> { "status":"ok", "scanners":[..], "printers":[..], "mock": bool }
//   POST /scan  {format}                -> binary body of the scan (application/pdf | image/jpeg)
//   POST /print?printer=..&ext=pdf      -> body = file bytes; prints to the queue -> { "status":"printed", pages }
//
// Run:  archiving-scan-agent            (real scanner via Windows WIA)
//       archiving-scan-agent --mock     (no scanner needed; returns a sample page — for testing the pipeline)

const string Prefix = "http://127.0.0.1:8765/";
bool mock = args.Contains("--mock");

using var listener = new HttpListener();
listener.Prefixes.Add(Prefix);
listener.Start();
Console.WriteLine($"Archiving scan agent listening on {Prefix}  (mock={mock})");
Console.WriteLine("Press Ctrl+C to stop.");

while (true)
{
    HttpListenerContext ctx;
    try { ctx = await listener.GetContextAsync(); }
    catch (Exception) { break; }
    _ = Task.Run(() => HandleAsync(ctx, mock));
}

static async Task HandleAsync(HttpListenerContext ctx, bool mock)
{
    var req = ctx.Request;
    var res = ctx.Response;

    // CORS — allow the cloud SPA origin (echo it back; loopback agents are not a CSRF target for opaque scans).
    res.AddHeader("Access-Control-Allow-Origin", req.Headers["Origin"] ?? "*");
    res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

    try
    {
        if (req.HttpMethod == "OPTIONS") { res.StatusCode = 204; res.Close(); return; }

        if (req.HttpMethod == "GET" && req.Url?.AbsolutePath == "/status")
        {
            var scanners = mock ? new[] { "Mock Scanner" } : SafeListScanners();
            var printers = mock ? new[] { "Mock Printer" } : SafeListPrinters();
            await WriteJsonAsync(res, new { status = "ok", scanners, printers, mock });
            return;
        }

        if (req.HttpMethod == "POST" && req.Url?.AbsolutePath == "/print")
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
                return;
            }

            var pages = mock ? 1 : Print.PrintFile(data, ext, printer);
            await WriteJsonAsync(res, new { status = "printed", printer = printer ?? "(افتراضية)", pages });
            return;
        }

        if (req.HttpMethod == "POST" && req.Url?.AbsolutePath == "/scan")
        {
            var (format, scanner) = await ReadScanRequestAsync(req);

            byte[] jpeg = mock ? Mock.SampleJpeg() : Wia.ScanJpeg(scanner);

            if (format == "jpeg")
            {
                res.ContentType = "image/jpeg";
                res.AddHeader("Content-Disposition", "attachment; filename=scan.jpg");
                await res.OutputStream.WriteAsync(jpeg);
            }
            else // default: pdf
            {
                var pdf = Pdf.FromJpeg(jpeg);
                res.ContentType = "application/pdf";
                res.AddHeader("Content-Disposition", "attachment; filename=scan.pdf");
                await res.OutputStream.WriteAsync(pdf);
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
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }
}

static async Task WriteJsonAsync(HttpListenerResponse res, object payload)
{
    res.ContentType = "application/json";
    var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
    await res.OutputStream.WriteAsync(bytes);
    res.Close();
}

static async Task<(string Format, string? Scanner)> ReadScanRequestAsync(HttpListenerRequest req)
{
    var format = "pdf";
    string? scanner = null;
    if (req.HasEntityBody)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("format", out var f) && f.GetString() is { } fs) format = fs.ToLowerInvariant();
                if (doc.RootElement.TryGetProperty("scanner", out var s) && s.ValueKind == JsonValueKind.String) scanner = s.GetString();
            }
            catch { /* tolerate empty/invalid body — use defaults */ }
        }
    }
    return (format == "jpeg" ? "jpeg" : "pdf", scanner);
}

static string[] SafeListScanners()
{
    try { return Wia.ListScanners(); }
    catch { return Array.Empty<string>(); }
}

static string[] SafeListPrinters()
{
    try { return Print.ListPrinters(); }
    catch { return Array.Empty<string>(); }
}
