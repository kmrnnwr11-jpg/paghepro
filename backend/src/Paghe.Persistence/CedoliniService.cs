using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Calcola e archivia i cedolini. Carica il rapporto dal DB, ricostruisce il dominio,
/// costruisce il motore con i parametri normativi dell'anno e persiste il risultato.
/// Un nuovo calcolo per lo stesso periodo sostituisce quello precedente (ricalcolo).
/// </summary>
public sealed class CedoliniService
{
    private readonly PagheDbContext _db;
    private readonly ParametriNormativiService _parametri;

    public CedoliniService(PagheDbContext db, ParametriNormativiService parametri)
    {
        _db = db;
        _parametri = parametri;
    }

    public async Task<CedolinoEntity> CalcolaEArchiviaAsync(
        Guid tenantId, Guid rapportoId, Cartellino cartellino, CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Rapporto di lavoro non trovato per il tenant indicato.");

        var dominio = ToDominio(rapporto);
        var parametri = await _parametri.CaricaParametriAsync(cartellino.Anno, ct);
        var ccnl = await _parametri.CaricaCcnlAsync(rapporto.CodiceCcnl, cartellino.Anno, ct);
        var cedolino = new MotoreCalcoloCedolino(parametri, ccnl).Calcola(dominio, cartellino);

        // Ricalcolo: rimuove cartellino e cedolino esistenti per lo stesso periodo.
        await RimuoviEsistenti(tenantId, rapportoId, cartellino.Anno, cartellino.Mese, ct);

        _db.Cartellini.Add(new CartellinoEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, RapportoLavoroId = rapportoId,
            Anno = cartellino.Anno, Mese = cartellino.Mese,
            OreStraordinario = cartellino.OreStraordinario,
            OreStraordinarioNotturno = cartellino.OreStraordinarioNotturno,
            OreStraordinarioFestivo = cartellino.OreStraordinarioFestivo,
            GiorniFerie = cartellino.GiorniFerie, OrePermessi = cartellino.OrePermessi,
            GiorniMalattia = cartellino.GiorniMalattia,
            OreAssenzaNonRetribuita = cartellino.OreAssenzaNonRetribuita,
        });

        var entity = new CedolinoEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AziendaId = rapporto.AziendaId,
            RapportoLavoroId = rapportoId, Anno = cedolino.Anno, Mese = cedolino.Mese,
            TotaleCompetenze = cedolino.TotaleCompetenze,
            TotaleTrattenute = cedolino.TotaleTrattenute,
            ImponibileInps = cedolino.ImponibileInps,
            ContributiDipendente = cedolino.ContributiDipendente,
            ContributiDatore = cedolino.ContributiDatore,
            ImponibileIrpef = cedolino.ImponibileIrpef,
            IrpefNetta = cedolino.IrpefNetta,
            AddizionaleRegionale = cedolino.AddizionaleRegionale,
            AddizionaleComunale = cedolino.AddizionaleComunale,
            NettoInBusta = cedolino.NettoInBusta,
            QuotaTfr = cedolino.QuotaTfr,
            CostoAzienda = cedolino.CostoAzienda,
            CalcolatoIl = DateTime.UtcNow,
            Voci = cedolino.Righe.Select(r => new CedolinoVoceEntity
            {
                Id = Guid.NewGuid(), Codice = r.Codice, Descrizione = r.Descrizione,
                Tipo = r.Tipo.ToString(), Quantita = r.Quantita, Base = r.Base, Importo = r.Importo,
            }).ToList(),
        };

        _db.Cedolini.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>
    /// Costruisce l'F24 da versare per un'azienda raccogliendo automaticamente i cedolini
    /// archiviati del periodo (ritenute IRPEF, addizionali regionale/comunale, contributi INPS).
    /// Il territorio delle addizionali è preso dal rapporto di lavoro di ciascun cedolino.
    /// </summary>
    public async Task<ModelloF24> GeneraF24DelMeseAsync(
        Guid tenantId, Guid aziendaId, int anno, int mese,
        string matricolaInps, string codiceSedeInps = "", CancellationToken ct = default)
    {
        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        if (cedolini.Count == 0)
            throw new ArgumentException($"Nessun cedolino archiviato per il periodo {mese:00}/{anno}.");

        var rapportoIds = cedolini.Select(c => c.RapportoLavoroId).Distinct().ToList();
        var rapporti = await _db.Rapporti
            .Where(r => r.TenantId == tenantId && rapportoIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var righe = cedolini.Select(c =>
        {
            rapporti.TryGetValue(c.RapportoLavoroId, out var rap);
            return new RigaPagaPerF24(
                c.IrpefNetta, c.AddizionaleRegionale, c.AddizionaleComunale,
                c.ContributiDipendente, c.ContributiDatore,
                rap?.Regione ?? "", rap?.CodiceCatastaleComune ?? "");
        }).ToList();

        var sostituto = new DatiSostitutoImposta(
            azienda.CodiceFiscale ?? azienda.PartitaIva ?? "",
            azienda.RagioneSociale, matricolaInps, codiceSedeInps);

        return F24DaCedolini.Costruisci(sostituto, righe, anno, mese);
    }

    public Task<CedolinoEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Cedolini.Include(c => c.Voci)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);

    public Task<List<CedolinoEntity>> ElencoPerRapportoAsync(
        Guid tenantId, Guid rapportoId, CancellationToken ct = default) =>
        _db.Cedolini.Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId)
            .OrderBy(c => c.Anno).ThenBy(c => c.Mese).ToListAsync(ct);

    private async Task RimuoviEsistenti(Guid tenantId, Guid rapportoId, int anno, int mese, CancellationToken ct)
    {
        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        _db.Cedolini.RemoveRange(cedolini);

        var cartellini = await _db.Cartellini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        _db.Cartellini.RemoveRange(cartellini);

        if (cedolini.Count > 0 || cartellini.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private static RapportoLavoro ToDominio(RapportoLavoroEntity e) => new()
    {
        CodiceCcnl = e.CodiceCcnl,
        Livello = e.Livello,
        Qualifica = Enum.Parse<Qualifica>(e.Qualifica),
        NumeroMensilita = e.NumeroMensilita,
        PartTime = e.PartTime,
        PercentualePartTime = e.PercentualePartTime,
        ScattiAnzianita = e.ScattiAnzianita,
        Superminimo = e.Superminimo,
        Regione = e.Regione,
        CodiceCatastaleComune = e.CodiceCatastaleComune,
        DetrazioniLavoroDipendente = e.DetrazioniLavoroDipendente,
        GiorniDetrazioneAnno = e.GiorniDetrazioneAnno,
    };
}
