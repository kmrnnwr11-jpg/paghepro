using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dati di un immobile in ingresso.</summary>
public sealed record ImmobileInput(
    string Descrizione, string CategoriaCatastale, decimal RenditaCatastale,
    decimal PercentualePossesso = 100m, int MesiPossesso = 12,
    decimal AliquotaPerMille = 8.6m, bool AbitazionePrincipale = false);

/// <summary>Riga IMU calcolata per un immobile.</summary>
public sealed record ImuRiga(
    string Id, string Descrizione, string CategoriaCatastale, decimal RenditaCatastale,
    int Moltiplicatore, decimal BaseImponibile, bool Esente, decimal Imposta);

/// <summary>Bozza IMU dell'anno: righe per immobile + totale imposta.</summary>
public sealed record ImuBozza(int Anno, decimal TotaleImposta, IReadOnlyList<ImuRiga> Righe);

/// <summary>
/// Anagrafica immobili e calcolo IMU (bozza). Base = rendita × 1,05 × moltiplicatore catastale;
/// imposta = base × aliquota‰ × % possesso × mesi/12. Abitazione principale non di lusso esente.
/// Semplificato — da validare con un Commercialista. Isolato per tenant.
/// </summary>
public sealed class ImuService
{
    private readonly PagheDbContext _db;
    public ImuService(PagheDbContext db) => _db = db;

    /// <summary>Moltiplicatore catastale IMU per categoria (art. 13 D.L. 201/2011).</summary>
    public static int Moltiplicatore(string categoria)
    {
        string c = (categoria ?? "").Trim().ToUpperInvariant();
        if (c is "A/10" or "D/5") return 80;
        if (c == "C/1") return 55;
        if (c.StartsWith("D")) return 65;
        if (c is "B" || c.StartsWith("B/") || c is "C/3" or "C/4" or "C/5") return 140;
        // Gruppo A (tranne A/10) e C/2, C/6, C/7
        return 160;
    }

    public Task<List<ImmobileEntity>> ElencoAsync(Guid tenantId, Guid aziendaId, CancellationToken ct = default) =>
        _db.Immobili.Where(i => i.TenantId == tenantId && i.AziendaId == aziendaId)
            .OrderBy(i => i.Descrizione).ToListAsync(ct);

    public async Task<ImmobileEntity> AggiungiAsync(
        Guid tenantId, Guid aziendaId, ImmobileInput input, CancellationToken ct = default)
    {
        var e = new ImmobileEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AziendaId = aziendaId,
            Descrizione = input.Descrizione,
            CategoriaCatastale = input.CategoriaCatastale,
            RenditaCatastale = input.RenditaCatastale,
            PercentualePossesso = input.PercentualePossesso,
            MesiPossesso = Math.Clamp(input.MesiPossesso, 0, 12),
            AliquotaPerMille = input.AliquotaPerMille,
            AbitazionePrincipale = input.AbitazionePrincipale,
            CreatoIl = DateTimeOffset.UtcNow,
        };
        _db.Immobili.Add(e);
        await _db.SaveChangesAsync(ct);
        return e;
    }

    public async Task<bool> EliminaAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var e = await _db.Immobili.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct);
        if (e is null) return false;
        _db.Immobili.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ImuBozza> CalcolaAsync(Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default)
    {
        var immobili = await ElencoAsync(tenantId, aziendaId, ct);
        var righe = immobili.Select(i =>
        {
            int molt = Moltiplicatore(i.CategoriaCatastale);
            decimal baseImp = Math.Round(i.RenditaCatastale * 1.05m * molt, 2);
            // Abitazione principale non di lusso esente (le categorie di lusso A/1, A/8, A/9 restano imponibili).
            string cat = i.CategoriaCatastale.Trim().ToUpperInvariant();
            bool esente = i.AbitazionePrincipale && cat is not ("A/1" or "A/8" or "A/9");
            decimal imposta = esente ? 0m
                : Math.Round(baseImp * (i.AliquotaPerMille / 1000m) * (i.PercentualePossesso / 100m) * (i.MesiPossesso / 12m), 2);
            return new ImuRiga(i.Id.ToString(), i.Descrizione, i.CategoriaCatastale, i.RenditaCatastale,
                molt, baseImp, esente, imposta);
        }).ToList();

        return new ImuBozza(anno, righe.Sum(r => r.Imposta), righe);
    }
}
