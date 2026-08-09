using Microsoft.EntityFrameworkCore;
using Paghe.Core.Parametri;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Legge le voci variabili dei CCNL (docs/dati/voci-variabili-ccnl-*.json, fonti CNEL e
/// associazioni) incorporate come risorsa e le trasforma nelle regole di calcolo che il motore
/// applica. Finché stavano solo nei test, ogni contratto calcolava gli straordinari con le
/// percentuali del Commercio: la logistica al 15% invece del 30%, l'agricoltura idem.
/// </summary>
public static class RegoleCcnlDataset
{
    /// <summary>Regole di tutti i CCNL del dataset incorporato, per tutti gli anni disponibili.</summary>
    public static List<RegoleCcnlEntity> Carica()
    {
        var asm = typeof(RegoleCcnlDataset).Assembly;
        var risultato = new List<RegoleCcnlEntity>();

        foreach (var risorsa in asm.GetManifestResourceNames()
                     .Where(n => n.Contains("voci-variabili-ccnl-", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = asm.GetManifestResourceStream(risorsa)!;
            using var lettore = new StreamReader(stream);
            foreach (var v in VociVariabiliCcnlJson.Carica(lettore.ReadToEnd()))
            {
                var m = v.Maggiorazioni ?? new MaggiorazioniCcnl();
                risultato.Add(new RegoleCcnlEntity
                {
                    Id = Guid.NewGuid(),
                    CcnlCodice = v.Codice,
                    Anno = v.Anno,
                    DivisoreOrario = v.DivisoreOrario,
                    // Il dataset non riporta il divisore giornaliero né la maturazione di ferie
                    // e permessi: restano nulli e vale il default dell'anno, finché non arriva
                    // il dato vero del contratto.
                    DivisoreGiornaliero = null,
                    MaggiorazioneStraordinario = m.StraordinarioDiurno,
                    MaggiorazioneStraordinarioNotturno = m.StraordinarioNotturno,
                    MaggiorazioneStraordinarioFestivo = m.StraordinarioFestivo,
                    GiorniFerieMese = null,
                    OrePermessiMese = null,
                    Fonte = v.Fonte,
                });
            }
        }

        return risultato;
    }

    /// <summary>
    /// Scrive le regole nel database, sostituendo quelle dello stesso contratto e anno.
    /// Idempotente: gira a ogni avvio, come l'import dei minimi.
    /// </summary>
    public static async Task<int> ImportaAsync(
        PagheDbContext db, IEnumerable<RegoleCcnlEntity> regole, CancellationToken ct = default)
    {
        int scritte = 0;
        foreach (var r in regole)
        {
            if (string.IsNullOrWhiteSpace(r.CcnlCodice)) continue;

            var vecchie = await db.RegoleCcnl
                .Where(x => x.CcnlCodice == r.CcnlCodice && x.Anno == r.Anno)
                .ToListAsync(ct);
            db.RegoleCcnl.RemoveRange(vecchie);
            db.RegoleCcnl.Add(r);
            scritte++;
        }

        await db.SaveChangesAsync(ct);
        return scritte;
    }
}
