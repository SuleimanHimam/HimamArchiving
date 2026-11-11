using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Archiving.Infrastructure.Storage;

/// <summary>
/// Builds and validates object keys, and maps them onto the file server safely.
///
/// <para><b>Naming scheme.</b> <c>{folder}/{yyyy}/{MM}/{ab}/{cd}/{32-hex}.enc</c> — for example
/// <c>documents/2026/09/3f/a1/3fa19c0e5b7d4e2a8c6f1b0d9e4a7c25.enc</c>.</para>
///
/// <list type="bullet">
///   <item><description><b>Random UUID, not the original filename.</b> The client-supplied name is
///   attacker-controlled and is the usual way path traversal, NTFS alternate data streams and reserved
///   device names (<c>CON</c>, <c>LPT1</c>) get onto a disk. It belongs in the metadata index, never in a
///   path. The extension is discarded too: every object is <c>.enc</c>, so nothing on the file server
///   discloses what a document is.</description></item>
///   <item><description><b>Two levels of hex fan-out</b> (256 × 256) keep directory sizes sane. A single
///   flat directory of a million entries degrades badly on NTFS and ext3/4; sharding on the first four
///   hex characters of the identifier bounds any one directory to a few thousand files even at tens of
///   millions of objects, and spreads writes across the tree.</description></item>
///   <item><description><b>Date segments</b> make the tree navigable for operators and let backup and
///   tiering policies select on age with a directory glob rather than a database query.</description></item>
/// </list>
/// </summary>
public static partial class StorageKeys
{
    /// <summary>Folder component: lowercase alphanumerics, dash, underscore and <c>/</c> for nesting.
    /// Anything else — including <c>.</c>, which is what makes <c>..</c> possible — is rejected outright
    /// rather than stripped, because silent rewriting is how a sanitiser ends up with a bypass.</summary>
    [GeneratedRegex(@"^[a-z0-9][a-z0-9_\-]*(/[a-z0-9][a-z0-9_\-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FolderPattern();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_\-]*(/[a-z0-9][a-z0-9_\-]*)*/\d{4}/\d{2}/[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f]{32}\.enc$",
        RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    public const string Extension = ".enc";

    /// <summary>Mints a fresh key under <paramref name="folder"/>. The caller's filename is not consulted
    /// at all — pass it to the metadata index instead.</summary>
    public static string NewKey(string folder, DateTime? utcNow = null)
    {
        var normalised = NormaliseFolder(folder);
        var stamp = utcNow ?? DateTime.UtcNow;

        // 16 CSPRNG bytes rather than Guid.NewGuid(): no MAC address, no timestamp, no sequence to predict.
        var id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        return $"{normalised}/{stamp:yyyy}/{stamp:MM}/{id[..2]}/{id[2..4]}/{id}{Extension}";
    }

    /// <summary>Validates and lowercases a folder component. Throws on anything that is not a plain
    /// relative path of safe segments.</summary>
    public static string NormaliseFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        // Refuse anything rooted rather than trimming it into a relative path: silently turning
        // "/etc/passwd" into "etc/passwd" quietly accepts a caller who was plainly asking for
        // somewhere else, and a sanitiser that rewrites instead of rejecting is where bypasses live.
        if (folder.StartsWith('/') || folder.StartsWith('\\') || Path.IsPathRooted(folder))
            throw new ArgumentException("Folder must be a relative path.", nameof(folder));

        var normalised = folder.Replace('\\', '/').Trim('/').ToLowerInvariant();

        if (!FolderPattern().IsMatch(normalised))
            throw new ArgumentException(
                "Folder must be lowercase alphanumeric segments separated by '/'.", nameof(folder));

        return normalised;
    }

    /// <summary>True when <paramref name="key"/> is a key this scheme could have produced. Applied on
    /// every read, so a key tampered with in the database still cannot address an arbitrary file.</summary>
    public static bool IsWellFormed(string? key) => key is not null && KeyPattern().IsMatch(key);

    /// <summary>
    /// Maps an object key onto an absolute path under <paramref name="root"/>, refusing anything that
    /// escapes it.
    ///
    /// <para>Three separate defences, because path traversal is the one bug here that hands over the whole
    /// file system: the key must match <see cref="IsWellFormed"/> (so <c>..</c>, drive letters, UNC
    /// prefixes and NUL bytes never get this far); the resolved path is compared against the root
    /// <b>with a trailing separator</b> (without it, root <c>C:\data\store</c> also accepts
    /// <c>C:\data\store-evil</c>); and the comparison is done on <see cref="Path.GetFullPath(string)"/>
    /// output so symlink-free normalisation has already happened.</para>
    /// </summary>
    public static string ResolvePath(string root, string key)
    {
        if (!IsWellFormed(key))
            throw new ArgumentException("Malformed storage key.", nameof(key));

        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var candidate = Path.GetFullPath(Path.Combine(rootFull, key.Replace('/', Path.DirectorySeparatorChar)));

        var prefix = rootFull + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Storage key resolves outside the storage root.");

        return candidate;
    }

    /// <summary>Strips a client-supplied filename down to something safe to echo back in a
    /// <c>Content-Disposition</c> header. Never used to build a path.</summary>
    public static string SafeDisplayName(string? fileName, string fallback = "document")
    {
        if (string.IsNullOrWhiteSpace(fileName)) return fallback;

        var name = Path.GetFileName(fileName.Replace('\\', '/'));

        // Control characters first, and removed outright rather than mapped: CR/LF are the response-header
        // injection vector, and turning them into '_' would leave the forged line in place, merely mangled.
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim(' ', '.');

        return name.Length == 0 ? fallback : name.Length > 200 ? name[..200] : name;
    }
}
