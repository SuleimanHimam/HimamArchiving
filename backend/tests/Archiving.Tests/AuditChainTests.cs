using Archiving.Domain.Entities;
using Archiving.Infrastructure.Services;
using Xunit;

namespace Archiving.Tests;

public class AuditChainTests
{
    private static AuditLog Sealed(long id, string action, string? previousHash)
    {
        var e = new AuditLog
        {
            Id = id,
            UserId = 1,
            Action = action,
            EntityType = "Document",
            EntityId = id,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, (int)id, DateTimeKind.Utc),
            PreviousHash = previousHash,
        };
        e.Hash = AuditHash.Compute(e, previousHash);
        return e;
    }

    private static List<AuditLog> ValidChain()
    {
        var e1 = Sealed(1, "Created", null);
        var e2 = Sealed(2, "Edited", e1.Hash);
        var e3 = Sealed(3, "Approved", e2.Hash);
        return [e1, e2, e3];
    }

    [Fact]
    public void Compute_is_deterministic()
    {
        var e = Sealed(1, "Created", null);
        Assert.Equal(e.Hash, AuditHash.Compute(e, e.PreviousHash));
    }

    [Fact]
    public void Compute_changes_when_content_changes()
    {
        var e = Sealed(1, "Created", null);
        var tampered = AuditHash.Compute(new AuditLog { Id = 1, UserId = 1, Action = "Deleted", EntityType = "Document", EntityId = 1, CreatedAt = e.CreatedAt }, null);
        Assert.NotEqual(e.Hash, tampered);
    }

    [Fact]
    public void Intact_chain_verifies()
    {
        var result = AuditChainVerifier.Verify(ValidChain());
        Assert.True(result.Intact);
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Verified);
        Assert.Null(result.FirstBrokenId);
    }

    [Fact]
    public void Tampered_content_is_detected()
    {
        var chain = ValidChain();
        chain[1].Action = "Deleted"; // modify content after the entry was sealed

        var result = AuditChainVerifier.Verify(chain);
        Assert.False(result.Intact);
        Assert.Equal(2, result.FirstBrokenId);
    }

    [Fact]
    public void Broken_link_is_detected()
    {
        var chain = ValidChain();
        chain[2].PreviousHash = "DEADBEEF"; // break the link to the prior entry

        var result = AuditChainVerifier.Verify(chain);
        Assert.False(result.Intact);
        Assert.Equal(3, result.FirstBrokenId);
    }

    [Fact]
    public void Empty_log_is_intact()
    {
        var result = AuditChainVerifier.Verify([]);
        Assert.True(result.Intact);
        Assert.Equal(0, result.Total);
    }
}
