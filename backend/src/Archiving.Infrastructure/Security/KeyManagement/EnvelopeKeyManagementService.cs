using System.Security.Cryptography;
using Archiving.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Security.KeyManagement;

/// <summary>
/// Envelope encryption over an <see cref="IKekProvider"/>: mints a fresh 32-byte DEK per object, has the
/// KMS wrap it, and never persists key material in the clear.
///
/// <para>The active KEK id is cached for a short window rather than fetched per upload — an external KMS
/// round trip per document would dominate ingest latency — but unwrapping always names the KEK version
/// recorded on the object's own row, so a rotation mid-flight can never make an object unreadable.</para>
/// </summary>
public sealed class EnvelopeKeyManagementService(
    IKekProvider provider,
    ILogger<EnvelopeKeyManagementService> logger) : IKeyManagementService
{
    private static readonly TimeSpan ActiveKeyCacheFor = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _activeKekId = string.Empty;
    private DateTime _activeKekReadAtUtc = DateTime.MinValue;

    public string ActiveKekId => _activeKekId;

    public async Task<DataKey> GenerateDataKeyAsync(CancellationToken ct = default)
    {
        var kekId = await ResolveActiveKekIdAsync(ct).ConfigureAwait(false);
        var dek = RandomNumberGenerator.GetBytes(32);
        try
        {
            var wrapped = await provider.WrapAsync(kekId, dek, ct).ConfigureAwait(false);
            return new DataKey(dek, wrapped, kekId);   // DataKey now owns dek and zeroes it on dispose
        }
        catch
        {
            CryptographicOperations.ZeroMemory(dek);
            throw;
        }
    }

    public Task<byte[]> UnwrapAsync(string kekId, byte[] wrapped, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kekId);
        ArgumentNullException.ThrowIfNull(wrapped);
        return provider.UnwrapAsync(kekId, wrapped, ct);
    }

    public async Task<(byte[] Wrapped, string KekId)> RewrapAsync(string kekId, byte[] wrapped, CancellationToken ct = default)
    {
        var active = await ResolveActiveKekIdAsync(ct).ConfigureAwait(false);
        if (string.Equals(kekId, active, StringComparison.Ordinal)) return (wrapped, kekId);

        var dek = await provider.UnwrapAsync(kekId, wrapped, ct).ConfigureAwait(false);
        try
        {
            var rewrapped = await provider.WrapAsync(active, dek, ct).ConfigureAwait(false);
            return (rewrapped, active);
        }
        finally { CryptographicOperations.ZeroMemory(dek); }
    }

    public async Task<string> CreateKekVersionAsync(CancellationToken ct = default)
    {
        var id = await provider.CreateKeyVersionAsync(ct).ConfigureAwait(false);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _activeKekId = id;
            _activeKekReadAtUtc = DateTime.UtcNow;
        }
        finally { _gate.Release(); }

        logger.LogInformation("KMS {Provider}: new KEK version {KekId} is now active", provider.Name, id);
        return id;
    }

    public async Task<KmsHealth> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var active = await provider.GetActiveKeyIdAsync(ct).ConfigureAwait(false);
            var known = await provider.ListKeyIdsAsync(ct).ConfigureAwait(false);

            // A wrap/unwrap round trip on a throwaway key — proves the KEK is usable, not merely present.
            var probe = RandomNumberGenerator.GetBytes(32);
            try
            {
                var wrapped = await provider.WrapAsync(active, probe, ct).ConfigureAwait(false);
                var opened = await provider.UnwrapAsync(active, wrapped, ct).ConfigureAwait(false);
                try
                {
                    if (!CryptographicOperations.FixedTimeEquals(probe, opened))
                        return new KmsHealth(false, provider.Name, active, known.Count, "Wrap/unwrap round trip did not round-trip.");
                }
                finally { CryptographicOperations.ZeroMemory(opened); }
            }
            finally { CryptographicOperations.ZeroMemory(probe); }

            return new KmsHealth(true, provider.Name, active, known.Count, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KMS health check failed for provider {Provider}", provider.Name);
            // Message only — never the stack or the endpoint, which the health endpoint may expose.
            return new KmsHealth(false, provider.Name, _activeKekId, 0, ex.Message);
        }
    }

    private async Task<string> ResolveActiveKekIdAsync(CancellationToken ct)
    {
        if (_activeKekId.Length > 0 && DateTime.UtcNow - _activeKekReadAtUtc < ActiveKeyCacheFor)
            return _activeKekId;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_activeKekId.Length > 0 && DateTime.UtcNow - _activeKekReadAtUtc < ActiveKeyCacheFor)
                return _activeKekId;

            _activeKekId = await provider.GetActiveKeyIdAsync(ct).ConfigureAwait(false);
            _activeKekReadAtUtc = DateTime.UtcNow;
            return _activeKekId;
        }
        finally { _gate.Release(); }
    }
}
