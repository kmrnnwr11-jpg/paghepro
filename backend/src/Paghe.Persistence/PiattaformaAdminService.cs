using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence;

/// <summary>Sintesi di uno studio (tenant) per il pannello del super-admin di piattaforma.</summary>
public sealed record StudioSintesi(
    Guid Id, string RagioneSociale, bool Sospeso, int NumeroUtenti, DateTime CreatoIl);

/// <summary>
/// Operazioni del <b>super-admin di piattaforma</b> (proprietario del SaaS): vede TUTTI gli studi e
/// può sospenderli/riattivarli. Volutamente <b>non</b> filtrato per tenant — l'accesso a queste
/// operazioni va protetto a livello di endpoint (ruolo <c>superadmin</c>).
/// </summary>
public sealed class PiattaformaAdminService
{
    private readonly PagheDbContext _db;
    public PiattaformaAdminService(PagheDbContext db) => _db = db;

    public async Task<IReadOnlyList<StudioSintesi>> ElencaStudiAsync(CancellationToken ct = default)
    {
        var utentiPerStudio = await _db.Utenti
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.N, ct);

        var studi = await _db.Tenants.OrderBy(t => t.RagioneSociale).ToListAsync(ct);
        return studi.Select(t => new StudioSintesi(
            t.Id, t.RagioneSociale, t.Sospeso,
            utentiPerStudio.TryGetValue(t.Id, out var n) ? n : 0, t.CreatoIl)).ToList();
    }

    /// <summary>Sospende o riattiva uno studio. Restituisce false se lo studio non esiste.</summary>
    public async Task<bool> ImpostaSospensioneAsync(Guid studioId, bool sospeso, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == studioId, ct);
        if (tenant is null) return false;
        tenant.Sospeso = sospeso;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
