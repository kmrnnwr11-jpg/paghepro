using System.Text.Json;

namespace Paghe.Persistence;

/// <summary>
/// Legge il dataset ufficiale dei minimi tabellari CCNL (docs/dati/minimi-ccnl-*.json,
/// fonti CNEL/associazioni) incorporato come risorsa, nel formato di <see cref="CcnlImportService"/>.
/// Caricato all'avvio, sovrascrive i valori d'esempio del Seed con quelli reali.
/// </summary>
public static class CcnlDatasetUfficiale
{
    private sealed record LivelloJson(
        string Livello, string? Descrizione, decimal? MinimoTabellare, decimal? Edr, decimal? ImportoScatto);
    private sealed record CcnlJson(
        string Codice, string? Descrizione, int Anno, string? Fonte, List<LivelloJson> Livelli);

    /// <summary>Tutti i CCNL del dataset incorporato (tutti gli anni disponibili).</summary>
    public static List<CcnlInput> Carica()
    {
        var asm = typeof(CcnlDatasetUfficiale).Assembly;
        var risultato = new List<CcnlInput>();

        foreach (var risorsa in asm.GetManifestResourceNames()
                     .Where(n => n.Contains("minimi-ccnl-", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = asm.GetManifestResourceStream(risorsa)!;
            var lista = JsonSerializer.Deserialize<List<CcnlJson>>(
                stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            risultato.AddRange(lista.Select(c => new CcnlInput(
                c.Codice,
                c.Descrizione ?? c.Codice,
                c.Anno,
                c.Livelli
                    // Senza minimo tabellare il livello non è calcolabile (es. 6J logistica,
                    // non riportato nella tabella CNEL): non lo si importa.
                    .Where(l => l.MinimoTabellare is not null)
                    .Select(l => new LivelloCcnlInput(
                        l.Livello, l.Descrizione ?? l.Livello,
                        l.MinimoTabellare!.Value, l.Edr ?? 0m, l.ImportoScatto ?? 0m))
                    .ToList())));
        }

        return risultato;
    }
}
