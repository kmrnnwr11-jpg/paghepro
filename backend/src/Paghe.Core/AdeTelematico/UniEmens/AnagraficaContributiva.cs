using System.Text.RegularExpressions;

namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Dati di inquadramento contributivo che il tracciato UniEmens chiede sul lavoratore e che
/// non si ricavano dal cedolino. Qui stanno solo le regole di forma: se un codice è ammesso
/// nel merito lo dice l'INPS, che confronta i suoi archivi al momento del controllo.
///
/// Il principio del prodotto resta quello: un dato che non c'è si chiede, non si inventa.
/// Queste funzioni servono a rifiutare subito un valore scritto male, non a riempire buchi.
/// </summary>
public static partial class AnagraficaContributiva
{
    /// <summary>Tipi di part-time del tracciato (Qualifica2): orizzontale, verticale, misto.</summary>
    public static readonly IReadOnlyDictionary<string, string> TipiPartTime =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["P"] = "Orizzontale (meno ore tutti i giorni)",
            ["V"] = "Verticale (giornate intere, ma non tutte)",
            ["M"] = "Misto (giorni ridotti e giornate non lavorate)",
        };

    /// <summary>
    /// Normalizza il tipo di part-time. Il tempo pieno usa "F" e lo scrive il generatore da
    /// sé: qui si accettano soltanto i tre codici del tempo parziale.
    /// </summary>
    public static string? TipoPartTime(string? valore)
    {
        string v = (valore ?? "").Trim().ToUpperInvariant();
        return TipiPartTime.ContainsKey(v) ? v : null;
    }

    /// <summary>
    /// Cittadinanza: tre cifre nella codifica delle Comunicazioni Obbligatorie (la stessa
    /// dell'UNILAV). È un elemento obbligatorio del tracciato.
    /// </summary>
    public static string? Cittadinanza(string? valore)
    {
        string v = (valore ?? "").Trim();
        return v.Length == 3 && v.All(char.IsAsciiDigit) ? v : null;
    }

    /// <summary>
    /// Tipo di contribuzione: due caratteri, per le situazioni particolari (apprendisti,
    /// lavoro intermittente, agevolazioni). Nel rapporto ordinario non si espone.
    /// </summary>
    public static string? TipoContribuzione(string? valore)
    {
        string v = (valore ?? "").Trim().ToUpperInvariant();
        return v.Length == 2 && v.All(char.IsAsciiLetterOrDigit) ? v : null;
    }

    /// <summary>
    /// Codice della qualifica professionale ISTAT (classificazione delle professioni:
    /// CP2011 fino alla competenza 04/2025, CP2021 da 05/2025). Elemento opzionale.
    /// I due formati ammessi dallo schema sono "8.1.3.3.0.2" e "8.1.3.3.02".
    /// </summary>
    public static string? QualificaProfessionaleIstat(string? valore)
    {
        string v = (valore ?? "").Trim();
        return FormatoQualProf().IsMatch(v) ? v : null;
    }

    // Gli stessi due pattern dello schema ufficiale, non una versione «quasi uguale».
    [GeneratedRegex(@"^[1-9]\.[0-9]\.[0-9]\.[0-9]\.([0-9]\.[0-9]{1,2}|[0-9]{1,2})$")]
    private static partial Regex FormatoQualProf();
}
