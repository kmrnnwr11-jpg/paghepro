namespace Paghe.Core.Fatturazione;

/// <summary>
/// Dati anagrafici di un soggetto (cedente/prestatore o cessionario/committente)
/// usati nella fattura e nel tracciato FatturaPA.
/// </summary>
public sealed class DatiAnagrafici
{
    public required string Denominazione { get; init; }

    /// <summary>Partita IVA (senza prefisso paese). Almeno uno tra PartitaIva e CodiceFiscale.</summary>
    public string? PartitaIva { get; init; }

    public string? CodiceFiscale { get; init; }

    public required string Indirizzo { get; init; }
    public required string Cap { get; init; }
    public required string Comune { get; init; }
    public required string Provincia { get; init; }
    public string Nazione { get; init; } = "IT";

    /// <summary>Regime fiscale (codice FatturaPA, es. RF01 = ordinario).</summary>
    public string RegimeFiscale { get; init; } = "RF01";
}
