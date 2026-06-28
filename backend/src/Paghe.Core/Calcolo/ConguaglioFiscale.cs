using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>
/// Esito del conguaglio fiscale di fine anno: IRPEF dovuta sull'anno a confronto con quella già
/// trattenuta mese per mese. <see cref="Conguaglio"/> &gt; 0 = <b>a debito</b> (da trattenere in più),
/// &lt; 0 = <b>a credito</b> (da rimborsare al dipendente).
/// </summary>
public sealed record RisultatoConguaglio(
    decimal IrpefLordaAnnua,
    decimal DetrazioniSpettanti,
    decimal IrpefNettaAnnua,
    decimal IrpefGiaTrattenuta,
    decimal Conguaglio,
    bool ADebito);

/// <summary>
/// Conguaglio fiscale IRPEF di fine anno (o alla cessazione): si ricalcola l'IRPEF netta dovuta
/// sull'imponibile annuo effettivo e la si confronta con quanto già trattenuto col metodo presuntivo
/// mensile. Calcolatore puro: scaglioni e detrazioni sono <b>parametri</b> (versionati per anno).
/// </summary>
public static class ConguaglioFiscale
{
    public static RisultatoConguaglio Calcola(
        decimal imponibileIrpefAnnuo,
        decimal irpefGiaTrattenuta,
        decimal detrazioniSpettanti,
        IReadOnlyList<ScaglioneIrpef> scaglioni)
    {
        decimal lorda = Arrotondamento.Euro(CalcoloIrpef.IrpefLordaAnnua(imponibileIrpefAnnuo, scaglioni));
        decimal detrazioni = Arrotondamento.Euro(detrazioniSpettanti);
        decimal netta = Math.Max(0m, Arrotondamento.Euro(lorda - detrazioni));
        decimal trattenuta = Arrotondamento.Euro(irpefGiaTrattenuta);
        decimal conguaglio = netta - trattenuta;
        return new RisultatoConguaglio(lorda, detrazioni, netta, trattenuta, conguaglio, conguaglio > 0m);
    }
}
