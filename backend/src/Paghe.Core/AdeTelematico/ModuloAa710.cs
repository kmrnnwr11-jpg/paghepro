namespace Paghe.Core.AdeTelematico;

/// <summary>Rappresentante (quadro C del modello AA7/10).</summary>
public sealed record RappresentanteAa710(
    string Cognome,
    string Nome,
    string CodiceCarica,
    string CodiceFiscale,
    string? ComuneNascita = null,
    string? ProvinciaNascita = null,
    DateOnly? DataNascita = null,
    DateOnly? DataInizioProcedimento = null);

/// <summary>Sede/luogo del modello AA7/10 (sede legale, domicilio fiscale, luogo esercizio).</summary>
public sealed record SedeAa710(
    string Indirizzo,
    string Cap,
    string Comune,
    string Provincia,
    bool ScrittureContabili = false);

/// <summary>Estremi di registrazione dell'atto costitutivo (quadro A, tipo 1).</summary>
public sealed record EstremiRegistrazioneAa710(
    DateOnly? Data = null,
    string? Ufficio = null,
    string? Numero = null,
    string? SottoNumero = null,
    string? Serie = null);

/// <summary>
/// Dati del modello AA7/10 (soggetti diversi dalle persone fisiche): domanda di attribuzione
/// del codice fiscale e dichiarazione di inizio attività, variazione dati o cessazione ai fini IVA.
/// Copre testata + quadri A, B e C del modello ufficiale 2025.
/// </summary>
public sealed record ModuloAa710
{
    /// <summary>1 = inizio con attribuzione CF e P.IVA, 2 = inizio con attribuzione P.IVA,
    /// 3 = variazione dati, 4 = cessazione attività.</summary>
    public required int TipoDichiarazione { get; init; }

    /// <summary>Codice fiscale numerico del soggetto (obbligatorio per i tipi 2, 3 e 4).</summary>
    public string? CodiceFiscale { get; init; }

    /// <summary>Partita IVA (obbligatoria per i tipi 3 e 4).</summary>
    public string? PartitaIva { get; init; }

    /// <summary>Data di inizio (tipi 1-2), di variazione (tipo 3) o di cessazione (tipo 4).</summary>
    public required DateOnly Data { get; init; }

    /// <summary>Solo tipo 1: estremi di registrazione dell'atto costitutivo.</summary>
    public EstremiRegistrazioneAa710? EstremiRegistrazione { get; init; }

    // ---- Quadro B ----
    public required string Denominazione { get; init; }
    /// <summary>Codice della tabella generale di classificazione natura giuridica (es. "2" = SRL).</summary>
    public required string NaturaGiuridica { get; init; }
    public string? Sigla { get; init; }
    public required SedeAa710 SedeLegale { get; init; }
    /// <summary>Solo se diverso dalla sede legale.</summary>
    public SedeAa710? DomicilioFiscale { get; init; }
    public string? CodiceAttivita { get; init; }
    public string? DescrizioneAttivita { get; init; }
    public decimal? VolumeAffariPresunto { get; init; }
    /// <summary>Luogo di esercizio dell'attività, se indicato.</summary>
    public SedeAa710? LuogoEsercizio { get; init; }

    // ---- Quadro C ----
    public required RappresentanteAa710 Rappresentante { get; init; }

    /// <summary>Controlli interni prima della stampa. Lista vuota = modulo compilabile.</summary>
    public IReadOnlyList<string> Valida()
    {
        var errori = new List<string>();

        if (TipoDichiarazione is < 1 or > 4)
            errori.Add("Tipo dichiarazione non valido: ammessi 1 (inizio con CF e P.IVA), 2 (inizio con P.IVA), 3 (variazione), 4 (cessazione).");
        if (TipoDichiarazione >= 2 && !ValidatoreFiscale.CodiceFiscaleValido(CodiceFiscale))
            errori.Add("Codice fiscale del soggetto mancante o non valido (obbligatorio per i tipi 2, 3 e 4).");
        if (TipoDichiarazione >= 3 && !ValidatoreFiscale.PartitaIvaValida(PartitaIva))
            errori.Add("Partita IVA mancante o non valida (obbligatoria per i tipi 3 e 4).");
        if (string.IsNullOrWhiteSpace(Denominazione))
            errori.Add("Denominazione o ragione sociale mancante.");
        if (!NatureGiuridiche.ContainsKey(NaturaGiuridica?.Trim() ?? ""))
            errori.Add("Codice natura giuridica mancante o non presente nella tabella ufficiale.");
        if (string.IsNullOrWhiteSpace(SedeLegale.Indirizzo) || string.IsNullOrWhiteSpace(SedeLegale.Comune))
            errori.Add("Sede legale incompleta: servono almeno indirizzo e comune.");
        if (string.IsNullOrWhiteSpace(Rappresentante.Cognome))
            errori.Add("Cognome (o denominazione) del rappresentante mancante.");
        if (!CodiciCarica.ContainsKey(Rappresentante.CodiceCarica?.Trim() ?? ""))
            errori.Add("Codice carica del rappresentante mancante o non previsto dal modello AA7/10.");
        if (!ValidatoreFiscale.CodiceFiscaleValido(Rappresentante.CodiceFiscale) &&
            !ValidatoreFiscale.CodiceFiscalePersonaFisicaValido(Rappresentante.CodiceFiscale))
            errori.Add("Codice fiscale del rappresentante mancante o non valido.");

        return errori;
    }

    /// <summary>Tabella generale di classificazione natura giuridica (istruzioni AA7/10 2025).</summary>
    public static readonly IReadOnlyDictionary<string, string> NatureGiuridiche =
        new Dictionary<string, string>
        {
            // Soggetti residenti
            ["1"] = "Società in accomandita per azioni",
            ["2"] = "Società a responsabilità limitata",
            ["3"] = "Società per azioni",
            ["4"] = "Società cooperative e loro consorzi iscritti nei registri prefettizi e nello schedario della cooperazione",
            ["5"] = "Altre società cooperative",
            ["6"] = "Mutue assicuratrici",
            ["7"] = "Consorzi con personalità giuridica",
            ["8"] = "Associazioni riconosciute",
            ["9"] = "Fondazioni",
            ["10"] = "Altri enti ed istituti con personalità giuridica",
            ["11"] = "Consorzi senza personalità giuridica",
            ["12"] = "Associazioni non riconosciute e comitati",
            ["13"] = "Altre organizzazioni di persone o di beni senza personalità giuridica (escluse le comunioni)",
            ["14"] = "Enti pubblici economici",
            ["15"] = "Enti pubblici non economici",
            ["16"] = "Casse mutue e fondi di previdenza, assistenza, pensioni o simili con o senza personalità giuridica",
            ["17"] = "Opere pie e società di mutuo soccorso",
            ["18"] = "Enti ospedalieri",
            ["19"] = "Enti ed istituti di previdenza e di assistenza sociale",
            ["20"] = "Aziende autonome di cura, soggiorno e turismo",
            ["21"] = "Aziende regionali, provinciali, comunali e loro consorzi",
            ["22"] = "Società, organizzazioni ed enti costituiti all'estero non altrimenti classificabili con sede dell'amministrazione od oggetto principale in Italia",
            ["23"] = "Società semplici ed equiparate ai sensi dell'art. 5, comma 3, lett. b), del TUIR",
            ["24"] = "Società in nome collettivo ed equiparate ai sensi dell'art. 5, comma 3, lett. b), del TUIR",
            ["25"] = "Società in accomandita semplice",
            ["26"] = "Società di armamento",
            ["27"] = "Associazione fra artisti e professionisti",
            ["28"] = "Aziende coniugali",
            ["29"] = "GEIE (Gruppi europei di interesse economico)",
            ["50"] = "Società per azioni, aziende speciali e consorzi di cui agli artt. 31, 113, 114, 115 e 116 del D.Lgs 267/2000 (enti locali)",
            ["51"] = "Condomìni",
            ["52"] = "Depositi I.V.A.",
            ["53"] = "Società sportive dilettantistiche costituite in società di capitali senza fine di lucro",
            ["54"] = "Trust",
            ["55"] = "Amministrazioni pubbliche",
            ["56"] = "Fondazioni bancarie",
            ["57"] = "Società europea",
            ["58"] = "Società cooperativa europea",
            ["59"] = "Rete di imprese",
            // Soggetti non residenti
            ["30"] = "Società semplici, irregolari e di fatto (non residenti)",
            ["31"] = "Società in nome collettivo (non residenti)",
            ["32"] = "Società in accomandita semplice (non residenti)",
            ["33"] = "Società di armamento (non residenti)",
            ["34"] = "Associazioni fra professionisti (non residenti)",
            ["35"] = "Società in accomandita per azioni (non residenti)",
            ["36"] = "Società a responsabilità limitata (non residenti)",
            ["37"] = "Società per azioni (non residenti)",
            ["38"] = "Consorzi (non residenti)",
            ["39"] = "Altri enti ed istituti (non residenti)",
            ["40"] = "Associazioni riconosciute, non riconosciute e di fatto (non residenti)",
            ["41"] = "Fondazioni (non residenti)",
            ["42"] = "Opere pie e società di mutuo soccorso (non residenti)",
            ["43"] = "Altre organizzazioni di persone e di beni (non residenti)",
            ["44"] = "Trust (non residenti)",
            ["45"] = "GEIE (Gruppi europei di interesse economico) (non residenti)",
        };

    /// <summary>Tabella codici carica ammessi dal modello AA7/10 (istruzioni 2025).</summary>
    public static readonly IReadOnlyDictionary<string, string> CodiciCarica =
        new Dictionary<string, string>
        {
            ["1"] = "Rappresentante legale, negoziale o di fatto, socio amministratore",
            ["3"] = "Curatore fallimentare / curatore della liquidazione giudiziale",
            ["4"] = "Commissario liquidatore (liquidazione coatta amministrativa o amministrazione straordinaria)",
            ["5"] = "Custode giudiziario, amministratore giudiziario o commissario giudiziale",
            ["6"] = "Rappresentante fiscale di soggetto non residente",
            ["8"] = "Liquidatore (liquidazione volontaria)",
            ["10"] = "Rappresentante fiscale di soggetto non residente (art. 44, c. 3, D.L. 331/1993)",
            ["15"] = "Commissario liquidatore di una pubblica amministrazione",
        };
}
