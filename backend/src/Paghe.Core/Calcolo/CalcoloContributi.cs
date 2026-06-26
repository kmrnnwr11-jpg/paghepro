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
}
