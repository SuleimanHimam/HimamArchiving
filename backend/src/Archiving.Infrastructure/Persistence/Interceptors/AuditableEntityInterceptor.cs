using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Archiving.Infrastructure.Persistence.Interceptors;

/// <summary>Stamps audit fields and converts hard deletes of <see cref="ISoftDelete"/> entities into soft deletes.</summary>
public sealed class AuditableEntityInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;
        var now = DateTime.UtcNow;
        var userId = currentUser.UserId;

        foreach (EntityEntry<IAuditableEntity> entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
                case EntityState.Deleted when entry.Entity is ISoftDelete soft:
                    entry.State = EntityState.Modified;       // never hard-delete
                    soft.IsDeleted = true;
                    soft.DeletedAt = now;
                    soft.DeletedBy = userId;
                    break;
            }
        }
    }
}
