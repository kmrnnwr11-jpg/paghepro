using Microsoft.EntityFrameworkCore;
using Paghe.Core.Calcolo;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Input manuali della dichiarazione Redditi PF (quelli non ricavabili dai cedolini).</summary>
public sealed record RedditiPfInput(
    decimal AltriRedditi = 0m,
    decimal OneriDeducibili = 0m,
    decimal OneriDetraibili19 = 0m,
    decimal Acconti = 0m);

/// <summary>
/// Bozza della dichiarazione Redditi PF / 730 per un contribuente (rapporto) e un anno. NON è il
/// file telematico ufficiale (Fase 4, livelli B/C — vedi PIANO-DICHIARAZIONI.md): è un calcolo
/// dichiarativo semplificato da validare con un Commercialista.
/// </summary>
public sealed record RedditiPfBozza(
    string RapportoId, string Nominativo, string CodiceFiscale, int Anno, int NumeroCedolini,
    // input manuali (modificabili)
    decimal AltriRedditi, decimal OneriDeducibili, decimal OneriDetraibili19, decimal Acconti,
    // precompilati dai cedolini
    decimal RedditoLavoroDipendente, decimal RitenuteOperate,
    // calcolati
    decimal RedditoComplessivo, decimal RedditoImponibile, decimal IrpefLorda,
    decimal DetrazioneLavoroDipendente, decimal DetrazioneOneri, decimal IrpefNetta,
    decimal SaldoADebito, decimal SaldoACredito);

public sealed class RedditiPfService
{
    private readonly PagheDbContext _db;
    private readonly ParametriNormativiService _parametri;

    public RedditiPfService(PagheDbContext db, ParametriNormativiService parametri)
    {
        _db = db;
        _parametri = parametri;
    }

    /// <summary>Salva (upsert) gli input manuali della dichiarazione per rapporto+anno, isolato per tenant.</summary>
    public async Task<RedditiPfBozza> SalvaInputAsync(
        Guid tenantId, Guid rapportoId, int anno, RedditiPfInput input, CancellationToken ct = default)
    {
        var e = await _db.DichiarazioniRedditiPf
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.RapportoLavoroId == rapportoId && x.Anno == anno, ct);
        if (e is null)
        {
            e = new DichiarazioneRedditiPfEntity { Id = Guid.NewGuid(), TenantId = tenantId, RapportoLavoroId = rapportoId, Anno = anno };
            _db.DichiarazioniRedditiPf.Add(e);
        }
        e.AltriRedditi = input.AltriRedditi;
        e.OneriDeducibili = input.OneriDeducibili;
        e.OneriDetraibili19 = input.OneriDetraibili19;
        e.Acconti = input.Acconti;
        e.AggiornataIl = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await CostruisciBozzaAsync(tenantId, rapportoId, anno, ct);
    }

    /// <summary>Costruisce la bozza: aggrega i cedolini dell'anno e applica il calcolo IRPEF dichiarativo.</summary>
    public async Task<RedditiPfBozza?> CostruisciBozzaAsync(
        Guid tenantId, Guid rapportoId, int anno, CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti
            .Include(r => r.Dipendente)
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct);
        if (rapporto is null) return null;

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno)
            .ToListAsync(ct);

        var input = await _db.DichiarazioniRedditiPf
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.RapportoLavoroId == rapportoId && x.Anno == anno, ct);

        decimal redditoLavoroDip = cedolini.Sum(c => c.ImponibileIrpef);
        decimal ritenute = cedolini.Sum(c => c.IrpefNetta);
        decimal altriRedditi = input?.AltriRedditi ?? 0m;
        decimal oneriDeducibili = input?.OneriDeducibili ?? 0m;
        decimal oneriDetraibili19 = input?.OneriDetraibili19 ?? 0m;
        decimal acconti = input?.Acconti ?? 0m;

        var parametri = await _parametri.CaricaParametriAsync(anno, ct);

        decimal redditoComplessivo = redditoLavoroDip + altriRedditi;
        decimal redditoImponibile = Math.Max(0m, redditoComplessivo - oneriDeducibili);
        decimal irpefLorda = CalcoloIrpef.IrpefLordaAnnua(redditoImponibile, parametri.ScaglioniIrpef);
        decimal detrLavoro = redditoLavoroDip > 0m
            ? CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(redditoComplessivo, 365)
            : 0m;
        decimal detrOneri = Math.Round(oneriDetraibili19 * 0.19m, 2);
        decimal irpefNetta = Math.Max(0m, irpefLorda - detrLavoro - detrOneri);
        decimal saldo = irpefNetta - ritenute - acconti;

        var dip = rapporto.Dipendente;
        return new RedditiPfBozza(
            RapportoId: rapporto.Id.ToString(),
            Nominativo: $"{dip?.Cognome} {dip?.Nome}".Trim(),
            CodiceFiscale: dip?.CodiceFiscale ?? "",
            Anno: anno,
            NumeroCedolini: cedolini.Count,
            AltriRedditi: altriRedditi,
            OneriDeducibili: oneriDeducibili,
            OneriDetraibili19: oneriDetraibili19,
            Acconti: acconti,
            RedditoLavoroDipendente: Math.Round(redditoLavoroDip, 2),
            RitenuteOperate: Math.Round(ritenute, 2),
            RedditoComplessivo: Math.Round(redditoComplessivo, 2),
            RedditoImponibile: Math.Round(redditoImponibile, 2),
            IrpefLorda: Math.Round(irpefLorda, 2),
            DetrazioneLavoroDipendente: Math.Round(detrLavoro, 2),
            DetrazioneOneri: detrOneri,
            IrpefNetta: Math.Round(irpefNetta, 2),
            SaldoADebito: saldo > 0m ? Math.Round(saldo, 2) : 0m,
            SaldoACredito: saldo < 0m ? Math.Round(-saldo, 2) : 0m);
    }
}
