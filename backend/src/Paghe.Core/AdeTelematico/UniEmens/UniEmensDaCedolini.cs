namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Dati di un lavoratore che confluiscono nella sua denuncia individuale UniEmens del mese.
/// Quello che il motore paghe conosce (imponibile, contributi, giorni, presenze) più
/// l'inquadramento contributivo, che va letto dall'anagrafica e non dedotto.
/// </summary>
public sealed record RigaUniEmens(
    string CodiceFiscale,
    string Cognome,
    string Nome,
    string Qualifica1,
    decimal ImponibileInps,
    decimal ContributiDipendente,
    decimal ContributiDatore,
    int GiorniRetribuiti)
{
    public string? Qualifica2 { get; init; }
    public string? Qualifica3 { get; init; }
    public string? TipoContribuzione { get; init; }
    public string? RegimePost95 { get; init; }
    public string? Cittadinanza { get; init; }
    public string? CodiceComune { get; init; }
    public string? CodiceContratto { get; init; }
    public string? TipoCodiceContratto { get; init; }
    public string? QualProf { get; init; }
    public string? TipoPaga { get; init; }
    public int? DivisoreOrarioContr { get; init; }
    public int? OrarioContrattuale { get; init; }

    /// <summary>Percentuale di part-time in centesimi (2500 = 25%); assente = tempo pieno.</summary>
    public int? PercPartTime { get; init; }

    public int? PercPartTimeMese { get; init; }

    /// <summary>Numero di mensilità in millesimi (14000 = 14 mensilità).</summary>
    public int? NumMensilita { get; init; }

    public decimal? RetribTeorica { get; init; }

    /// <summary>Ore lavorabili del mese in centesimi di ora (4600 = 46 ore).</summary>
    public int? OreLavorabili { get; init; }

    /// <summary>Ore effettivamente contribuite in centesimi; richieste per i part-time.</summary>
    public int? OreContribuite { get; init; }

    public string? RispettoMinimale { get; init; }

    /// <summary>Settimane utili in centesimi (100 = una settimana); solo per i part-time.</summary>
    public int? SettimaneUtili { get; init; }

    public string? TipoLavoratore { get; init; }

    /// <summary>
    /// Calendario giornaliero reale del cartellino. Non esiste un calendario predefinito:
    /// l'assenza anche di un solo giorno blocca la costruzione del flusso.
    /// </summary>
    public IReadOnlyList<GiornoUniEmens> Calendario { get; init; } = [];

    /// <summary>
    /// Coperture settimanali determinate dal calendario e dalle voci reali del cedolino.
    /// Le settimane ordinarie omesse sono interamente retribuite; una copertura figurativa
    /// non viene mai dedotta dal solo fatto che esista un evento giornaliero.
    /// </summary>
    public IReadOnlyList<SettimanaUniEmens> CopertureSettimanali { get; init; } = [];

    public IReadOnlyList<DifferenzaAccreditoUniEmens> DifferenzeAccredito { get; init; } = [];
}

/// <summary>
/// Costruisce il flusso UniEmens del mese dai dati dei cedolini di un'azienda.
///
/// Il calendario è sempre quello registrato nel cartellino. Coperture settimanali e
/// differenze di accredito arrivano già determinate dal servizio sui dati reali del
/// cedolino; nessun giorno viene ricostruito da un'ipotesi contrattuale.
/// </summary>
public static class UniEmensDaCedolini
{
    public static FlussoUniEmens Costruisci(
        MittenteUniEmens mittente,
        TestataAziendaUniEmens testata,
        IReadOnlyList<RigaUniEmens> righe)
    {
        ArgumentNullException.ThrowIfNull(mittente);
        ArgumentNullException.ThrowIfNull(testata);
        ArgumentNullException.ThrowIfNull(righe);
        if (righe.Count == 0)
            throw new ArgumentException("Il flusso UniEmens deve contenere almeno un lavoratore.");

        int giorniDelMese = DateTime.DaysInMonth(testata.Anno, testata.Mese);

        var lavoratori = righe
            .Select(r => Individuale(r, testata.Anno, testata.Mese, giorniDelMese))
            .ToList();

        decimal totaleADebito = righe.Sum(r => r.ContributiDipendente + r.ContributiDatore);
        var aziendale = new DenunciaAziendaleUniEmens(
            NumLavoratori: righe.Count,
            ForzaAziendale: ForzaAziendale(righe),
            NumDenIndiv: righe.Count,
            TotaleADebito: totaleADebito,
            TotaleACredito: 0m);

        var posizione = new PosizioneContributivaUniEmens(
            testata.Matricola, lavoratori, aziendale, testata.Composizione);
        return new FlussoUniEmens(
            mittente,
            new AziendaUniEmens(
                testata.Anno, testata.Mese, testata.CfAzienda, testata.RagioneSociale, posizione));
    }

    /// <summary>
    /// Forza aziendale: i dipendenti a tempo parziale contano in proporzione all'orario
    /// (allegato tecnico). Quattro part-time al 25% valgono uno.
    /// </summary>
    private static int ForzaAziendale(IReadOnlyList<RigaUniEmens> righe)
    {
        decimal forza = righe.Sum(r => (r.PercPartTime ?? 10000) / 10000m);
        return (int)Math.Round(forza, MidpointRounding.AwayFromZero);
    }

    private static DenunciaIndividualeUniEmens Individuale(
        RigaUniEmens r, int anno, int mese, int giorniDelMese)
    {
        var giorni = VerificaCalendarioReale(r, giorniDelMese);
        var settimane = Settimane(r, giorni, anno, mese);
        VerificaDifferenzeAccredito(r);

        var retribuzione = new DatiRetributiviUniEmens(
            Imponibile: r.ImponibileInps,
            // Il tracciato espone la contribuzione complessiva della posizione, quota del
            // lavoratore compresa: è quanto l'azienda deve all'INPS per quel lavoratore.
            Contributo: r.ContributiDipendente + r.ContributiDatore,
            GiorniRetribuiti: r.GiorniRetribuiti,
            Settimane: settimane,
            Giorni: giorni,
            TipoLavoratore: r.TipoLavoratore,
            RetribTeorica: r.RetribTeorica,
            OreLavorabili: r.OreLavorabili,
            // Per chi non è part-time l'INPS vuole i giorni contribuiti (DR00085): per il
            // lavoratore mensilizzato coincidono con i giorni retribuiti del mese.
            GiorniContribuiti: r.OreContribuite is null ? r.GiorniRetribuiti : null,
            OreContribuite: r.OreContribuite,
            RispettoMinimale: r.RispettoMinimale,
            SettimaneUtili: r.SettimaneUtili)
        {
            DifferenzeAccredito = r.DifferenzeAccredito,
        };

        return new DenunciaIndividualeUniEmens(
            CodiceFiscale: r.CodiceFiscale,
            Cognome: r.Cognome,
            Nome: r.Nome,
            Qualifica1: r.Qualifica1,
            Retribuzione: retribuzione)
        {
            Qualifica2 = r.Qualifica2,
            Qualifica3 = r.Qualifica3,
            TipoContribuzione = r.TipoContribuzione,
            RegimePost95 = r.RegimePost95,
            Cittadinanza = r.Cittadinanza,
            CodiceComune = r.CodiceComune,
            CodiceContratto = r.CodiceContratto,
            TipoCodiceContratto = r.TipoCodiceContratto,
            QualProf = r.QualProf,
            TipoPaga = r.TipoPaga,
            DivisoreOrarioContr = r.DivisoreOrarioContr,
            OrarioContrattuale = r.OrarioContrattuale,
            PercPartTime = r.PercPartTime,
            PercPartTimeMese = r.PercPartTimeMese,
            NumMensilita = r.NumMensilita,
        };
    }

    private static IReadOnlyList<GiornoUniEmens> VerificaCalendarioReale(
        RigaUniEmens r, int giorniDelMese)
    {
        var giorni = r.Calendario.OrderBy(g => g.Giorno).ToList();
        var attesi = Enumerable.Range(1, giorniDelMese).ToArray();
        if (giorni.Count != giorniDelMese
            || !giorni.Select(g => g.Giorno).SequenceEqual(attesi))
            throw new ArgumentException(
                $"Il calendario giornaliero completo e reale del lavoratore {r.CodiceFiscale} "
                + $"deve contenere una sola riga per ciascuno dei {giorniDelMese} giorni del mese.");

        foreach (var giorno in giorni)
        {
            if (giorno.Eventi.Count > 3)
                throw new ArgumentException(
                    $"Il giorno {giorno.Giorno} del lavoratore {r.CodiceFiscale} contiene più "
                    + "dei tre eventi ammessi dallo schema UniEmens.");

            var codici = giorno.Eventi.Select(e => e.CodiceEvento).ToList();
            if (codici.Any(c => string.IsNullOrWhiteSpace(c) || c.Trim().Length != 3)
                || codici.Distinct(StringComparer.Ordinal).Count() != codici.Count)
                throw new ArgumentException(
                    $"Il dettaglio evento del giorno {giorno.Giorno} del lavoratore "
                    + $"{r.CodiceFiscale} non rispetta i codici univoci di tre caratteri "
                    + "previsti dallo schema UniEmens.");

            if (giorno.Eventi.Count > 0
                && giorno.TipoCopertura is not ("0" or "1" or "2" or "X" or "D"))
                throw new ArgumentException(
                    $"Il giorno {giorno.Giorno} del lavoratore {r.CodiceFiscale} contiene "
                    + "un evento ma non una copertura giornaliera UniEmens valida.");

            foreach (var evento in giorno.Eventi)
            {
                bool tipoPresente = !string.IsNullOrWhiteSpace(
                    evento.TipoInformazioneAggiuntiva);
                bool informazionePresente = !string.IsNullOrWhiteSpace(
                    evento.InformazioneAggiuntiva);
                if (tipoPresente != informazionePresente
                    || (tipoPresente && evento.TipoInformazioneAggiuntiva!.Trim().Length != 2)
                    || (informazionePresente && evento.InformazioneAggiuntiva!.Trim().Length > 50)
                    || evento.NumeroOreEvento is <= 0 or > 2400)
                    throw new ArgumentException(
                        $"Il dettaglio evento del giorno {giorno.Giorno} del lavoratore "
                        + $"{r.CodiceFiscale} non rispetta EventoGiorn dello schema UniEmens.");
            }
        }

        return giorni;
    }

    private static IReadOnlyList<SettimanaUniEmens> Settimane(
        RigaUniEmens r, IReadOnlyList<GiornoUniEmens> giorni, int anno, int mese)
    {
        var idAttesi = SettimaneInps.DelMese(anno, mese).ToHashSet();
        if (r.CopertureSettimanali.Select(s => s.Id).Distinct().Count()
            != r.CopertureSettimanali.Count)
            throw new ArgumentException(
                $"Le coperture settimanali del lavoratore {r.CodiceFiscale} contengono duplicati.");

        foreach (var settimana in r.CopertureSettimanali)
        {
            if (!idAttesi.Contains(settimana.Id))
                throw new ArgumentException(
                    $"La settimana {settimana.Id} del lavoratore {r.CodiceFiscale} non appartiene "
                    + $"al periodo {mese:00}/{anno}.");
            if (settimana.TipoCopertura is not ("X" or "1" or "2" or "0" or "D"))
                throw new ArgumentException(
                    $"La settimana {settimana.Id} del lavoratore {r.CodiceFiscale} ha una "
                    + "copertura UniEmens non valida.");

            var codici = settimana.CodiciEvento.Select(c => c.Trim()).ToList();
            if (codici.Any(c => c.Length != 3)
                || codici.Distinct(StringComparer.Ordinal).Count() != codici.Count
                || (settimana.TipoCopertura is "1" or "2") != (codici.Count > 0))
                throw new ArgumentException(
                    $"La settimana {settimana.Id} del lavoratore {r.CodiceFiscale} non ha "
                    + "codici evento coerenti con la copertura settimanale.");
        }

        var esplicite = r.CopertureSettimanali.ToDictionary(s => s.Id);
        foreach (var gruppo in giorni
            .Where(g => g.Eventi.Count > 0 && g.TipoCopertura is "1" or "2")
            .GroupBy(g => SettimaneInps.IdSettimana(new DateOnly(anno, mese, g.Giorno))))
        {
            if (!esplicite.TryGetValue(gruppo.Key, out var settimana)
                || settimana.TipoCopertura is not ("1" or "2"))
                throw new ArgumentException(
                    $"La settimana {gruppo.Key} del lavoratore {r.CodiceFiscale} contiene "
                    + "un evento figurativo giornaliero ma non la relativa copertura esplicita.");

            var mancanti = gruppo.SelectMany(g => g.Eventi)
                .Select(e => e.CodiceEvento.Trim())
                .Distinct(StringComparer.Ordinal)
                .Except(settimana.CodiciEvento.Select(c => c.Trim()), StringComparer.Ordinal)
                .ToList();
            if (mancanti.Count > 0)
                throw new ArgumentException(
                    $"La settimana {gruppo.Key} del lavoratore {r.CodiceFiscale} non espone "
                    + $"i codici evento giornalieri: {string.Join(", ", mancanti)}.");
        }

        return idAttesi.Order()
            .Select(id => esplicite.TryGetValue(id, out var settimana)
                ? settimana
                : new SettimanaUniEmens(id))
            .ToList();
    }

    private static void VerificaDifferenzeAccredito(RigaUniEmens r)
    {
        var codici = r.DifferenzeAccredito.Select(d => d.CodiceEvento.Trim()).ToList();
        if (codici.Any(c => c.Length != 3)
            || codici.Distinct(StringComparer.Ordinal).Count() != codici.Count)
            throw new ArgumentException(
                $"Le differenze accredito del lavoratore {r.CodiceFiscale} devono avere "
                + "codici evento univoci di tre caratteri.");

        foreach (var differenza in r.DifferenzeAccredito)
        {
            if (differenza.Importo < 1m)
                throw new ArgumentException(
                    $"La differenza accredito {differenza.CodiceEvento} del lavoratore "
                    + $"{r.CodiceFiscale} deve essere almeno pari a un euro.");

            foreach (var quota in differenza.QuoteEvento)
            {
                if (string.IsNullOrWhiteSpace(quota.MotivoEvento)
                    || quota.MotivoEvento.Trim().Length > 50
                    || string.IsNullOrWhiteSpace(quota.TipoMotivoEvento)
                    || quota.TipoMotivoEvento.Trim().Length != 2
                    || quota.Importo < 1m)
                    throw new ArgumentException(
                        $"La quota evento della differenza {differenza.CodiceEvento} del "
                        + $"lavoratore {r.CodiceFiscale} non rispetta InfoEvento dello schema.");
            }

            if (differenza.QuoteEvento.Count > 0
                && Math.Round(differenza.QuoteEvento.Sum(q => q.Importo),
                    MidpointRounding.AwayFromZero)
                    != Math.Round(differenza.Importo, MidpointRounding.AwayFromZero))
                throw new ArgumentException(
                    $"Le quote della differenza {differenza.CodiceEvento} del lavoratore "
                    + $"{r.CodiceFiscale} non quadrano con l'importo complessivo.");
        }
    }
}
