using System.Text.Json;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Api;

/// <summary>Un campo che l'assistente propone di scrivere, già pronto per l'utente.</summary>
public sealed record CampoProposto(string Etichetta, string Valore);

/// <summary>Un campo che manca: cosa è, perché serve, se blocca il salvataggio.</summary>
public sealed record CampoMancante(string Etichetta, string Perche, bool Bloccante);

/// <summary>
/// Operazione che scrive sull'archivio, preparata dall'assistente e in attesa dell'utente.
/// L'AI non la esegue mai da sola: arriva alla SPA come scheda da confermare.
/// </summary>
public sealed record AzionePropostaAi(
    string Strumento,
    string Titolo,
    string ArgomentiJson,
    IReadOnlyList<CampoProposto> Campi,
    IReadOnlyList<CampoMancante> Mancanti,
    bool PuoProcedere,
    string? Sezione);

/// <summary>
/// Gli strumenti che l'assistente può usare: letture, eseguite subito, e scritture, che
/// diventano una proposta da confermare. Tutto passa per i servizi esistenti, quindi
/// valgono le stesse regole del resto del programma (isolamento per tenant, anti-doppioni,
/// blocchi anti-errore). Il tenant arriva sempre dal token, mai dal modello.
/// </summary>
public sealed class AiChatStrumenti
{
    private readonly AziendeService _aziende;
    private readonly AnagraficheLavoroService _anagrafiche;
    private readonly ScadenzarioService _scadenzario;

    public AiChatStrumenti(
        AziendeService aziende, AnagraficheLavoroService anagrafiche, ScadenzarioService scadenzario)
    {
        _aziende = aziende;
        _anagrafiche = anagrafiche;
        _scadenzario = scadenzario;
    }

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static object Testo(string descrizione) => new { type = "string", description = descrizione };

    /// <summary>Catalogo completo offerto al modello a ogni turno.</summary>
    public static IReadOnlyList<StrumentoAi> Catalogo { get; } =
    [
        new("elenco_aziende",
            "Elenca le aziende in archivio dello studio (denominazione, P.IVA, sede, n° dipendenti). "
            + "Usalo per trovare l'id di un'azienda a partire dal nome.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() },
            Scrittura: false),

        new("scheda_azienda",
            "Scheda completa di un'azienda: anagrafica, dati fiscali e contributivi, e l'elenco "
            + "dei dati che mancano per fare cedolini, F24 e UniEmens.",
            new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["aziendaId"] = Testo("Id (GUID) dell'azienda, come restituito da elenco_aziende."),
                },
                required = new[] { "aziendaId" },
            },
            Scrittura: false),

        new("elenco_dipendenti",
            "Elenca i dipendenti di un'azienda con CCNL, livello, qualifica e data di assunzione.",
            new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["aziendaId"] = Testo("Id (GUID) dell'azienda."),
                },
                required = new[] { "aziendaId" },
            },
            Scrittura: false),

        new("scadenze_studio",
            "Scadenze e promemoria dello studio (DURC, F24, manuali) con i giorni che mancano.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() },
            Scrittura: false),

        new("registra_azienda",
            "Prepara la registrazione di una NUOVA azienda cliente con i dati che l'utente ha "
            + "fornito. Compila tutti i campi che conosci dal messaggio dell'utente e lascia "
            + "vuoti gli altri: non inventare mai P.IVA, codice fiscale, matricole o codici. "
            + "L'operazione non viene eseguita: l'utente la vede e la conferma.",
            SchemaAzienda(nuova: true),
            Scrittura: true),

        new("correggi_azienda",
            "Prepara la correzione dei dati di un'azienda già in archivio: indica solo i campi "
            + "da cambiare. L'utente conferma prima che venga scritto.",
            SchemaAzienda(nuova: false),
            Scrittura: true),

        new("registra_dipendente",
            "Prepara l'assunzione di un dipendente in un'azienda dell'archivio (anagrafica + "
            + "rapporto di lavoro). L'utente conferma prima che venga scritto.",
            SchemaDipendente(),
            Scrittura: true),
    ];

    public static bool EsisteStrumento(string nome) => Catalogo.Any(s => s.Nome == nome);

    public static bool ÈScrittura(string nome) =>
        Catalogo.FirstOrDefault(s => s.Nome == nome)?.Scrittura ?? false;

    // ---------------- Letture: eseguite subito, il risultato torna al modello ----------------

    /// <summary>Esegue uno strumento di lettura e restituisce il JSON che legge il modello.</summary>
    public async Task<string> EseguiLetturaAsync(
        Guid tenantId, string nome, string argomentiJson, CancellationToken ct = default)
    {
        var arg = Argomenti(argomentiJson);
        switch (nome)
        {
            case "elenco_aziende":
            {
                var elenco = await _aziende.ElencoSintesiAsync(tenantId, ct);
                return Serializza(elenco.Select(s => new
                {
                    aziendaId = s.Azienda.Id,
                    denominazione = s.Azienda.RagioneSociale,
                    partitaIva = s.Azienda.PartitaIva,
                    codiceFiscale = s.Azienda.CodiceFiscale,
                    comune = s.Azienda.Comune,
                    provincia = s.Azienda.Provincia,
                    dipendenti = s.NumeroDipendenti,
                    datiMancanti = s.DatiMancanti,
                }));
            }

            case "scheda_azienda":
            {
                if (!Guid.TryParse(arg.GetValueOrDefault("aziendaId"), out var id))
                    return Serializza(new { errore = "aziendaId non valido: usa elenco_aziende per trovarlo." });
                var s = await _aziende.GetSintesiAsync(tenantId, id, ct);
                if (s is null) return Serializza(new { errore = "Azienda non trovata in archivio." });
                var a = s.Azienda;
                return Serializza(new
                {
                    aziendaId = a.Id,
                    denominazione = a.RagioneSociale,
                    a.PartitaIva,
                    a.CodiceFiscale,
                    sede = $"{a.Indirizzo}, {a.Cap} {a.Comune} ({a.Provincia})",
                    a.CodiceCatastale,
                    a.Telefono,
                    a.Email,
                    a.Pec,
                    a.CodiceDestinatarioSdi,
                    a.RegimeFiscale,
                    a.Settore,
                    a.CodiceAteco,
                    a.MatricolaInps,
                    a.CodiceSedeInps,
                    a.CscInps,
                    a.PatInail,
                    a.CcnlDefault,
                    a.LivelloDefault,
                    ibanPresente = !string.IsNullOrWhiteSpace(a.Iban),
                    legaleRappresentante =
                        $"{a.LegaleRappresentanteNome} {a.LegaleRappresentanteCognome}".Trim(),
                    a.LegaleRappresentanteCodiceFiscale,
                    dipendenti = s.NumeroDipendenti,
                    datiMancanti = s.DatiMancanti,
                    prontaPerF24EUniemens = s.DatiContributiviCompleti,
                });
            }

            case "elenco_dipendenti":
            {
                if (!Guid.TryParse(arg.GetValueOrDefault("aziendaId"), out var id))
                    return Serializza(new { errore = "aziendaId non valido: usa elenco_aziende per trovarlo." });
                var rapporti = await _anagrafiche.ElencoRapportiAsync(tenantId, id, ct);
                return Serializza(rapporti.Select(r => new
                {
                    rapportoId = r.Id,
                    nominativo = $"{r.Dipendente?.Cognome} {r.Dipendente?.Nome}".Trim(),
                    codiceFiscale = r.Dipendente?.CodiceFiscale,
                    r.CodiceCcnl,
                    r.Livello,
                    r.Qualifica,
                    dataAssunzione = r.DataAssunzione.ToString("yyyy-MM-dd"),
                    dataCessazione = r.DataCessazione?.ToString("yyyy-MM-dd"),
                    r.PartTime,
                    r.PercentualePartTime,
                }));
            }

            case "scadenze_studio":
            {
                var oggi = DateOnly.FromDateTime(DateTime.UtcNow);
                var promemoria = await _scadenzario.ElencoAsync(tenantId, rigenera: true, ct);
                return Serializza(promemoria.Select(p => new
                {
                    p.Titolo,
                    p.Descrizione,
                    scadenza = p.DataScadenza.ToString("yyyy-MM-dd"),
                    giorniAllaScadenza = p.DataScadenza.DayNumber - oggi.DayNumber,
                    p.Stato,
                    p.Tipo,
                }));
            }

            default:
                return Serializza(new { errore = $"Strumento '{nome}' sconosciuto." });
        }
    }

    // ---------------- Scritture: prima si propongono, poi l'utente conferma ----------------

    /// <summary>Costruisce la scheda da mostrare all'utente: dati proposti e dati mancanti.</summary>
    public async Task<AzionePropostaAi> PreparaScritturaAsync(
        Guid tenantId, string nome, string argomentiJson, CancellationToken ct = default)
    {
        switch (nome)
        {
            case "registra_azienda":
            {
                var a = Leggi<ArgAzienda>(argomentiJson);
                var campi = CampiAzienda(a);
                var mancanti = MancantiAzienda(a);
                return new AzionePropostaAi(
                    nome,
                    $"Registrare l'azienda {Oppure(a.RagioneSociale, "(senza denominazione)")}",
                    argomentiJson, campi, mancanti,
                    PuoProcedere: !mancanti.Any(m => m.Bloccante),
                    Sezione: "aziende");
            }

            case "correggi_azienda":
            {
                var a = Leggi<ArgAzienda>(argomentiJson);
                var esistente = Guid.TryParse(a.AziendaId, out var id)
                    ? await _aziende.GetSintesiAsync(tenantId, id, ct)
                    : null;
                var campi = CampiAzienda(a);
                var mancanti = new List<CampoMancante>();
                if (esistente is null)
                    mancanti.Add(new CampoMancante("Azienda",
                        "Non trovo l'azienda da correggere: serve un'azienda già in archivio.", true));
                if (campi.Count == 0)
                    mancanti.Add(new CampoMancante("Campi da correggere",
                        "Nessun dato da cambiare: indica quali campi correggere.", true));
                return new AzionePropostaAi(
                    nome,
                    $"Correggere i dati di {esistente?.Azienda.RagioneSociale ?? "un'azienda"}",
                    argomentiJson, campi, mancanti,
                    PuoProcedere: mancanti.Count == 0,
                    Sezione: "aziende");
            }

            case "registra_dipendente":
            {
                var d = Leggi<ArgDipendente>(argomentiJson);
                var azienda = Guid.TryParse(d.AziendaId, out var id)
                    ? await _aziende.GetSintesiAsync(tenantId, id, ct)
                    : null;
                var campi = CampiDipendente(d);
                var mancanti = MancantiDipendente(d);
                if (azienda is null)
                    mancanti.Insert(0, new CampoMancante("Azienda",
                        "Serve l'azienda che assume: sceglila dall'archivio.", true));
                return new AzionePropostaAi(
                    nome,
                    $"Assumere {Oppure($"{d.Cognome} {d.Nome}".Trim(), "un dipendente")}"
                        + (azienda is null ? "" : $" in {azienda.Azienda.RagioneSociale}"),
                    argomentiJson, campi, mancanti,
                    PuoProcedere: !mancanti.Any(m => m.Bloccante),
                    Sezione: "dipendenti");
            }

            default:
                return new AzionePropostaAi(nome, "Operazione sconosciuta", argomentiJson,
                    [], [new CampoMancante("Operazione", $"'{nome}' non esiste.", true)], false, null);
        }
    }

    /// <summary>Esegue davvero la scrittura, dopo la conferma dell'utente.</summary>
    public async Task<(bool Riuscita, string Messaggio, object? Esito)> EseguiScritturaAsync(
        Guid tenantId, string nome, string argomentiJson, CancellationToken ct = default)
    {
        try
        {
            switch (nome)
            {
                case "registra_azienda":
                {
                    var a = Leggi<ArgAzienda>(argomentiJson);
                    var creata = await _aziende.CreaAsync(tenantId, InEntita(a, new AziendaEntity()), ct);
                    var sintesi = await _aziende.GetSintesiAsync(tenantId, creata.Id, ct);
                    string manca = sintesi is null || sintesi.DatiMancanti.Count == 0
                        ? "Anagrafica completa."
                        : "Restano da completare: " + string.Join(", ", sintesi.DatiMancanti) + ".";
                    return (true,
                        $"Azienda «{creata.RagioneSociale}» registrata in archivio. {manca}",
                        new { aziendaId = creata.Id, denominazione = creata.RagioneSociale });
                }

                case "correggi_azienda":
                {
                    var a = Leggi<ArgAzienda>(argomentiJson);
                    if (!Guid.TryParse(a.AziendaId, out var id))
                        return (false, "Id azienda mancante o non valido.", null);
                    var corretta = await _aziende.CorreggiAsync(tenantId, id, InCorrezione(a), ct);
                    return corretta is null
                        ? (false, "Azienda non trovata in archivio.", null)
                        : (true, $"Dati di «{corretta.RagioneSociale}» aggiornati.",
                            new { aziendaId = corretta.Id, denominazione = corretta.RagioneSociale });
                }

                case "registra_dipendente":
                {
                    var d = Leggi<ArgDipendente>(argomentiJson);
                    if (!Guid.TryParse(d.AziendaId, out var aziendaId))
                        return (false, "Id azienda mancante o non valido.", null);

                    var dipendente = new DipendenteEntity
                    {
                        CodiceFiscale = d.CodiceFiscale ?? "",
                        Nome = d.Nome ?? "",
                        Cognome = d.Cognome ?? "",
                        Iban = d.Iban,
                    };
                    var rapporto = new RapportoLavoroEntity
                    {
                        CodiceCcnl = d.CodiceCcnl ?? "",
                        Livello = d.Livello ?? "",
                        Qualifica = d.Qualifica ?? "Impiegato",
                        NumeroMensilita = d.NumeroMensilita ?? 14,
                        PartTime = d.PartTime ?? false,
                        PercentualePartTime = d.PercentualePartTime ?? 100m,
                        ScattiAnzianita = d.ScattiAnzianita ?? 0,
                        Superminimo = d.Superminimo ?? 0m,
                        Regione = d.Regione ?? "",
                        CodiceCatastaleComune = d.CodiceCatastaleComune ?? "",
                        DataAssunzione = Data(d.DataAssunzione) ?? DateOnly.FromDateTime(DateTime.UtcNow),
                        OreSettimanali = d.OreSettimanali ?? 40,
                        TipoContratto = d.TipoContratto ?? "Tempo indeterminato",
                    };
                    var creato = await _anagrafiche.CreaDipendenteERapportoAsync(
                        tenantId, aziendaId, dipendente, rapporto, ct);
                    return (true,
                        $"{dipendente.Cognome} {dipendente.Nome} assunto/a: anagrafica e rapporto di lavoro creati.",
                        new { rapportoId = creato.Id, dipendenteId = creato.DipendenteId });
                }

                default:
                    return (false, $"Operazione '{nome}' sconosciuta.", null);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Sono i controlli del programma (duplicati, dati incoerenti): il messaggio è
            // già scritto per un essere umano, quindi si riporta così com'è.
            return (false, ex.Message, null);
        }
    }

    // ---------------- Mappatura e schede ----------------

    private static AziendaEntity InEntita(ArgAzienda a, AziendaEntity e)
    {
        // Solo i campi presenti: così la stessa mappatura vale per creazione e correzione.
        if (a.RagioneSociale is not null) e.RagioneSociale = a.RagioneSociale;
        if (a.PartitaIva is not null) e.PartitaIva = a.PartitaIva;
        if (a.CodiceFiscale is not null) e.CodiceFiscale = a.CodiceFiscale;
        if (a.Indirizzo is not null) e.Indirizzo = a.Indirizzo;
        if (a.Cap is not null) e.Cap = a.Cap;
        if (a.Comune is not null) e.Comune = a.Comune;
        if (a.Provincia is not null) e.Provincia = a.Provincia;
        if (a.Nazione is not null) e.Nazione = a.Nazione;
        if (a.RegimeFiscale is not null) e.RegimeFiscale = a.RegimeFiscale;
        if (a.Telefono is not null) e.Telefono = a.Telefono;
        if (a.Email is not null) e.Email = a.Email;
        if (a.Pec is not null) e.Pec = a.Pec;
        if (a.CodiceDestinatarioSdi is not null) e.CodiceDestinatarioSdi = a.CodiceDestinatarioSdi;
        if (a.CodiceCatastale is not null) e.CodiceCatastale = a.CodiceCatastale;
        if (a.Settore is not null) e.Settore = a.Settore;
        if (a.CodiceAteco is not null) e.CodiceAteco = a.CodiceAteco;
        if (a.MatricolaInps is not null) e.MatricolaInps = a.MatricolaInps;
        if (a.CodiceSedeInps is not null) e.CodiceSedeInps = a.CodiceSedeInps;
        if (a.CscInps is not null) e.CscInps = a.CscInps;
        if (a.PatInail is not null) e.PatInail = a.PatInail;
        if (a.CcnlDefault is not null) e.CcnlDefault = a.CcnlDefault;
        if (a.LivelloDefault is not null) e.LivelloDefault = a.LivelloDefault;
        if (a.Iban is not null) e.Iban = a.Iban;
        if (a.LegaleRappresentanteNome is not null) e.LegaleRappresentanteNome = a.LegaleRappresentanteNome;
        if (a.LegaleRappresentanteCognome is not null) e.LegaleRappresentanteCognome = a.LegaleRappresentanteCognome;
        if (a.LegaleRappresentanteCodiceFiscale is not null)
            e.LegaleRappresentanteCodiceFiscale = a.LegaleRappresentanteCodiceFiscale;
        if (a.HaDipendenti is not null) e.HaDipendenti = a.HaDipendenti.Value;
        if (a.TipoContribuente is not null) e.TipoContribuente = a.TipoContribuente;
        return e;
    }

    /// <summary>Correzione: solo i campi indicati dal modello, gli altri restano com'erano.</summary>
    private static AziendeService.CorrezioneAzienda InCorrezione(ArgAzienda a) => new(
        RagioneSociale: a.RagioneSociale, PartitaIva: a.PartitaIva, CodiceFiscale: a.CodiceFiscale,
        Indirizzo: a.Indirizzo, Cap: a.Cap, Comune: a.Comune, Provincia: a.Provincia,
        Telefono: a.Telefono, Email: a.Email, HaDipendenti: a.HaDipendenti,
        TipoContribuente: a.TipoContribuente, Pec: a.Pec,
        CodiceDestinatarioSdi: a.CodiceDestinatarioSdi, CodiceCatastale: a.CodiceCatastale,
        Settore: a.Settore, CodiceAteco: a.CodiceAteco, MatricolaInps: a.MatricolaInps,
        CodiceSedeInps: a.CodiceSedeInps, CscInps: a.CscInps, PatInail: a.PatInail,
        CcnlDefault: a.CcnlDefault, LivelloDefault: a.LivelloDefault, Iban: a.Iban,
        LegaleRappresentanteNome: a.LegaleRappresentanteNome,
        LegaleRappresentanteCognome: a.LegaleRappresentanteCognome,
        LegaleRappresentanteCodiceFiscale: a.LegaleRappresentanteCodiceFiscale);

    private static List<CampoProposto> CampiAzienda(ArgAzienda a)
    {
        var campi = new List<CampoProposto>();
        void Add(string etichetta, string? valore)
        {
            if (!string.IsNullOrWhiteSpace(valore)) campi.Add(new CampoProposto(etichetta, valore!));
        }
        Add("Denominazione", a.RagioneSociale);
        Add("Partita IVA", a.PartitaIva);
        Add("Codice fiscale", a.CodiceFiscale);
        Add("Indirizzo", a.Indirizzo);
        Add("CAP", a.Cap);
        Add("Comune", a.Comune);
        Add("Provincia", a.Provincia);
        Add("Codice catastale Comune", a.CodiceCatastale);
        Add("Telefono", a.Telefono);
        Add("Email", a.Email);
        Add("PEC", a.Pec);
        Add("Codice destinatario SdI", a.CodiceDestinatarioSdi);
        Add("Regime fiscale", a.RegimeFiscale);
        Add("Settore", a.Settore);
        Add("Codice ATECO", a.CodiceAteco);
        Add("Matricola INPS", a.MatricolaInps);
        Add("Codice sede INPS", a.CodiceSedeInps);
        Add("CSC INPS", a.CscInps);
        Add("P.A.T. INAIL", a.PatInail);
        Add("CCNL", a.CcnlDefault);
        Add("Livello CCNL", a.LivelloDefault);
        Add("IBAN", a.Iban);
        Add("Legale rappresentante",
            $"{a.LegaleRappresentanteNome} {a.LegaleRappresentanteCognome}".Trim());
        Add("CF legale rappresentante", a.LegaleRappresentanteCodiceFiscale);
        return campi;
    }

    private static List<CampoMancante> MancantiAzienda(ArgAzienda a)
    {
        var mancanti = new List<CampoMancante>();
        void Serve(string? valore, string etichetta, string perche, bool bloccante)
        {
            if (string.IsNullOrWhiteSpace(valore)) mancanti.Add(new CampoMancante(etichetta, perche, bloccante));
        }
        Serve(a.RagioneSociale, "Denominazione", "senza il nome non posso salvare l'azienda", true);
        Serve(a.Indirizzo, "Indirizzo sede", "serve nella busta paga e nelle fatture", true);
        Serve(a.Cap, "CAP", "serve nella busta paga e nelle fatture", true);
        Serve(a.Comune, "Comune", "serve nella busta paga e nelle fatture", true);
        Serve(a.Provincia, "Provincia", "serve nella busta paga e nelle fatture", true);
        if (string.IsNullOrWhiteSpace(a.PartitaIva) && string.IsNullOrWhiteSpace(a.CodiceFiscale))
            mancanti.Add(new CampoMancante("Partita IVA o codice fiscale",
                "identifica l'azienda: senza, niente fatture né F24", false));
        Serve(a.CodiceAteco, "Codice ATECO", "determina inquadramento INPS/INAIL", false);
        Serve(a.MatricolaInps, "Matricola INPS", "serve per UniEmens e F24", false);
        Serve(a.CscInps, "CSC INPS", "serve per UniEmens", false);
        Serve(a.PatInail, "P.A.T. INAIL", "serve per l'autoliquidazione INAIL", false);
        Serve(a.CodiceCatastale, "Codice catastale del Comune", "serve per le addizionali IRPEF", false);
        Serve(a.CcnlDefault, "CCNL", "serve per le tabelle retributive dei cedolini", false);
        if (string.IsNullOrWhiteSpace(a.Pec) && string.IsNullOrWhiteSpace(a.CodiceDestinatarioSdi))
            mancanti.Add(new CampoMancante("PEC o codice destinatario SdI",
                "serve per inviare le fatture elettroniche", false));
        return mancanti;
    }

    private static List<CampoProposto> CampiDipendente(ArgDipendente d)
    {
        var campi = new List<CampoProposto>();
        void Add(string etichetta, string? valore)
        {
            if (!string.IsNullOrWhiteSpace(valore)) campi.Add(new CampoProposto(etichetta, valore!));
        }
        Add("Cognome", d.Cognome);
        Add("Nome", d.Nome);
        Add("Codice fiscale", d.CodiceFiscale);
        Add("CCNL", d.CodiceCcnl);
        Add("Livello", d.Livello);
        Add("Qualifica", d.Qualifica);
        Add("Tipo contratto", d.TipoContratto);
        Add("Data assunzione", d.DataAssunzione);
        Add("Ore settimanali", d.OreSettimanali?.ToString());
        if (d.PartTime == true)
            Add("Part-time", $"{d.PercentualePartTime ?? 100m:0.##}%");
        Add("Mensilità", d.NumeroMensilita?.ToString());
        Add("Superminimo", d.Superminimo?.ToString("0.00"));
        Add("Regione", d.Regione);
        Add("Comune (codice catastale)", d.CodiceCatastaleComune);
        Add("IBAN", d.Iban);
        return campi;
    }

    private static List<CampoMancante> MancantiDipendente(ArgDipendente d)
    {
        var mancanti = new List<CampoMancante>();
        void Serve(string? valore, string etichetta, string perche, bool bloccante)
        {
            if (string.IsNullOrWhiteSpace(valore)) mancanti.Add(new CampoMancante(etichetta, perche, bloccante));
        }
        Serve(d.Cognome, "Cognome", "identifica la persona", true);
        Serve(d.Nome, "Nome", "identifica la persona", true);
        Serve(d.CodiceFiscale, "Codice fiscale", "serve per UniEmens, CU e per evitare doppioni", true);
        Serve(d.CodiceCcnl, "CCNL", "senza contratto non ci sono tabelle retributive", true);
        Serve(d.Livello, "Livello", "determina la paga base", true);
        Serve(d.DataAssunzione, "Data di assunzione",
            "senza data uso oggi, ma le buste paga precedenti verrebbero bloccate", false);
        Serve(d.Regione, "Regione di residenza", "serve per l'addizionale regionale IRPEF", false);
        Serve(d.CodiceCatastaleComune, "Comune di residenza (codice catastale)",
            "serve per l'addizionale comunale IRPEF", false);
        return mancanti;
    }

    // ---------------- Utilità ----------------

    private static string Oppure(string? valore, string sostituto) =>
        string.IsNullOrWhiteSpace(valore) ? sostituto : valore!;

    private static DateOnly? Data(string? testo) =>
        DateOnly.TryParse(testo, out var d) ? d : null;

    private static string Serializza(object valore) => JsonSerializer.Serialize(valore);

    /// <summary>Argomenti come dizionario di stringhe, per gli strumenti semplici.</summary>
    private static Dictionary<string, string> Argomenti(string json)
    {
        var esito = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return esito;
            foreach (var p in doc.RootElement.EnumerateObject())
                esito[p.Name] = p.Value.ValueKind == JsonValueKind.String
                    ? p.Value.GetString() ?? "" : p.Value.ToString();
        }
        catch (JsonException) { /* il modello ha sbagliato JSON: argomenti vuoti */ }
        return esito;
    }

    private static T Leggi<T>(string json) where T : new()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    // Argomenti degli strumenti di scrittura: proprietà nullable, così "assente" e "vuoto"
    // restano distinguibili (serve alla correzione, che tocca solo i campi indicati).
    private sealed class ArgAzienda
    {
        public string? AziendaId { get; set; }
        public string? RagioneSociale { get; set; }
        public string? PartitaIva { get; set; }
        public string? CodiceFiscale { get; set; }
        public string? Indirizzo { get; set; }
        public string? Cap { get; set; }
        public string? Comune { get; set; }
        public string? Provincia { get; set; }
        public string? Nazione { get; set; }
        public string? RegimeFiscale { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Pec { get; set; }
        public string? CodiceDestinatarioSdi { get; set; }
        public string? CodiceCatastale { get; set; }
        public string? Settore { get; set; }
        public string? CodiceAteco { get; set; }
        public string? MatricolaInps { get; set; }
        public string? CodiceSedeInps { get; set; }
        public string? CscInps { get; set; }
        public string? PatInail { get; set; }
        public string? CcnlDefault { get; set; }
        public string? LivelloDefault { get; set; }
        public string? Iban { get; set; }
        public string? LegaleRappresentanteNome { get; set; }
        public string? LegaleRappresentanteCognome { get; set; }
        public string? LegaleRappresentanteCodiceFiscale { get; set; }
        public string? TipoContribuente { get; set; }
        public bool? HaDipendenti { get; set; }
    }

    private sealed class ArgDipendente
    {
        public string? AziendaId { get; set; }
        public string? CodiceFiscale { get; set; }
        public string? Nome { get; set; }
        public string? Cognome { get; set; }
        public string? CodiceCcnl { get; set; }
        public string? Livello { get; set; }
        public string? Qualifica { get; set; }
        public string? TipoContratto { get; set; }
        public string? DataAssunzione { get; set; }
        public string? Regione { get; set; }
        public string? CodiceCatastaleComune { get; set; }
        public string? Iban { get; set; }
        public int? NumeroMensilita { get; set; }
        public int? OreSettimanali { get; set; }
        public int? ScattiAnzianita { get; set; }
        public bool? PartTime { get; set; }
        public decimal? PercentualePartTime { get; set; }
        public decimal? Superminimo { get; set; }
    }

    // ---------------- Schemi JSON degli strumenti di scrittura ----------------

    private static object SchemaAzienda(bool nuova)
    {
        var proprieta = new Dictionary<string, object>
        {
            ["ragioneSociale"] = Testo("Denominazione completa dell'azienda."),
            ["partitaIva"] = Testo("Partita IVA, 11 cifre. Solo se l'utente l'ha scritta."),
            ["codiceFiscale"] = Testo("Codice fiscale dell'azienda (11 cifre o 16 caratteri per le ditte individuali)."),
            ["indirizzo"] = Testo("Via e numero civico della sede legale."),
            ["cap"] = Testo("CAP della sede legale."),
            ["comune"] = Testo("Comune della sede legale."),
            ["provincia"] = Testo("Sigla della provincia, 2 lettere (es. MI)."),
            ["codiceCatastale"] = Testo("Codice catastale del Comune (es. F205), serve per le addizionali IRPEF."),
            ["telefono"] = Testo("Telefono."),
            ["email"] = Testo("Email ordinaria."),
            ["pec"] = Testo("Indirizzo PEC."),
            ["codiceDestinatarioSdi"] = Testo("Codice destinatario SdI, 7 caratteri."),
            ["regimeFiscale"] = Testo("Regime fiscale FatturaPA (RF01 ordinario, RF19 forfettario)."),
            ["settore"] = Testo("Settore: Industria, Artigianato, Commercio, Terziario, Edilizia, Agricoltura."),
            ["codiceAteco"] = Testo("Codice ATECO dell'attività."),
            ["matricolaInps"] = Testo("Matricola aziendale INPS."),
            ["codiceSedeInps"] = Testo("Codice della sede INPS competente."),
            ["cscInps"] = Testo("CSC INPS, 5 cifre."),
            ["patInail"] = Testo("P.A.T. INAIL, 8 cifre."),
            ["ccnlDefault"] = Testo("Codice del CCNL applicato di norma in azienda."),
            ["livelloDefault"] = Testo("Livello CCNL usato di norma."),
            ["iban"] = Testo("IBAN aziendale per F24 e bonifici."),
            ["legaleRappresentanteNome"] = Testo("Nome del legale rappresentante."),
            ["legaleRappresentanteCognome"] = Testo("Cognome del legale rappresentante."),
            ["legaleRappresentanteCodiceFiscale"] = Testo("Codice fiscale del legale rappresentante."),
            ["tipoContribuente"] = Testo("Tipo contribuente (es. Societa, DittaIndividuale)."),
            ["haDipendenti"] = new { type = "boolean", description = "Vero se l'azienda ha dipendenti." },
        };
        if (!nuova)
            proprieta["aziendaId"] = Testo("Id (GUID) dell'azienda da correggere, da elenco_aziende.");

        return new
        {
            type = "object",
            properties = proprieta,
            required = nuova ? Array.Empty<string>() : new[] { "aziendaId" },
        };
    }

    private static object SchemaDipendente() => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["aziendaId"] = Testo("Id (GUID) dell'azienda che assume, da elenco_aziende."),
            ["nome"] = Testo("Nome del lavoratore."),
            ["cognome"] = Testo("Cognome del lavoratore."),
            ["codiceFiscale"] = Testo("Codice fiscale del lavoratore, 16 caratteri."),
            ["codiceCcnl"] = Testo("Codice del CCNL applicato al rapporto."),
            ["livello"] = Testo("Livello di inquadramento nel CCNL."),
            ["qualifica"] = Testo("Qualifica: Operaio, Impiegato, Quadro, Dirigente, Apprendista."),
            ["tipoContratto"] = Testo("Es. Tempo indeterminato, Tempo determinato, Apprendistato."),
            ["dataAssunzione"] = Testo("Data di assunzione in formato AAAA-MM-GG."),
            ["oreSettimanali"] = new { type = "integer", description = "Ore settimanali contrattuali (40 a tempo pieno)." },
            ["partTime"] = new { type = "boolean", description = "Vero se il rapporto è part-time." },
            ["percentualePartTime"] = new { type = "number", description = "Percentuale di part-time (100 = tempo pieno)." },
            ["numeroMensilita"] = new { type = "integer", description = "Mensilità annue (12, 13 o 14)." },
            ["scattiAnzianita"] = new { type = "integer", description = "Scatti di anzianità maturati." },
            ["superminimo"] = new { type = "number", description = "Superminimo mensile in euro." },
            ["regione"] = Testo("Regione di residenza, per l'addizionale regionale IRPEF."),
            ["codiceCatastaleComune"] = Testo("Codice catastale del Comune di residenza, per l'addizionale comunale."),
            ["iban"] = Testo("IBAN del lavoratore per il bonifico dello stipendio."),
        },
        required = new[] { "aziendaId" },
    };
}
