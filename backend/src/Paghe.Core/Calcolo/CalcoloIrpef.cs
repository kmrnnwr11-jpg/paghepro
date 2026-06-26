using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>Calcolo dell'IRPEF lorda per scaglioni e delle detrazioni da lavoro dipendente.</summary>
public static class CalcoloIrpef
{
    /// <summary>
    /// IRPEF lorda annua calcolata applicando le aliquote a scaglioni alla quota di
    /// reddito che cade in ciascuno scaglione. Non arrotondata.
    /// </summary>
    public static decimal IrpefLordaAnnua(decimal imponibileAnnuo, IReadOnlyList<ScaglioneIrpef> scaglioni)
    {
        if (imponibileAnnuo <= 0m)
            return 0m;

        decimal imposta = 0m;
        foreach (var s in scaglioni)
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

    /// <summary>
    /// Detrazione annua da lavoro dipendente (art. 13 TUIR), formula a fasce di reddito,
    /// rapportata ai giorni di spettanza. Comprende l'ulteriore detrazione per la fascia
    /// 25.000–35.000. Valori dei parametri come da disciplina vigente (verificare per anno).
    /// </summary>
    public static decimal DetrazioneLavoroDipendenteAnnua(decimal redditoComplessivoAnnuo, int giorni)
    {
        decimal detrazione;

        if (redditoComplessivoAnnuo <= 15000m)
            detrazione = 1955m;
        else if (redditoComplessivoAnnuo <= 28000m)
            detrazione = 1910m + 1190m * (28000m - redditoComplessivoAnnuo) / 13000m;
        else if (redditoComplessivoAnnuo <= 50000m)
            detrazione = 1910m * (50000m - redditoComplessivoAnnuo) / 22000m;
        else
            detrazione = 0m;

        // Ulteriore detrazione per la fascia 25.000–35.000.
        if (redditoComplessivoAnnuo > 25000m && redditoComplessivoAnnuo <= 35000m)
            detrazione += 65m;

        // Rapporto ai giorni di spettanza nell'anno.
        detrazione = detrazione * giorni / 365m;

        return detrazione < 0m ? 0m : detrazione;
    }
}
