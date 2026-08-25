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

    /// <summary>
    /// Numero di scatti di anzianità maturati, indicato a mano. Quando il CCNL dichiara la
    /// periodicità e la data di assunzione è nota, il motore li matura da solo e questo campo
    /// viene ignorato (revisione consulente 24/08/2026).
    /// </summary>
    public int ScattiAnzianita { get; init; }

    /// <summary>Data di assunzione: serve alla maturazione automatica degli scatti e delle mensilità aggiuntive.</summary>
    public DateOnly? DataAssunzione { get; init; }

    /// <summary>
    /// Sigla della provincia dell'unità operativa (per gli elementi provinciali del CCNL, come il
    /// terzo elemento del Terziario). In mancanza vale la provincia della sede aziendale.
    /// </summary>
    public string? ProvinciaLavoro { get; init; }

    /// <summary>
    /// Aliquota INPS complessiva c/datore del profilo contributivo dell'azienda (CSC, codici
    /// autorizzazione, dimensione), se determinata dal professionista. Null = tabella per
    /// qualifica. Il consulente (24/08/2026): l'aliquota datoriale non va mai cablata,
    /// dipende dal profilo reale dell'azienda.
    /// </summary>
    public decimal? AliquotaInpsDatoreProfilo { get; init; }

    /// <summary>
    /// Aliquota complessiva c/dipendente risolta dal profilo contributivo reale (CSC, CA,
    /// organico, fondo di integrazione e agevolazioni). Nel percorso archiviabile e obbligatoria:
    /// H011 identifica il contratto collettivo, non la classificazione contributiva.
    /// </summary>
    public decimal? AliquotaInpsDipendenteProfilo { get; init; }

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

    /// <summary>
    /// L'azienda aderisce ai fondi contrattuali del CCNL (assistenza sanitaria, ente bilaterale):
    /// se no, le relative quote non vanno in busta. Gli eventuali elementi retributivi
    /// sostitutivi previsti dal contratto restano da gestire a parte.
    /// </summary>
    public bool AderisceFondiContrattuali { get; init; } = true;

    // ---- Addizionali da conguaglio dell'anno precedente ------------------------------
    // Il motore trattiene soltanto RATE originate dal conguaglio (regionale e saldo comunale
    // in 11 rate gennaio-novembre, acconto comunale in 9 rate da marzo). Null = dato mancante:
    // nessuna trattenuta mensile inventata e percorso archiviabile bloccato.

    /// <summary>Addizionale regionale dovuta sul reddito dell'anno precedente (importo annuo da rateizzare).</summary>
    public decimal? AddizionaleRegionaleConguaglioAnnuo { get; init; }

    /// <summary>Saldo dell'addizionale comunale dell'anno precedente (importo annuo da rateizzare).</summary>
    public decimal? AddizionaleComunaleSaldoAnnuo { get; init; }

    /// <summary>Acconto 30% dell'addizionale comunale dell'anno corrente (importo da rateizzare da marzo).</summary>
    public decimal? AddizionaleComunaleAccontoAnnuo { get; init; }

    /// <summary>Ferie disponibili all'inizio dell'anno, riportate dall'anno precedente.</summary>
    public decimal FerieResidueIniziali { get; init; }

    /// <summary>ROL/ex festività disponibili all'inizio dell'anno, in ore.</summary>
    public decimal PermessiResiduiIniziali { get; init; }

    /// <summary>Riporto ROL al 1 gennaio. Null conserva soltanto la compatibilita storica.</summary>
    public decimal? RolResiduiIniziali { get; init; }

    /// <summary>Riporto ex festivita al 1 gennaio. Null conserva soltanto la compatibilita storica.</summary>
    public decimal? ExFestivitaResidueIniziali { get; init; }

    /// <summary>
    /// Copia del rapporto con un diverso superminimo. Serve al calcolo inverso netto → lordo,
    /// che cerca per tentativi il superminimo corrispondente al netto voluto.
    /// </summary>
    public RapportoLavoro ConSuperminimo(decimal superminimo) => new()
    {
        CodiceCcnl = CodiceCcnl,
        Livello = Livello,
        Qualifica = Qualifica,
        NumeroMensilita = NumeroMensilita,
        PartTime = PartTime,
        PercentualePartTime = PercentualePartTime,
        ScattiAnzianita = ScattiAnzianita,
        DataAssunzione = DataAssunzione,
        ProvinciaLavoro = ProvinciaLavoro,
        AliquotaInpsDatoreProfilo = AliquotaInpsDatoreProfilo,
        AliquotaInpsDipendenteProfilo = AliquotaInpsDipendenteProfilo,
        Superminimo = superminimo,
        Regione = Regione,
        CodiceCatastaleComune = CodiceCatastaleComune,
        DetrazioniLavoroDipendente = DetrazioniLavoroDipendente,
        GiorniDetrazioneAnno = GiorniDetrazioneAnno,
        AziendaOltre15Dipendenti = AziendaOltre15Dipendenti,
        ContributiFondiSettore = ContributiFondiSettore,
        AderisceFondiContrattuali = AderisceFondiContrattuali,
        AddizionaleRegionaleConguaglioAnnuo = AddizionaleRegionaleConguaglioAnnuo,
        AddizionaleComunaleSaldoAnnuo = AddizionaleComunaleSaldoAnnuo,
        AddizionaleComunaleAccontoAnnuo = AddizionaleComunaleAccontoAnnuo,
        FerieResidueIniziali = FerieResidueIniziali,
        PermessiResiduiIniziali = PermessiResiduiIniziali,
        RolResiduiIniziali = RolResiduiIniziali,
        ExFestivitaResidueIniziali = ExFestivitaResidueIniziali,
    };
}
