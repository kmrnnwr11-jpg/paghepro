using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Carica i parametri normativi e i CCNL dal database, versionati per anno, e li trasforma
/// negli oggetti di dominio usati dal motore di calcolo. Espone anche l'inizializzazione
/// del database a partire dai dati di esempio (<see cref="Seed"/>).
///
/// Così le aliquote, gli scaglioni e i minimi CCNL non sono più cablati nel codice ma
/// risiedono su tabelle aggiornabili per anno senza ricompilare.
/// </summary>
public sealed class ParametriNormativiService
{
    private readonly PagheDbContext _db;

    public ParametriNormativiService(PagheDbContext db) => _db = db;

    /// <summary>Popola il database con i parametri di esempio se non è già stato fatto.</summary>
    public async Task ImportaSeedSeVuotoAsync(CancellationToken ct = default)
    {
        if (await _db.ParametriAnno.AnyAsync(ct))
            return;

        var p = Seed.Parametri2026();

        _db.ParametriAnno.Add(new ParametriAnnoEntity
        {
            Anno = p.Anno,
            DivisoreOrario = p.DivisoreOrario,
            MaggiorazioneStraordinario = p.MaggiorazioneStraordinario,
            MaggiorazioneStraordinarioNotturno = p.MaggiorazioneStraordinarioNotturno,
            MaggiorazioneStraordinarioFestivo = p.MaggiorazioneStraordinarioFestivo,
            TfrDivisore = p.TfrDivisore,
            TfrContributoFondo = p.TfrContributoFondo,
        });

        foreach (var (qualifica, aliquota) in p.AliquoteInps)
            _db.InpsAliquote.Add(new InpsAliquotaEntity
            {
                Id = Guid.NewGuid(), Anno = p.Anno, Qualifica = qualifica.ToString(),
                Dipendente = aliquota.Dipendente, Datore = aliquota.Datore,
            });

        foreach (var s in p.ScaglioniIrpef)
            _db.IrpefScaglioni.Add(new IrpefScaglioneEntity
            {
                Id = Guid.NewGuid(), Anno = p.Anno,
                DaImporto = s.DaImporto, AImporto = s.AImporto, Aliquota = s.Aliquota,
            });

        foreach (var (regione, aliquota) in p.AddizionaleRegionale)
            _db.AddizionaliRegionali.Add(new AddizionaleRegionaleEntity
            {
                Id = Guid.NewGuid(), Anno = p.Anno, Regione = regione, Aliquota = aliquota,
            });

        foreach (var (codice, aliquota) in p.AddizionaleComunale)
            _db.AddizionaliComunali.Add(new AddizionaleComunaleEntity
            {
                Id = Guid.NewGuid(), Anno = p.Anno, CodiceCatastale = codice, Aliquota = aliquota,
            });

        foreach (var ccnl in Seed.TuttiICcnl2026())
        {
            _db.Ccnl.Add(new CcnlEntity { Codice = ccnl.Codice, Descrizione = ccnl.Descrizione });
            foreach (var liv in ccnl.Livelli)
                _db.CcnlLivelli.Add(new CcnlLivelloEntity
                {
                    Id = Guid.NewGuid(), CcnlCodice = ccnl.Codice, Anno = p.Anno,
                    Livello = liv.Codice, Descrizione = liv.Descrizione,
                    MinimoTabellare = liv.MinimoTabellare, Edr = liv.Edr, ImportoScatto = liv.ImportoScatto,
                });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Costruisce i parametri normativi di dominio per l'anno indicato.</summary>
    public async Task<ParametriNormativi> CaricaParametriAsync(int anno, CancellationToken ct = default)
    {
        var pa = await _db.ParametriAnno.FirstOrDefaultAsync(x => x.Anno == anno, ct)
            ?? throw new ArgumentException($"Parametri normativi non presenti per l'anno {anno}.");

        var aliquote = await _db.InpsAliquote.Where(x => x.Anno == anno).ToListAsync(ct);
        var scaglioni = await _db.IrpefScaglioni.Where(x => x.Anno == anno)
            .OrderBy(x => x.DaImporto).ToListAsync(ct);
        var regionali = await _db.AddizionaliRegionali.Where(x => x.Anno == anno).ToListAsync(ct);
        var comunali = await _db.AddizionaliComunali.Where(x => x.Anno == anno).ToListAsync(ct);

        // Regimi a scaglioni + esenzione dai file ufficiali MEF (se disponibili per l'anno):
        // prevalgono sull'aliquota piatta di DB, che resta come fallback.
        var regimiMef = CaricaRegimiMef.PerAnno(anno);

        return new ParametriNormativi
        {
            Anno = anno,
            ScaglioniIrpef = scaglioni
                .Select(s => new ScaglioneIrpef(s.DaImporto, s.AImporto, s.Aliquota)).ToList(),
            AliquoteInps = aliquote.ToDictionary(
                a => Enum.Parse<Qualifica>(a.Qualifica),
                a => new AliquotaInps(a.Dipendente, a.Datore)),
            AddizionaleRegionale = regionali.ToDictionary(
                r => r.Regione, r => r.Aliquota, StringComparer.OrdinalIgnoreCase),
            AddizionaleComunale = comunali.ToDictionary(
                c => c.CodiceCatastale, c => c.Aliquota, StringComparer.OrdinalIgnoreCase),
            RegimiAddizionaleRegionale = regimiMef.Regionali,
            RegimiAddizionaleComunale = regimiMef.Comunali,
            DivisoreOrario = pa.DivisoreOrario,
            MaggiorazioneStraordinario = pa.MaggiorazioneStraordinario,
            MaggiorazioneStraordinarioNotturno = pa.MaggiorazioneStraordinarioNotturno,
            MaggiorazioneStraordinarioFestivo = pa.MaggiorazioneStraordinarioFestivo,
            TfrDivisore = pa.TfrDivisore,
            TfrContributoFondo = pa.TfrContributoFondo,
            TfrVariazioneIstat = pa.VariazioneIstatTfr,
        };
    }

    /// <summary>Costruisce il CCNL di dominio per il codice e l'anno indicati.</summary>
    public async Task<Ccnl> CaricaCcnlAsync(string codice, int anno, CancellationToken ct = default)
    {
        var ccnl = await _db.Ccnl.FirstOrDefaultAsync(c => c.Codice == codice, ct)
            ?? throw new ArgumentException($"CCNL '{codice}' non presente.");

        var livelli = await _db.CcnlLivelli
            .Where(l => l.CcnlCodice == codice && l.Anno == anno).ToListAsync(ct);
        if (livelli.Count == 0)
            throw new ArgumentException($"Nessun livello per il CCNL '{codice}' nell'anno {anno}.");

        return new Ccnl(ccnl.Codice, ccnl.Descrizione,
            livelli.Select(l => new LivelloCcnl(
                l.Livello, l.Descrizione, l.MinimoTabellare, l.Edr, l.ImportoScatto)));
    }
}
