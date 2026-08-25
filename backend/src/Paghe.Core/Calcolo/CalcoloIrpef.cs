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
    ///
    /// <paramref name="minimoAnnuo"/> è il minimo di detrazione previsto dallo stesso art. 13 c.1
    /// (690 €, 1.380 € per i rapporti a tempo determinato): non si rapporta ai giorni, quindi va
    /// applicato dopo il ragguaglio. Lasciato a zero non cambia nulla per chi non lo indica.
    /// </summary>
    public static decimal DetrazioneLavoroDipendenteAnnua(
        decimal redditoComplessivoAnnuo, int giorni, decimal minimoAnnuo = 0m)
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

        // Il minimo di legge vale solo finché una detrazione spetta: a reddito troppo alto
        // la detrazione è zero e resta zero.
        if (minimoAnnuo > 0m && detrazione > 0m && detrazione < minimoAnnuo)
            detrazione = minimoAnnuo;

        return detrazione < 0m ? 0m : detrazione;
    }

    /// <summary>
    /// Ulteriore detrazione del cuneo fiscale (L. 207/2024 art. 1 c. 6, Circ. AdE 4/E/2025):
    /// <paramref name="importoAnnuo"/> pieno per i redditi oltre <paramref name="redditoOltre"/> e
    /// fino a <paramref name="redditoFinoA"/>, poi decrescente fino ad azzerarsi a
    /// <paramref name="redditoAzzeramento"/>. Rapportata ai giorni di spettanza come la
    /// detrazione principale. Le soglie sono parametri versionati per anno.
    /// </summary>
    public static decimal UlterioreDetrazioneCuneoAnnua(
        decimal redditoComplessivoAnnuo, int giorni,
        decimal importoAnnuo, decimal redditoOltre, decimal redditoFinoA, decimal redditoAzzeramento)
    {
        if (importoAnnuo <= 0m
            || redditoComplessivoAnnuo <= redditoOltre
            || redditoComplessivoAnnuo >= redditoAzzeramento)
            return 0m;

        decimal detrazione = redditoComplessivoAnnuo <= redditoFinoA
            ? importoAnnuo
            : importoAnnuo * (redditoAzzeramento - redditoComplessivoAnnuo)
                / (redditoAzzeramento - redditoFinoA);

        return detrazione * giorni / 365m;
    }
}
