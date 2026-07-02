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

    /// <summary>Campi correggibili dell'anagrafica azienda (es. dopo una visura letta male). Null = non cambiare.</summary>
    public sealed record CorrezioneAzienda(
        string? RagioneSociale = null, string? PartitaIva = null, string? CodiceFiscale = null,
        string? Indirizzo = null, string? Cap = null, string? Comune = null, string? Provincia = null);

    /// <summary>
    /// Corregge l'anagrafica di un'azienda (isolata per tenant): aggiorna solo i campi indicati.
    /// Pensata per rivedere ciò che l'import automatico (visura/OCR) ha letto male.
    /// Restituisce l'azienda aggiornata, null se non esiste per il tenant.
    /// </summary>
    public async Task<AziendaEntity?> CorreggiAsync(
        Guid tenantId, Guid id, CorrezioneAzienda c, CancellationToken ct = default)
    {
        var a = await GetAsync(tenantId, id, ct);
        if (a is null) return null;

        if (c.RagioneSociale is not null) a.RagioneSociale = c.RagioneSociale.Trim();
        if (c.PartitaIva is not null) a.PartitaIva = c.PartitaIva.Trim().Length == 0 ? null : c.PartitaIva.Trim();
        if (c.CodiceFiscale is not null) a.CodiceFiscale = c.CodiceFiscale.Trim().Length == 0 ? null : c.CodiceFiscale.Trim().ToUpperInvariant();
        if (c.Indirizzo is not null) a.Indirizzo = c.Indirizzo.Trim();
        if (c.Cap is not null) a.Cap = c.Cap.Trim();
        if (c.Comune is not null) a.Comune = c.Comune.Trim();
        if (c.Provincia is not null) a.Provincia = c.Provincia.Trim().ToUpperInvariant();

        await _db.SaveChangesAsync(ct);
        return a;
    }

    public Task<List<AziendaEntity>> ElencoAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Aziende.Where(a => a.TenantId == tenantId).OrderBy(a => a.RagioneSociale).ToListAsync(ct);
}
