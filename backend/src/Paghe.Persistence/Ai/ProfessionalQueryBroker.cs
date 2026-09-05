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

public sealed record RigaDipendente(
    Guid RapportoId, string Nominativo, string? CodiceCcnl, string? Livello);

public sealed record RigaCartellino(Guid RapportoId, string Nominativo, bool Elaborato);

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

        // Prima si contano soltanto le righe nello scope (nessuna entità o PII viene
        // materializzata), poi si prenota la quota e solo allora si costruisce la proiezione.
        var piano = await PreparaLetturaAsync(tenantId, manifest, nome, ct);
        if (piano.Errore is not null)
            return new(false, piano.Errore, piano.Risorsa, [], 0, verifica.LettureRimanenti);

        var (accesso, manifestPrenotato) = await governor.AutorizzaAccessoConManifestAsync(
            tenantId, utenteId, lease, nome, "read", piano.Quantita, ct);
        if (!accesso.Consentito || manifestPrenotato is null)
            return new(false, accesso.Motivo, piano.Risorsa, [], 0, accesso.LettureRimanenti);

        Proiezione proiezione;
        try
        {
            proiezione = nome.ToLowerInvariant() switch
            {
                "scadenzario" => await ScadenzeAsync(
                    tenantId, manifestPrenotato, piano.Quantita, piano.Totale, ct),
                "aziende" => await AziendaAsync(
                    tenantId, manifestPrenotato, piano.Quantita, piano.Totale, ct),
                "dipendenti" => await DipendentiAsync(
                    tenantId, manifestPrenotato, piano.Quantita, piano.Totale, ct),
                "cartellini" => await CartelliniAsync(
                    tenantId, manifestPrenotato, piano.Quantita, piano.Totale, ct),
                "cedolini" => await CedoliniMeseAsync(
                    tenantId, manifestPrenotato, piano.Quantita, piano.Totale, ct),
                _ => new Proiezione(nome, [], 0,
                    $"La risorsa '{nome}' non ha ancora una proiezione nel broker."),
            };
        }
        catch
        {
            await governor.RimborsaLettureAsync(
                tenantId, utenteId, manifestPrenotato.GrantId, piano.Quantita,
                CancellationToken.None);
            throw;
        }

        if (proiezione.Errore is not null)
        {
            await governor.RimborsaLettureAsync(
                tenantId, utenteId, manifestPrenotato.GrantId, piano.Quantita, ct);
            return new(false, proiezione.Errore, proiezione.Risorsa, [], 0, verifica.LettureRimanenti);
        }

        if (proiezione.Righe.Count > piano.Quantita)
        {
            await governor.RimborsaLettureAsync(
                tenantId, utenteId, manifestPrenotato.GrantId, piano.Quantita, ct);
            return new(false, "La proiezione ha superato la prenotazione: lettura annullata.",
                proiezione.Risorsa, [], 0, accesso.LettureRimanenti);
        }

        var nonConsegnate = piano.Quantita - proiezione.Righe.Count;
        if (nonConsegnate > 0)
            await governor.RimborsaLettureAsync(
                tenantId, utenteId, manifestPrenotato.GrantId, nonConsegnate, ct);

        // Ultima vidimazione prima dell'egress: una revoca durante la query nega le righe e
        // restituisce tutta la quota effettivamente materializzata.
        var (finale, manifestFinale) = await governor.VerificaAccessoConManifestAsync(
            tenantId, utenteId, lease, nome, "read", ct);
        if (!finale.Consentito || manifestFinale?.GrantId != manifestPrenotato.GrantId)
        {
            await governor.RimborsaLettureAsync(
                tenantId, utenteId, manifestPrenotato.GrantId, proiezione.Righe.Count,
                CancellationToken.None);
            return new(false, finale.Motivo ?? "Mandato non più spendibile.",
                proiezione.Risorsa, [], 0, finale.LettureRimanenti);
        }

        return new(true, null, proiezione.Risorsa, proiezione.Righe,
            proiezione.Totale, finale.LettureRimanenti);
    }

    private async Task<PianoLettura> PreparaLetturaAsync(
        Guid tenantId, ManifestGrant manifest, string nome, CancellationToken ct)
    {
        switch (nome.ToLowerInvariant())
        {
            case "scadenzario":
            {
                var query = db.Promemoria.AsNoTracking().Where(p => p.TenantId == tenantId);
                if (manifest.AziendaId is { } aziendaId)
                    query = query.Where(p => p.AziendaId == aziendaId || p.AziendaId == null);
                var totale = await query.CountAsync(ct);
                return Piano(nome, totale, MassimoRighe);
            }
            case "aziende":
            {
                if (manifest.AziendaId is not { } aziendaId)
                    return Errore(nome, "Il mandato non indica l'azienda: serve scope S1/S2.");
                var esiste = await db.Aziende.AsNoTracking()
                    .AnyAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct);
                return esiste ? Piano(nome, 1, 1) : Errore(nome, "Azienda del mandato non trovata.");
            }
            case "dipendenti":
            {
                if (manifest.AziendaId is not { } aziendaId)
                    return Errore(nome, "Il mandato non indica l'azienda: serve scope S1/S2.");
                var totale = await db.Rapporti.AsNoTracking().CountAsync(r =>
                    r.TenantId == tenantId && r.AziendaId == aziendaId
                    && r.Dipendente != null && r.Dipendente.TenantId == tenantId, ct);
                return Piano(nome, totale, MassimoRighe);
            }
            case "cartellini":
            case "cedolini":
            {
                if (manifest.AziendaId is not { } aziendaId || manifest.Periodo is not { } periodo)
                    return Errore(nome, nome.Equals("cartellini", StringComparison.OrdinalIgnoreCase)
                        ? "Serve un mandato S2 con azienda e periodo per leggere i cartellini."
                        : "Serve un mandato S2 con azienda e periodo per controllare i cedolini.");
                if (!ProvaPeriodo(periodo, out _, out _, out var inizio, out var fine))
                    return Errore(nome, "Periodo del mandato non valido.");
                if (nome.Equals("cedolini", StringComparison.OrdinalIgnoreCase)
                    && !await db.Aziende.AsNoTracking()
                        .AnyAsync(a => a.TenantId == tenantId && a.Id == aziendaId, ct))
                    return Errore(nome, "Azienda del mandato non trovata.");
                var totale = await db.Rapporti.AsNoTracking().CountAsync(r =>
                    r.TenantId == tenantId && r.AziendaId == aziendaId
                    && r.Dipendente != null && r.Dipendente.TenantId == tenantId
                    && r.DataAssunzione <= fine
                    && (r.DataCessazione == null || r.DataCessazione >= inizio), ct);
                return Piano(nome, totale,
                    nome.Equals("cedolini", StringComparison.OrdinalIgnoreCase) ? 10 : MassimoRighe);
            }
            default:
                return Errore(nome,
                    $"La risorsa '{nome}' non ha ancora una proiezione nel broker.");
        }
    }

    private static PianoLettura Piano(string risorsa, int totale, int limite) =>
        new(risorsa, totale, Math.Min(totale, limite), null);

    private static PianoLettura Errore(string risorsa, string errore) =>
        new(risorsa, 0, 0, errore);

    private async Task<Proiezione> ScadenzeAsync(
        Guid tenantId, ManifestGrant manifest, int limite, int totale, CancellationToken ct)
    {
        var oggi = DateOnly.FromDateTime(orologio.GetUtcNow().UtcDateTime);
        var query = db.Promemoria.AsNoTracking().Where(p => p.TenantId == tenantId);
        if (manifest.AziendaId is { } aziendaId)
            query = query.Where(p => p.AziendaId == aziendaId || p.AziendaId == null);
        var minimi = await query
            .OrderBy(p => p.DataScadenza)
            .Select(p => new
            {
                p.Titolo,
                p.Stato,
                p.DataScadenza,
                p.GiorniPreavviso,
            })
            .Take(limite)
            .ToListAsync(ct);
        var righe = minimi.Select(p =>
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

        return new("Scadenzario", righe.Cast<object>().ToList(), totale, null);
    }

    private async Task<Proiezione> AziendaAsync(
        Guid tenantId, ManifestGrant manifest, int limite, int totale, CancellationToken ct)
    {
        if (manifest.AziendaId is not { } aziendaId)
            return new("Aziende", [], 0, "Il mandato non indica l'azienda: serve scope S1/S2.");

        var azienda = await db.Aziende.AsNoTracking()
            .Where(a => a.Id == aziendaId && a.TenantId == tenantId)
            .Select(a => new RigaAzienda(
                a.RagioneSociale,
                a.MatricolaInps != null && a.MatricolaInps != "",
                a.PatInail != null && a.PatInail != "",
                a.CodiceCatastale != null && a.CodiceCatastale != "",
                a.IbanProtetto != null && a.IbanProtetto != "",
                (a.Pec != null && a.Pec != "")
                    || (a.CodiceDestinatarioSdi != null && a.CodiceDestinatarioSdi != "")))
            .Take(limite)
            .FirstOrDefaultAsync(ct);
        if (azienda is null)
            return new("Aziende", [], 0, "Azienda del mandato non trovata.");

        return new("Aziende", [azienda], totale, null);
    }

    private async Task<Proiezione> DipendentiAsync(
        Guid tenantId, ManifestGrant manifest, int limite, int totale, CancellationToken ct)
    {
        if (manifest.AziendaId is not { } aziendaId)
            return new("Dipendenti", [], 0, "Il mandato non indica l'azienda: serve scope S1/S2.");

        // Predicate e proiezione restano nel SQL: davanti a un archivio legacy corrotto il record
        // dell'altro tenant non viene materializzato né passato agli interceptor di decifratura.
        var query = db.Rapporti.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId
                && r.Dipendente != null && r.Dipendente.TenantId == tenantId);
        var righe = await query
            .OrderBy(r => r.Dipendente!.Cognome).ThenBy(r => r.Dipendente!.Nome)
            .Select(r => new RigaDipendente(
                r.Id,
                (r.Dipendente!.Cognome + " " + r.Dipendente.Nome).Trim(),
                r.CodiceCcnl,
                r.Livello))
            .Take(limite)
            .ToListAsync(ct);
        return new("Dipendenti", righe.Cast<object>().ToList(), totale, null);
    }

    private async Task<Proiezione> CartelliniAsync(
        Guid tenantId, ManifestGrant manifest, int limite, int totale, CancellationToken ct)
    {
        if (manifest.AziendaId is not { } aziendaId || manifest.Periodo is not { } periodo)
            return new("Cartellini", [], 0, "Serve un mandato S2 con azienda e periodo per leggere i cartellini.");
        if (!ProvaPeriodo(periodo, out var anno, out var mese, out var inizio, out var fine))
            return new("Cartellini", [], 0, "Periodo del mandato non valido.");

        var rapporti = await RapportiDelMeseAsync(tenantId, aziendaId, inizio, fine, limite, ct);
        var rapportoIds = rapporti.Select(r => r.Id).ToArray();
        var compilati = (await db.Cartellini.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Anno == anno && c.Mese == mese
                && rapportoIds.Contains(c.RapportoLavoroId))
            .Select(c => c.RapportoLavoroId)
            .ToListAsync(ct)).ToHashSet();
        var righe = rapporti.Select(r => new RigaCartellino(
            r.Id, r.Nominativo, compilati.Contains(r.Id))).ToList();
        return new("Cartellini", righe.Cast<object>().ToList(), totale, null);
    }

    private async Task<Proiezione> CedoliniMeseAsync(
        Guid tenantId, ManifestGrant manifest, int limite, int totale, CancellationToken ct)
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

        var rapporti = await RapportiDelMeseAsync(
            tenantId, aziendaId, inizio, fine, Math.Min(limite, dimensioneLotto), ct);
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
            // Un operaio agricolo non passa dal ciclo ordinario: ha tariffa provinciale, ore e
            // giornate distinte e si elabora dalla pagina Agricoltura OTD. Segnarlo «bloccato»
            // per la posizione INPS/CCNL aziendale sarebbe un'informazione falsa.
            var agricolo = string.Equals(r.CodiceCcnl, "AGRICOLTURA_OPERAI", StringComparison.OrdinalIgnoreCase);
            var stato = protetto ? "protetto"
                : cedolino is not null ? "gia_elaborato"
                : agricolo ? "agricolo"
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
                "agricolo" => "Rapporto agricolo (CCNL operai agricoli): si elabora dalla pagina Agricoltura OTD, fuori dal ciclo ordinario del mese.",
                "bloccato" => "Completare posizione INPS, PAT INAIL e CCNL dell'azienda.",
                "da_compilare" => "Cartellino del mese non ancora compilato.",
                // Formulazione senza ambiguità: «applicherà i blocchi» veniva letto (anche
                // dal modello) come «mese bloccato». Qui il mese è lavorabile.
                _ => "Cartellino del mese presente: nessun cedolino elaborato, presenze modificabili.",
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
            .Select(r => new RapportoMinimo(
                r.Id, (r.Dipendente!.Cognome + " " + r.Dipendente.Nome).Trim(), r.CodiceCcnl))
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

    private sealed record Proiezione(string Risorsa, List<object> Righe, int Totale, string? Errore);
    private sealed record PianoLettura(
        string Risorsa, int Totale, int Quantita, string? Errore);
    private sealed record RapportoMinimo(Guid Id, string Nominativo, string CodiceCcnl);
}
