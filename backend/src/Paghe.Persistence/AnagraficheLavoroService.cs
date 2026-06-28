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

    /// <summary>
    /// GDPR — diritto all'oblio (art. 17): anonimizza i dati personali del dipendente (nome, cognome,
    /// codice fiscale, IBAN) <b>mantenendo</b> i record contabili collegati (cedolini), soggetti a
    /// conservazione fiscale obbligatoria. È idempotente e isolato per tenant; l'audit log registra
    /// automaticamente l'operazione. Restituisce false se il dipendente non esiste.
    /// </summary>
    public async Task<bool> AnonimizzaDipendenteAsync(
        Guid tenantId, Guid dipendenteId, CancellationToken ct = default)
    {
        var dip = await _db.Dipendenti
            .FirstOrDefaultAsync(d => d.Id == dipendenteId && d.TenantId == tenantId, ct);
        if (dip is null) return false;
        if (dip.Anonimizzato) return true; // già anonimizzato: idempotente

        dip.Nome = "(anonimizzato)";
        dip.Cognome = "(anonimizzato)";
        dip.CodiceFiscale = $"ANON-{Guid.NewGuid():N}"; // resta univoco per (tenant, codice fiscale)
        dip.Iban = null;
        dip.Anonimizzato = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
