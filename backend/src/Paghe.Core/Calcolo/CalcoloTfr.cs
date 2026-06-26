namespace Paghe.Core.Calcolo;

/// <summary>Calcolo della quota di TFR maturata.</summary>
public static class CalcoloTfr
{
    /// <summary>
    /// Quota mensile di accantonamento TFR.
    /// TFR annuo = retribuzione utile annua / 13,5, al netto del contributo dello 0,50%
    /// trattenuto a favore del fondo. La quota mensile è 1/12 della quota annua.
    /// (La rivalutazione annua del fondo è gestita a fine anno, fuori da questo metodo.)
    /// </summary>
    public static decimal QuotaMensile(
        decimal retribuzioneUtileMensile,
        int numeroMensilita,
        decimal divisore,
        decimal contributoFondo)
    {
        decimal retribuzioneAnnua = retribuzioneUtileMensile * numeroMensilita;
        decimal quotaAnnua = retribuzioneAnnua / divisore;
        decimal contributo = retribuzioneAnnua * contributoFondo;
        decimal quotaNetta = quotaAnnua - contributo;
        return Arrotondamento.Euro(quotaNetta / 12m);
    }
}
