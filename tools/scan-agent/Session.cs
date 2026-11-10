using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>Which Windows session this agent is running in.
///
/// This matters because of one fact that shapes the whole multi-session design: <b>loopback is
/// machine-wide, not per-session.</b> Every Remote Desktop session on a host shares one 127.0.0.1
/// and one TCP port space. So on an RDS/Terminal Server box:
///
///   * only the first agent to start can bind the fixed port — every later session fails, and
///   * whoever does hold the port is reachable from *every other session* on the machine.
///
/// The first is why <see cref="AgentServer"/> picks a free port out of a range rather than
/// insisting on 8765; the second is why it demands a per-instance token. Neither is needed on an
/// ordinary single-user PC, and neither costs anything there — the first port is free and the
/// browser is handed the token automatically.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Session
{
    /// <summary>Windows session id of this process. 0 is services, 1 the console user on a normal
    /// PC, 2+ typically Remote Desktop sessions.</summary>
    public static int Id => Process.GetCurrentProcess().SessionId;

    public static string User => Environment.UserName;

    public static string Machine => Environment.MachineName;

    /// <summary>True when this process is running inside a Remote Desktop session. The value can
    /// change over the life of a process — a session started at the console and later taken over
    /// remotely flips it — so it is read per call rather than cached.</summary>
    public static bool IsRemote
    {
        get
        {
            try { return GetSystemMetrics(SM_REMOTESESSION) != 0; }
            catch { return false; }
        }
    }

    /// <summary>A short human label for the window and the log: "SERVER01 · ahmed · جلسة 3 (سطح مكتب بعيد)".</summary>
    public static string Describe() =>
        $"{Machine} · {User} · جلسة {Id}{(IsRemote ? " (سطح مكتب بعيد)" : "")}";

    private const int SM_REMOTESESSION = 0x1000;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
