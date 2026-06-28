using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence;

/// <summary>Riga Intrastat: cessioni intracomunitarie verso un paese UE.</summary>
public sealed record IntrastatRiga(string Paese, int NumeroOperazioni, decimal TotaleImponibile);

/// <summary>
/// Bozza Intrastat (cessioni intracomunitarie) dell'anno: aggrega le fatture verso clienti di
/// paesi UE (diversi dall'Italia) per paese. NON è il file telematico ufficiale (Agenzia Dogane) —
/// da validare. Vedi PIANO-DICHIARAZIONI.md, Fase 6.
/// </summary>
public sealed record IntrastatBozza(int Anno, decimal TotaleCessioni, IReadOnlyList<IntrastatRiga> Righe);

public sealed class IntrastatService
{
    private readonly PagheDbContext _db;
    public IntrastatService(PagheDbContext db) => _db = db;

    /// <summary>Codici paese UE (ISO) esclusa l'Italia.</summary>
    private static readonly HashSet<string> PaesiUe = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT","BE","BG","HR","CY","CZ","DK","EE","FI","FR","DE","GR","HU","IE","LV","LT",
        "LU","MT","NL","PL","PT","RO","SK","SI","ES","SE",
    };

    public async Task<IntrastatBozza> CostruisciAsync(
        Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default)
    {
        var fatture = await _db.Fatture
            .Where(f => f.TenantId == tenantId && f.AziendaId == aziendaId && f.Anno == anno)
            .Select(f => new { f.CessionarioNazione, f.TotaleImponibile })
            .ToListAsync(ct);

        var righe = fatture
            .Where(f => PaesiUe.Contains(f.CessionarioNazione.Trim()))
            .GroupBy(f => f.CessionarioNazione.Trim().ToUpperInvariant())
            .Select(g => new IntrastatRiga(g.Key, g.Count(), Math.Round(g.Sum(x => x.TotaleImponibile), 2)))
            .OrderByDescending(r => r.TotaleImponibile)
            .ToList();

        return new IntrastatBozza(anno, righe.Sum(r => r.TotaleImponibile), righe);
    }
}
