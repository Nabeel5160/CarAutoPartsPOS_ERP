using System.Text.Json;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CarAutoParts.Infrastructure.Data.Interceptors;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        WriteAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        WriteAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteAuditLogs(DbContext? context)
    {
        if (context is null)
            return;

        var userName = _currentUser.CurrentUser?.Username ?? "system";
        var entries = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity is not AuditLog && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        // Audit logs are immutable — reject any attempt to soft-delete or hard-delete them.
        foreach (var auditEntry in context.ChangeTracker.Entries<AuditLog>()
                     .Where(e => e.State is EntityState.Deleted or EntityState.Modified))
        {
            if (auditEntry.State == EntityState.Deleted
                || (auditEntry.State == EntityState.Modified
                    && auditEntry.Property(nameof(BaseEntity.IsDeleted)).IsModified
                    && auditEntry.Entity.IsDeleted))
            {
                throw new InvalidOperationException("Audit logs are immutable and cannot be deleted.");
            }
        }

        // Void-not-delete: block soft-delete of posted/voided money documents.
        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>()
                     .Where(e => e.State == EntityState.Modified
                                 && e.Property(nameof(BaseEntity.IsDeleted)).IsModified
                                 && e.Entity.IsDeleted))
        {
            if (entry.Entity is SalesInvoice inv && (inv.IsVoided || inv.PaymentStatus != PaymentStatus.Pending))
                throw new InvalidOperationException("Posted/voided sales invoices cannot be deleted — use void.");
            if (entry.Entity is JournalEntry je && je.Status is JournalStatus.Posted or JournalStatus.Voided)
                throw new InvalidOperationException("Posted/voided journals cannot be deleted — use void.");
            if (entry.Entity is PurchaseInvoice pi && pi.Status == PurchaseInvoiceStatus.Posted)
                throw new InvalidOperationException("Posted purchase invoices cannot be deleted.");
        }

        foreach (var entry in entries)
        {
            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Create,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => AuditAction.Update
            };

            string? oldValues = null;
            string? newValues = null;

            if (entry.State == EntityState.Modified)
            {
                oldValues = SerializeValues(entry.OriginalValues);
                newValues = SerializeValues(entry.CurrentValues);
            }
            else if (entry.State == EntityState.Added)
            {
                newValues = SerializeValues(entry.CurrentValues);
            }
            else if (entry.State == EntityState.Deleted)
            {
                oldValues = SerializeValues(entry.OriginalValues);
            }

            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedBy = userName;
                action = AuditAction.Delete;
                newValues = JsonSerializer.Serialize(new { IsDeleted = true }, JsonOptions);
            }

            context.Set<AuditLog>().Add(new AuditLog
            {
                Action = action,
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id == 0 ? null : entry.Entity.Id,
                UserName = userName,
                OldValues = oldValues,
                NewValues = newValues,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private static string SerializeValues(PropertyValues values)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var property in values.Properties)
        {
            if (property.Name is nameof(BaseEntity.Id))
                continue;
            dict[property.Name] = values[property];
        }

        return JsonSerializer.Serialize(dict, JsonOptions);
    }
}
