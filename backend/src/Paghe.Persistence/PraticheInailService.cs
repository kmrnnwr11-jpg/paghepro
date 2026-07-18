using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Archivia e gestisce le pratiche di autoliquidazione INAIL: calcolo dai cedolini (via
/// <see cref="InailService"/>) e stato della pratica (con transizioni validate). La bozza
/// leggibile resta sull'entità (scaricabile dall'endpoint): non effettua invii, l'autoliquidazione
/// va presentata sui servizi online INAIL. Tutto isolato per tenant.
/// </summary>
public sealed class PraticheInailService
{
    private readonly PagheDbContext _db;
    private readonly InailService _inail;

    public PraticheInailService(PagheDbContext db, InailService inail)
    {
        _db = db;
        _inail = inail;
    }

    /// <summary>Calcola l'autoliquidazione dell'anno dai cedolini archiviati e la salva come pratica (stato Generata).</summary>
    public async Task<PraticaInailEntity> CalcolaEArchiviaAsync(
        Guid tenantId, Guid aziendaId, int anno, decimal rataGiaVersata = 0m, CancellationToken ct = default)
    {
        var bozza = await _inail.CalcolaAutoliquidazioneAsync(tenantId, aziendaId, anno, rataGiaVersata, ct);
        var risultato = new RisultatoAutoliquidazioneInail(
            bozza.RetribuzioniAnno, bozza.TassoPerMille, bozza.Regolazione,
            bozza.RataAnticipata, bozza.RataGiaVersata, bozza.TotaleDaVersare);
        var ora = DateTime.UtcNow;

        var entity = new PraticaInailEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = aziendaId,
            Anno = anno,
            Pat = bozza.Pat,
            VoceTariffa = bozza.VoceTariffa,
            NumeroCedolini = bozza.NumeroCedolini,
            RetribuzioniAnno = bozza.RetribuzioniAnno,
            TassoPerMille = bozza.TassoPerMille,
            Regolazione = bozza.Regolazione,
            RataAnticipata = bozza.RataAnticipata,
            RataGiaVersata = bozza.RataGiaVersata,
            TotaleDaVersare = bozza.TotaleDaVersare,
            Stato = StatoPraticaTelematica.Generata.ToString(),
            Bozza = new BozzaLeggibileInailWriter().Genera(anno, bozza.Pat, bozza.VoceTariffa, risultato),
            CreataIl = ora,
            UltimoAggiornamento = ora,
        };

        _db.PraticheInail.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<List<PraticaInailEntity>> ElencoAsync(
        Guid tenantId, Guid? aziendaId = null, CancellationToken ct = default) =>
        _db.PraticheInail
            .Where(p => p.TenantId == tenantId && (aziendaId == null || p.AziendaId == aziendaId))
            .OrderByDescending(p => p.Anno).ThenByDescending(p => p.CreataIl)
            .ToListAsync(ct);

    public Task<PraticaInailEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.PraticheInail.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);

    /// <summary>Applica una transizione di stato alla pratica, validandola con la macchina a stati.</summary>
    public async Task<PraticaInailEntity> CambiaStatoAsync(
        Guid tenantId, Guid id, StatoPraticaTelematica nuovo, CancellationToken ct = default)
    {
        var p = await GetAsync(tenantId, id, ct)
            ?? throw new ArgumentException("Pratica INAIL non trovata per il tenant indicato.");

        var corrente = Enum.Parse<StatoPraticaTelematica>(p.Stato);
        if (!PraticaTelematica.PuoTransizionare(corrente, nuovo))
            throw new InvalidOperationException($"Transizione non ammessa da {corrente} a {nuovo}.");

        p.Stato = nuovo.ToString();
        p.UltimoAggiornamento = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return p;
    }
}
