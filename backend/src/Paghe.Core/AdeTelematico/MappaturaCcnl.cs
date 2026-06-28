using System.Text.RegularExpressions;

namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Mappa il CCNL letto da un UNILAV (codice CNEL + descrizione, es. "0190 - AGRICOLTURA - Operai
/// florovivaisti") al codice del CCNL <b>interno</b> configurato nel motore, così il cedolino è
/// calcolabile sul contratto giusto. Riconosce per codice CNEL e, in fallback, per parole chiave.
/// Estendibile: basta aggiungere righe alle tabelle.
/// </summary>
public static class MappaturaCcnl
{
    /// <summary>CCNL interno riconosciuto + livello di default (quando quello UNILAV non è un livello valido).</summary>
    public sealed record CcnlRiconosciuto(string CodiceInterno, string LivelloDefault);

    // Codice CNEL (prefisso numerico dell'UNILAV) -> CCNL interno + livello di default.
    private static readonly (string codiceCnel, string interno, string livello)[] PerCodice =
    {
        ("0190", "AGRICOLTURA_OPERAI", "3"),
        ("7370", "TRASPORTI_LOGISTICA", "3"),
    };

    // Parola chiave nella descrizione -> CCNL interno + livello di default.
    private static readonly (string parola, string interno, string livello)[] PerParola =
    {
        ("florovivaist", "AGRICOLTURA_OPERAI", "3"),
        ("agricol", "AGRICOLTURA_OPERAI", "3"),
        ("logistic", "TRASPORTI_LOGISTICA", "3"),
        ("trasporto", "TRASPORTI_LOGISTICA", "3"),
        ("spedizion", "TRASPORTI_LOGISTICA", "3"),
        ("edil", "EDILIZIA_INDUSTRIA", "2"),
        ("metalmecc", "METALMECCANICI_INDUSTRIA", "3"),
        ("turismo", "TURISMO_PUBBLICI_ESERCIZI", "4"),
        ("pubblici esercizi", "TURISMO_PUBBLICI_ESERCIZI", "4"),
        ("multiserviz", "MULTISERVIZI_PULIZIE", "3"),
        ("pulizi", "MULTISERVIZI_PULIZIE", "3"),
        ("studi professional", "STUDI_PROFESSIONALI", "3"),
        ("studio professional", "STUDI_PROFESSIONALI", "3"),
        ("chimic", "CHIMICO_FARMACEUTICO", "2"),
        ("farmaceutic", "CHIMICO_FARMACEUTICO", "2"),
        ("aliment", "ALIMENTARE_INDUSTRIA", "3"),
        ("commercio", "COMMERCIO_TERZIARIO", "3"),
        ("terziario", "COMMERCIO_TERZIARIO", "3"),
    };

    /// <summary>Riconosce il CCNL interno dal testo UNILAV, oppure null se non mappabile.</summary>
    public static CcnlRiconosciuto? Riconosci(string? ccnlUnilav)
    {
        if (string.IsNullOrWhiteSpace(ccnlUnilav))
            return null;

        // Se è già un codice interno noto, restituiscilo così com'è.
        var diretto = PerCodice.Concat(PerParola.Select(p => (p.parola, p.interno, p.livello)))
            .Select(x => x.interno).Append("COMMERCIO_TERZIARIO").Distinct()
            .FirstOrDefault(c => string.Equals(c, ccnlUnilav.Trim(), StringComparison.OrdinalIgnoreCase));
        if (diretto is not null)
            return new CcnlRiconosciuto(diretto, "COMMERCIO_TERZIARIO" == diretto ? "3" : LivelloDefault(diretto));

        var codice = Regex.Match(ccnlUnilav, @"\d{3,4}").Value;
        foreach (var (cnel, interno, liv) in PerCodice)
            if (codice == cnel)
                return new CcnlRiconosciuto(interno, liv);

        var testo = ccnlUnilav.ToLowerInvariant();
        foreach (var (parola, interno, liv) in PerParola)
            if (testo.Contains(parola))
                return new CcnlRiconosciuto(interno, liv);

        return null;
    }

    private static string LivelloDefault(string interno) =>
        PerParola.FirstOrDefault(p => p.interno == interno).livello
        ?? PerCodice.FirstOrDefault(p => p.interno == interno).livello
        ?? "1";
}
