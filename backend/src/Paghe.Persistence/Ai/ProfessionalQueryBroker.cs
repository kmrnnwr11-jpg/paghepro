using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;

namespace Paghe.Persistence.Ai;

// Righe minimizzate per risorsa: il broker restituisce SOLO i campi che servono ai
// controlli, mai valori sensibili (dell'IBAN esce soltanto il flag di presenza).

public sealed record RigaScadenza(string Titolo, string Urgenza, int GiorniAllaScadenza);

public sealed record RigaAzienda(
    string RagioneSociale,
    bool MatricolaInpsPresente,
    bool PatInailPresente,
    bool CodiceCatastalePresente,
    bool IbanPresente,
    bool RecapitoFiscalePresente);

public sealed record RigaDipendente(string Nominativo, string? CodiceCcnl, string? Livello);

public sealed record RigaCartellino(string Nominativo, bool Elaborato);

/// <summary>
/// Stato operativo minimo del mese: nessun importo e nessun dato sanitario. Il limite di dieci
/// è il checkpoint visuale del lotto, non un limite globale del prodotto.
/// </summary>
public sealed record RigaCedolinoMese(
    Guid RapportoId,
    string Nominativo,
    string Stato,
    string Motivo,
    Guid? CedolinoId,
    string? StatoCedolino);

public sealed record EsitoQueryBroker(
    bool Consentito,
    string? Motivo,
    string Risorsa,
    IReadOnlyList<object> Righe,
    int Totale,
    int LettureRimanenti);

/// <summary>
/// ProfessionalQueryBroker del piano §7.1: l'unico punto da cui una esecuzione AI ottiene
/// dati. Autorizza la lease sul Governor e poi costruisce SOLO proiezioni minimizzate,
/// filtrate dallo scope del MANIFEST del mandato (tenant, azienda, periodo) — mai da
/// parametri del client. La quota viene consumata per record effettivamente restituito, con
/// checkpoint massimo di 200 righe (10 per il controllo mensile cedolini).
/// </summary>
public sealed class ProfessionalQueryBroker(
    PagheDbContext db,
    AiGrantService governor,
    ScadenzarioService scadenzario,
    TimeProvider orologio)
{
    private const int MassimoRighe = 200;

    public async Task<EsitoQueryBroker> LeggiAsync(
        Guid tenantId, Guid utenteId, string? lease, string risorsa, CancellationToken ct = default)
    {
        var nome = (risorsa ?? "").Trim();
        var (verifica, manifest) = await governor.VerificaAccessoConManifestAsync(
            tenantId, utenteId, lease, nome, "read", ct);
        if (!verifica.Consentito || manifest is null)
            return new(false, verifica.Motivo, nome, [], 0, verifica.LettureRimanenti);

        var proiezione = nome.ToLowerInvariant() switch
        {
            "scadenzario" => await ScadenzeAsync(tenantId, manifest, ct),
            "aziende" => await AziendaAsync(tenantId, manifest, ct),
            "dipendenti" => await DipendentiAsync(tenantId, manifest, ct),
            "cartellini" => await CartelliniAsync(tenantId, manifest, ct),
            "cedolini" => await CedoliniMeseAsync(tenantId, manifest, ct),
            _ => new Proiezione(nome, [], 0, $"La risorsa '{nome}' non ha ancora una proiezione nel broker."),
        };

        if (proiezione.Errore is not null)
            return new(false, proiezione.Errore, proiezione.Risorsa, [], 0, verifica.LettureRimanenti);

        var quantita = proiezione.Righe.Count;
        var (accesso, _) = await governor.AutorizzaAccessoConManifestAsync(
            tenantId, utenteId, lease, nome, "read", quantita, ct);
        return accesso.Consentito
            ? new(true, null, proiezione.Risorsa, proiezione.Righe, proiezione.Totale, accesso.LettureRimanenti)
            : new(false, accesso.Motivo, proiezione.Risorsa, [], 0, accesso.LettureRimanenti);
    }

    private async Task<Proiezione> ScadenzeAsync(
        Guid tenantId, ManifestGrant manifest, CancellationToken ct)
    {
        var lista = await scadenzario.ElencoAsync(tenantId, rigenera: false);
        // Il mandato aziendale vede i promemoria dell'azienda e quelli generici dello studio.
        var pertinenti = manifest.AziendaId is { } aziendaId
            ? lista.Where(p => p.AziendaId == aziendaId || p.AziendaId == null)
            : lista;

        var oggi = DateOnly.FromDateTime(orologio.GetUtcNow().UtcDateTime);
        var righe = pertinenti.Select(p =>
        {
            var giorni = p.DataScadenza.DayNumber - oggi.DayNumber;
            var urgenza =
                p.Stato is "Completato" or "Annullato" ? "chiuso"
                : giorni < 0 ? "scaduto"
                : giorni == 0 ? "oggi"
                : giorni <= p.GiorniPreavviso ? "in_scadenza"
                : "futuro";
            return new RigaScadenza(p.Titolo, urgenza, giorni);
        }).ToList();

        return Pagina(righe, "Scadenzario");
    }

    private async Task<Proiezione> AziendaAsync(
        Guid tenantId, ManifestGrant manifest, CancellationToken ct)
    {
        if (manifest.AziendaId is not { } aziendaId)
            return new("Aziende", [], 0, "Il mandato non indica l'azienda: serve scope S1/S2.");

        var azienda = await db.Aziende.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct);
        if (azienda is null)
            return new("Aziende", [], 0, "Azienda del mandato non trovata.");

        var righe = new List<object>
        {
            new RigaAzienda(
                azienda.RagioneSociale,
                MatricolaInpsPresente: !string.IsNullOrWhiteSpace(azienda.MatricolaInps),
                PatInailPresente: !string.IsNullOrWhiteSpace(azienda.PatInail),
                CodiceCatastalePresente: !string.IsNullOrWhiteSpace(azienda.CodiceCatastale),
                IbanPresente: !string.IsNullOrWhiteSpace(azienda.IbanProtetto),
                RecapitoFiscalePresente: !string.IsNullOrWhiteSpace(azienda.Pec)
                    || !string.IsNullOrWhiteSpace(azienda.CodiceDestinatarioSdi)),
        };
        return new("Aziende", righe, 1, null);
    }

    private async Task<Proiezione> DipendentiAsync(
        Guid tenantId, ManifestGrant manifest, CancellationToken ct)
    {
        if (manifest.AziendaId is not { } aziendaId)
            return new("Dipendenti", [], 0, "Il mandato non indica l'azienda: serve scope S1/S2.");

        // Predicate e proiezione restano nel SQL: davanti a un archivio legacy corrotto il record
        // dell'altro tenant non viene materializzato né passato agli interceptor di decifratura.
        var query = db.Rapporti.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId
                && r.Dipendente != null && r.Dipendente.TenantId == tenantId);
        var totale = await query.CountAsync(ct);
        var righe = await query
            .OrderBy(r => r.Dipendente!.Cognome).ThenBy(r => r.Dipendente!.Nome)
            .Select(r => new RigaDipendente(
                (r.Dipendente!.Cognome + " " + r.Dipendente.Nome).Trim(),
                r.CodiceCcnl,
                r.Livello))
            .Take(MassimoRighe)
            .ToListAsync(ct);
        return new("Dipendenti", righe.Cast<object>().ToList(), totale, null);
    }

    private async Task<Proiezione> CartelliniAsync(
        Guid tenantId, ManifestGrant manifest, CancellationToken ct)
    {
        if (manifest.AziendaId is not { } aziendaId || manifest.Periodo is not { } periodo)
            return new("Cartellini", [], 0, "Serve un mandato S2 con azienda e periodo per leggere i cartellini.");
        if (!ProvaPeriodo(periodo, out var anno, out var mese, out var inizio, out var fine))
            return new("Cartellini", [], 0, "Periodo del mandato non valido.");

        var rapporti = await RapportiDelMeseAsync(tenantId, aziendaId, inizio, fine, MassimoRighe, ct);
        var rapportoIds = rapporti.Select(r => r.Id).ToArray();
        var compilati = (await db.Cartellini.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Anno == anno && c.Mese == mese
                && rapportoIds.Contains(c.RapportoLavoroId))
            .Select(c => c.RapportoLavoroId)
            .ToListAsync(ct)).ToHashSet();
        var righe = rapporti.Select(r => new RigaCartellino(r.Nominativo, compilati.Contains(r.Id))).ToList();
        return Pagina(righe, "Cartellini");
    }

    private async Task<Proiezione> CedoliniMeseAsync(
        Guid tenantId, ManifestGrant manifest, CancellationToken ct)
    {
        const int dimensioneLotto = 10;
        if (manifest.AziendaId is not { } aziendaId || manifest.Periodo is not { } periodo)
            return new("Cedolini", [], 0, "Serve un mandato S2 con azienda e periodo per controllare i cedolini.");
        if (!ProvaPeriodo(periodo, out var anno, out var mese, out var inizio, out var fine))
            return new("Cedolini", [], 0, "Periodo del mandato non valido.");

        var azienda = await db.Aziende.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Id == aziendaId)
            .Select(a => new
            {
                Inps = a.MatricolaInps != null && a.MatricolaInps != ""
                    && a.CodiceSedeInps != null && a.CodiceSedeInps != "",
                Inail = a.PatInail != null && a.PatInail != "",
                Ccnl = a.CcnlDefault != null && a.CcnlDefault != "",
            })
            .FirstOrDefaultAsync(ct);
        if (azienda is null)
            return new("Cedolini", [], 0, "Azienda del mandato non trovata.");

        var totale = await db.Rapporti.AsNoTracking().CountAsync(r =>
            r.TenantId == tenantId && r.AziendaId == aziendaId
            && r.Dipendente != null && r.Dipendente.TenantId == tenantId
            && r.DataAssunzione <= fine && (r.DataCessazione == null || r.DataCessazione >= inizio), ct);
        var rapporti = await RapportiDelMeseAsync(tenantId, aziendaId, inizio, fine, dimensioneLotto, ct);
        var rapportoIds = rapporti.Select(r => r.Id).ToArray();
        var cartellini = (await db.Cartellini.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Anno == anno && c.Mese == mese
                && rapportoIds.Contains(c.RapportoLavoroId))
            .Select(c => c.RapportoLavoroId)
            .ToListAsync(ct)).ToHashSet();
        var cedolini = await db.Cedolini.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId
                && c.Anno == anno && c.Mese == mese && rapportoIds.Contains(c.RapportoLavoroId))
            .Select(c => new { c.Id, c.RapportoLavoroId, c.Stato })
            .ToDictionaryAsync(c => c.RapportoLavoroId, ct);

        var aziendaCompleta = azienda.Inps && azienda.Inail && azienda.Ccnl;
        var righe = rapporti.Select(r =>
        {
            cedolini.TryGetValue(r.Id, out var cedolino);
            var storicoPreWorkflow = cedolino?.Stato == nameof(StatoCedolino.PreWorkflow);
            var controllato = cedolino?.Stato == nameof(StatoCedolino.Controllata);
            var protetto = storicoPreWorkflow
                || controllato
                || cedolino?.Stato == nameof(StatoCedolino.Validata)
                || cedolino?.Stato == nameof(StatoCedolino.Chiusa);
            var stato = protetto ? "protetto"
                : cedolino is not null ? "gia_elaborato"
                : !aziendaCompleta ? "bloccato"
                : !cartellini.Contains(r.Id) ? "da_compilare"
                : "cartellino_presente";
            var motivo = storicoPreWorkflow
                ? "Cedolino storico precedente al workflow: sola lettura; non risulta validato o chiuso nel nuovo processo."
                : controllato
                    ? "Cedolino controllato: dati congelati e disponibili in sola lettura."
                : stato switch
            {
                "protetto" => "Cedolino validato o chiuso: sola lettura.",
                "gia_elaborato" => "Esiste già una bozza del cedolino.",
                "bloccato" => "Completare posizione INPS, PAT INAIL e CCNL dell'azienda.",
                "da_compilare" => "Cartellino del mese non ancora compilato.",
                _ => "Controllo preliminare: cartellino presente. Il motore applicherà gli altri blocchi prima del calcolo.",
            };
            return new RigaCedolinoMese(r.Id, r.Nominativo, stato, motivo, cedolino?.Id, cedolino?.Stato);
        }).ToList();
        return new("Cedolini", righe.Cast<object>().ToList(), totale, null);
    }

    private async Task<List<RapportoMinimo>> RapportiDelMeseAsync(
        Guid tenantId, Guid aziendaId, DateOnly inizio, DateOnly fine, int limite, CancellationToken ct) =>
        await db.Rapporti.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId
                && r.Dipendente != null && r.Dipendente.TenantId == tenantId
                && r.DataAssunzione <= fine && (r.DataCessazione == null || r.DataCessazione >= inizio))
            .OrderBy(r => r.Dipendente!.Cognome).ThenBy(r => r.Dipendente!.Nome)
            .Select(r => new RapportoMinimo(r.Id, (r.Dipendente!.Cognome + " " + r.Dipendente.Nome).Trim()))
            .Take(limite)
            .ToListAsync(ct);

    private static bool ProvaPeriodo(
        string periodo, out int anno, out int mese, out DateOnly inizio, out DateOnly fine)
    {
        anno = mese = 0;
        inizio = fine = default;
        var parti = periodo.Split('-');
        if (parti.Length != 2 || !int.TryParse(parti[0], out anno) || !int.TryParse(parti[1], out mese)
            || anno is < 2000 or > 2200 || mese is < 1 or > 12)
            return false;
        inizio = new DateOnly(anno, mese, 1);
        fine = inizio.AddMonths(1).AddDays(-1);
        return true;
    }

    private static Proiezione Pagina<T>(List<T> righe, string risorsa)
        where T : notnull =>
        new(risorsa, righe.Take(MassimoRighe).Cast<object>().ToList(), righe.Count, null);

    private sealed record Proiezione(string Risorsa, List<object> Righe, int Totale, string? Errore);
    private sealed record RapportoMinimo(Guid Id, string Nominativo);
}
