namespace Paghe.Core.Domain;

/// <summary>
/// Rapporto di lavoro: collega il lavoratore all'azienda e definisce inquadramento
/// e parametri retributivi rilevanti per il calcolo del cedolino.
/// (Versione MVP: rapporto subordinato, retribuzione mensile fissa.)
/// </summary>
public sealed class RapportoLavoro
{
    /// <summary>Codice del CCNL applicato (es. "COMMERCIO_TERZIARIO").</summary>
    public required string CodiceCcnl { get; init; }

    /// <summary>Livello di inquadramento nel CCNL (es. "3").</summary>
    public required string Livello { get; init; }

    public Qualifica Qualifica { get; init; } = Qualifica.Impiegato;

    /// <summary>Numero di mensilità annue (12, 13 o 14).</summary>
    public int NumeroMensilita { get; init; } = 14;

    public bool PartTime { get; init; }

    /// <summary>Percentuale di part-time (100 = full-time).</summary>
    public decimal PercentualePartTime { get; init; } = 100m;

    /// <summary>Numero di scatti di anzianità maturati.</summary>
    public int ScattiAnzianita { get; init; }

    /// <summary>Eventuale superminimo individuale mensile (full-time).</summary>
    public decimal Superminimo { get; init; }

    /// <summary>Regione di residenza (per addizionale regionale IRPEF).</summary>
    public string Regione { get; init; } = "";

    /// <summary>Codice catastale del comune di residenza (per addizionale comunale).</summary>
    public string CodiceCatastaleComune { get; init; } = "";

    /// <summary>Spettano le detrazioni da lavoro dipendente.</summary>
    public bool DetrazioniLavoroDipendente { get; init; } = true;

    /// <summary>Giorni dell'anno per cui spettano le detrazioni (default anno intero).</summary>
    public int GiorniDetrazioneAnno { get; init; } = 365;

    /// <summary>
    /// L'azienda ha più di 15 dipendenti (rientra nella CIG): la quota INPS c/dipendente è maggiorata
    /// dello 0,30% (contributo aggiuntivo), es. 9,19% → 9,49%. È una regola <b>generale</b> legata alla
    /// dimensione dell'azienda, non al settore/CCNL.
    /// </summary>
    public bool AziendaOltre15Dipendenti { get; init; }

    /// <summary>
    /// Fondi di settore a carico del dipendente, in aggiunta alla quota INPS base (es. FIS 0,2667%
    /// nel Multiservizi, FSBS 0,15% nella somministrazione). Espresso come frazione (0,002667 = 0,2667%).
    /// </summary>
    public decimal ContributiFondiSettore { get; init; }
}
