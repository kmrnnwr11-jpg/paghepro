using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>
/// Calcolo dell'addizionale IRPEF (regionale o comunale) dovuta sull'anno, secondo il regime
/// del comune/regione: soglia di esenzione + scaglioni progressivi (come pubblicato dal MEF).
/// Calcolatore puro: il regime è un parametro versionato per anno.
/// </summary>
public static class CalcoloAddizionali
{
    /// <summary>
    /// Addizionale annua dovuta. Regole:
    /// <list type="bullet">
    /// <item>se il regime non ha scaglioni → 0 (comune/regione senza addizionale);</item>
    /// <item>se l'imponibile ≤ soglia di esenzione → 0 (esenzione "a soglia": sotto non si paga nulla);</item>
    /// <item>altrimenti si applica l'aliquota di ciascuno scaglione alla quota di reddito che vi ricade
    /// (progressivo). L'aliquota unica è un solo scaglione [0, +∞), quindi si applica all'intero imponibile.</item>
    /// </list>
    /// </summary>
    public static decimal Dovuta(decimal imponibileAnnuo, RegimeAddizionale regime)
    {
        if (regime is null || regime.Scaglioni.Count == 0)
            return 0m;
        if (imponibileAnnuo <= 0m || imponibileAnnuo <= regime.SogliaEsenzione)
            return 0m;

        decimal imposta = 0m;
        foreach (var s in regime.Scaglioni)
        {
            if (imponibileAnnuo <= s.DaImporto)
                break;
            decimal limiteSuperiore = s.AImporto ?? imponibileAnnuo;
            decimal quotaNelloScaglione = Math.Min(imponibileAnnuo, limiteSuperiore) - s.DaImporto;
            if (quotaNelloScaglione > 0m)
                imposta += quotaNelloScaglione * s.Aliquota;
        }
        return imposta;
    }
}
