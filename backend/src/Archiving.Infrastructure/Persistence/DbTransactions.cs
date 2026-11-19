using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Persistence;

/// <summary>
/// One explicit transaction around a unit of work that saves more than once (an approval and its effect,
/// a number and the record that carries it).
///
/// SQL Server runs with <c>EnableRetryOnFailure</c>, which refuses a user transaction unless it is opened
/// inside the execution strategy, so the body runs as the strategy's delegate. On a retry the change
/// tracker is cleared first, so the body starts from the database again rather than from half-applied
/// entities. Non-relational providers (the in-memory test store) have no transactions: the body runs as is.
/// A call made while a transaction is already open joins it.
/// </summary>
internal static class DbTransactions
{
    public static async Task<T> InTransactionAsync<T>(this AppDbContext db, Func<Task<T>> body,
        Func<T, bool> commit, CancellationToken ct)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null)
            return await body();

        var attempt = 0;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await body();
                if (commit(result)) await tx.CommitAsync(ct);
                else
                {
                    await tx.RollbackAsync(ct);
                    db.ChangeTracker.Clear();
                }
                return result;
            }
            catch
            {
                db.ChangeTracker.Clear();
                throw;
            }
        });
    }
}
