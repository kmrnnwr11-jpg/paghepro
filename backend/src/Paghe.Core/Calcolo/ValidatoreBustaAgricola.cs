using Paghe.Core.Domain;

namespace Paghe.Core.Calcolo;

/// <summary>Gravità di una segnalazione del validatore: un ERRORE blocca la validazione
/// dell'elaborazione mensile (la busta resta bozza), un AVVISO chiede solo attenzione.</summary>
public static class LivelloSegnalazione
{
    public const string Errore = "ERRORE";
    public const string Avviso = "AVVISO";
}

/// <summary>Una segnalazione del validatore pre-generazione della busta agricola.</summary>
public sealed record SegnalazioneBustaAgricola(string Livello, string Codice, string Messaggio);

/// <summary>
/// Controlli di coerenza della busta agricola PRIMA del calcolo: ore, giornate, trasferte,
/// date del rapporto. Il validatore non corregge i dati di paga (salvo le normalizzazioni
/// dichiarate, sempre accompagnate da un avviso) e non inventa nulla: produce un elenco di
/// segnalazioni che accompagna la bozza, compare in calce al PDF e blocca la validazione
/// dell'elaborazione mensile finché restano errori.
/// </summary>
public static class ValidatoreBustaAgricola
{
    /// <summary>Limiti di plausibilità delle ore per giornata (configurabili dal chiamante).</summary>
    public const decimal OreGiornataMinime = 3m;
    public const decimal OreGiornataMassime = 10m;

    /// <summary>Sotto questo importo una trasferta giornaliera è sospetta (dato «a incastro»).</summary>
    public const decimal ImportoTrasfertaMinimoPlausibile = 5m;

    /// <summary>
    /// Giorni di detrazione fiscale del mese (art. 13 TUIR): giorni di calendario del periodo
    /// in cui il rapporto esiste, festivi compresi.
    /// <c>min(fine mese, cessazione) − max(inizio mese, assunzione) + 1</c>, mai negativo.
    /// La stessa regola vale per il ragguaglio annuo: la detrazione annua è rapportata a
    /// giorni di calendario su 365, non alle giornate lavorate.
    /// </summary>
    public static int GiorniDetrazioneNelMese(
        int anno, int mese, DateOnly? assunzione, DateOnly? cessazione)
    {
        var inizioMese = new DateOnly(anno, mese, 1);
        var fineMese = new DateOnly(anno, mese, DateTime.DaysInMonth(anno, mese));
        var dal = assunzione is { } a && a > inizioMese ? a : inizioMese;
        var al = cessazione is { } c && c < fineMese ? c : fineMese;
        return al < dal ? 0 : al.DayNumber - dal.DayNumber + 1;
    }

    /// <summary>
    /// Normalizza i gruppi di trasferta prima del calcolo. Due sole correzioni, entrambe
    /// dichiarate con un avviso: (1) giorni e importo palesemente invertiti — giorni
    /// impossibili (&gt; 31, o &gt; giornate lavorate) con un importo che invece sarebbe un
    /// numero di giorni plausibile — vengono scambiati; (2) i gruppi con lo stesso importo
    /// giornaliero vengono riuniti in una riga sola. Un dato ambiguo non viene toccato.
    /// </summary>
    public static IReadOnlyList<TrasfertaGiorniAgricola> NormalizzaTrasferte(
        IReadOnlyList<TrasfertaGiorniAgricola> gruppi,
        int giornateLavorate,
        List<SegnalazioneBustaAgricola> segnalazioni)
    {
        var corretti = new List<TrasfertaGiorniAgricola>();
        foreach (var gruppo in gruppi)
        {
            bool giorniImpossibili = gruppo.Giorni > 31m
                || (giornateLavorate > 0 && gruppo.Giorni > giornateLavorate);
            bool importoSarebbeGiorniPlausibili = gruppo.ImportoGiorno <= 31m
                && (giornateLavorate <= 0 || gruppo.ImportoGiorno <= giornateLavorate);
            if (giorniImpossibili && importoSarebbeGiorniPlausibili
                && gruppo.Giorni > gruppo.ImportoGiorno)
            {
                corretti.Add(new TrasfertaGiorniAgricola(gruppo.ImportoGiorno, gruppo.Giorni));
                segnalazioni.Add(new(LivelloSegnalazione.Avviso, "TRASF_INVERTITA",
                    $"Trasferta con quantità e importo invertiti ({gruppo.Giorni:0.##} × "
                    + $"{gruppo.ImportoGiorno:0.##} €): normalizzata a {gruppo.ImportoGiorno:0.##} "
                    + $"giorni × {gruppo.Giorni:0.##} €. Verificare il dato di origine."));
            }
            else
            {
                corretti.Add(gruppo);
            }
        }

        // Gruppi con lo stesso importo giornaliero → una sola riga (es. 4 g + 1 g a 20 €).
        return corretti
            .GroupBy(g => g.ImportoGiorno)
            .Select(g => new TrasfertaGiorniAgricola(g.Sum(x => x.Giorni), g.Key))
            .ToList();
    }

    /// <summary>Controlli di coerenza sui dati del mese. Non calcola e non modifica nulla.</summary>
    public static List<SegnalazioneBustaAgricola> Valida(
        int anno,
        int mese,
        decimal oreOrdinarie,
        int giornateLavorate,
        IReadOnlyList<TrasfertaGiorniAgricola> trasferte,
        DateOnly? dataAssunzione,
        DateOnly? dataCessazione,
        decimal limiteEsenzioneTrasfertaGiorno,
        decimal? oreGiornaliereContratto = null,
        string? matricolaInpsAzienda = null,
        bool perValidazione = false)
    {
        var esito = new List<SegnalazioneBustaAgricola>();

        // Date del rapporto: una cessazione prima dell'assunzione o prima del mese elaborato
        // rende la busta impossibile, non solo sospetta.
        if (dataAssunzione is { } assunzione && dataCessazione is { } cessazione)
        {
            if (cessazione < assunzione)
                esito.Add(new(LivelloSegnalazione.Errore, "CESSAZIONE_PRIMA_ASSUNZIONE",
                    $"La data di cessazione ({cessazione:dd/MM/yyyy}) precede l'assunzione "
                    + $"({assunzione:dd/MM/yyyy})."));
            else if (cessazione < new DateOnly(anno, mese, 1))
                esito.Add(new(LivelloSegnalazione.Errore, "CESSAZIONE_PRIMA_DEL_MESE",
                    $"Il rapporto è cessato il {cessazione:dd/MM/yyyy}, prima del mese elaborato."));
        }

        // Ore e giornate si dichiarano insieme: una sola delle due a zero è un dato monco.
        if (oreOrdinarie == 0m && giornateLavorate > 0)
            esito.Add(new(LivelloSegnalazione.Errore, "ORE_ZERO_CON_GIORNATE",
                $"Dichiarate {giornateLavorate} giornate lavorate ma zero ore."));
        if (oreOrdinarie > 0m && giornateLavorate == 0)
            esito.Add(new(LivelloSegnalazione.Errore, "GIORNATE_ZERO_CON_ORE",
                $"Dichiarate {oreOrdinarie:0.##} ore lavorate ma zero giornate."));

        // Le giornate INPS non possono superare i giorni di calendario in cui il rapporto
        // esisteva davvero nel mese.
        int giorniCalendario = GiorniDetrazioneNelMese(anno, mese, dataAssunzione, dataCessazione);
        if (giornateLavorate > giorniCalendario)
            esito.Add(new(LivelloSegnalazione.Errore, "GIORNATE_OLTRE_CALENDARIO",
                $"{giornateLavorate} giornate lavorate ma il rapporto copre solo "
                + $"{giorniCalendario} giorni di calendario nel mese."));

        if (giornateLavorate > 0 && oreOrdinarie > 0m)
        {
            decimal orePerGiornata = oreOrdinarie / giornateLavorate;
            if (orePerGiornata < OreGiornataMinime || orePerGiornata > OreGiornataMassime)
                esito.Add(new(LivelloSegnalazione.Avviso, "ORE_GIORNATA_FUORI_RANGE",
                    $"Media di {orePerGiornata:0.##} ore per giornata: fuori dall'intervallo "
                    + $"di plausibilità {OreGiornataMinime:0.##}–{OreGiornataMassime:0.##}."));
            else if (oreGiornaliereContratto is { } contratto && contratto > 0m
                && Math.Abs(orePerGiornata - contratto) > 0.5m)
                esito.Add(new(LivelloSegnalazione.Avviso, "GIORNATE_INCOERENTI_COL_CONTRATTO",
                    $"Media di {orePerGiornata:0.##} ore per giornata contro le "
                    + $"{contratto:0.##} ore giornaliere del contratto: verificare le giornate "
                    + "dichiarate all'INPS."));
        }

        decimal giorniTrasferta = trasferte.Sum(t => t.Giorni);
        if (giorniTrasferta > giornateLavorate)
            esito.Add(new(LivelloSegnalazione.Errore, "TRASFERTE_OLTRE_GIORNATE",
                $"{giorniTrasferta:0.##} giorni di trasferta su {giornateLavorate} giornate "
                + "lavorate: la trasferta presuppone una giornata di lavoro."));

        foreach (var gruppo in trasferte.Where(t => t.Giorni > 0m))
        {
            if (gruppo.ImportoGiorno > limiteEsenzioneTrasfertaGiorno)
                esito.Add(new(LivelloSegnalazione.Errore, "TRASFERTA_OLTRE_LIMITE_ESENZIONE",
                    $"Indennità di trasferta di {gruppo.ImportoGiorno:0.00} €/giorno oltre il "
                    + $"limite di esenzione di {limiteEsenzioneTrasfertaGiorno:0.00} €: la parte "
                    + "eccedente è resa imponibile in busta, ma il dato va rivisto col consulente."));
            else if (gruppo.ImportoGiorno < ImportoTrasfertaMinimoPlausibile)
                esito.Add(new(LivelloSegnalazione.Avviso, "TRASFERTA_IMPORTO_IMPLAUSIBILE",
                    $"Indennità di trasferta di {gruppo.ImportoGiorno:0.00} €/giorno: un importo "
                    + $"sotto i {ImportoTrasfertaMinimoPlausibile:0.00} € non è una trasferta "
                    + "plausibile e va documentato."));
        }

        if (string.IsNullOrWhiteSpace(matricolaInpsAzienda))
            esito.Add(new(
                perValidazione ? LivelloSegnalazione.Errore : LivelloSegnalazione.Avviso,
                "MATRICOLA_INPS_MANCANTE",
                "L'azienda non ha la matricola/posizione INPS: obbligatoria per la denuncia "
                + "PosAgri e per validare l'elaborazione."));

        return esito;
    }

    /// <summary>
    /// Controllo di caricamento contratti (elaborazione mensile): a parità di data di
    /// assunzione, una data di cessazione diversa da quella prevalente del gruppo è quasi
    /// sempre un errore di digitazione.
    /// </summary>
    public static List<SegnalazioneBustaAgricola> ValidaCessazioniOmogenee(
        IReadOnlyList<(string Nominativo, DateOnly Assunzione, DateOnly? Cessazione)> rapporti)
    {
        var esito = new List<SegnalazioneBustaAgricola>();
        // In caso di parità dentro il gruppo decide la cessazione più diffusa nell'intero
        // elenco: con due soli assunti lo stesso giorno, quello «fuori coro» è chi ha una
        // data che nessun altro contratto usa.
        var diffusione = rapporti
            .GroupBy(r => r.Cessazione ?? DateOnly.MaxValue)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var gruppo in rapporti.GroupBy(r => r.Assunzione).Where(g => g.Count() > 1))
        {
            var prevalente = gruppo
                .GroupBy(r => r.Cessazione)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => diffusione.GetValueOrDefault(g.Key ?? DateOnly.MaxValue))
                .First().Key;
            foreach (var fuoriCoro in gruppo.Where(r => r.Cessazione != prevalente))
                esito.Add(new(LivelloSegnalazione.Avviso, "CESSAZIONE_DIFFORME",
                    $"{fuoriCoro.Nominativo}: assunto il {gruppo.Key:dd/MM/yyyy} come altri "
                    + $"{gruppo.Count() - 1} lavoratori, ma cessa il "
                    + $"{(fuoriCoro.Cessazione is { } c ? c.ToString("dd/MM/yyyy") : "mai")} invece del "
                    + $"{(prevalente is { } p ? p.ToString("dd/MM/yyyy") : "mai")}: "
                    + "possibile errore di digitazione."));
        }
        return esito;
    }
}
