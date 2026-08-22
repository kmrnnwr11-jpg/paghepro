using System.Text.Json;
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
public sealed class EsecutoreLettureAurora(ProfessionalQueryBroker broker)
    : IEsecutoreLettureAurora
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RisultatoStrumentoAurora> EseguiAsync(
        ContestoAurora contesto, DescrittoreStrumento descrittore,
        string argomentiJson, CancellationToken ct)
    {
        var esito = await broker.LeggiAsync(
            contesto.TenantId, contesto.UtenteId, contesto.Lease, descrittore.Risorsa, ct);
        if (!esito.Consentito)
            return new(
                JsonSerializer.Serialize(new { negato = true, motivo = esito.Motivo }, Json),
                Negato: true, esito.Motivo, Righe: 0);

        return new(
            JsonSerializer.Serialize(new
            {
                righe = esito.Righe,
                totale = esito.Totale,
                lettureRimanenti = esito.LettureRimanenti,
            }, Json),
            Negato: false, null, esito.Righe.Count);
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
        foreach (var descrittore in ScrittureDichiarate)
        {
            var d = descrittore;
            services.AddScoped<IStrumentoAurora>(_ => new StrumentoScritturaDichiarata(d));
        }
        services.AddScoped<AiToolRegistry>();
        services.AddScoped<IEsecutoreLettureAurora, EsecutoreLettureAurora>();
        services.AddScoped<AiRuntimeStore>();
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
