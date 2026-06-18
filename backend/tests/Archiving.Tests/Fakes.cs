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

/// <summary>No-op audit writer for tests.</summary>
public sealed class NoopAuditWriter : IAuditWriter
{
    public Task WriteAsync(string action, string entityType, long entityId, string? entityTitle = null,
        string? oldValues = null, string? newValues = null, CancellationToken ct = default) => Task.CompletedTask;
}
