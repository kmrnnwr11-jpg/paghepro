using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence;

/// <summary>
/// Bozza di Certificazione Unica (CU) per un rapporto di lavoro e un anno: aggrega i cedolini
/// archiviati dell'anno nei totali certificati. NON è il file telematico ufficiale dell'Agenzia
/// Entrate (quello richiede le specifiche annuali AE e l'invio via Entratel — vedi PIANO-DICHIARAZIONI.md).
/// </summary>
public sealed record CuBozza(
    string CodiceFiscale,
    string Nominativo,
    int Anno,
    int NumeroCedolini,
    decimal TotaleImponibileIrpef,
    decimal TotaleRitenuteIrpef,
    decimal TotaleAddizionaleRegionale,
    decimal TotaleAddizionaleComunale,
    decimal TotaleImponibileInps,
    decimal TotaleNetto);

public sealed class CertificazioneUnicaService
{
    private readonly PagheDbContext _db;
    public CertificazioneUnicaService(PagheDbContext db) => _db = db;

    /// <summary>Costruisce la bozza CU per il rapporto e l'anno indicati. Null se il rapporto non esiste per il tenant.</summary>
    public async Task<CuBozza?> CostruisciAsync(
        Guid tenantId, Guid rapportoId, int anno, CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti
            .Include(r => r.Dipendente)
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct);
        if (rapporto is null) return null;

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno)
            .ToListAsync(ct);

        var dip = rapporto.Dipendente;
        return new CuBozza(
            CodiceFiscale: dip?.CodiceFiscale ?? "",
            Nominativo: $"{dip?.Cognome} {dip?.Nome}".Trim(),
            Anno: anno,
            NumeroCedolini: cedolini.Count,
            TotaleImponibileIrpef: cedolini.Sum(c => c.ImponibileIrpef),
            TotaleRitenuteIrpef: cedolini.Sum(c => c.IrpefNetta),
            TotaleAddizionaleRegionale: cedolini.Sum(c => c.AddizionaleRegionale),
            TotaleAddizionaleComunale: cedolini.Sum(c => c.AddizionaleComunale),
            TotaleImponibileInps: cedolini.Sum(c => c.ImponibileInps),
            TotaleNetto: cedolini.Sum(c => c.NettoInBusta));
    }
}
