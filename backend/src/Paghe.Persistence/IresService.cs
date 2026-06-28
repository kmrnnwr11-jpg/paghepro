using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence;

/// <summary>
/// Bozza IRES (Redditi società di capitali) per azienda e anno: parte dall'utile ante imposte del
/// bilancio, applica le variazioni fiscali nette indicate e l'aliquota IRES. NON è il calcolo
/// ufficiale (le variazioni in aumento/diminuzione sono molte e specifiche) — da validare con un
/// Commercialista. Vedi PIANO-DICHIARAZIONI.md, Fase 5.
/// </summary>
public sealed record IresBozza(
    int Anno,
    bool HaBilancio,
    decimal UtileEsercizio,
    decimal ImposteBilancio,
    decimal RedditoAnteImposte,
    decimal VariazioniNette,
    decimal BaseImponibile,
    decimal Aliquota,
    decimal Imposta);

public sealed class IresService
{
    private readonly PagheDbContext _db;
    public IresService(PagheDbContext db) => _db = db;

    /// <summary>Aliquota IRES ordinaria.</summary>
    public const decimal AliquotaStandard = 0.24m;

    public async Task<IresBozza> CostruisciAsync(
        Guid tenantId, Guid aziendaId, int anno, decimal variazioniNette = 0m, CancellationToken ct = default)
    {
        var bilancio = await _db.Bilanci
            .Where(b => b.TenantId == tenantId && b.AziendaId == aziendaId && b.Anno == anno)
            .OrderByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (bilancio is null)
            return new IresBozza(anno, false, 0m, 0m, 0m, variazioniNette, 0m, AliquotaStandard, 0m);

        decimal redditoAnteImposte = bilancio.UtilePerditaEsercizio + bilancio.Imposte;
        decimal baseImponibile = Math.Max(0m, redditoAnteImposte + variazioniNette);
        decimal imposta = Math.Round(baseImponibile * AliquotaStandard, 2);

        return new IresBozza(
            Anno: anno,
            HaBilancio: true,
            UtileEsercizio: Math.Round(bilancio.UtilePerditaEsercizio, 2),
            ImposteBilancio: Math.Round(bilancio.Imposte, 2),
            RedditoAnteImposte: Math.Round(redditoAnteImposte, 2),
            VariazioniNette: Math.Round(variazioniNette, 2),
            BaseImponibile: Math.Round(baseImponibile, 2),
            Aliquota: AliquotaStandard,
            Imposta: imposta);
    }
}
