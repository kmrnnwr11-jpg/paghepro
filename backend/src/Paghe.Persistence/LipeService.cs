using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence;

/// <summary>Modulo LIPE (un periodo di liquidazione IVA nel trimestre).</summary>
public sealed record LipeModulo(
    string Periodo,
    decimal IvaEsigibile,
    decimal IvaDetratta,
    decimal IvaDovuta,
    decimal IvaCredito);

/// <summary>
/// Bozza della Comunicazione Liquidazioni Periodiche IVA (LIPE) per un trimestre: raccoglie le
/// liquidazioni IVA del periodo. NON è il file telematico ufficiale (serve la specifica AE annuale
/// e l'invio via Entratel — vedi PIANO-DICHIARAZIONI.md, Fase 3).
/// </summary>
public sealed record LipeBozza(
    int Anno,
    int Trimestre,
    IReadOnlyList<LipeModulo> Moduli,
    decimal TotaleDovuta,
    decimal TotaleCredito);

public sealed class LipeService
{
    private readonly PagheDbContext _db;
    public LipeService(PagheDbContext db) => _db = db;

    /// <summary>Costruisce la bozza LIPE per azienda, anno e trimestre (1–4), isolata per tenant.</summary>
    public async Task<LipeBozza> CostruisciAsync(
        Guid tenantId, Guid aziendaId, int anno, int trimestre, CancellationToken ct = default)
    {
        int t = Math.Clamp(trimestre, 1, 4);
        int meseDa = (t - 1) * 3 + 1;
        int meseA = t * 3;

        var liquidazioni = await _db.LiquidazioniIva
            .Where(l => l.TenantId == tenantId && l.AziendaId == aziendaId
                && l.Dal.Year == anno && l.Dal.Month >= meseDa && l.Dal.Month <= meseA)
            .OrderBy(l => l.Dal)
            .ToListAsync(ct);

        var moduli = liquidazioni.Select(l => new LipeModulo(
            Periodo: EtichettaPeriodo(l.Dal, l.Periodicita, t),
            IvaEsigibile: l.IvaVendite,
            IvaDetratta: l.IvaAcquistiDetraibile,
            IvaDovuta: l.IvaDaVersare,
            IvaCredito: l.CreditoDaRiportare)).ToList();

        return new LipeBozza(anno, t, moduli,
            TotaleDovuta: moduli.Sum(m => m.IvaDovuta),
            TotaleCredito: moduli.Sum(m => m.IvaCredito));
    }

    private static readonly string[] Mesi =
        { "", "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno",
          "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre" };

    private static string EtichettaPeriodo(DateOnly dal, string periodicita, int trimestre) =>
        periodicita.Equals("Trimestrale", StringComparison.OrdinalIgnoreCase)
            ? $"{trimestre}° trimestre {dal.Year}"
            : $"{Mesi[dal.Month]} {dal.Year}";
}
