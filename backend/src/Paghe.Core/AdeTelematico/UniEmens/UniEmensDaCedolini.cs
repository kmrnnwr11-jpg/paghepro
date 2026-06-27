using Paghe.Core.Calcolo;

namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Dati di un lavoratore che confluiscono nella sua denuncia individuale UniEmens del mese.
/// Anagrafica minima + imponibile previdenziale e contributi del cedolino.
/// </summary>
public sealed record RigaUniEmens(
    string CodiceFiscale,
    string Cognome,
    string Nome,
    string Qualifica,
    decimal ImponibileInps,
    decimal ContributiDipendente,
    decimal ContributiDatore,
    int GiorniRetribuiti,
    int SettimaneUtili = 0,
    string TipoLavoratore = "");

/// <summary>
/// Costruisce il flusso UniEmens del mese dai dati dei cedolini di un'azienda. Prepara solo
/// il modello/file: la validazione col software di controllo ufficiale INPS e l'invio
/// telematico restano demandati ai canali ufficiali (cassetto previdenziale / intermediario).
/// </summary>
public static class UniEmensDaCedolini
{
    public static FlussoUniEmens Costruisci(
        DatiMittenteUniEmens mittente,
        DenunciaAziendaleUniEmens azienda,
        IReadOnlyList<RigaUniEmens> righe)
    {
        ArgumentNullException.ThrowIfNull(mittente);
        ArgumentNullException.ThrowIfNull(azienda);
        ArgumentNullException.ThrowIfNull(righe);
        if (righe.Count == 0)
            throw new ArgumentException("Il flusso UniEmens deve contenere almeno un lavoratore.");

        var lavoratori = righe
            .Select(r => new DenunciaIndividualeUniEmens(
                CodiceFiscale: r.CodiceFiscale,
                Cognome: r.Cognome,
                Nome: r.Nome,
                Qualifica: r.Qualifica,
                Imponibile: Arrotondamento.Euro(r.ImponibileInps),
                // Contributo complessivo (quota dipendente + quota datore) sull'imponibile.
                // La scomposizione per gestione/aliquota va affinata nelle iterazioni successive.
                Contributo: Arrotondamento.Euro(r.ContributiDipendente + r.ContributiDatore),
                GiorniRetribuiti: r.GiorniRetribuiti,
                SettimaneUtili: r.SettimaneUtili,
                TipoLavoratore: r.TipoLavoratore))
            .ToList();

        return new FlussoUniEmens(mittente, azienda, lavoratori);
    }
}
