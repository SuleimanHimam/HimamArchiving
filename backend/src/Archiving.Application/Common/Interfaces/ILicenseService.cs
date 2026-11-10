namespace Archiving.Application.Common.Interfaces;

/// <summary>Why the installed licence is (or is not) usable right now.</summary>
public enum LicenseState
{
    /// <summary>No licence file at the configured path.</summary>
    Missing = 0,
    /// <summary>Unreadable, malformed, or the signature does not match the vendor public key.</summary>
    Invalid = 1,
    /// <summary>Signed for a different machine than the one this instance is running on.</summary>
    MachineMismatch = 2,
    /// <summary>Past its expiry date and past the grace window.</summary>
    Expired = 3,
    /// <summary>Past its expiry date but still inside the grace window — works, warns loudly.</summary>
    Grace = 4,
    /// <summary>Signed, bound to this machine, and in date.</summary>
    Valid = 5,
}

/// <summary>The licence as evaluated for this machine at this moment.</summary>
/// <param name="MachineId">Fingerprint of the host this instance is running on — the value that must
/// be quoted when ordering a licence.</param>
public sealed record LicenseStatus(
    LicenseState    State,
    string          MachineId,
    string?         LicenseId,
    string?         Customer,
    string?         Edition,
    DateTime?       IssuedAt,
    DateTime?       ExpiresAt,
    int?            DaysRemaining,
    int?            MaxUsers,
    IReadOnlySet<string> Features,
    string          Message)
{
    /// <summary>True when the product may be used — a licence in its grace window still counts.</summary>
    public bool IsUsable => State is LicenseState.Valid or LicenseState.Grace;
}

public interface ILicenseService
{
    /// <summary>Fingerprint of the current host. Stable across restarts; changes if the OS is reinstalled.</summary>
    string MachineId { get; }

    /// <summary>False when no signing key is configured: licensing is dormant and nothing is gated.
    /// A development install, or a build made before licensing was switched on, sits here.</summary>
    bool IsEnforced { get; }

    /// <summary>Current status, re-read from disk when the licence file changes or the cache ages out.</summary>
    LicenseStatus GetStatus();

    /// <summary>True when <paramref name="feature"/> is included in a usable licence. A licence that
    /// lists no features at all is unrestricted (every feature on).</summary>
    bool HasFeature(string feature);

    /// <summary>Validates a pasted licence token against this machine and, if it is good, writes it to
    /// the configured path. Returns the status the token produces — the file is only written when usable.</summary>
    LicenseStatus Install(string licenseText);
}
