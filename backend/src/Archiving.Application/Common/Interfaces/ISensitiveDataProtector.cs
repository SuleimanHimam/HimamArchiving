namespace Archiving.Application.Common.Interfaces;

/// <summary>
/// Column-level protection for personal data the standard marks as sensitive (national id, mobile,
/// bank account). Values are encrypted by the application under a data key from
/// <see cref="IKeyManagementService"/>, so the database and its backups never hold them in clear.
/// </summary>
public interface ISensitiveDataProtector
{
    Task<byte[]> ProtectAsync(string plain, CancellationToken ct = default);
    Task<string> UnprotectAsync(byte[] protectedValue, CancellationToken ct = default);

    /// <summary>"••••5431": the last four characters only, as the standard's display rule shows.</summary>
    static string Mask(string plain) =>
        plain.Length <= 4 ? new string('•', plain.Length) : "••••" + plain[^4..];
}
