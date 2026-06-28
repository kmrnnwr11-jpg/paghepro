using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Esito dell'import UNILAV: azienda (datore) e rapporto creati, più le avvertenze.</summary>
public sealed record EsitoImportUnilav(
    AziendaEntity Azienda,
    bool AziendaCreata,
    RapportoLavoroEntity Rapporto,
    bool LavoratoreEsistente,
    IReadOnlyList<string> Avvertenze);

/// <summary>
/// Da una Comunicazione Obbligatoria UNILAV (testo estratto dal PDF) registra <b>tutto il necessario</b>
/// in modo isolato per tenant: trova o crea il <b>datore di lavoro</b> (azienda), poi crea il
/// <b>lavoratore</b> e il <b>rapporto</b>. Se il lavoratore (stesso codice fiscale) esiste già, gli
/// aggiunge un nuovo rapporto invece di duplicarlo (indice unico TenantId+CodiceFiscale).
/// </summary>
public sealed class ImportUnilavService
{
    private readonly PagheDbContext _db;

    public ImportUnilavService(PagheDbContext db) => _db = db;

    public async Task<EsitoImportUnilav> ImportaDaTestoAsync(
        Guid tenantId, string testoUnilav,
        string? ccnlDefault = null, string? livelloDefault = null,
        Guid? aziendaIdScelto = null, CancellationToken ct = default)
    {
        var imp = ImportazioneUnilavPdf.Importa(testoUnilav);
        var avvertenze = new List<string>(imp.Avvertenze);

        string? cfLav = imp.Lavoratore.CodiceFiscale?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(cfLav))
            throw new ArgumentException(
                "Impossibile registrare il lavoratore: codice fiscale non riconosciuto nel PDF UNILAV.");

        // 1) Azienda: se l'utente ne ha scelta una, ci si attacca; altrimenti si trova/crea il datore del PDF.
        AziendaEntity azienda;
        bool aziendaCreata;
        if (aziendaIdScelto is { } scelta)
        {
            azienda = await _db.Aziende.FirstOrDefaultAsync(a => a.Id == scelta && a.TenantId == tenantId, ct)
                ?? throw new ArgumentException("Azienda scelta non trovata per il tenant indicato.");
            aziendaCreata = false;
        }
        else
        {
            (azienda, aziendaCreata) = await TrovaOCreaDatoreAsync(tenantId, imp.Datore, avvertenze, ct);
        }

        // 2) Rapporto di lavoro (inquadramento) dai dati UNILAV.
        var rapporto = new RapportoLavoroEntity
        {
            CodiceCcnl = Primo(imp.Rapporto.Ccnl, ccnlDefault) ?? "",
            Livello = Primo(imp.Rapporto.Livello, livelloDefault) ?? "",
            Qualifica = ImportazioneUnilav.MappaQualifica(imp.Rapporto.Qualifica).ToString(),
            PartTime = ImportazioneUnilav.EParTime(imp.Rapporto.TipoOrario),
            PercentualePartTime = imp.Rapporto.PercentualePartTime ?? 100m,
            DataAssunzione = imp.Rapporto.DataInizio ?? DateOnly.FromDateTime(DateTime.UtcNow),
        };
        avvertenze.Add("Regione e comune (catastale) del rapporto non sono nell'UNILAV: indicarli per le addizionali.");

        // 3) Lavoratore: riusa se esiste già (indice unico TenantId+CodiceFiscale), altrimenti crealo.
        var esistente = await _db.Dipendenti
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.CodiceFiscale == cfLav, ct);

        if (esistente is not null)
        {
            avvertenze.Add($"Lavoratore con codice fiscale {cfLav} già presente: aggiunto un nuovo rapporto.");
            rapporto.Id = Guid.NewGuid();
            rapporto.TenantId = tenantId;
            rapporto.AziendaId = azienda.Id;
            rapporto.DipendenteId = esistente.Id;
            _db.Rapporti.Add(rapporto);
            await _db.SaveChangesAsync(ct);
            rapporto.Dipendente = esistente;
            return new EsitoImportUnilav(azienda, aziendaCreata, rapporto, true, avvertenze);
        }

        var dipendente = new DipendenteEntity
        {
            CodiceFiscale = cfLav,
            Nome = imp.Lavoratore.Nome ?? "",
            Cognome = imp.Lavoratore.Cognome ?? "",
        };
        var creato = await new AnagraficheLavoroService(_db)
            .CreaDipendenteERapportoAsync(tenantId, azienda.Id, dipendente, rapporto, ct);
        creato.Dipendente = dipendente;
        return new EsitoImportUnilav(azienda, aziendaCreata, creato, false, avvertenze);
    }

    private async Task<(AziendaEntity azienda, bool creata)> TrovaOCreaDatoreAsync(
        Guid tenantId, DatoreLavoroUnilav datore, List<string> avvertenze, CancellationToken ct)
    {
        string? cf = datore.CodiceFiscale?.Trim();

        if (!string.IsNullOrWhiteSpace(cf))
        {
            var trovata = await _db.Aziende.FirstOrDefaultAsync(
                a => a.TenantId == tenantId && (a.CodiceFiscale == cf || a.PartitaIva == cf), ct);
            if (trovata is not null)
                return (trovata, false);
        }

        if (string.IsNullOrWhiteSpace(datore.Denominazione) && string.IsNullOrWhiteSpace(cf))
            throw new ArgumentException(
                "Impossibile registrare il datore di lavoro: né denominazione né codice fiscale presenti nel PDF.");

        var nuova = new AziendaEntity
        {
            RagioneSociale = string.IsNullOrWhiteSpace(datore.Denominazione)
                ? $"Datore {cf}"
                : datore.Denominazione!,
        };
        if (!string.IsNullOrWhiteSpace(cf))
        {
            if (Regex.IsMatch(cf, "^[0-9]{11}$")) nuova.PartitaIva = cf;
            else nuova.CodiceFiscale = cf;
        }

        avvertenze.Add("Datore di lavoro creato dai dati UNILAV: completare indirizzo, CAP, comune e regime fiscale.");
        var creata = await new AziendeService(_db).CreaAsync(tenantId, nuova, ct);
        return (creata, true);
    }

    private static string? Primo(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : (!string.IsNullOrWhiteSpace(b) ? b : null);
}
