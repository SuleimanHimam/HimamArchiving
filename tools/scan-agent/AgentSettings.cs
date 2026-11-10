using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>The handful of things the agent has to remember between runs, kept in
/// <c>%APPDATA%\ArchivingScanAgent\settings.json</c>.
///
/// APPDATA rather than a file beside the executable is the whole point: on a Remote Desktop host one
/// installed copy of the agent is run by many people at once, and each of them needs their own
/// token. A per-user path gives that for free, and it also means a roaming profile carries the
/// pairing to whichever server the user lands on tomorrow.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class AgentSettings
{
    /// <summary>Where the web app lives, e.g. <c>https://archive.example.com</c>. Used to build the
    /// pairing link the agent opens in the user's browser. Empty until someone fills it in, or until
    /// the installer passes <c>--app-url=</c>.</summary>
    public string AppUrl { get; set; } = "";

    /// <summary>The shared secret this user's browser must present. Persisted rather than generated
    /// per run so that pairing survives a restart — the port a given agent ends up on does not, and
    /// the browser rediscovers that each time by probing the range with this token.</summary>
    public string Token { get; set; } = "";

    /// <summary>Set once the agent has opened the pairing link, so it does not reopen a browser
    /// window on every single login. Cleared whenever the token or the URL changes.</summary>
    public bool Paired { get; set; }

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArchivingScanAgent");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AgentSettings Load()
    {
        AgentSettings s;
        try
        {
            s = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AgentSettings>(File.ReadAllText(FilePath)) ?? new AgentSettings()
                : new AgentSettings();
        }
        catch
        {
            // A corrupt or unreadable settings file must not stop the agent from starting; a fresh
            // token and an empty URL just means the user pairs again.
            s = new AgentSettings();
        }

        if (string.IsNullOrWhiteSpace(s.Token))
        {
            s.Token = NewToken();
            s.Paired = false;
            s.Save();
        }
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* read-only profile — the agent still works for this run, it just forgets */ }
    }

    /// <summary>Replaces the token, which invalidates every browser previously paired with it.
    /// Offered in the UI as the way out of "someone else used my session".</summary>
    public void ResetToken()
    {
        Token = NewToken();
        Paired = false;
        Save();
    }

    public void SetAppUrl(string url)
    {
        url = (url ?? "").Trim().TrimEnd('/');
        if (string.Equals(url, AppUrl, StringComparison.OrdinalIgnoreCase)) return;
        AppUrl = url;
        Paired = false;   // a different app origin has a different localStorage — pair again
        Save();
    }

    /// <summary>The link that hands this user's browser the token.
    ///
    /// Two deliberate details. It carries no port, because the browser finds the agent by probing the
    /// range with the token — which is what lets the pairing survive the agent landing on a different
    /// port after a restart. And the token rides in the URL *fragment*, which browsers never put on
    /// the wire, so it cannot end up in the web server's access log on the way past.</summary>
    public string PairingUrl =>
        string.IsNullOrWhiteSpace(AppUrl) ? "" : $"{AppUrl}/scan-agent/connect#token={Uri.EscapeDataString(Token)}";

    /// <summary>URL-safe, 160 bits. Long enough that guessing it from another session on the same
    /// host is not a thing, short enough to paste by hand when a browser has to be paired manually.</summary>
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(20)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
