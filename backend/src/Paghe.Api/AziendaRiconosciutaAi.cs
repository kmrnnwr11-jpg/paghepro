using Paghe.Core.AdeTelematico;
using Paghe.Core.PartiteIva;

namespace Paghe.Api;

/// <summary>
/// Anagrafica d'impresa riconosciuta in un testo fornito dall'utente (documento allegato alla
/// chat o messaggi scritti dall'utente). È una proposta: nessun dato viene salvato da qui, la
/// registrazione resta un pulsante che preme l'utente sulla card della SPA.
/// </summary>
public sealed record AziendaRiconosciutaAi(
    string? RagioneSociale,
    string? PartitaIva,
    string? CodiceFiscale,
    string? Indirizzo,
    string? Cap,
    string? Comune,
    string? Provincia,
    string? CodiceAteco,
    string? LegaleRappresentante,
    IReadOnlyList<string> CampiDaControllare)
{
    /// <summary>
    /// Proposta dai dati del parser deterministico (con gli eventuali campi completati
    /// dall'AI già fusi). Null se il testo non identifica un'impresa: serve la partita IVA
    /// oppure ragione sociale e codice fiscale, e comunque la ragione sociale.
    /// </summary>
    public static AziendaRiconosciutaAi? Da(
        DatiAziendaDaVisura d, IReadOnlyList<string> campiDallAi, bool riconosciuta)
    {
        if (!riconosciuta && string.IsNullOrWhiteSpace(d.PartitaIva)) return null;
        if (string.IsNullOrWhiteSpace(d.RagioneSociale)) return null;
        return new(
            d.RagioneSociale, d.PartitaIva, d.CodiceFiscale, d.Indirizzo, d.Cap,
            d.Comune, d.Provincia, d.CodiceAteco, d.LegaleRappresentante, campiDallAi);
    }

    /// <summary>
    /// Riconoscimento SOLO deterministico da un singolo messaggio: nessuna chiamata a
    /// provider AI. Il grassetto markdown viene rimosso prima del parsing, perché in chat i
    /// dati arrivano spesso incollati con `**etichette**`. In chat il parser è più severo
    /// che sulla visura: una P.IVA o un CF col checksum sbagliato (per esempio un numero di
    /// protocollo citato per caso) non contano come identificativi.
    /// </summary>
    internal static AziendaRiconosciutaAi? RiconosciNelTesto(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        string pulito = testo.Replace("**", "", StringComparison.Ordinal);
        var d = VisuraCameraleParser.Estrai(pulito);
        if (d.PartitaIva is not null && !ValidatoreFiscale.PartitaIvaValida(d.PartitaIva.Trim()))
            d = d with { PartitaIva = null };
        if (d.CodiceFiscale is not null && !ValidatoreFiscale.CodiceFiscaleValido(d.CodiceFiscale.Trim()))
            d = d with { CodiceFiscale = null };
        bool riconosciuta = !string.IsNullOrWhiteSpace(d.PartitaIva)
            || (!string.IsNullOrWhiteSpace(d.RagioneSociale)
                && !string.IsNullOrWhiteSpace(d.CodiceFiscale));
        return Da(d, [], riconosciuta);
    }
}
