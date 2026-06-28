namespace Paghe.Core.Calcolo;

/// <summary>
/// Ratei delle mensilità aggiuntive (13ª e 14ª). Una mensilità aggiuntiva matura per competenza,
/// in quote mensili pari a 1/<paramref name="mesiPeriodo"/> della retribuzione mensile utile
/// (12 mesi per la 13ª; per la 14ª il periodo di maturazione dipende dal CCNL, di norma 12 mesi
/// con finestra luglio–giugno). All'erogazione si liquida l'intera mensilità maturata.
/// </summary>
public static class MensilitaAggiuntive
{
    /// <summary>Quota (rateo) maturata in un mese: retribuzione mensile utile / mesi del periodo.</summary>
    public static decimal RateoMensile(decimal retribuzioneMensileUtile, int mesiPeriodo = 12)
    {
        if (mesiPeriodo <= 0)
            throw new ArgumentException("I mesi del periodo devono essere positivi.", nameof(mesiPeriodo));
        return Arrotondamento.Euro(retribuzioneMensileUtile / mesiPeriodo);
    }

    /// <summary>Importo maturato dopo <paramref name="mesiMaturati"/> mesi del periodo di competenza.</summary>
    public static decimal Maturato(decimal retribuzioneMensileUtile, int mesiMaturati, int mesiPeriodo = 12)
    {
        if (mesiPeriodo <= 0)
            throw new ArgumentException("I mesi del periodo devono essere positivi.", nameof(mesiPeriodo));
        if (mesiMaturati < 0)
            throw new ArgumentException("I mesi maturati non possono essere negativi.", nameof(mesiMaturati));
        return Arrotondamento.Euro(retribuzioneMensileUtile * mesiMaturati / mesiPeriodo);
    }
}
