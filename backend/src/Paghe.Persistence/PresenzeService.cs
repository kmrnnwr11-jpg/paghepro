using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence.Cedolini;

namespace Paghe.Persistence;

/// <summary>Riga del cruscotto presenze del mese: stato cedolino e ore di un dipendente.
/// I saldi (ferie/permessi residui) vengono dall'ultimo cedolino elaborato prima del mese
/// e guidano l'inserimento; sono null quando non esiste ancora uno storico.</summary>
public sealed record PresenzaRiga(
    string RapportoId,
    string? CartellinoId,
    int? VersioneCartellino,
    string Nominativo,
    decimal OreStraordinario,
    decimal OreStraordinarioNotturno,
    decimal OreStraordinarioFestivo,
    decimal GiorniFerie,
    decimal OrePermessi,
    decimal OreExFestivita,
    decimal GiorniMalattia,
    int NumeroEventoMalattiaAnno,
    decimal GiorniInfortunio,
    decimal OreAssenzaNonRetribuita,
    string StatoAssegniFamiliari,
    decimal ImportoAssegniFamiliari,
    bool Elaborato,
    decimal Netto,
    decimal? FerieResidue,
    decimal? PermessiResidui,
    int? SaldiAnno,
    int? SaldiMese,
    /// <summary>Ore contrattuali della giornata piena: precompila il calendario giornaliero.</summary>
    decimal OreGiornaliere,
    /// <summary>Calendario già compilato per questo mese; vuoto = cartellino a totali.</summary>
    IReadOnlyList<GiornataCartellino> Giornate);

/// <summary>Cruscotto presenze/elaborazione di un mese per un'azienda.</summary>
public sealed record PresenzeMese(
    int Anno,
    int Mese,
    int Totale,
    int Elaborati,
    IReadOnlyList<PresenzaRiga> Righe);

/// <summary>
/// Cruscotto presenze del mese: per ogni dipendente dell'azienda indica le ore di straordinario
/// con tutte le variabili archiviate, lo stato di elaborazione del cedolino e il netto.
/// Isolato per tenant.
/// </summary>
public sealed class PresenzeService
{
    private readonly PagheDbContext _db;
    public PresenzeService(PagheDbContext db) => _db = db;

    public async Task<PresenzeMese> CostruisciAsync(
        Guid tenantId, Guid aziendaId, int anno, int mese, CancellationToken ct = default)
    {
        var rapporti = await _db.Rapporti
            .Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId)
            .ToListAsync(ct);
        var rapportoIds = rapporti.Select(r => r.Id).ToList();

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        var cartellini = await _db.Cartellini
            .Where(c => c.TenantId == tenantId && c.Anno == anno && c.Mese == mese && rapportoIds.Contains(c.RapportoLavoroId))
            .ToListAsync(ct);
        var cedoliniPrecedenti = await _db.Cedolini.UtilizzabiliAValle()
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId
                && rapportoIds.Contains(c.RapportoLavoroId)
                && (c.Anno < anno || (c.Anno == anno && c.Mese < mese)))
            .ToListAsync(ct);

        var righe = rapporti
            .OrderBy(r => r.Dipendente?.Cognome)
            .Select(r =>
            {
                var ced = cedolini.FirstOrDefault(c => c.RapportoLavoroId == r.Id);
                var cart = cartellini.FirstOrDefault(c => c.RapportoLavoroId == r.Id);
                var precedente = cedoliniPrecedenti
                    .Where(c => c.RapportoLavoroId == r.Id)
                    .OrderByDescending(c => c.Anno).ThenByDescending(c => c.Mese)
                    .FirstOrDefault();
                return new PresenzaRiga(
                    RapportoId: r.Id.ToString(),
                    CartellinoId: cart?.Id.ToString(),
                    VersioneCartellino: cart?.Versione,
                    Nominativo: $"{r.Dipendente?.Cognome} {r.Dipendente?.Nome}".Trim(),
                    OreStraordinario: cart?.OreStraordinario ?? 0m,
                    OreStraordinarioNotturno: cart?.OreStraordinarioNotturno ?? 0m,
                    OreStraordinarioFestivo: cart?.OreStraordinarioFestivo ?? 0m,
                    GiorniFerie: cart?.GiorniFerie ?? 0m,
                    OrePermessi: cart?.OrePermessi ?? 0m,
                    OreExFestivita: cart?.OreExFestivita ?? 0m,
                    GiorniMalattia: cart?.GiorniMalattia ?? 0m,
                    NumeroEventoMalattiaAnno: Math.Max(1, cart?.NumeroEventoMalattiaAnno ?? 1),
                    GiorniInfortunio: cart?.GiorniInfortunio ?? 0m,
                    OreAssenzaNonRetribuita: cart?.OreAssenzaNonRetribuita ?? 0m,
                    StatoAssegniFamiliari: cart?.StatoAssegniFamiliari
                        ?? nameof(StatoAssegniFamiliari.DaVerificare),
                    ImportoAssegniFamiliari: cart?.ImportoAssegniFamiliari ?? 0m,
                    Elaborato: ced is not null,
                    Netto: ced?.NettoInBusta ?? 0m,
                    FerieResidue: precedente?.FerieResidue,
                    PermessiResidui: precedente?.PermessiResidui,
                    SaldiAnno: precedente?.Anno,
                    SaldiMese: precedente?.Mese,
                    // Cinque giorni a settimana: è l'orario più comune e serve solo a
                    // precompilare la griglia, non a calcolare. L'utente può correggerlo giorno
                    // per giorno, e con l'agricoltura (sei giorni) il valore arriverà dal CCNL.
                    OreGiornaliere: Math.Round(r.OreSettimanali / 5m, 2),
                    Giornate: cart?.Giornate ?? []);
            })
            .ToList();

        return new PresenzeMese(anno, mese, righe.Count, righe.Count(x => x.Elaborato), righe);
    }
}
