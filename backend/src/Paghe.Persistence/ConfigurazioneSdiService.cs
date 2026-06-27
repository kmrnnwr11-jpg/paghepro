using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dati di configurazione del canale SdI inviati dall'utente (self-service).</summary>
public sealed record ConfigurazioneSdiInput(
    string Modalita,
    string Provider = "",
    string BaseUrl = "",
    string? ApiKey = null,
    string EndpointInvio = "invoices",
    string EndpointEsito = "invoices/{id}",
    string CampoId = "id",
    string CampoStato = "status",
    bool FirmaLocale = false,
    bool Attivo = true);

/// <summary>
/// Gestisce la configurazione del canale SdI per ogni tenant (collegamento provider self-service),
/// isolata per tenant. L'ApiKey, se non re-inviata (null), viene conservata e non sovrascritta.
/// </summary>
public sealed class ConfigurazioneSdiService
{
    private readonly PagheDbContext _db;
    public ConfigurazioneSdiService(PagheDbContext db) => _db = db;

    public Task<ConfigurazioneSdiEntity?> GetAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.ConfigurazioniSdi.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

    public async Task<ConfigurazioneSdiEntity> SalvaAsync(
        Guid tenantId, ConfigurazioneSdiInput input, CancellationToken ct = default)
    {
        var c = await _db.ConfigurazioniSdi.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (c is null)
        {
            c = new ConfigurazioneSdiEntity { Id = Guid.NewGuid(), TenantId = tenantId };
            _db.ConfigurazioniSdi.Add(c);
        }

        c.Modalita = input.Modalita;
        c.Provider = input.Provider;
        c.BaseUrl = input.BaseUrl;
        // ApiKey null = l'utente non l'ha re-inserita: mantieni quella esistente (non perdere il segreto).
        if (input.ApiKey is not null)
            c.ApiKey = string.IsNullOrWhiteSpace(input.ApiKey) ? null : input.ApiKey;
        c.EndpointInvio = input.EndpointInvio;
        c.EndpointEsito = input.EndpointEsito;
        c.CampoId = input.CampoId;
        c.CampoStato = input.CampoStato;
        c.FirmaLocale = input.FirmaLocale;
        c.Attivo = input.Attivo;
        c.AggiornataIl = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return c;
    }
}
