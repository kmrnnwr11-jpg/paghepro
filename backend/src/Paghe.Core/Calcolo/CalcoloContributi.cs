using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>Calcolo dei contributi previdenziali INPS sull'imponibile del mese.</summary>
public static class CalcoloContributi
{
    /// <summary>
    /// Restituisce la quota a carico del dipendente e la quota a carico del datore,
    /// applicando le rispettive aliquote all'imponibile previdenziale.
    /// </summary>
    public static (decimal dipendente, decimal datore) Calcola(
        decimal imponibileInps, AliquotaInps aliquota)
    {
        var dipendente = Arrotondamento.Euro(imponibileInps * aliquota.Dipendente);
        var datore = Arrotondamento.Euro(imponibileInps * aliquota.Datore);
        return (dipendente, datore);
    }

    /// <summary>
    /// Quota INPS c/dipendente <b>effettiva</b>: aliquota base (es. 9,19% operaio) + 0,30% di contributo
    /// CIG per le aziende &gt;15 dipendenti + eventuali <b>fondi di settore</b> (es. FIS 0,2667% nel
    /// Multiservizi, FSBS 0,15% nella somministrazione). Ricavati e verificati su buste reali.
    /// </summary>
    public static decimal QuotaInpsDipendente(
        decimal aliquotaBaseDipendente, bool aziendaOltre15Dipendenti, decimal fondiSettore = 0m) =>
        aliquotaBaseDipendente + (aziendaOltre15Dipendenti ? 0.003m : 0m) + fondiSettore;
}
