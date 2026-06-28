using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Gdpr;

/// <summary>
/// Interceptor EF Core che scrive una riga di <see cref="AuditLogEntity"/> per ogni
/// inserimento/modifica/cancellazione di un'entità <b>tenant-scoped</b> (cioè con una proprietà
/// <c>TenantId</c>). Le righe di audit sono salvate nella <b>stessa transazione</b> dell'operazione.
///
/// <para>Regola di selezione: si auditano solo le entità con <c>TenantId</c>. Questo include i dati
/// di business (dipendenti, cedolini, fatture, aziende…) ed esclude automaticamente i parametri
/// normativi e le tabelle di sistema (senza tenant), evitando rumore.</para>
///
/// <para>GDPR: non vengono salvati i valori, solo i <b>nomi</b> dei campi modificati.</para>
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ScriviAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ScriviAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ScriviAudit(DbContext? context)
    {
        if (context is null) return;

        var contesto = AmbientContestoAudit.Corrente;
        var ora = DateTime.UtcNow;
        var voci = new List<AuditLogEntity>();

        // ToList(): fissiamo lo stato prima di aggiungere le righe di audit al tracker.
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLogEntity) continue; // non auditare l'audit
            if (entry.Metadata.FindProperty(nameof(AuditLogEntity.TenantId)) is null) continue; // solo tenant-scoped

            var azione = entry.State switch
            {
                EntityState.Added => "Inserimento",
                EntityState.Modified => "Modifica",
                EntityState.Deleted => "Cancellazione",
                _ => null,
            };
            if (azione is null) continue;

            var tenantId = entry.Property(nameof(AuditLogEntity.TenantId)).CurrentValue as Guid?
                           ?? contesto.TenantId ?? Guid.Empty;

            var chiave = entry.Metadata.FindPrimaryKey() is { } pk
                ? string.Join(",", pk.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()))
                : "";

            var campi = entry.State == EntityState.Modified
                ? string.Join(",", entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name))
                : null;

            voci.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UtenteId = contesto.UtenteId,
                Azione = azione,
                Entita = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                ChiaveEntita = chiave,
                CampiModificati = campi,
                QuandoUtc = ora,
            });
        }

        if (voci.Count > 0)
            context.Set<AuditLogEntity>().AddRange(voci);
    }
}
