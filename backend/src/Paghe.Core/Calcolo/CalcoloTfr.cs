using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>Esito della rivalutazione annua del fondo TFR (art. 2120 c.c.) con imposta sostitutiva.</summary>
public sealed record RivalutazioneTfr(
    decimal Coefficiente,
    decimal RivalutazioneLorda,
    decimal ImpostaSostitutiva,
    decimal RivalutazioneNetta,
    decimal FondoRivalutato);

/// <summary>Esito della tassazione separata del TFR alla cessazione (art. 19 TUIR).</summary>
public sealed record TassazioneSeparataTfr(
    decimal RedditoRiferimento,
    decimal AliquotaMedia,
    decimal Imposta,
    decimal NettoTfr);

/// <summary>Calcolo della quota di TFR maturata, della rivalutazione annua e della tassazione separata.</summary>
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

    /// <summary>
    /// Rivalutazione annua del fondo TFR accantonato (art. 2120 c.c.): coefficiente =
    /// <b>1,5% fisso + 75% della variazione ISTAT</b> dei prezzi al consumo. Sulla rivalutazione
    /// è dovuta un'imposta sostitutiva (default 17%). <paramref name="variazioneIstat"/> è in forma
    /// decimale (es. 0,012 = 1,2%); il <paramref name="fondoIniziale"/> è il TFR al 31/12 precedente.
    /// </summary>
    public static RivalutazioneTfr Rivalutazione(
        decimal fondoIniziale,
        decimal variazioneIstat,
        decimal aliquotaImpostaSostitutiva = 0.17m)
    {
        decimal coefficiente = 0.015m + 0.75m * variazioneIstat;
        decimal lorda = Arrotondamento.Euro(fondoIniziale * coefficiente);
        decimal imposta = Arrotondamento.Euro(lorda * aliquotaImpostaSostitutiva);
        decimal netta = lorda - imposta;
        return new RivalutazioneTfr(coefficiente, lorda, imposta, netta, fondoIniziale + netta);
    }

    /// <summary>
    /// Tassazione separata del TFR alla cessazione (art. 19 TUIR): si calcola il reddito di
    /// riferimento <c>= TFR imponibile × 12 / anni di anzianità</c>, si ricava l'aliquota media
    /// applicando l'IRPEF a scaglioni a tale reddito, e la si applica al TFR imponibile.
    /// Il <paramref name="tfrImponibile"/> è il TFR <b>al netto delle rivalutazioni</b> (già tassate
    /// con imposta sostitutiva). Gli scaglioni sono passati come parametro (versionati per anno).
    /// </summary>
    public static TassazioneSeparataTfr TassazioneSeparata(
        decimal tfrImponibile,
        decimal anniAnzianita,
        IReadOnlyList<ScaglioneIrpef> scaglioni)
    {
        if (anniAnzianita <= 0m)
            throw new ArgumentException("Gli anni di anzianità devono essere positivi.", nameof(anniAnzianita));
        if (tfrImponibile <= 0m)
            return new TassazioneSeparataTfr(0m, 0m, 0m, Arrotondamento.Euro(tfrImponibile));

        decimal redditoRiferimento = tfrImponibile * 12m / anniAnzianita;
        decimal irpefSuRiferimento = CalcoloIrpef.IrpefLordaAnnua(redditoRiferimento, scaglioni);
        decimal aliquotaMedia = redditoRiferimento > 0m ? irpefSuRiferimento / redditoRiferimento : 0m;
        decimal imposta = Arrotondamento.Euro(tfrImponibile * aliquotaMedia);
        return new TassazioneSeparataTfr(
            Arrotondamento.Euro(redditoRiferimento), aliquotaMedia, imposta, tfrImponibile - imposta);
    }
}
