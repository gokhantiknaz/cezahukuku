using CezaHukuku.Domain.Common;
using CezaHukuku.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CezaHukuku.Infrastructure.Persistence;

/// <summary>Eklenen/güncellenen varlıkların CreatedAt/UpdatedAt alanlarını doldurur.</summary>
public sealed class TimestampInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
            return;

        var now = clock.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case Entity entity when entry.State == EntityState.Added:
                    entity.CreatedAt = now;
                    entity.UpdatedAt = now;
                    break;
                case Entity entity when entry.State == EntityState.Modified:
                    entity.UpdatedAt = now;
                    break;
                case ApplicationUser user when entry.State == EntityState.Added:
                    user.CreatedAt = now;
                    break;
            }
        }
    }
}
