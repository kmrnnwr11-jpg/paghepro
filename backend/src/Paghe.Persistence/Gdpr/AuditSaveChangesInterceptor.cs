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
            ["GiornateProtette"] = "CalendarioPresenze",
            ["DatiVoceProtetti"] = "DatiVoceCedolino",
            ["FirmaSiglaTimbroProtetta"] = "FirmaSiglaTimbro",
            ["ParametriApplicatiProtetti"] = "ParametriApplicatiCedolino",
            ["DatiValidazioneProtetti"] = "DatiValidazioneCedolino",
            ["DatiProtetti"] = "DatiSnapshotCedolino",
            ["PdfProtetto"] = "PdfSnapshotCedolino",
            [nameof(UtenteEntity.SegretoTotp)] = "DueFattori",
            [nameof(ConfigurazioneSdiEntity.ApiKey)] = "CredenzialeProvider",
            [nameof(CasellaEmailEntity.Password)] = "CredenzialeCasellaPosta",
            [nameof(MessaggioEmailEntity.Corpo)] = "CorpoMessaggio",
        };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        VerificaTestateCedolino(eventData.Context);
        VerificaEvidenzeCedolino(eventData.Context);
        VerificaVociCedolino(eventData.Context);
        ScriviAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await VerificaTestateCedolinoAsync(eventData.Context, cancellationToken);
        await VerificaEvidenzeCedolinoAsync(eventData.Context, cancellationToken);
        await VerificaVociCedolinoAsync(eventData.Context, cancellationToken);
        ScriviAudit(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
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

            if (entry.Entity is CedolinoSnapshotEntity or CedolinoDocumentoEntity
                && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException(
                    "Snapshot e documento chiuso del cedolino sono immutabili.");

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

    private static void VerificaTestateCedolino(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<CedolinoEntity>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            if (entry.State == EntityState.Added)
            {
                VerificaTransizioneCedolino(entry, persistito: null);
                continue;
            }

            var riferimento = RiferimentoTestataImmutabile(entry);
            var persistito = context.Set<CedolinoEntity>()
                .AsNoTracking()
                .Where(c => c.TenantId == riferimento.TenantId
                    && c.Id == riferimento.CedolinoId)
                .Select(c => new StatoVersioneCedolino(c.Stato, c.Versione))
                .SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "Cedolino da modificare o eliminare non trovato nello stesso tenant.");
            VerificaTransizioneCedolino(entry, persistito, context);
        }
    }

    private static async Task VerificaTestateCedolinoAsync(
        DbContext? context,
        CancellationToken cancellationToken)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<CedolinoEntity>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            if (entry.State == EntityState.Added)
            {
                VerificaTransizioneCedolino(entry, persistito: null);
                continue;
            }

            var riferimento = RiferimentoTestataImmutabile(entry);
            var persistito = await context.Set<CedolinoEntity>()
                .AsNoTracking()
                .Where(c => c.TenantId == riferimento.TenantId
                    && c.Id == riferimento.CedolinoId)
                .Select(c => new StatoVersioneCedolino(c.Stato, c.Versione))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Cedolino da modificare o eliminare non trovato nello stesso tenant.");
            VerificaTransizioneCedolino(entry, persistito, context);
        }
    }

    private static RiferimentoCedolino RiferimentoTestataImmutabile(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CedolinoEntity> entry)
    {
        var tenantCorrente = entry.Property(c => c.TenantId).CurrentValue;
        var tenantOriginale = entry.Property(c => c.TenantId).OriginalValue;
        var idCorrente = entry.Property(c => c.Id).CurrentValue;
        var idOriginale = entry.Property(c => c.Id).OriginalValue;
        if (tenantCorrente == Guid.Empty || idCorrente == Guid.Empty
            || tenantCorrente != tenantOriginale || idCorrente != idOriginale)
            throw new InvalidOperationException(
                "L'identità tenant/cedolino di una testata esistente è immutabile.");
        return new RiferimentoCedolino(tenantCorrente, idCorrente);
    }

    private static void VerificaTransizioneCedolino(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CedolinoEntity> entry,
        StatoVersioneCedolino? persistito,
        DbContext? context = null)
    {
        var statoCorrente = entry.Property(c => c.Stato).CurrentValue ?? "";
        if (entry.State == EntityState.Added)
        {
            if (!StatoUguale(statoCorrente, Paghe.Core.Domain.StatoCedolino.Bozza))
                throw new InvalidOperationException(
                    "Un nuovo cedolino deve nascere nello stato Bozza.");
            if (entry.Property(c => c.Versione).CurrentValue != 1)
                throw new InvalidOperationException(
                    "Un nuovo cedolino deve nascere con versione 1.");
            return;
        }

        if (persistito is null)
            throw new InvalidOperationException(
                "Manca lo stato persistito necessario a verificare il workflow del cedolino.");
        var statoPersistito = persistito.Stato;

        if (StatoUguale(statoPersistito, Paghe.Core.Domain.StatoCedolino.PreWorkflow))
            throw new InvalidOperationException(
                "Un cedolino dell'archivio pre-workflow è immutabile e non cancellabile.");

        if (entry.State == EntityState.Deleted)
        {
            if (!StatoUguale(statoPersistito, Paghe.Core.Domain.StatoCedolino.Bozza))
                throw new InvalidOperationException(
                    "Solo un cedolino in bozza può essere eliminato o sostituito.");
            return;
        }

        if (StatoUguale(statoPersistito, Paghe.Core.Domain.StatoCedolino.Chiusa))
            throw new InvalidOperationException("Un cedolino chiuso è immutabile.");

        if (StatoUguale(statoPersistito, Paghe.Core.Domain.StatoCedolino.Bozza))
        {
            if (!StatoUguale(statoCorrente, Paghe.Core.Domain.StatoCedolino.Bozza)
                && !StatoUguale(statoCorrente, Paghe.Core.Domain.StatoCedolino.Controllata))
                throw new InvalidOperationException(
                    "Una bozza può restare Bozza oppure passare soltanto a Controllata.");

            if (StatoUguale(statoCorrente, Paghe.Core.Domain.StatoCedolino.Bozza))
            {
                RichiediVersioneInvariata(entry, persistito.Versione);
                return;
            }

            RichiediIncrementoVersione(entry, persistito.Versione);
            RichiediMetadatiControllo(entry.Entity);
            RichiediSnapshotAtomico(context, entry.Entity);
            return;
        }

        if (StatoUguale(statoPersistito, Paghe.Core.Domain.StatoCedolino.Controllata))
        {
            if (!StatoUguale(statoCorrente, Paghe.Core.Domain.StatoCedolino.Validata))
                throw new InvalidOperationException(
                    "Un cedolino controllato può passare soltanto a Validata.");

            var ammessi = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(CedolinoEntity.Stato), nameof(CedolinoEntity.Versione),
                nameof(CedolinoEntity.ValidatoIl), nameof(CedolinoEntity.ValidatoDa),
                nameof(CedolinoEntity.DatiValidazioneProtetti),
            };
            if (entry.Properties.Any(p => p.IsModified && !ammessi.Contains(p.Metadata.Name)))
                throw new InvalidOperationException(
                    "Dopo il congelamento si può soltanto registrare la validazione; "
                    + "dati e importi sono immutabili.");
            RichiediIncrementoVersione(entry, persistito.Versione);
            RichiediMetadatiValidazione(entry.Entity);
            return;
        }

        if (StatoUguale(statoPersistito, Paghe.Core.Domain.StatoCedolino.Validata))
        {
            if (!StatoUguale(statoCorrente, Paghe.Core.Domain.StatoCedolino.Chiusa))
                throw new InvalidOperationException(
                    "Un cedolino validato può passare soltanto a Chiusa.");

            var ammessi = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(CedolinoEntity.Stato), nameof(CedolinoEntity.Versione),
                nameof(CedolinoEntity.ChiusoIl), nameof(CedolinoEntity.ChiusoDa),
            };
            if (entry.Properties.Any(p => p.IsModified && !ammessi.Contains(p.Metadata.Name)))
                throw new InvalidOperationException(
                    "Una versione validata può soltanto essere chiusa; dati e importi sono immutabili.");
            RichiediIncrementoVersione(entry, persistito.Versione);
            RichiediMetadatiChiusura(entry.Entity);
            RichiediDocumentoAtomico(context, entry.Entity);
            return;
        }

        throw new InvalidOperationException(
            "Lo stato persistito del cedolino non appartiene al workflow consentito.");
    }

    private static void RichiediVersioneInvariata(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CedolinoEntity> entry,
        int versionePersistita)
    {
        if (entry.Property(c => c.Versione).CurrentValue != versionePersistita)
            throw new InvalidOperationException(
                "La versione non può cambiare mentre il cedolino resta in Bozza.");
    }

    private static void RichiediIncrementoVersione(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CedolinoEntity> entry,
        int versionePersistita)
    {
        if (versionePersistita < 1
            || versionePersistita == int.MaxValue
            || entry.Property(c => c.Versione).CurrentValue != versionePersistita + 1)
            throw new InvalidOperationException(
                "Ogni transizione del cedolino deve incrementare la versione esattamente di uno.");
    }

    private static void RichiediMetadatiControllo(CedolinoEntity cedolino)
    {
        if (cedolino.ControllatoIl is null
            || cedolino.ControllatoDa is not { } attore
            || attore == Guid.Empty)
            throw new InvalidOperationException(
                "Il passaggio a Controllata richiede data e attore del controllo.");
    }

    private static void RichiediMetadatiValidazione(CedolinoEntity cedolino)
    {
        if (cedolino.ValidatoIl is null
            || cedolino.ValidatoDa is not { } attore
            || attore == Guid.Empty
            || string.IsNullOrWhiteSpace(cedolino.DatiValidazioneProtetti))
            throw new InvalidOperationException(
                "Il passaggio a Validata richiede data, attore e dati protetti della validazione.");
    }

    private static void RichiediMetadatiChiusura(CedolinoEntity cedolino)
    {
        if (cedolino.ChiusoIl is null
            || cedolino.ChiusoDa is not { } attore
            || attore == Guid.Empty)
            throw new InvalidOperationException(
                "Il passaggio a Chiusa richiede data e attore della chiusura.");
    }

    private static void RichiediSnapshotAtomico(DbContext? context, CedolinoEntity cedolino)
    {
        if (context is null)
            throw new InvalidOperationException(
                "Il passaggio a Controllata richiede il contesto della transazione.");

        var candidati = context.ChangeTracker.Entries<CedolinoSnapshotEntity>()
            .Where(e => e.State == EntityState.Added
                && e.Entity.TenantId == cedolino.TenantId
                && e.Entity.CedolinoId == cedolino.Id)
            .Take(2)
            .ToList();
        if (candidati.Count != 1
            || candidati[0].Entity.Id == Guid.Empty
            || candidati[0].Entity.CreatoDa != cedolino.ControllatoDa
            || candidati[0].Entity.CreatoIl != cedolino.ControllatoIl
            || candidati[0].Entity.VersioneSchema <= 0
            || string.IsNullOrWhiteSpace(candidati[0].Entity.DatiProtetti)
            || string.IsNullOrWhiteSpace(candidati[0].Entity.HashDatiSha256))
            throw new InvalidOperationException(
                "Il passaggio a Controllata richiede uno snapshot Added coerente nello stesso tenant e salvataggio.");
    }

    private static void RichiediDocumentoAtomico(DbContext? context, CedolinoEntity cedolino)
    {
        if (context is null)
            throw new InvalidOperationException(
                "Il passaggio a Chiusa richiede il contesto della transazione.");

        var candidati = context.ChangeTracker.Entries<CedolinoDocumentoEntity>()
            .Where(e => e.State == EntityState.Added
                && e.Entity.TenantId == cedolino.TenantId
                && e.Entity.CedolinoId == cedolino.Id)
            .Take(2)
            .ToList();
        if (candidati.Count != 1
            || candidati[0].Entity.Id == Guid.Empty
            || candidati[0].Entity.CreatoDa != cedolino.ChiusoDa
            || candidati[0].Entity.CreatoIl != cedolino.ChiusoIl
            || string.IsNullOrWhiteSpace(candidati[0].Entity.PdfProtetto)
            || string.IsNullOrWhiteSpace(candidati[0].Entity.HashPdfSha256)
            || string.IsNullOrWhiteSpace(candidati[0].Entity.HashFirmaSiglaTimbroSha256))
            throw new InvalidOperationException(
                "Il passaggio a Chiusa richiede un documento Added coerente nello stesso tenant e salvataggio.");
    }

    private static void VerificaEvidenzeCedolino(DbContext? context)
    {
        if (context is null) return;

        foreach (var snapshot in context.ChangeTracker.Entries<CedolinoSnapshotEntity>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            var riferimento = RiferimentoEvidenza(
                snapshot.Entity.TenantId, snapshot.Entity.CedolinoId, "snapshot");
            var persistito = context.Set<CedolinoEntity>()
                .AsNoTracking()
                .Where(c => c.TenantId == riferimento.TenantId
                    && c.Id == riferimento.CedolinoId)
                .Select(c => new StatoVersioneCedolino(c.Stato, c.Versione))
                .SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "Snapshot Added senza testata cedolino persistita nello stesso tenant.");
            VerificaSnapshotAggiunto(context, riferimento, persistito);
        }

        foreach (var documento in context.ChangeTracker.Entries<CedolinoDocumentoEntity>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            var riferimento = RiferimentoEvidenza(
                documento.Entity.TenantId, documento.Entity.CedolinoId, "documento");
            var persistito = context.Set<CedolinoEntity>()
                .AsNoTracking()
                .Where(c => c.TenantId == riferimento.TenantId
                    && c.Id == riferimento.CedolinoId)
                .Select(c => new StatoVersioneCedolino(c.Stato, c.Versione))
                .SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "Documento Added senza testata cedolino persistita nello stesso tenant.");
            VerificaDocumentoAggiunto(context, riferimento, persistito);
        }
    }

    private static async Task VerificaEvidenzeCedolinoAsync(
        DbContext? context,
        CancellationToken cancellationToken)
    {
        if (context is null) return;

        foreach (var snapshot in context.ChangeTracker.Entries<CedolinoSnapshotEntity>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            var riferimento = RiferimentoEvidenza(
                snapshot.Entity.TenantId, snapshot.Entity.CedolinoId, "snapshot");
            var persistito = await context.Set<CedolinoEntity>()
                .AsNoTracking()
                .Where(c => c.TenantId == riferimento.TenantId
                    && c.Id == riferimento.CedolinoId)
                .Select(c => new StatoVersioneCedolino(c.Stato, c.Versione))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Snapshot Added senza testata cedolino persistita nello stesso tenant.");
            VerificaSnapshotAggiunto(context, riferimento, persistito);
        }

        foreach (var documento in context.ChangeTracker.Entries<CedolinoDocumentoEntity>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            var riferimento = RiferimentoEvidenza(
                documento.Entity.TenantId, documento.Entity.CedolinoId, "documento");
            var persistito = await context.Set<CedolinoEntity>()
                .AsNoTracking()
                .Where(c => c.TenantId == riferimento.TenantId
                    && c.Id == riferimento.CedolinoId)
                .Select(c => new StatoVersioneCedolino(c.Stato, c.Versione))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Documento Added senza testata cedolino persistita nello stesso tenant.");
            VerificaDocumentoAggiunto(context, riferimento, persistito);
        }
    }

    private static RiferimentoCedolino RiferimentoEvidenza(
        Guid tenantId,
        Guid cedolinoId,
        string nome)
    {
        if (tenantId == Guid.Empty || cedolinoId == Guid.Empty)
            throw new InvalidOperationException(
                $"Il {nome} Added richiede TenantId e CedolinoId validi.");
        return new RiferimentoCedolino(tenantId, cedolinoId);
    }

    private static void VerificaSnapshotAggiunto(
        DbContext context,
        RiferimentoCedolino riferimento,
        StatoVersioneCedolino persistito)
    {
        var parent = RichiediUnicoParentInTransizione(context, riferimento, "snapshot");
        if (!StatoUguale(persistito.Stato, Paghe.Core.Domain.StatoCedolino.Bozza)
            || !StatoUguale(parent.Entity.Stato, Paghe.Core.Domain.StatoCedolino.Controllata))
            throw new InvalidOperationException(
                "Uno snapshot Added deve accompagnare esattamente la transizione Bozza→Controllata.");

        RichiediIncrementoVersione(parent, persistito.Versione);
        RichiediMetadatiControllo(parent.Entity);
        RichiediSnapshotAtomico(context, parent.Entity);
    }

    private static void VerificaDocumentoAggiunto(
        DbContext context,
        RiferimentoCedolino riferimento,
        StatoVersioneCedolino persistito)
    {
        var parent = RichiediUnicoParentInTransizione(context, riferimento, "documento");
        if (!StatoUguale(persistito.Stato, Paghe.Core.Domain.StatoCedolino.Validata)
            || !StatoUguale(parent.Entity.Stato, Paghe.Core.Domain.StatoCedolino.Chiusa))
            throw new InvalidOperationException(
                "Un documento Added deve accompagnare esattamente la transizione Validata→Chiusa.");

        RichiediIncrementoVersione(parent, persistito.Versione);
        RichiediMetadatiChiusura(parent.Entity);
        RichiediDocumentoAtomico(context, parent.Entity);
    }

    private static Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CedolinoEntity>
        RichiediUnicoParentInTransizione(
            DbContext context,
            RiferimentoCedolino riferimento,
            string evidenza)
    {
        var parent = context.ChangeTracker.Entries<CedolinoEntity>()
            .Where(e => e.State != EntityState.Detached
                && e.Entity.TenantId == riferimento.TenantId
                && e.Entity.Id == riferimento.CedolinoId)
            .Take(2)
            .ToList();
        if (parent.Count != 1 || parent[0].State != EntityState.Modified)
            throw new InvalidOperationException(
                $"Il {evidenza} Added richiede la transizione della testata nello stesso SaveChanges.");
        return parent[0];
    }

    private static bool StatoUguale(string valore, Paghe.Core.Domain.StatoCedolino atteso) =>
        string.Equals(valore, atteso.ToString(), StringComparison.Ordinal);

    private static void VerificaVociCedolino(DbContext? context)
    {
        if (context is null) return;

        foreach (var riferimento in RiferimentiVociModificate(context))
        {
            var parentTracciato = TrovaParentTracciato(context, riferimento);
            if (parentTracciato?.State == EntityState.Added)
            {
                RichiediParentBozza(parentTracciato.Entity.Stato);
                continue;
            }

            var statoPersistito = context.Set<CedolinoEntity>()
                .AsNoTracking()
                .Where(c => c.TenantId == riferimento.TenantId
                    && c.Id == riferimento.CedolinoId)
                .Select(c => c.Stato)
                .SingleOrDefault();
            RichiediParentEsistenteInBozza(statoPersistito);

            if (parentTracciato is not null)
                RichiediParentBozza(parentTracciato.Entity.Stato);
        }
    }

    private static async Task VerificaVociCedolinoAsync(
        DbContext? context,
        CancellationToken cancellationToken)
    {
        if (context is null) return;

        foreach (var riferimento in RiferimentiVociModificate(context))
        {
            var parentTracciato = TrovaParentTracciato(context, riferimento);
            if (parentTracciato?.State == EntityState.Added)
            {
                RichiediParentBozza(parentTracciato.Entity.Stato);
                continue;
            }

            var statoPersistito = await context.Set<CedolinoEntity>()
                .AsNoTracking()
                .Where(c => c.TenantId == riferimento.TenantId
                    && c.Id == riferimento.CedolinoId)
                .Select(c => c.Stato)
                .SingleOrDefaultAsync(cancellationToken);
            RichiediParentEsistenteInBozza(statoPersistito);

            if (parentTracciato is not null)
                RichiediParentBozza(parentTracciato.Entity.Stato);
        }
    }

    private static IReadOnlyCollection<RiferimentoCedolino> RiferimentiVociModificate(
        DbContext context)
    {
        var riferimenti = new HashSet<RiferimentoCedolino>();
        foreach (var entry in context.ChangeTracker.Entries<CedolinoVoceEntity>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.State == EntityState.Added)
            {
                AggiungiRiferimento(
                    riferimenti,
                    entry.Property(v => v.TenantId).CurrentValue,
                    entry.Property(v => v.CedolinoId).CurrentValue);
                continue;
            }

            AggiungiRiferimento(
                riferimenti,
                entry.Property(v => v.TenantId).OriginalValue,
                entry.Property(v => v.CedolinoId).OriginalValue);

            if (entry.State == EntityState.Modified)
                AggiungiRiferimento(
                    riferimenti,
                    entry.Property(v => v.TenantId).CurrentValue,
                    entry.Property(v => v.CedolinoId).CurrentValue);
        }

        return riferimenti;
    }

    private static void AggiungiRiferimento(
        ISet<RiferimentoCedolino> riferimenti,
        Guid tenantId,
        Guid cedolinoId)
    {
        if (tenantId == Guid.Empty || cedolinoId == Guid.Empty)
            throw new InvalidOperationException(
                "Una voce cedolino richiede TenantId e CedolinoId validi.");
        riferimenti.Add(new RiferimentoCedolino(tenantId, cedolinoId));
    }

    private static Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CedolinoEntity>?
        TrovaParentTracciato(DbContext context, RiferimentoCedolino riferimento) =>
        context.ChangeTracker.Entries<CedolinoEntity>().SingleOrDefault(e =>
            e.State != EntityState.Detached
            && e.Entity.TenantId == riferimento.TenantId
            && e.Entity.Id == riferimento.CedolinoId);

    private static void RichiediParentEsistenteInBozza(string? stato)
    {
        if (stato is null)
            throw new InvalidOperationException(
                "Voce cedolino senza testata dello stesso tenant: operazione bloccata.");
        RichiediParentBozza(stato);
    }

    private static void RichiediParentBozza(string stato)
    {
        if (!StatoUguale(stato, Paghe.Core.Domain.StatoCedolino.Bozza))
            throw new InvalidOperationException(
                "Le voci possono essere create, modificate o eliminate soltanto mentre il cedolino è in Bozza.");
    }

    private readonly record struct RiferimentoCedolino(Guid TenantId, Guid CedolinoId);
    private sealed record StatoVersioneCedolino(string Stato, int Versione);
}
