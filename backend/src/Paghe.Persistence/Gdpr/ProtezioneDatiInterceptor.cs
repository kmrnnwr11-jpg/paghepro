using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Gdpr;

/// <summary>
/// Cifra prima del salvataggio e decifra dopo la materializzazione i payload che richiedono AAD
/// tenant/record-bound. I converter EF restano adatti solo ai segreti senza identità di record.
/// </summary>
internal sealed class ProtezioneDatiInterceptor : SaveChangesInterceptor, IMaterializationInterceptor
{
    public object InitializedInstance(
        MaterializationInterceptionData materializationData,
        object entity)
    {
        var protettore = OttieniProtettore(materializationData.Context);
        switch (entity)
        {
            case AziendaEntity azienda:
                azienda.CaricaIbanDecifrato(DecifraIban(protettore,
                    azienda.IbanProtetto,
                    ContestoDatiProtetti.IbanAzienda(azienda.TenantId, azienda.Id)));
                azienda.CaricaFirmaSiglaTimbroDecifrata(DecifraBytes(
                    protettore,
                    azienda.FirmaSiglaTimbroProtetta,
                    ContestoDatiProtetti.FirmaSiglaTimbroAzienda(azienda.TenantId, azienda.Id)));
                break;
            case DipendenteEntity dipendente:
                dipendente.CaricaIbanDecifrato(DecifraIban(protettore,
                    dipendente.IbanProtetto,
                    ContestoDatiProtetti.IbanDipendente(dipendente.TenantId, dipendente.Id)));
                break;
            case AvvisoPagamentoEntity avviso:
                avviso.CaricaIbanDecifrato(DecifraIban(protettore,
                    avviso.IbanProtetto,
                    ContestoDatiProtetti.IbanAvviso(avviso.TenantId, avviso.Id)));
                break;
            case CartellinoEntity cartellino:
                if (string.IsNullOrWhiteSpace(cartellino.DatiSaluteProtetti))
                    throw new InvalidOperationException(
                        "Cartellino privo del payload salute protetto: eseguire la migrazione GDPR.");
                var payloadSalute = protettore.SvelaConContesto(
                    cartellino.DatiSaluteProtetti,
                    ContestoDatiProtetti.SaluteCartellino(cartellino.TenantId, cartellino.Id))!;
                var salute = PayloadDatiProtetti.DeserializzaSalute(payloadSalute);
                cartellino.CaricaDatiSalute(
                    salute.Malattia,
                    salute.NumeroEvento,
                    salute.Infortunio);
                // Il calendario può mancare: i cartellini compilati a totali non ne hanno uno.
                cartellino.CaricaGiornate(PayloadDatiProtetti.DeserializzaGiornate(
                    string.IsNullOrWhiteSpace(cartellino.GiornateProtette)
                        ? null
                        : protettore.SvelaConContesto(
                            cartellino.GiornateProtette,
                            ContestoDatiProtetti.GiornateCartellino(cartellino.TenantId, cartellino.Id)),
                    cartellino.Anno, cartellino.Mese));
                break;
            case CedolinoVoceEntity voce:
                if (string.IsNullOrWhiteSpace(voce.DatiVoceProtetti))
                    throw new InvalidOperationException(
                        "Voce cedolino priva del payload protetto: eseguire la migrazione GDPR.");
                var payloadVoce = protettore.SvelaConContesto(
                    voce.DatiVoceProtetti,
                    ContestoDatiProtetti.VoceCedolino(voce.TenantId, voce.Id))!;
                var dati = PayloadDatiProtetti.DeserializzaVoce(payloadVoce);
                voce.CaricaDatiVoce(
                    dati.Codice,
                    dati.Descrizione,
                    dati.Tipo,
                    dati.Quantita,
                    dati.Base,
                    dati.Importo);
                break;
        }

        return entity;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Proteggi(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Proteggi(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Conferma(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Conferma(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Proteggi(DbContext? context)
    {
        if (context is null) return;
        var protettore = OttieniProtettore(context);
        context.ChangeTracker.DetectChanges();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Detached or EntityState.Deleted) continue;

            switch (entry.Entity)
            {
                case AziendaEntity a when entry.State == EntityState.Added
                        || a.IbanModificato || a.FirmaSiglaTimbroModificata:
                    ValidaIdentita(a.TenantId, a.Id, nameof(AziendaEntity));
                    if (entry.State == EntityState.Added || a.IbanModificato)
                    {
                        a.IbanProtetto = CifraIban(protettore,
                            a.Iban,
                            ContestoDatiProtetti.IbanAzienda(a.TenantId, a.Id));
                        entry.Property(nameof(AziendaEntity.IbanProtetto)).IsModified =
                            entry.State != EntityState.Added;
                    }
                    if (entry.State == EntityState.Added || a.FirmaSiglaTimbroModificata)
                    {
                        a.FirmaSiglaTimbroProtetta = CifraBytes(
                            protettore,
                            a.FirmaSiglaTimbro,
                            ContestoDatiProtetti.FirmaSiglaTimbroAzienda(a.TenantId, a.Id));
                        entry.Property(nameof(AziendaEntity.FirmaSiglaTimbroProtetta)).IsModified =
                            entry.State != EntityState.Added;
                    }
                    break;
                case DipendenteEntity d when entry.State == EntityState.Added || d.IbanModificato:
                    ValidaIdentita(d.TenantId, d.Id, nameof(DipendenteEntity));
                    d.IbanProtetto = CifraIban(protettore,
                        d.Iban,
                        ContestoDatiProtetti.IbanDipendente(d.TenantId, d.Id));
                    entry.Property(nameof(DipendenteEntity.IbanProtetto)).IsModified =
                        entry.State != EntityState.Added;
                    break;
                case AvvisoPagamentoEntity a when entry.State == EntityState.Added || a.IbanModificato:
                    ValidaIdentita(a.TenantId, a.Id, nameof(AvvisoPagamentoEntity));
                    a.IbanProtetto = CifraIban(protettore,
                        a.Iban,
                        ContestoDatiProtetti.IbanAvviso(a.TenantId, a.Id));
                    entry.Property(nameof(AvvisoPagamentoEntity.IbanProtetto)).IsModified =
                        entry.State != EntityState.Added;
                    break;
                case CartellinoEntity c when entry.State == EntityState.Added
                        || c.DatiSaluteModificati || c.GiornateModificate:
                    ValidaIdentita(c.TenantId, c.Id, nameof(CartellinoEntity));
                    c.DatiSaluteProtetti = protettore.ProteggiConContesto(
                        PayloadDatiProtetti.SerializzaSalute(
                            c.GiorniMalattia,
                            c.NumeroEventoMalattiaAnno,
                            c.GiorniInfortunio),
                        ContestoDatiProtetti.SaluteCartellino(c.TenantId, c.Id))!;
                    entry.Property(nameof(CartellinoEntity.DatiSaluteProtetti)).IsModified =
                        entry.State != EntityState.Added;
                    c.GiornateProtette = c.Giornate.Count == 0
                        ? ""
                        : protettore.ProteggiConContesto(
                            PayloadDatiProtetti.SerializzaGiornate(c.Giornate),
                            ContestoDatiProtetti.GiornateCartellino(c.TenantId, c.Id))!;
                    entry.Property(nameof(CartellinoEntity.GiornateProtette)).IsModified =
                        entry.State != EntityState.Added;
                    break;
                case CedolinoVoceEntity v when entry.State == EntityState.Added || v.DatiVoceModificati:
                    ValidaIdentita(v.TenantId, v.Id, nameof(CedolinoVoceEntity));
                    v.DatiVoceProtetti = protettore.ProteggiConContesto(
                        PayloadDatiProtetti.SerializzaVoce(v),
                        ContestoDatiProtetti.VoceCedolino(v.TenantId, v.Id))!;
                    entry.Property(nameof(CedolinoVoceEntity.DatiVoceProtetti)).IsModified =
                        entry.State != EntityState.Added;
                    break;
            }
        }
    }

    private static string? CifraIban(ProtettoreDati protettore, string? iban, string contesto) =>
        string.IsNullOrWhiteSpace(iban)
            ? null
            : protettore.ProteggiConContesto(iban, contesto);

    private static string? DecifraIban(
        ProtettoreDati protettore,
        string? ibanProtetto,
        string contesto) =>
        string.IsNullOrWhiteSpace(ibanProtetto)
            ? null
            : protettore.SvelaConContesto(ibanProtetto, contesto);

    private static string? CifraBytes(ProtettoreDati protettore, byte[]? dati, string contesto) =>
        dati is null || dati.Length == 0
            ? null
            : protettore.ProteggiConContesto(Convert.ToBase64String(dati), contesto);

    private static byte[]? DecifraBytes(ProtettoreDati protettore, string? datiProtetti, string contesto) =>
        string.IsNullOrWhiteSpace(datiProtetti)
            ? null
            : Convert.FromBase64String(protettore.SvelaConContesto(datiProtetti, contesto)!);

    private static ProtettoreDati OttieniProtettore(DbContext? context) =>
        context is PagheDbContext paghe
            ? paghe.Protettore
            : throw new InvalidOperationException(
                "Protezione dati disponibile soltanto sul PagheDbContext.");

    private static void Conferma(DbContext? context)
    {
        if (context is null) return;
        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case AziendaEntity a:
                    a.ConfermaProtezioneIban();
                    a.ConfermaProtezioneFirmaSiglaTimbro();
                    break;
                case DipendenteEntity d: d.ConfermaProtezioneIban(); break;
                case AvvisoPagamentoEntity a: a.ConfermaProtezioneIban(); break;
                case CartellinoEntity c:
                    c.ConfermaProtezioneDatiSalute();
                    c.ConfermaProtezioneGiornate();
                    break;
                case CedolinoVoceEntity v: v.ConfermaProtezioneDatiVoce(); break;
            }
        }
    }

    private static void ValidaIdentita(Guid tenantId, Guid id, string entita)
    {
        if (tenantId == Guid.Empty || id == Guid.Empty)
            throw new InvalidOperationException(
                $"{entita} richiede TenantId e Id prima della cifratura tenant-bound.");
    }
}

/// <summary>AAD stabile; cambiare versione richiede una migrazione esplicita.</summary>
internal static class ContestoDatiProtetti
{
    private const string Versione = "paghe-gdpr:v1";

    public static string IbanAzienda(Guid tenantId, Guid id) =>
        Crea(tenantId, "Aziende", id, "IbanProtetto");

    public static string FirmaSiglaTimbroAzienda(Guid tenantId, Guid id) =>
        Crea(tenantId, "Aziende", id, "FirmaSiglaTimbroProtetta");

    public static string IbanDipendente(Guid tenantId, Guid id) =>
        Crea(tenantId, "Dipendenti", id, "IbanProtetto");

    public static string IbanAvviso(Guid tenantId, Guid id) =>
        Crea(tenantId, "AvvisiPagamento", id, "IbanProtetto");

    public static string SaluteCartellino(Guid tenantId, Guid id) =>
        Crea(tenantId, "Cartellini", id, "DatiSaluteProtetti");

    public static string GiornateCartellino(Guid tenantId, Guid id) =>
        Crea(tenantId, "Cartellini", id, "GiornateProtette");

    public static string VoceCedolino(Guid tenantId, Guid id) =>
        Crea(tenantId, "CedolinoVoceEntity", id, "DatiVoceProtetti");

    public static string ParametriCedolino(Guid tenantId, Guid id) =>
        Crea(tenantId, "Cedolini", id, "ParametriApplicatiProtetti");

    public static string ValidazioneCedolino(Guid tenantId, Guid id) =>
        Crea(tenantId, "Cedolini", id, "DatiValidazioneProtetti");

    public static string PdfCedolinoAgricolo(Guid tenantId, Guid id) =>
        Crea(tenantId, "CedoliniAgricoli", id, "PdfProtetto");

    public static string ArgomentiProposta(Guid tenantId, Guid id) =>
        Crea(tenantId, "AiProposte", id, "ArgomentiProtetti");

    public static string TitoloProposta(Guid tenantId, Guid id) =>
        Crea(tenantId, "AiProposte", id, "Titolo");

    public static string DiffProposta(Guid tenantId, Guid id) =>
        Crea(tenantId, "AiProposte", id, "DiffJson");

    public static string EsitoProposta(Guid tenantId, Guid id) =>
        Crea(tenantId, "AiProposte", id, "EsitoProtetto");

    public static string UndoProposta(Guid tenantId, Guid id) =>
        Crea(tenantId, "AiProposte", id, "UndoProtetto");

    public static string EsitoUndoProposta(Guid tenantId, Guid id) =>
        Crea(tenantId, "AiProposte", id, "EsitoUndoProtetto");

    public static string DatiSnapshotCedolino(Guid tenantId, Guid id, Guid cedolinoId) =>
        $"{Crea(tenantId, "CedoliniSnapshot", id, "DatiProtetti")}|cedolino:{cedolinoId:D}";

    public static string PdfSnapshotCedolino(Guid tenantId, Guid id, Guid cedolinoId) =>
        $"{Crea(tenantId, "CedoliniDocumenti", id, "PdfProtetto")}|cedolino:{cedolinoId:D}";

    private static string Crea(Guid tenantId, string tabella, Guid id, string campo) =>
        $"{Versione}|{tenantId:D}|{tabella}|{id:D}|{campo}";
}
