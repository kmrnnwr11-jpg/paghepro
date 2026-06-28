using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence;

/// <summary>Riga per percipiente del 770 (totali ritenute dell'anno).</summary>
public sealed record Mod770Percipiente(
    string Nominativo,
    string CodiceFiscale,
    decimal RitenuteIrpef,
    decimal AddizionaleRegionale,
    decimal AddizionaleComunale);

/// <summary>
/// Bozza del Modello 770 (sostituto d'imposta): aggrega, per azienda e anno, le ritenute operate
/// sui cedolini (IRPEF + addizionali), in totale e per percipiente. NON è il file telematico
/// ufficiale (serve la specifica AE e l'invio via Entratel — vedi PIANO-DICHIARAZIONI.md, Fase 2).
/// </summary>
public sealed record Mod770Bozza(
    int Anno,
    int NumeroPercipienti,
    int NumeroCedolini,
    decimal TotaleRitenuteIrpef,
    decimal TotaleAddizionaleRegionale,
    decimal TotaleAddizionaleComunale,
    IReadOnlyList<Mod770Percipiente> Percipienti);

public sealed class Modello770Service
{
    private readonly PagheDbContext _db;
    public Modello770Service(PagheDbContext db) => _db = db;

    /// <summary>Costruisce la bozza 770 per azienda e anno, isolata per tenant.</summary>
    public async Task<Mod770Bozza> CostruisciAsync(
        Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default)
    {
        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId && c.Anno == anno)
            .ToListAsync(ct);

        var rapporti = await _db.Rapporti
            .Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId)
            .ToListAsync(ct);
        var anagrafica = rapporti.ToDictionary(
            r => r.Id,
            r => ($"{r.Dipendente?.Cognome} {r.Dipendente?.Nome}".Trim(), r.Dipendente?.CodiceFiscale ?? ""));

        var percipienti = cedolini
            .GroupBy(c => c.RapportoLavoroId)
            .Select(g =>
            {
                var (nome, cf) = anagrafica.TryGetValue(g.Key, out var a) ? a : ("", "");
                return new Mod770Percipiente(
                    Nominativo: nome,
                    CodiceFiscale: cf,
                    RitenuteIrpef: g.Sum(c => c.IrpefNetta),
                    AddizionaleRegionale: g.Sum(c => c.AddizionaleRegionale),
                    AddizionaleComunale: g.Sum(c => c.AddizionaleComunale));
            })
            .OrderBy(p => p.Nominativo)
            .ToList();

        return new Mod770Bozza(
            Anno: anno,
            NumeroPercipienti: percipienti.Count,
            NumeroCedolini: cedolini.Count,
            TotaleRitenuteIrpef: cedolini.Sum(c => c.IrpefNetta),
            TotaleAddizionaleRegionale: cedolini.Sum(c => c.AddizionaleRegionale),
            TotaleAddizionaleComunale: cedolini.Sum(c => c.AddizionaleComunale),
            Percipienti: percipienti);
    }

    /// <summary>
    /// Export CSV (bozza rivedibile) dei percipienti del 770: separatore ';', decimali con punto
    /// (cultura invariante). NON è il file telematico ufficiale — serve al commercialista per
    /// controllare le ritenute certificate prima della generazione/controllo col software ufficiale AE.
    /// </summary>
    public static string EsportaCsv(Mod770Bozza bozza)
    {
        static string N(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.AppendLine("Nominativo;CodiceFiscale;RitenuteIrpef;AddizionaleRegionale;AddizionaleComunale");
        foreach (var p in bozza.Percipienti)
            sb.AppendLine(string.Join(';',
                p.Nominativo, p.CodiceFiscale, N(p.RitenuteIrpef),
                N(p.AddizionaleRegionale), N(p.AddizionaleComunale)));
        return sb.ToString();
    }
}
