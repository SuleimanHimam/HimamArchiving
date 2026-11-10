using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Archiving.Infrastructure.Licensing;

/// <summary>Derives a stable identifier for the host the API is installed on, so a licence issued for
/// one server cannot be copied to another: the licence carries this value, and start-up compares.
/// <para>The identifier is deliberately a one-way hash of hardware/OS identity — it discloses nothing
/// about the machine, and it is what a customer quotes to us when ordering.</para>
/// <para>Stability is the whole point, so exactly one source is used per OS rather than a mix that
/// could shift when a network card is added or removed. It survives reboots, upgrades and hardware
/// swaps; it changes when the OS is reinstalled or the system volume is reformatted, which is when a
/// re-issue is genuinely warranted.</para></summary>
public static class MachineFingerprint
{
    private static readonly Lazy<string> Cached = new(Compute, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>e.g. <c>K3M8Q-1ZR4T-9WYH2-B7NDX</c>. Computed once per process.</summary>
    public static string Current => Cached.Value;

    private static string Compute()
    {
        var seed = PrimarySource() ?? FallbackMacAddress() ?? Environment.MachineName;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("archiving-machine-v1|" + seed));
        return Group(Base32(hash, chars: 20));
    }

    private static string? PrimarySource()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Serial of the volume Windows itself is installed on.
                var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
                if (!string.IsNullOrEmpty(root) &&
                    GetVolumeInformationW(root, null, 0, out var serial, out _, out _, null, 0) &&
                    serial != 0)
                {
                    return "win-vol:" + serial.ToString("X8");
                }
                return null;
            }

            // Linux (and any systemd-ish host): the installation's own machine id.
            foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
            {
                if (File.Exists(path))
                {
                    var id = File.ReadAllText(path).Trim();
                    if (id.Length > 0) return "machine-id:" + id;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Fall through to the MAC-based identity rather than refusing to start.
        }
        return null;
    }

    /// <summary>Lowest physical MAC address — a last resort when the OS identity is unreadable.</summary>
    private static string? FallbackMacAddress()
    {
        try
        {
            var mac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .Select(n => n.GetPhysicalAddress().ToString())
                .Where(a => a.Length >= 12 && a != new string('0', a.Length))
                .OrderBy(a => a, StringComparer.Ordinal)
                .FirstOrDefault();
            return mac is null ? null : "mac:" + mac;
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    /// <summary>Crockford base32 (no I/L/O/U), so a fingerprint can be read down a phone line.</summary>
    private static string Base32(byte[] data, int chars)
    {
        const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var sb = new StringBuilder(chars);
        int bits = 0, value = 0;
        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5 && sb.Length < chars)
            {
                sb.Append(Alphabet[(value >> (bits - 5)) & 31]);
                bits -= 5;
            }
            if (sb.Length == chars) break;
        }
        return sb.ToString();
    }

    private static string Group(string s) =>
        string.Join('-', Enumerable.Range(0, s.Length / 5).Select(i => s.Substring(i * 5, 5)));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationW(
        string rootPathName,
        StringBuilder? volumeNameBuffer, int volumeNameSize,
        out uint volumeSerialNumber, out uint maximumComponentLength, out uint fileSystemFlags,
        StringBuilder? fileSystemNameBuffer, int fileSystemNameSize);
}
