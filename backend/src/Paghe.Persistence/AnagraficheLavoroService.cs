using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Gestione di dipendenti e rapporti di lavoro, isolata per tenant.</summary>
public sealed class AnagraficheLavoroService
{
    private readonly PagheDbContext _db;

    public AnagraficheLavoroService(PagheDbContext db) => _db = db;

    /// <summary>Crea un dipendente e il relativo rapporto di lavoro per un'azienda.</summary>
    public async Task<RapportoLavoroEntity> CreaDipendenteERapportoAsync(
        Guid tenantId, Guid aziendaId, DipendenteEntity dipendente, RapportoLavoroEntity rapporto,
        CancellationToken ct = default)
    {
        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");

        dipendente.Id = dipendente.Id == Guid.Empty ? Guid.NewGuid() : dipendente.Id;
        dipendente.TenantId = tenantId;

        rapporto.Id = Guid.NewGuid();
        rapporto.TenantId = tenantId;
        rapporto.AziendaId = aziendaId;
        rapporto.DipendenteId = dipendente.Id;

        _db.Dipendenti.Add(dipendente);
        _db.Rapporti.Add(rapporto);
        await _db.SaveChangesAsync(ct);
        return rapporto;
    }

    public Task<List<RapportoLavoroEntity>> ElencoRapportiAsync(
        Guid tenantId, Guid aziendaId, CancellationToken ct = default) =>
        _db.Rapporti.Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId)
            .ToListAsync(ct);

    public Task<RapportoLavoroEntity?> GetRapportoAsync(
        Guid tenantId, Guid rapportoId, CancellationToken ct = default) =>
        _db.Rapporti.Include(r => r.Dipendente)
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct);
}
