using System.Globalization;
using System.Text;
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

    /// <summary>
    /// Costruisce le bozze CU di tutti i dipendenti di un'azienda per l'anno: una CU per ciascun
    /// rapporto che ha almeno un cedolino archiviato nell'anno. Ordinate per cognome/nome.
    /// </summary>
    public async Task<IReadOnlyList<CuBozza>> CostruisciPerAziendaAsync(
        Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default)
    {
        var rapporti = await _db.Rapporti
            .Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId)
            .ToListAsync(ct);

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId && c.Anno == anno)
            .ToListAsync(ct);
        var perRapporto = cedolini.GroupBy(c => c.RapportoLavoroId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var bozze = new List<CuBozza>();
        foreach (var r in rapporti)
        {
            if (!perRapporto.TryGetValue(r.Id, out var cs) || cs.Count == 0)
                continue; // nessun cedolino nell'anno: nessuna CU da certificare

            var dip = r.Dipendente;
            bozze.Add(new CuBozza(
                CodiceFiscale: dip?.CodiceFiscale ?? "",
                Nominativo: $"{dip?.Cognome} {dip?.Nome}".Trim(),
                Anno: anno,
                NumeroCedolini: cs.Count,
                TotaleImponibileIrpef: cs.Sum(c => c.ImponibileIrpef),
                TotaleRitenuteIrpef: cs.Sum(c => c.IrpefNetta),
                TotaleAddizionaleRegionale: cs.Sum(c => c.AddizionaleRegionale),
                TotaleAddizionaleComunale: cs.Sum(c => c.AddizionaleComunale),
                TotaleImponibileInps: cs.Sum(c => c.ImponibileInps),
                TotaleNetto: cs.Sum(c => c.NettoInBusta)));
        }

        return bozze
            .OrderBy(b => b.Nominativo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Export CSV (bozza rivedibile) delle CU: separatore ';', decimali con punto (cultura invariante).
    /// NON è il file telematico ufficiale — serve al commercialista per controllare i totali certificati
    /// prima della generazione/controllo col software ufficiale dell'Agenzia Entrate.
    /// </summary>
    public static string EsportaCsv(IReadOnlyList<CuBozza> bozze)
    {
        static string N(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.AppendLine("CodiceFiscale;Nominativo;Anno;ImponibileIrpef;RitenuteIrpef;" +
                      "AddizionaleRegionale;AddizionaleComunale;ImponibileInps;Netto");
        foreach (var b in bozze)
            sb.AppendLine(string.Join(';',
                b.CodiceFiscale, b.Nominativo, b.Anno, N(b.TotaleImponibileIrpef), N(b.TotaleRitenuteIrpef),
                N(b.TotaleAddizionaleRegionale), N(b.TotaleAddizionaleComunale),
                N(b.TotaleImponibileInps), N(b.TotaleNetto)));
        return sb.ToString();
    }
}
