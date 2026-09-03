using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Esito di un singolo rapporto nell'elaborazione massiva.</summary>
public sealed record EsitoElaborazioneRapporto(
    Guid RapportoId, string Nominativo, bool Riuscita, decimal? Netto, string? Errore);

/// <summary>Esito complessivo: quanti elaborati, quanti falliti, il dettaglio per rapporto.</summary>
public sealed record EsitoElaborazioneMassiva(
    int Anno, int Mese, int Elaborati, int Falliti,
    IReadOnlyList<EsitoElaborazioneRapporto> Esiti);

/// <summary>
/// «Elabora i pronti»: calcola in un colpo solo i cedolini dei rapporti che hanno già il
/// cartellino del mese salvato e nessun cedolino. Ogni rapporto passa dallo STESSO percorso
/// fail-closed dell'elaborazione manuale (<see cref="CedoliniService.CalcolaEArchiviaVerificatoAsync"/>):
/// stessi controlli, stessi numeri. Un rapporto che fallisce non ferma gli altri: il suo
/// errore torna per riga, in chiaro. Gli agricoli restano fuori (ciclo dedicato).
/// </summary>
public sealed class ElaborazioneMassivaService
{
    private readonly PagheDbContext _db;
    private readonly CedoliniService _cedolini;

    public ElaborazioneMassivaService(PagheDbContext db, CedoliniService cedolini)
    {
        _db = db;
        _cedolini = cedolini;
    }

    /// <summary>
    /// Ricostruisce il cartellino di dominio dall'entità salvata, campo per campo: la fedeltà
    /// è coperta da test perché un campo dimenticato produrrebbe buste diverse da quelle
    /// elaborate a mano dalla pagina Presenze. I dati aggregati dell'evento sanitario non si
    /// copiano: il percorso verificato li ricava dalle giornate, come per il browser.
    /// </summary>
    public static Cartellino CartellinoDaEntita(CartellinoEntity entita) => new()
    {
        Anno = entita.Anno,
        Mese = entita.Mese,
        OreStraordinario = entita.OreStraordinario,
        OreStraordinarioNotturno = entita.OreStraordinarioNotturno,
        OreStraordinarioFestivo = entita.OreStraordinarioFestivo,
        GiorniFerie = entita.GiorniFerie,
        OrePermessi = entita.OrePermessi,
        OreExFestivita = entita.OreExFestivita,
        GiorniMalattia = entita.GiorniMalattia,
        NumeroEventoMalattiaAnno = Math.Max(1, entita.NumeroEventoMalattiaAnno),
        GiorniInfortunio = entita.GiorniInfortunio,
        OreAssenzaNonRetribuita = entita.OreAssenzaNonRetribuita,
        StatoAssegniFamiliari = Enum.TryParse<StatoAssegniFamiliari>(
            entita.StatoAssegniFamiliari, out var anf)
                ? anf : StatoAssegniFamiliari.DaVerificare,
        ImportoAssegniFamiliari = entita.ImportoAssegniFamiliari,
        GiorniTrasfertaItalia = entita.GiorniTrasfertaItalia,
        GiorniTrasfertaEstero = entita.GiorniTrasfertaEstero,
        IndennitaTrasfertaGiornaliera = entita.IndennitaTrasfertaGiornaliera,
        RimborsoTrasferta = Enum.TryParse<TipoRimborsoTrasferta>(
            entita.RimborsoTrasferta, out var rimborso)
                ? rimborso : TipoRimborsoTrasferta.Forfettario,
        RimborsiSpeseDocumentate = entita.RimborsiSpeseDocumentate,
        FringeBenefit = entita.FringeBenefit,
        FringeBenefitGiaErogatoAnno = entita.FringeBenefitGiaErogatoAnno,
        FigliACarico = entita.FigliACarico,
        GiorniBuonoPasto = entita.GiorniBuonoPasto,
        ValoreBuonoPasto = entita.ValoreBuonoPasto,
        BuonoPastoElettronico = entita.BuonoPastoElettronico,
        PremioRisultato = entita.PremioRisultato,
        RedditoAnnoPrecedente = entita.RedditoAnnoPrecedente,
        RiferimentoQuattordicesimaSeparata = entita.RiferimentoQuattordicesimaSeparata,
        DataPagamentoQuattordicesima = entita.DataPagamentoQuattordicesima,
        Giornate = entita.Giornate,
    };

    public async Task<EsitoElaborazioneMassiva> ElaboraProntiAsync(
        Guid tenantId, Guid aziendaId, int anno, int mese, CancellationToken ct = default)
    {
        if (anno is < 2000 or > 2200 || mese is < 1 or > 12)
            throw new ArgumentException("Periodo non valido: indica un anno e un mese reali.");
        bool aziendaEsiste = await _db.Aziende.AsNoTracking()
            .AnyAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct);
        if (!aziendaEsiste)
            throw new ArgumentException("Azienda non trovata per il tenant indicato.");

        var inizio = new DateOnly(anno, mese, 1);
        var fine = inizio.AddMonths(1).AddDays(-1);
        var rapporti = await _db.Rapporti.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId
                && r.CodiceCcnl != "AGRICOLTURA_OPERAI"
                && r.Dipendente != null && r.Dipendente.TenantId == tenantId
                && r.DataAssunzione <= fine
                && (r.DataCessazione == null || r.DataCessazione >= inizio))
            .Select(r => new
            {
                r.Id,
                Nominativo = (r.Dipendente!.Cognome + " " + r.Dipendente.Nome).Trim(),
            })
            .ToListAsync(ct);
        var rapportoIds = rapporti.Select(r => r.Id).ToArray();

        var conCedolino = (await _db.Cedolini.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Anno == anno && c.Mese == mese
                && rapportoIds.Contains(c.RapportoLavoroId))
            .Select(c => c.RapportoLavoroId)
            .ToListAsync(ct)).ToHashSet();
        var cartellini = await _db.Cartellini.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Anno == anno && c.Mese == mese
                && rapportoIds.Contains(c.RapportoLavoroId))
            .ToListAsync(ct);
        var cartellinoPerRapporto = cartellini.ToDictionary(c => c.RapportoLavoroId);

        var esiti = new List<EsitoElaborazioneRapporto>();
        foreach (var rapporto in rapporti)
        {
            if (conCedolino.Contains(rapporto.Id)) continue;
            if (!cartellinoPerRapporto.TryGetValue(rapporto.Id, out var cartellino)) continue;

            try
            {
                var cedolino = await _cedolini.CalcolaEArchiviaVerificatoAsync(
                    tenantId, rapporto.Id, CartellinoDaEntita(cartellino), ct);
                esiti.Add(new(rapporto.Id, rapporto.Nominativo, true,
                    cedolino.NettoInBusta, null));
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                // L'errore di un dipendente non ferma il mese degli altri: torna per riga
                // con le stesse parole del percorso manuale.
                esiti.Add(new(rapporto.Id, rapporto.Nominativo, false, null, e.Message));
            }
        }

        return new EsitoElaborazioneMassiva(
            anno, mese,
            esiti.Count(e => e.Riuscita),
            esiti.Count(e => !e.Riuscita),
            esiti);
    }
}
