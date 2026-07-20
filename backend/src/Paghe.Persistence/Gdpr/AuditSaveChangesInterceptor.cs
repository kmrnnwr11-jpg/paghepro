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
    private static readonly IReadOnlyDictionary<string, string> NomiSemanticiCampiProtetti =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["IbanProtetto"] = "Iban",
            ["DatiSaluteProtetti"] = "DatiSalute",
            ["DatiVoceProtetti"] = "DatiVoceCedolino",
            [nameof(UtenteEntity.SegretoTotp)] = "DueFattori",
            [nameof(ConfigurazioneSdiEntity.ApiKey)] = "CredenzialeProvider",
            [nameof(CasellaEmailEntity.Password)] = "CredenzialeCasellaPosta",
            [nameof(MessaggioEmailEntity.Corpo)] = "CorpoMessaggio",
        };

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

        // Dopo un DbUpdateException EF mantiene nel tracker le righe di audit Added generate dal
        // tentativo fallito. Prima di un retry le rimuoviamo e le rigeneriamo dallo stato corrente,
        // evitando duplicati senza perdere i SaveChanges successivi realmente distinti.
        foreach (var auditPrecedente in context.ChangeTracker.Entries<AuditLogEntity>()
                     .Where(e => e.State == EntityState.Added
                         && e.Entity.Categoria == "ModificaDati")
                     .ToList())
            auditPrecedente.State = EntityState.Detached;

        // ToList(): fissiamo lo stato prima di aggiungere le righe di audit al tracker.
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is PolicyRetentionEntity or EsecuzioneRetentionEntity
                && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException(
                    "Policy e ricevute di conservazione sono append-only.");

            if (entry.Entity is AuditLogEntity)
            {
                // Il registro è append-only anche per il codice applicativo: può essere inserito,
                // ma mai riscritto o cancellato tramite EF.
                if (entry.State is EntityState.Modified or EntityState.Deleted)
                    throw new InvalidOperationException("Il registro attività è append-only.");
                continue; // non auditare l'audit
            }

            if (entry.Entity is AiAuditEntity)
            {
                // Control plane del Governor AI: catena hash propria, append-only come il registro.
                if (entry.State is EntityState.Modified or EntityState.Deleted)
                    throw new InvalidOperationException("L'audit AI è append-only.");
                continue;
            }

            // I mandati AI sono control plane già auditati dalla catena del Governor
            // (emissione/accesso/diniego/revoca): duplicarne ogni battito di contatore qui
            // renderebbe illeggibile il registro attività.
            if (entry.Entity is AiScopeGrantEntity)
                continue;
            // Le sessioni hanno audit semantico negli endpoint auth. L'interceptor non deve
            // mai esporre neppure i nomi tecnici dei campi che custodiscono hash/selettori.
            if (entry.Entity is SessioneRefreshEntity)
                continue;
            var proprietaTenant = entry.Metadata.FindProperty(nameof(AuditLogEntity.TenantId));
            var eventoPiattaforma = proprietaTenant is null
                && entry.Entity is TenantEntity
                && entry.State == EntityState.Modified
                && contesto.TenantId is not null
                && contesto.UtenteId is not null;
            if (proprietaTenant is null && !eventoPiattaforma) continue; // tenant-scoped o azione piattaforma

            var azione = entry.State switch
            {
                EntityState.Added => "Inserimento",
                EntityState.Modified => "Modifica",
                EntityState.Deleted => "Cancellazione",
                _ => null,
            };
            if (azione is null) continue;

            var valoreTenant = eventoPiattaforma
                ? contesto.TenantId
                : entry.Property(nameof(AuditLogEntity.TenantId)).CurrentValue;
            var tenantId = valoreTenant is Guid t ? t : Guid.Empty;
            if (tenantId == Guid.Empty)
                throw new InvalidOperationException(
                    $"Impossibile auditare {entry.Metadata.ClrType.Name}: TenantId mancante.");
            if (contesto.TenantId is { } tenantRichiesta && tenantRichiesta != tenantId)
                throw new InvalidOperationException(
                    "Operazione bloccata: il tenant del record non coincide con il tenant autenticato.");
            if (!eventoPiattaforma && entry.State == EntityState.Modified)
            {
                var originale = entry.Property(nameof(AuditLogEntity.TenantId)).OriginalValue;
                if (originale is Guid tenantOriginale && tenantOriginale != tenantId)
                    throw new InvalidOperationException(
                        "Operazione bloccata: non è consentito spostare un record fra tenant.");
            }

            var chiave = entry.Metadata.FindPrimaryKey() is { } pk
                ? string.Join(",", pk.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()))
                : "";

            var campi = entry.State == EntityState.Modified
                ? string.Join(",", entry.Properties
                    .Where(p => p.IsModified)
                    .Select(p => NomiSemanticiCampiProtetti.TryGetValue(p.Metadata.Name, out var nome)
                        ? nome
                        : p.Metadata.Name)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal))
                : null;

            voci.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UtenteId = contesto.UtenteId,
                Categoria = "ModificaDati",
                Azione = azione,
                Entita = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                ChiaveEntita = chiave,
                CampiModificati = campi,
                Esito = "Riuscito",
                CorrelationId = contesto.CorrelationId,
                QuandoUtc = ora,
            });
        }

        if (voci.Count > 0)
            context.Set<AuditLogEntity>().AddRange(voci);
    }
}
