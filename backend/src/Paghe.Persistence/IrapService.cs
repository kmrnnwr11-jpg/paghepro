using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence;

/// <summary>
/// Bozza IRAP semplificata per azienda e anno: valore della produzione (ricavi) meno costi esterni
/// e costo del lavoro (deducibile per i dipendenti a tempo indeterminato), per aliquota standard.
/// NON è il calcolo ufficiale (varia per tipo di contribuente e regione) — da validare con un
/// Commercialista. Vedi PIANO-DICHIARAZIONI.md, Fase 5.
/// </summary>
public sealed record IrapBozza(
    int Anno,
    decimal Ricavi,
    decimal CostiEsterni,
    decimal CostoLavoroDeducibile,
    decimal BaseImponibile,
    decimal Aliquota,
    decimal Imposta);

public sealed class IrapService
{
    private readonly PagheDbContext _db;
    public IrapService(PagheDbContext db) => _db = db;

    /// <summary>Aliquota IRPEF ordinaria; alcune regioni applicano maggiorazioni/riduzioni.</summary>
    public const decimal AliquotaStandard = 0.039m;

    public async Task<IrapBozza> CostruisciAsync(
        Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default)
    {
        decimal ricavi = await _db.Fatture
            .Where(f => f.TenantId == tenantId && f.AziendaId == aziendaId && f.Anno == anno)
            .SumAsync(f => (decimal?)f.TotaleImponibile, ct) ?? 0m;

        decimal costiEsterni = await _db.Spese
            .Where(s => s.TenantId == tenantId && s.AziendaId == aziendaId && s.Data.Year == anno)
            .SumAsync(s => (decimal?)s.Imponibile, ct) ?? 0m;

        decimal costoLavoro = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId && c.Anno == anno)
            .SumAsync(c => (decimal?)c.CostoAzienda, ct) ?? 0m;

        decimal baseImponibile = Math.Max(0m, ricavi - costiEsterni - costoLavoro);
        decimal imposta = Math.Round(baseImponibile * AliquotaStandard, 2);

        return new IrapBozza(
            Anno: anno,
            Ricavi: Math.Round(ricavi, 2),
            CostiEsterni: Math.Round(costiEsterni, 2),
            CostoLavoroDeducibile: Math.Round(costoLavoro, 2),
            BaseImponibile: Math.Round(baseImponibile, 2),
            Aliquota: AliquotaStandard,
            Imposta: imposta);
    }
}
