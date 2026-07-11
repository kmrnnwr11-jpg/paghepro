using Paghe.Core.Domain;
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

    /// <summary>
    /// Applica il conguaglio al cedolino (tipicamente quello di dicembre): aggiunge la voce
    /// CONGUAGLIO_IRPEF (trattenuta se a debito, competenza se a credito) e aggiorna totali,
    /// netto e IrpefNetta. Con un credito ampio l'IrpefNetta del mese può diventare negativa
    /// (rimborso maggiore dell'imposta del mese). Il costo azienda non cambia: è imposta del
    /// lavoratore. Helper puro, riusato identico alla rigenerazione del PDF.
    /// </summary>
    public static void ApplicaACedolino(Cedolino cedolino, decimal conguaglio)
    {
        if (conguaglio == 0m)
            return;

        bool aDebito = conguaglio > 0m;
        cedolino.Righe.Add(new CedolinoRiga
        {
            Codice = "CONGUAGLIO_IRPEF",
            Descrizione = $"Conguaglio IRPEF fine anno ({(aDebito ? "a debito" : "a credito")})",
            Tipo = aDebito ? TipoVoce.Trattenuta : TipoVoce.Competenza,
            Importo = -conguaglio,
        });

        if (aDebito)
            cedolino.TotaleTrattenute = Arrotondamento.Euro(cedolino.TotaleTrattenute + conguaglio);
        else
            cedolino.TotaleCompetenze = Arrotondamento.Euro(cedolino.TotaleCompetenze - conguaglio);

        cedolino.IrpefNetta = Arrotondamento.Euro(cedolino.IrpefNetta + conguaglio);
        cedolino.NettoInBusta = Arrotondamento.Euro(cedolino.NettoInBusta - conguaglio);
    }
}
