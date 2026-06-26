using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Servizio per la gestione delle anagrafiche aziende, isolato per tenant.</summary>
public sealed class AziendeService
{
    private readonly PagheDbContext _db;

    public AziendeService(PagheDbContext db) => _db = db;

    public async Task<AziendaEntity> CreaAsync(Guid tenantId, AziendaEntity azienda, CancellationToken ct = default)
    {
        azienda.Id = azienda.Id == Guid.Empty ? Guid.NewGuid() : azienda.Id;
        azienda.TenantId = tenantId;
        _db.Aziende.Add(azienda);
        await _db.SaveChangesAsync(ct);
        return azienda;
    }

    public Task<AziendaEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Aziende.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct);

    public Task<List<AziendaEntity>> ElencoAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Aziende.Where(a => a.TenantId == tenantId).OrderBy(a => a.RagioneSociale).ToListAsync(ct);
}
