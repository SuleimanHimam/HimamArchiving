using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace Archiving.Mcp;

/// <summary>
/// The one door this server uses to reach the system: its public REST API.
///
/// It would be quicker to open the MySQL connection directly, and that is exactly why this class
/// exists. Every rule that makes مراسيل a records system rather than a file share lives above the
/// database — the RBAC policies on each controller action, the department scoping applied per
/// caller, and above all the hash-chained audit log, which is written by the API and would simply
/// not record a read that bypassed it. An MCP server on the database would be a way to read
/// classified correspondence with no trace, which is precisely what the product promises is
/// impossible. So: HTTP, as a real signed-in user, with the same permissions that user has in the
/// browser.
/// </summary>
internal sealed class ArchivingClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<ArchivingClient> _log;
    private readonly string _email;
    private readonly string _password;

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _expiresAtUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _authGate = new(1, 1);

    public ArchivingClient(ArchivingOptions options, ILogger<ArchivingClient> log)
    {
        _log = log;
        _email = options.Email;
        _password = options.Password;
        _http = new HttpClient { BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>The signed-in user, refreshed on each successful authentication. Exposed so the
    /// whoami tool can report which account — and therefore which permissions — the caller is
    /// actually operating under, rather than leaving that invisible.</summary>
    public JsonElement? CurrentUser { get; private set; }

    /// <summary>GET a JSON endpoint, authenticating first and once more on a 401.</summary>
    public async Task<JsonElement> GetAsync(string path, IDictionary<string, string?>? query = null, CancellationToken ct = default)
    {
        var url = BuildUrl(path, query);

        var response = await SendAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // The access token expired between the check and the call, or the server restarted and
            // rejected it. One forced re-login, then the answer stands or fails for a real reason.
            _log.LogInformation("Access token rejected; signing in again.");
            await AuthenticateAsync(force: true, ct);
            response.Dispose();
            response = await SendAsync(url, ct);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new McpException(TranslateStatus(response.StatusCode, path, body));

            if (string.IsNullOrWhiteSpace(body)) return default;
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        await AuthenticateAsync(force: false, ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _http.SendAsync(request, ct);
    }

    /// <summary>
    /// Turns an HTTP failure into something the model can act on rather than a bare status code.
    /// 403 in particular is not an error to retry around: it means this account is not allowed to
    /// see this, and saying so plainly stops the model from trying six other routes to the same
    /// forbidden record.
    /// </summary>
    private static string TranslateStatus(HttpStatusCode status, string path, string body) => status switch
    {
        HttpStatusCode.Forbidden =>
            $"Permission denied for {path}. The signed-in Archiving account does not hold the permission this "
          + "action requires, or the record belongs to a department outside its scope. This is a deliberate "
          + "refusal by the system, not a transient error — do not retry.",
        HttpStatusCode.NotFound => $"No such record at {path}.",
        HttpStatusCode.BadRequest => $"The system rejected the request to {path}: {Trim(body)}",
        _ => $"The Archiving API returned {(int)status} for {path}. {Trim(body)}",
    };

    private static string Trim(string body) =>
        body.Length <= 400 ? body : body[..400] + "…";

    /// <summary>Signs in when there is no usable token. The sixty-second margin keeps a token from
    /// expiring in flight on a slow call.</summary>
    private async Task AuthenticateAsync(bool force, CancellationToken ct)
    {
        if (!force && _accessToken is not null && DateTime.UtcNow < _expiresAtUtc.AddSeconds(-60)) return;

        await _authGate.WaitAsync(ct);
        try
        {
            // Re-checked inside the gate: several tools may have queued behind one expiry, and
            // without this each of them would perform its own redundant login.
            if (!force && _accessToken is not null && DateTime.UtcNow < _expiresAtUtc.AddSeconds(-60)) return;

            using var response = await _http.PostAsJsonAsync("api/auth/login",
                new { email = _email, password = _password }, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new McpException(response.StatusCode == HttpStatusCode.Unauthorized
                    ? $"Could not sign in to Archiving as '{_email}' — the account or password in "
                    + "ARCHIVING_USER / ARCHIVING_PASSWORD is wrong, or the account is disabled."
                    : $"Sign-in to {_http.BaseAddress} failed with {(int)response.StatusCode}. {Trim(body)}");
            }

            var auth = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            _accessToken = auth.GetProperty("accessToken").GetString();
            _refreshToken = auth.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;
            _expiresAtUtc = auth.TryGetProperty("expiresAt", out var exp) && exp.TryGetDateTime(out var when)
                ? when.ToUniversalTime()
                : DateTime.UtcNow.AddMinutes(30);   // conservative when the server omits it
            CurrentUser = auth.TryGetProperty("user", out var u) ? u.Clone() : null;

            _log.LogInformation("Signed in to {BaseUrl} as {Email}.", _http.BaseAddress, _email);
        }
        catch (HttpRequestException ex)
        {
            throw new McpException(
                $"Cannot reach the Archiving API at {_http.BaseAddress}. Check that the service is running and "
              + $"that ARCHIVING_API_URL is correct. ({ex.Message})");
        }
        finally
        {
            _authGate.Release();
        }
    }

    private static string BuildUrl(string path, IDictionary<string, string?>? query)
    {
        if (query is null) return path;
        var pairs = query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
            .ToArray();
        return pairs.Length == 0 ? path : $"{path}?{string.Join("&", pairs)}";
    }

    public void Dispose()
    {
        _http.Dispose();
        _authGate.Dispose();
    }
}

// Failures are raised as McpException rather than a custom exception type, and that detail matters
// more than it looks. The SDK deliberately hides ordinary exception messages from the client —
// a tool that throws InvalidOperationException reaches the model as the useless
// "An error occurred invoking 'get_document'." McpException is the SDK's channel for errors that
// are *meant* to be read, so the careful wording in TranslateStatus ("this is a deliberate refusal,
// do not retry") survives the trip instead of being swallowed one layer from its audience.

/// <summary>Where the system is and who to sign in as. Read from the environment rather than a file
/// so a desktop MCP client can carry the credentials in its own config, which is where users
/// already expect to put secrets for these servers.</summary>
internal sealed record ArchivingOptions(string BaseUrl, string Email, string Password)
{
    public static ArchivingOptions FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("ARCHIVING_API_URL");
        var user = Environment.GetEnvironmentVariable("ARCHIVING_USER");
        var pass = Environment.GetEnvironmentVariable("ARCHIVING_PASSWORD");

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(url)) missing.Add("ARCHIVING_API_URL");
        if (string.IsNullOrWhiteSpace(user)) missing.Add("ARCHIVING_USER");
        if (string.IsNullOrWhiteSpace(pass)) missing.Add("ARCHIVING_PASSWORD");

        if (missing.Count > 0)
            throw new InvalidOperationException(
                "Missing configuration: " + string.Join(", ", missing) + ". Set these in the MCP client's "
              + "server definition — see the README for the exact block.");

        return new ArchivingOptions(url!, user!, pass!);
    }
}
