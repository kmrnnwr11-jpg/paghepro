using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dati di una persona in ingresso (creazione o aggiornamento).</summary>
public sealed record PersonaInput(
    string Cognome,
    string Nome,
    string CodiceFiscale,
    string? Sesso = null,
    DateOnly? DataNascita = null,
    string? ComuneNascita = null,
    string? ProvinciaNascita = null,
    string? IndirizzoResidenza = null,
    string? CapResidenza = null,
    string? ComuneResidenza = null,
    string? ProvinciaResidenza = null,
    string? Telefono = null,
    string? Email = null);

/// <summary>
/// Archivio persone fisiche del tenant: si registrano una volta e si riusano per compilare
/// in automatico i moduli ufficiali. Isolato per tenant.
/// </summary>
public sealed class PersoneService
{
    private readonly PagheDbContext _db;
    public PersoneService(PagheDbContext db) => _db = db;

    public Task<List<PersonaEntity>> ElencoAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Persone.Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Cognome).ThenBy(p => p.Nome).ToListAsync(ct);

    public Task<PersonaEntity?> OttieniAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Persone.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task<PersonaEntity> CreaAsync(Guid tenantId, PersonaInput input, CancellationToken ct = default)
    {
        string cf = NormalizzaCf(input.CodiceFiscale);
        bool duplicato = await _db.Persone.AnyAsync(
            p => p.TenantId == tenantId && p.CodiceFiscale == cf, ct);
        if (duplicato)
            throw new InvalidOperationException($"Esiste già una persona con codice fiscale {cf}.");

        var e = new PersonaEntity { Id = Guid.NewGuid(), TenantId = tenantId, CreataIl = DateTimeOffset.UtcNow };
        Applica(e, input, cf);
        _db.Persone.Add(e);
        await _db.SaveChangesAsync(ct);
        return e;
    }

    public async Task<PersonaEntity?> AggiornaAsync(
        Guid tenantId, Guid id, PersonaInput input, CancellationToken ct = default)
    {
        var e = await OttieniAsync(tenantId, id, ct);
        if (e is null) return null;

        string cf = NormalizzaCf(input.CodiceFiscale);
        bool duplicato = await _db.Persone.AnyAsync(
            p => p.TenantId == tenantId && p.CodiceFiscale == cf && p.Id != id, ct);
        if (duplicato)
            throw new InvalidOperationException($"Esiste già un'altra persona con codice fiscale {cf}.");

        Applica(e, input, cf);
        await _db.SaveChangesAsync(ct);
        return e;
    }

    public async Task<bool> EliminaAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var e = await OttieniAsync(tenantId, id, ct);
        if (e is null) return false;
        _db.Persone.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Applica(PersonaEntity e, PersonaInput input, string cf)
    {
        e.Cognome = input.Cognome.Trim();
        e.Nome = input.Nome.Trim();
        e.CodiceFiscale = cf;
        e.Sesso = Pulisci(input.Sesso)?.ToUpperInvariant();
        e.DataNascita = input.DataNascita;
        e.ComuneNascita = Pulisci(input.ComuneNascita);
        e.ProvinciaNascita = Pulisci(input.ProvinciaNascita)?.ToUpperInvariant();
        e.IndirizzoResidenza = Pulisci(input.IndirizzoResidenza);
        e.CapResidenza = Pulisci(input.CapResidenza);
        e.ComuneResidenza = Pulisci(input.ComuneResidenza);
        e.ProvinciaResidenza = Pulisci(input.ProvinciaResidenza)?.ToUpperInvariant();
        e.Telefono = Pulisci(input.Telefono);
        e.Email = Pulisci(input.Email);
    }

    private static string NormalizzaCf(string cf) =>
        (cf ?? "").Trim().ToUpperInvariant();

    private static string? Pulisci(string? testo) =>
        string.IsNullOrWhiteSpace(testo) ? null : testo.Trim();
}
