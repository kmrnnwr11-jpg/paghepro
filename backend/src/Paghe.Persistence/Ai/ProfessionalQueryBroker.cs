using Microsoft.EntityFrameworkCore;

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

public sealed record RigaCartellino(
    string Nominativo,
    bool Elaborato,
    decimal GiorniMalattia,
    int NumeroEventoMalattiaAnno);

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
/// parametri del client. In G2 una lettura di quota = una query (limite 200 righe);
/// il conteggio per record arriverà con l'indice G4.
/// </summary>
public sealed class ProfessionalQueryBroker(
    PagheDbContext db,
    AiGrantService governor,
    ScadenzarioService scadenzario,
    PresenzeService presenze,
    AnagraficheLavoroService anagrafiche,
    TimeProvider orologio)
{
    private const int MassimoRighe = 200;

    public async Task<EsitoQueryBroker> LeggiAsync(
        Guid tenantId, Guid utenteId, string? lease, string risorsa, CancellationToken ct = default)
    {
        var nome = (risorsa ?? "").Trim();
        var (esito, manifest) = await governor.AutorizzaAccessoConManifestAsync(
            tenantId, utenteId, lease, nome, "read", 1, ct);
        if (!esito.Consentito || manifest is null)
            return new(false, esito.Motivo, nome, [], 0, esito.LettureRimanenti);

        return nome.ToLowerInvariant() switch
        {
            "scadenzario" => await ScadenzeAsync(tenantId, manifest, esito, ct),
            "aziende" => await AziendaAsync(tenantId, manifest, esito, ct),
            "dipendenti" => await DipendentiAsync(tenantId, manifest, esito, ct),
            "cartellini" => await CartelliniAsync(tenantId, manifest, esito, ct),
            _ => new(false, $"La risorsa '{nome}' non ha ancora una proiezione nel broker.", nome, [], 0, esito.LettureRimanenti),
        };
    }

    private async Task<EsitoQueryBroker> ScadenzeAsync(
        Guid tenantId, ManifestGrant manifest, EsitoAccessoAi esito, CancellationToken ct)
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

        return Pagina(righe, "Scadenzario", esito);
    }

    private async Task<EsitoQueryBroker> AziendaAsync(
        Guid tenantId, ManifestGrant manifest, EsitoAccessoAi esito, CancellationToken ct)
    {
        if (manifest.AziendaId is not { } aziendaId)
            return new(false, "Il mandato non indica l'azienda: serve scope S1/S2.", "Aziende", [], 0, esito.LettureRimanenti);

        var azienda = await db.Aziende.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct);
        if (azienda is null)
            return new(false, "Azienda del mandato non trovata.", "Aziende", [], 0, esito.LettureRimanenti);

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
        return new(true, null, "Aziende", righe, 1, esito.LettureRimanenti);
    }

    private async Task<EsitoQueryBroker> DipendentiAsync(
        Guid tenantId, ManifestGrant manifest, EsitoAccessoAi esito, CancellationToken ct)
    {
        if (manifest.AziendaId is not { } aziendaId)
            return new(false, "Il mandato non indica l'azienda: serve scope S1/S2.", "Dipendenti", [], 0, esito.LettureRimanenti);

        var rapporti = await anagrafiche.ElencoRapportiAsync(tenantId, aziendaId);
        var righe = rapporti.Select(r => new RigaDipendente(
            $"{r.Dipendente?.Cognome} {r.Dipendente?.Nome}".Trim(),
            r.CodiceCcnl,
            r.Livello)).ToList();
        return Pagina(righe, "Dipendenti", esito);
    }

    private async Task<EsitoQueryBroker> CartelliniAsync(
        Guid tenantId, ManifestGrant manifest, EsitoAccessoAi esito, CancellationToken ct)
    {
        if (manifest.AziendaId is not { } aziendaId || manifest.Periodo is not { } periodo)
            return new(false, "Serve un mandato S2 con azienda e periodo per leggere i cartellini.", "Cartellini", [], 0, esito.LettureRimanenti);

        var parti = periodo.Split('-');
        var mese = await presenze.CostruisciAsync(
            tenantId, aziendaId, int.Parse(parti[0]), int.Parse(parti[1]), ct);
        var righe = mese.Righe.Select(r => new RigaCartellino(
            r.Nominativo, r.Elaborato, r.GiorniMalattia, r.NumeroEventoMalattiaAnno)).ToList();
        return Pagina(righe, "Cartellini", esito);
    }

    private static EsitoQueryBroker Pagina<T>(List<T> righe, string risorsa, EsitoAccessoAi esito)
        where T : notnull =>
        new(true, null, risorsa,
            righe.Take(MassimoRighe).Cast<object>().ToList(),
            righe.Count, esito.LettureRimanenti);
}
