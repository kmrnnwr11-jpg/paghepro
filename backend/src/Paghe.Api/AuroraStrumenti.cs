using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Ai;

namespace Paghe.Api;

/// <summary>
/// Strumento di lettura appoggiato al <see cref="ProfessionalQueryBroker"/>: lo scope
/// (azienda, periodo) viene dal manifest del mandato, mai da argomenti del modello — per
/// questo lo schema è vuoto. Il broker vidima la lease, minimizza le righe e consuma quota.
/// </summary>
public sealed class StrumentoLetturaBroker(DescrittoreStrumento descrittore) : IStrumentoAurora
{
    public DescrittoreStrumento Descrittore { get; } = descrittore;
}

/// <summary>
/// Esecutore centrale delle letture: un plugin A2 può solo scegliere un descrittore già
/// filtrato dal registry; dati e quota passano sempre dal ProfessionalQueryBroker.
/// </summary>
public sealed class EsecutoreLettureAurora(
    ProfessionalQueryBroker broker,
    Paghe.Persistence.PagheDbContext db,
    Paghe.Persistence.UniEmensService uniemens)
    : IEsecutoreLettureAurora
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RisultatoStrumentoAurora> EseguiAsync(
        ContestoAurora contesto, DescrittoreStrumento descrittore,
        string argomentiJson, CancellationToken ct)
    {
        // L'autorizzazione resta identica per tutte le letture: lease, scope e quota dal broker.
        var esito = await broker.LeggiAsync(
            contesto.TenantId, contesto.UtenteId, contesto.Lease, descrittore.Risorsa, ct);
        if (!esito.Consentito)
            return new(
                JsonSerializer.Serialize(new { negato = true, motivo = esito.Motivo }, Json),
                Negato: true, esito.Motivo, Righe: 0);

        if (string.Equals(descrittore.Nome, "sintesi_uniemens", StringComparison.Ordinal))
            return await SintesiUniemensAsync(contesto, ct);

        var righe = contesto.RapportoIdWorkspace is { } rapportoId
            ? esito.Righe.Where(riga => RigaNelRapporto(riga, rapportoId)).ToArray()
            : [.. esito.Righe];

        return new(
            JsonSerializer.Serialize(new
            {
                righe,
                totale = contesto.RapportoIdWorkspace is null ? esito.Totale : righe.Length,
                lettureRimanenti = esito.LettureRimanenti,
            }, Json),
            Negato: false, null, righe.Length);
    }

    private static bool RigaNelRapporto(object riga, Guid rapportoId) => riga switch
    {
        RigaDipendente dipendente => dipendente.RapportoId == rapportoId,
        RigaCartellino cartellino => cartellino.RapportoId == rapportoId,
        RigaCedolinoMese cedolino => cedolino.RapportoId == rapportoId,
        // Risorse intrinsecamente aziendali (anagrafica, scadenze, UniEmens aggregato)
        // restano disponibili perché non rappresentano un altro lavoratore.
        _ => true,
    };

    /// <summary>
    /// Sintesi minimizzata della denuncia UniEmens: al modello arrivano soltanto conteggi ed
    /// esiti, mai codici fiscali, imponibili o il file XML (che resta sulla pagina Elaborazioni).
    /// </summary>
    private async Task<RisultatoStrumentoAurora> SintesiUniemensAsync(
        ContestoAurora contesto, CancellationToken ct)
    {
        if (contesto.Manifest?.AziendaId is not { } aziendaId
            || contesto.Manifest.Periodo is not { } periodo
            || periodo.Length != 7
            || !int.TryParse(periodo[..4], out var anno)
            || !int.TryParse(periodo[5..], out var mese))
            return new(
                JsonSerializer.Serialize(new
                {
                    negato = true,
                    motivo = "Serve un mandato S2 con azienda e periodo.",
                }, Json), Negato: true, "Mandato senza azienda/periodo.", Righe: 0);

        var matricola = await db.Aziende.AsNoTracking()
            .Where(a => a.Id == aziendaId && a.TenantId == contesto.TenantId)
            .Select(a => a.MatricolaInps)
            .FirstOrDefaultAsync(ct) ?? "";
        try
        {
            var esito = await uniemens.GeneraDelMeseAsync(
                contesto.TenantId, aziendaId, anno, mese, matricola, ct);
            return new(
                JsonSerializer.Serialize(new
                {
                    bozzaDisponibile = true,
                    periodo,
                    posizioni = esito.Flusso.Azienda.Posizione.Lavoratori.Count,
                    conformeSchemaUfficiale = esito.ErroriSchema.Count == 0,
                    datiDaCompletare = esito.DaCompletare.Count,
                    avvertenza = "Il file XML si scarica dalla pagina Elaborazioni e va fatto "
                        + "passare dal software di controllo ufficiale INPS prima dell'invio.",
                }, Json), Negato: false, null, Righe: 1);
        }
        catch (ArgumentException ex)
        {
            // Motivi di dominio già pensati per l'operatore (cedolini in bozza, azienda
            // non pronta): il modello li riporta senza inventare.
            return new(
                JsonSerializer.Serialize(new
                {
                    bozzaDisponibile = false,
                    periodo,
                    motivo = ex.Message,
                }, Json), Negato: false, null, Righe: 1);
        }
    }
}

/// <summary>
/// Strumento di scrittura dichiarato ma non ancora attivo: esiste nel registro con il suo
/// gate (fase A3), così catalogo e piani dicono la verità, ma non è mai offerto al modello
/// né applicabile finché il gate non apre. Le capacità legacy di scrittura della vecchia
/// chat sono riclassificate qui.
/// </summary>
public sealed class StrumentoScritturaDichiarata(DescrittoreStrumento descrittore) : IStrumentoAurora
{
    public DescrittoreStrumento Descrittore { get; } = descrittore;
}

/// <summary>Primo tool A3 reale: patch server-side del solo calendario presenze.</summary>
public sealed class StrumentoCompilaPresenzeAurora(
    CompilaPresenzeAuroraService servizio,
    AiGrantService governor) : IStrumentoAurora
{
    private static readonly string[] DipendenzeLettura =
        ["Dipendenti", "Rapporti", "Cartellini", "Cedolini", "PraticheF24"];

    public DescrittoreStrumento Descrittore { get; } =
        AuroraStrumentiCatalogo.CompilaPresenze;

    public async Task<PropostaAurora> PreparaAsync(
        ContestoAurora contesto, string argomentiJson, CancellationToken ct)
    {
        if (contesto.Manifest?.AziendaId is not { } aziendaId
            || contesto.Manifest.Periodo is not { } periodo)
            return new("Presenze non modificabili", [], false,
                "Serve un mandato S2 con azienda e periodo.");
        try
        {
            // Tutte le dipendenze vengono validate prima; la quota è prenotata con un solo
            // incremento, quindi un residuo insufficiente non produce consumi parziali.
            var (accesso, _) = await governor.AutorizzaPacchettoLettureAsync(
                contesto.TenantId, contesto.UtenteId, contesto.Lease,
                DipendenzeLettura, ct);
            if (!accesso.Consentito)
                return new("Presenze non modificabili", [], false, accesso.Motivo);

            try
            {
                var proposta = await servizio.PreparaAsync(
                    contesto.TenantId, aziendaId, periodo, argomentiJson,
                    ct, contesto.RapportoIdWorkspace);
                return new(
                    proposta.Titolo,
                    proposta.Diff,
                    true,
                    null,
                    proposta.ComandoCanonicoJson,
                    proposta.UndoJson,
                    proposta.PrecondizioneHash,
                    proposta.PostcondizioneHash);
            }
            catch (OperazioneAuroraNonConsentitaException ex)
            {
                // I controlli hanno effettivamente letto i dati e il motivo viene restituito:
                // la quota resta consumata. I motivi D3A sono già resi indistinguibili dal service.
                return new("Presenze non modificabili", [], false, ex.Message);
            }
            catch
            {
                // Errore operativo senza risultato: riconcilia l'intero pacchetto.
                await governor.RimborsaLettureAsync(
                    contesto.TenantId, contesto.UtenteId,
                    contesto.Manifest.GrantId, DipendenzeLettura.Length,
                    CancellationToken.None);
                throw;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    }

    public async Task<(bool Riuscita, string EsitoJson)> ApplicaAsync(
        ContestoAurora contesto, string argomentiJson, CancellationToken ct)
    {
        if (contesto.AziendaIdProposta is not { } aziendaId
            || contesto.PeriodoProposta is not { } periodo)
            throw new OperazioneAuroraNonConsentitaException(
                "Scope server-side della proposta assente.");
        var esito = await servizio.ApplicaAsync(
            contesto.TenantId, aziendaId, periodo, argomentiJson, ct);
        return (true, esito.EsitoJson);
    }

    public async Task<(bool Riuscita, string EsitoJson)> RipristinaAsync(
        ContestoAurora contesto, string undoJson, CancellationToken ct)
    {
        if (contesto.AziendaIdProposta is not { } aziendaId
            || contesto.PeriodoProposta is not { } periodo)
            throw new OperazioneAuroraNonConsentitaException(
                "Scope server-side della proposta assente.");
        var esito = await servizio.RipristinaAsync(
            contesto.TenantId, aziendaId, periodo, undoJson, ct);
        return (true, esito.EsitoJson);
    }
}

/// <summary>Secondo tool A3: elabora il cedolino bozza del mese dal cartellino versionato.</summary>
public sealed class StrumentoElaboraCedolinoAurora(
    ElaboraCedolinoAuroraService servizio,
    AiGrantService governor) : IStrumentoAurora
{
    private static readonly string[] DipendenzeLettura =
        ["Dipendenti", "Rapporti", "Cartellini", "Cedolini", "PraticheF24"];

    public DescrittoreStrumento Descrittore { get; } =
        AuroraStrumentiCatalogo.ElaboraCedolino;

    public async Task<PropostaAurora> PreparaAsync(
        ContestoAurora contesto, string argomentiJson, CancellationToken ct)
    {
        if (contesto.Manifest?.AziendaId is not { } aziendaId
            || contesto.Manifest.Periodo is not { } periodo)
            return new("Cedolino non elaborabile", [], false,
                "Serve un mandato S2 con azienda e periodo.");
        try
        {
            var (accesso, _) = await governor.AutorizzaPacchettoLettureAsync(
                contesto.TenantId, contesto.UtenteId, contesto.Lease,
                DipendenzeLettura, ct);
            if (!accesso.Consentito)
                return new("Cedolino non elaborabile", [], false, accesso.Motivo);

            try
            {
                var proposta = await servizio.PreparaAsync(
                    contesto.TenantId, aziendaId, periodo, argomentiJson,
                    ct, contesto.RapportoIdWorkspace);
                return new(
                    proposta.Titolo,
                    proposta.Diff,
                    true,
                    null,
                    proposta.ComandoCanonicoJson,
                    proposta.UndoJson,
                    proposta.PrecondizioneHash,
                    proposta.PostcondizioneHash);
            }
            catch (OperazioneAuroraNonConsentitaException ex)
            {
                return new("Cedolino non elaborabile", [], false, ex.Message);
            }
            catch
            {
                await governor.RimborsaLettureAsync(
                    contesto.TenantId, contesto.UtenteId,
                    contesto.Manifest.GrantId, DipendenzeLettura.Length,
                    CancellationToken.None);
                throw;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    }

    public async Task<(bool Riuscita, string EsitoJson)> ApplicaAsync(
        ContestoAurora contesto, string argomentiJson, CancellationToken ct)
    {
        if (contesto.AziendaIdProposta is not { } aziendaId
            || contesto.PeriodoProposta is not { } periodo)
            throw new OperazioneAuroraNonConsentitaException(
                "Scope server-side della proposta assente.");
        var esito = await servizio.ApplicaAsync(
            contesto.TenantId, aziendaId, periodo, argomentiJson, ct);
        return (true, esito.EsitoJson);
    }

    public async Task<(bool Riuscita, string EsitoJson)> RipristinaAsync(
        ContestoAurora contesto, string undoJson, CancellationToken ct)
    {
        if (contesto.AziendaIdProposta is not { } aziendaId
            || contesto.PeriodoProposta is not { } periodo)
            throw new OperazioneAuroraNonConsentitaException(
                "Scope server-side della proposta assente.");
        var esito = await servizio.RipristinaAsync(
            contesto.TenantId, aziendaId, periodo, undoJson, ct);
        return (true, esito.EsitoJson);
    }
}

/// <summary>Catalogo Aurora della fase A2: le sette capacità legacy riclassificate.</summary>
public static class AuroraStrumentiCatalogo
{
    private static readonly object SchemaVuoto = new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>(),
        additionalProperties = false,
    };

    // Gli schemi completi delle scritture arrivano con la fase A3 (diff + conferma attiva):
    // dichiararli oggi darebbe l'impressione di uno strumento pronto.
    private static readonly object SchemaRinviatoA3 = new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>(),
        additionalProperties = false,
        description = "Definito nella fase A3 del piano Aurora.",
    };

    private static readonly object SchemaCompilaPresenze = new
    {
        type = "object",
        properties = new
        {
            rapportoId = new
            {
                type = "string",
                format = "uuid",
                description = "ID rapporto restituito da elenco_dipendenti.",
            },
            date = new
            {
                type = "array",
                minItems = 1,
                maxItems = 31,
                uniqueItems = true,
                items = new { type = "string", format = "date" },
            },
            causale = new
            {
                type = "string",
                @const = "AssenzaNonRetribuita",
            },
        },
        required = new[] { "rapportoId", "date", "causale" },
        additionalProperties = false,
    };

    public static readonly DescrittoreStrumento CompilaPresenze = new(
        "compila_presenze",
        "Prepara assenze non retribuite su giornate lavorate (da confermare)",
        "Prepara una modifica reversibile del cartellino del mese: trasforma soltanto le date "
        + "indicate da giornata lavorata completa ad assenza non retribuita. Usa rapportoId "
        + "restituito da elenco_dipendenti. Non usare per malattia, infortunio, rapporti parziali, "
        + "date future o mesi con cedolino.",
        SchemaCompilaPresenze,
        "scrittura_reversibile",
        "diff",
        "operatore",
        Risorsa: "Cartellini",
        Operazione: "draft",
        Gate: "A3",
        Idempotente: true,
        Undo: "Ripristino automatico finché il cartellino non cambia e non esiste un cedolino.",
        OperazioneConferma: "apply_reversible",
        VersioneStrumento: CompilaPresenzeAuroraService.VersioneStrumento,
        PermessiRichiesti:
        [
            "Dipendenti|read",
            "Rapporti|read",
            "Cartellini|read",
            "Cedolini|read",
            "PraticheF24|read",
            "Cartellini|draft",
            "Cartellini|apply_reversible",
        ]);

    private static readonly object SchemaElaboraCedolino = new
    {
        type = "object",
        properties = new
        {
            rapportoId = new
            {
                type = "string",
                description = "Id del rapporto di lavoro (da elenco_dipendenti).",
            },
        },
        required = new[] { "rapportoId" },
        additionalProperties = false,
    };

    public static readonly DescrittoreStrumento ElaboraCedolino = new(
        "elabora_cedolino",
        "Elabora il cedolino bozza del mese dal cartellino (da confermare)",
        "Chiama questo strumento DIRETTAMENTE quando l'utente chiede di elaborare, calcolare o "
        + "fare il cedolino/la busta paga del mese: non chiedere conferma prima, perché lo "
        + "strumento non applica nulla — prepara una proposta che l'utente conferma dalla card. "
        + "Usa rapportoId restituito da elenco_dipendenti. Non usare se il cedolino del mese "
        + "esiste già o se il cartellino è da compilare. La bozza va poi controllata e validata "
        + "dall'operatore nel workflow.",
        SchemaElaboraCedolino,
        "scrittura_reversibile",
        "diff",
        "operatore",
        Risorsa: "Cedolini",
        Operazione: "draft",
        Gate: "A3",
        Idempotente: true,
        Undo: "La bozza viene eliminata finché nessuno l'ha controllata o validata.",
        OperazioneConferma: "apply_reversible",
        VersioneStrumento: ElaboraCedolinoAuroraService.VersioneStrumento,
        PermessiRichiesti:
        [
            "Dipendenti|read",
            "Rapporti|read",
            "Cartellini|read",
            "Cedolini|read",
            "PraticheF24|read",
            "Cedolini|draft",
            "Cedolini|apply_reversible",
        ]);

    /// <summary>Le letture attive: scope dal mandato, righe minimizzate dal broker.</summary>
    public static readonly IReadOnlyList<DescrittoreStrumento> Letture =
    [
        new("scheda_azienda",
            "Legge lo stato dell'anagrafica dell'azienda del mandato",
            "Stato dell'anagrafica dell'azienda del mandato corrente: denominazione e quali dati "
            + "contributivi (INPS, INAIL, codice catastale, IBAN, recapito fiscale) risultano "
            + "presenti o mancanti. Nessun argomento: l'azienda è quella del mandato.",
            SchemaVuoto, "lettura", "tabella", "sola_lettura",
            Risorsa: "Aziende", Operazione: "read", Gate: "A2",
            Idempotente: true, Undo: "Nessun effetto: sola lettura."),

        new("elenco_dipendenti",
            "Elenca i dipendenti dell'azienda del mandato",
            "Dipendenti dell'azienda del mandato corrente con CCNL e livello (nominativo e "
            + "inquadramento, senza dati fiscali o economici). Nessun argomento.",
            SchemaVuoto, "lettura", "tabella", "sola_lettura",
            Risorsa: "Dipendenti", Operazione: "read", Gate: "A2",
            Idempotente: true, Undo: "Nessun effetto: sola lettura."),

        new("sintesi_uniemens",
            "Prepara la sintesi della denuncia UniEmens del mese (bozza)",
            "Genera la BOZZA della denuncia UniEmens per azienda e mese del mandato e ne "
            + "restituisce solo la sintesi: numero di posizioni e avvertenze. Richiede cedolini "
            + "validati o chiusi; con sole bozze spiega cosa manca. Il file XML si scarica "
            + "dalla pagina Elaborazioni, non passa mai in chat. Nessun argomento.",
            SchemaVuoto, "lettura", "testo", "sola_lettura",
            Risorsa: "Cedolini", Operazione: "read", Gate: "A2",
            Idempotente: true, Undo: "Nessun effetto: la bozza non viene salvata."),

        new("scadenze_studio",
            "Legge scadenze e promemoria pertinenti al mandato",
            "Scadenze e promemoria (DURC, F24, manuali) dell'azienda del mandato e dello studio, "
            + "con urgenza e giorni mancanti. Nessun argomento.",
            SchemaVuoto, "lettura", "tabella", "sola_lettura",
            Risorsa: "Scadenzario", Operazione: "read", Gate: "A2",
            Idempotente: true, Undo: "Nessun effetto: sola lettura."),

        new("presenze_mese",
            "Controlla quali cartellini del mese del mandato sono compilati",
            "Per ogni dipendente dell'azienda e del periodo del mandato dice se il cartellino "
            + "presenze del mese risulta compilato. Nessun importo e nessun dato di salute. "
            + "Nessun argomento.",
            SchemaVuoto, "lettura", "tabella", "sola_lettura",
            Risorsa: "Cartellini", Operazione: "read", Gate: "A2",
            Idempotente: true, Undo: "Nessun effetto: sola lettura."),

        new("stato_cedolini_mese",
            "Controlla lo stato operativo dei cedolini del mese del mandato",
            "Stato operativo dei cedolini del periodo del mandato (cartellino presente, da "
            + "compilare, già elaborato, bloccato, protetto) con il motivo, per un lotto massimo "
            + "di dieci rapporti. Nessun importo. Nessun argomento.",
            SchemaVuoto, "lettura", "tabella", "sola_lettura",
            Risorsa: "Cedolini", Operazione: "read", Gate: "A2",
            Idempotente: true, Undo: "Nessun effetto: sola lettura."),
    ];

    /// <summary>
    /// Le scritture legacy riclassificate: dichiarate al gate A3, quindi mai offerte al
    /// modello oggi. `elenco_aziende` della vecchia chat non rientra: leggeva l'intero
    /// archivio (scope S3), non concedibile in G2 — l'azienda arriva dal mandato.
    /// </summary>
    public static readonly IReadOnlyList<DescrittoreStrumento> ScrittureDichiarate =
    [
        new("registra_azienda",
            "Prepara la registrazione di una nuova azienda cliente (da confermare)",
            "Non disponibile in questa fase.",
            SchemaRinviatoA3, "scrittura_reversibile", "diff", "operatore",
            Risorsa: "Aziende", Operazione: "draft", Gate: "A3",
            Idempotente: true, Undo: "Correggere o eliminare l'anagrafica dalla pagina Aziende."),

        new("correggi_azienda",
            "Prepara la correzione dei dati di un'azienda in archivio (da confermare)",
            "Non disponibile in questa fase.",
            SchemaRinviatoA3, "scrittura_reversibile", "diff", "operatore",
            Risorsa: "Aziende", Operazione: "draft", Gate: "A3",
            Idempotente: true, Undo: "Ripristinare i valori precedenti dalla scheda azienda."),

        new("registra_dipendente",
            "Prepara l'assunzione di un dipendente (anagrafica + rapporto, da confermare)",
            "Non disponibile in questa fase.",
            SchemaRinviatoA3, "scrittura_reversibile", "diff", "operatore",
            Risorsa: "Dipendenti", Operazione: "draft", Gate: "A3",
            Idempotente: true, Undo: "Correggere il rapporto dalla scheda dipendente."),
    ];

    /// <summary>Registra il runtime Aurora e gli strumenti a plugin nel contenitore.</summary>
    public static IServiceCollection AddAurora(this IServiceCollection services)
    {
        foreach (var descrittore in Letture)
        {
            var d = descrittore;
            services.AddScoped<IStrumentoAurora>(sp =>
                new StrumentoLetturaBroker(d));
        }
        // I tre placeholder legacy restano nel catalogo documentale ma non entrano nel DI.
        // Il solo tool A3 registrato è quello realmente implementato e allowlisted.
        services.AddScoped<CompilaPresenzeAuroraService>();
        services.AddScoped<IStrumentoAurora, StrumentoCompilaPresenzeAurora>();
        services.AddScoped<ElaboraCedolinoAuroraService>();
        services.AddScoped<IStrumentoAurora, StrumentoElaboraCedolinoAurora>();
        services.AddScoped<AiToolRegistry>();
        services.AddScoped<IEsecutoreLettureAurora, EsecutoreLettureAurora>();
        services.AddScoped<AiRuntimeStore>();
        services.AddScoped<RiconciliazioneProposteAurora>();
        // Factory esplicita: il timeout per strumento resta il default di produzione.
        services.AddScoped(sp => new AuroraAgentRuntime(
            sp.GetRequiredService<AiChatClient>(),
            sp.GetRequiredService<AiToolRegistry>(),
            sp.GetRequiredService<Paghe.Persistence.Ai.AiGrantService>(),
            sp.GetRequiredService<IEsecutoreLettureAurora>(),
            sp.GetRequiredService<Paghe.Persistence.Ai.AiRuntimeStore>(),
            sp.GetRequiredService<Paghe.Persistence.ConsumoAiService>(),
            sp.GetRequiredService<AiOptions>()));
        return services;
    }
}
