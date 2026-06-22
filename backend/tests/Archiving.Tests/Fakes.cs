using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Enums;

namespace Archiving.Tests;

/// <summary>A fully-cleared authenticated user for service tests.</summary>
public sealed class TestCurrentUser : ICurrentUser
{
    public long? UserId => 5;
    public string? Email => "admin@x";
    public bool IsAuthenticated => true;
    public ConfidentialityLevel Clearance => ConfidentialityLevel.HighlyConfidential;
    public string? IpAddress => null;
    public string? UserAgent => null;
}

/// <summary>A current user whose id can be swapped between calls (for segregation-of-duties tests).</summary>
public sealed class MutableCurrentUser : ICurrentUser
{
    public long? UserId { get; set; } = 5;
    public string? Email => "u@x";
    public bool IsAuthenticated => true;
    public ConfidentialityLevel Clearance => ConfidentialityLevel.HighlyConfidential;
    public string? IpAddress => null;
    public string? UserAgent => null;
}

/// <summary>Deterministic password hasher for tests: hash is "h:" + password.</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "h:" + password;
    public bool Verify(string password, string hash) => hash == "h:" + password;
}

/// <summary>No-op storage that reports crypto-shred without touching disk.</summary>
public sealed class NoopFileStorage : IFileStorage
{
    public Task<StoredFile> SaveAsync(string f, string n, Stream c, CancellationToken ct = default) =>
        Task.FromResult(new StoredFile("k", 0, "x"));
    public Task<Stream?> OpenAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
    public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task<string> CryptoShredAsync(string key, CancellationToken ct = default) => Task.FromResult("CryptoShred");
    public Task<string> SecureOverwriteAsync(string key, int passes = 3, CancellationToken ct = default) => Task.FromResult("SecureOverwrite");
}

/// <summary>No-op certificate service for tests.</summary>
public sealed class NoopCertificateService : ICertificateService
{
    public Task<long> IssueAsync(long destructionRequestId, CancellationToken ct = default) => Task.FromResult(1L);
    public Task<(Stream Stream, string FileName)?> OpenAsync(long destructionRequestId, CancellationToken ct = default)
        => Task.FromResult<(Stream Stream, string FileName)?>(null);
}

/// <summary>No-op audit writer for tests.</summary>
public sealed class NoopAuditWriter : IAuditWriter
{
    public Task WriteAsync(string action, string entityType, long entityId, string? entityTitle = null,
        string? oldValues = null, string? newValues = null, CancellationToken ct = default) => Task.CompletedTask;
}
