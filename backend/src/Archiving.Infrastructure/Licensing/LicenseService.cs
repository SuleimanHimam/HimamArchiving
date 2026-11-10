using System.Security.Cryptography;
using Archiving.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Archiving.Infrastructure.Licensing;

/// <summary>Loads, verifies and caches the installed licence.
/// <para>Everything happens offline: the licence is a signed blob, the vendor public key is in config,
/// and the machine binding is checked against a locally computed fingerprint. No activation server,
/// nothing to phone home to, which is what an on-premises archive in a closed network needs.</para></summary>
public sealed class LicenseService : ILicenseService
{
    private readonly LicenseOptions           _options;
    private readonly string                   _path;
    private readonly ILogger<LicenseService>  _log;
    private readonly Lock                     _gate = new();

    private LicenseStatus? _cached;
    private DateTime       _cachedAtUtc;
    private (long Length, DateTime Written) _cachedFileStamp;

    public LicenseService(IOptions<LicenseOptions> options, IHostEnvironment env, ILogger<LicenseService> log)
    {
        _options = options.Value;
        _log     = log;
        _path    = Path.IsPathRooted(_options.Path)
            ? _options.Path
            : Path.Combine(env.ContentRootPath, _options.Path);
    }

    public string MachineId => MachineFingerprint.Current;

    /// <summary>False until a signing key is configured, which is what keeps a pre-licensing install
    /// (and local development) running with no licence at all.</summary>
    public bool IsEnforced => _options.IsConfigured;

    public LicenseStatus GetStatus()
    {
        lock (_gate)
        {
            // Re-read when the file changed underneath us (an admin dropped a new licence in) or when
            // the cache has aged out — the latter is what makes an expiry take effect without a restart.
            if (_cached is not null &&
                DateTime.UtcNow - _cachedAtUtc < _options.RecheckInterval &&
                FileStamp() == _cachedFileStamp)
            {
                return _cached;
            }

            _cachedFileStamp = FileStamp();
            _cachedAtUtc     = DateTime.UtcNow;
            _cached          = Evaluate();
            return _cached;
        }
    }

    public bool HasFeature(string feature)
    {
        if (!IsEnforced) return true;      // licensing dormant: nothing is gated

        var status = GetStatus();
        if (!status.IsUsable) return false;
        // No feature list at all means "everything" — the common case for a full-product licence.
        return status.Features.Count == 0 || status.Features.Contains(feature);
    }

    public LicenseStatus Install(string licenseText)
    {
        var status = Evaluate(licenseText);
        if (!status.IsUsable) return status;

        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, licenseText.Trim() + Environment.NewLine);

        lock (_gate)
        {
            _cached          = status;
            _cachedAtUtc     = DateTime.UtcNow;
            _cachedFileStamp = FileStamp();
        }
        _log.LogInformation("Licence installed for {Customer} (id {LicenseId}), expires {ExpiresAt}.",
            status.Customer, status.LicenseId, status.ExpiresAt?.ToString("u") ?? "never");
        return status;
    }

    private (long, DateTime) FileStamp()
    {
        var info = new FileInfo(_path);
        return info.Exists ? (info.Length, info.LastWriteTimeUtc) : (-1, default);
    }

    private LicenseStatus Evaluate()
    {
        if (!File.Exists(_path))
            return Fail(LicenseState.Missing, $"No licence installed. Expected a licence file at {_path}.");

        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch (IOException e)
        {
            return Fail(LicenseState.Invalid, $"Licence file could not be read: {e.Message}");
        }
        return Evaluate(text);
    }

    private LicenseStatus Evaluate(string licenseText)
    {
        if (_options.EffectivePublicKey is not { } publicKey)
            return Fail(LicenseState.Invalid, "No licence public key is configured (License:PublicKey).");

        ECDsa key;
        try
        {
            key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            return Fail(LicenseState.Invalid, "The configured licence public key is not a valid key.");
        }

        using (key)
        {
            if (!LicenseToken.TryRead(licenseText, key, out var payload, out var error) || payload is null)
                return Fail(LicenseState.Invalid, error);

            // Machine binding: this is what stops a licence being carried to a second server.
            if (payload.MachineId != LicensePayload.AnyMachine &&
                !string.Equals(payload.MachineId, MachineId, StringComparison.OrdinalIgnoreCase))
            {
                return Fail(LicenseState.MachineMismatch,
                    $"This licence was issued for a different machine ({payload.MachineId}). " +
                    $"This server is {MachineId}.",
                    payload);
            }

            var now = DateTime.UtcNow;
            if (payload.ExpiresAt is { } expiry)
            {
                var graceEnd = expiry.AddDays(payload.GraceDays);
                if (now > graceEnd)
                    return Fail(LicenseState.Expired, $"Licence expired on {expiry:yyyy-MM-dd}.", payload);

                if (now > expiry)
                {
                    var left = (int)Math.Ceiling((graceEnd - now).TotalDays);
                    return Build(LicenseState.Grace, payload,
                        $"Licence expired on {expiry:yyyy-MM-dd}. Grace period ends in {left} day(s).");
                }
            }

            return Build(LicenseState.Valid, payload, "Licence is valid.");
        }
    }

    private LicenseStatus Build(LicenseState state, LicensePayload p, string message) => new(
        state, MachineId, p.Id, p.Customer, p.Edition, p.IssuedAt, p.ExpiresAt,
        p.ExpiresAt is { } e ? (int)Math.Ceiling((e - DateTime.UtcNow).TotalDays) : null,
        p.MaxUsers, p.Features.ToHashSet(StringComparer.OrdinalIgnoreCase), message);

    private LicenseStatus Fail(LicenseState state, string message, LicensePayload? p = null) => new(
        state, MachineId, p?.Id, p?.Customer, p?.Edition, p?.IssuedAt, p?.ExpiresAt,
        null, p?.MaxUsers, new HashSet<string>(StringComparer.OrdinalIgnoreCase), message);
}
