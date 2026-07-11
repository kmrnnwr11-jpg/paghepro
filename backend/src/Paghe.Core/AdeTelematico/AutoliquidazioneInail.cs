using Paghe.Core.Calcolo;

namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Esito dell'autoliquidazione INAIL: regolazione dell'anno chiuso e rata anticipata
/// per l'anno successivo, al netto della rata già versata l'anno prima.
/// </summary>
public sealed record RisultatoAutoliquidazioneInail(
    decimal RetribuzioniAnno,
    decimal TassoPerMille,
    decimal Regolazione,
    decimal RataAnticipata,
    decimal RataGiaVersata,
    decimal TotaleDaVersare);

/// <summary>
/// Calcolo dell'autoliquidazione del premio INAIL. Il tasso (per mille) e la PAT arrivano
/// dalla lettera "basi di calcolo" INAIL dell'azienda: qui si applica il tasso alle
/// retribuzioni dell'anno. La rata anticipata usa le stesse retribuzioni come presunto
/// (presunto = consuntivo, il default standard). Calcolatore puro, nessun dato cablato.
/// </summary>
public static class AutoliquidazioneInail
{
    public static RisultatoAutoliquidazioneInail Calcola(
        decimal retribuzioniAnno, decimal tassoPerMille, decimal rataGiaVersata = 0m)
    {
        if (tassoPerMille <= 0m)
            throw new ArgumentException("Il tasso INAIL (per mille) deve essere maggiore di zero.");
        if (retribuzioniAnno < 0m)
            throw new ArgumentException("Le retribuzioni dell'anno non possono essere negative.");

        decimal regolazione = Arrotondamento.Euro(retribuzioniAnno * tassoPerMille / 1000m);
        decimal rataAnticipata = regolazione;
        decimal totale = Arrotondamento.Euro(regolazione - rataGiaVersata + rataAnticipata);

        return new RisultatoAutoliquidazioneInail(
            retribuzioniAnno, tassoPerMille, regolazione, rataAnticipata, rataGiaVersata, totale);
    }
}
