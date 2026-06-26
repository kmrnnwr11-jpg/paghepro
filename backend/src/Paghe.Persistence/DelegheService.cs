using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dati per registrare una delega di un cliente verso l'intermediario.</summary>
public sealed record DelegaInput(
    Guid AziendaId,
    string TipoAdempimento,
    DateOnly DataInizio,
    DateOnly? DataFine = null,
    string? Riferimento = null,
    string? Note = null);

/// <summary>
/// Gestione delle deleghe intermediario→cliente per adempimento, isolata per tenant.
/// La trasmissione per conto di un cliente deve essere autorizzata da una delega attiva
/// (<see cref="EsisteDelegaAttivaAsync"/>).
/// </summary>
public sealed class DelegheService
{
    private readonly PagheDbContext _db;

    public DelegheService(PagheDbContext db) => _db = db;

    public async Task<DelegaEntity> CreaAsync(Guid tenantId, DelegaInput input, CancellationToken ct = default)
    {
        bool aziendaOk = await _db.Aziende.AnyAsync(a => a.Id == input.AziendaId && a.TenantId == tenantId, ct);
        if (!aziendaOk)
            throw new ArgumentException("Azienda non trovata per il tenant indicato.");
        if (string.IsNullOrWhiteSpace(input.TipoAdempimento))
            throw new ArgumentException("Il tipo di adempimento della delega è obbligatorio.");
        if (input.DataFine is { } fine && fine < input.DataInizio)
            throw new ArgumentException("La data fine delega non può precedere la data inizio.");

        var entity = new DelegaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = input.AziendaId,
            TipoAdempimento = input.TipoAdempimento,
            Stato = "Attiva",
            DataInizio = input.DataInizio,
            DataFine = input.DataFine,
            Riferimento = input.Riferimento,
            Note = input.Note,
            CreataIl = DateTime.UtcNow,
        };
        _db.Deleghe.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<List<DelegaEntity>> ElencoAsync(Guid tenantId, Guid? aziendaId = null, CancellationToken ct = default) =>
        _db.Deleghe
            .Where(d => d.TenantId == tenantId && (aziendaId == null || d.AziendaId == aziendaId))
            .OrderByDescending(d => d.CreataIl)
            .ToListAsync(ct);

    public Task<DelegaEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Deleghe.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);

    public async Task<DelegaEntity> RevocaAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var d = await _db.Deleghe.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Delega non trovata per il tenant indicato.");
        d.Stato = "Revocata";
        if (d.DataFine is null || d.DataFine > DateOnly.FromDateTime(DateTime.UtcNow))
            d.DataFine = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return d;
    }

    /// <summary>Vero se esiste una delega attiva (non revocata, nel periodo di validità) per l'adempimento.</summary>
    public async Task<bool> EsisteDelegaAttivaAsync(
        Guid tenantId, Guid aziendaId, string tipoAdempimento, DateOnly? alla = null, CancellationToken ct = default)
    {
        var data = alla ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return await _db.Deleghe.AnyAsync(d =>
            d.TenantId == tenantId &&
            d.AziendaId == aziendaId &&
            d.TipoAdempimento == tipoAdempimento &&
            d.Stato == "Attiva" &&
            d.DataInizio <= data &&
            (d.DataFine == null || d.DataFine >= data), ct);
    }
}
