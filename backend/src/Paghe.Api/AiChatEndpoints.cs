using System.Globalization;
using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>Un turno di conversazione: il messaggio dell'utente più la storia precedente.</summary>
public sealed record RichiestaChatAi(
    string Messaggio,
    List<MessaggioChatAi>? Storia = null,
    Guid? AziendaId = null);

/// <summary>Conferma di un'operazione che l'assistente aveva soltanto proposto.</summary>
public sealed record RichiestaAzioneChatAi(
    string Strumento,
    string ArgomentiJson,
    List<MessaggioChatAi>? Storia = null,
    Guid? AziendaId = null);

/// <summary>Risposta di un turno: testo, storia aggiornata e l'eventuale operazione da confermare.</summary>
public sealed record RispostaChatEndpoint(
    string Testo, List<MessaggioChatAi> Storia, AzionePropostaAi? Azione, bool Ok);

/// <summary>Esito dell'operazione confermata dall'utente.</summary>
public sealed record EsitoAzioneChatEndpoint(
    bool Riuscita, string Messaggio, List<MessaggioChatAi> Storia, object? Esito);

/// <summary>
/// Assistente a chat dello studio. Il modello (DeepSeek o qualsiasi endpoint
/// OpenAI-compatibile) conversa e usa gli strumenti del programma: le letture le esegue
/// subito, le scritture le PROPONE soltanto — l'utente le conferma con un bottone.
/// La conversazione non è tenuta sul server: la storia va e torna con la richiesta.
/// </summary>
public static class AiChatEndpoints
{
    /// <summary>Quanti giri di strumenti al massimo in un turno, per non avvitarsi.</summary>
    private const int MassimoGiri = 6;

    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapAiChat(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ai/chat/stato", (AiChatClient chat, AiOptions opzioni) => Results.Ok(new
        {
            attiva = chat.Attivo,
            provider = opzioni.Provider,
            modello = chat.Modello,
            messaggio = chat.Attivo ? null : CosaManca(opzioni),
        })).RequireAuthorization();

        // Un turno di conversazione.
        app.MapPost("/api/ai/chat", async (
            RichiestaChatAi richiesta, AiChatClient chat, AiChatStrumenti strumenti,
            ConsumoAiService consumi, AziendeService aziende, AiOptions opzioni,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!chat.Attivo)
                return Results.Ok(new RispostaChatEndpoint(
                    CosaManca(opzioni), richiesta.Storia ?? [], null, false));

            if (string.IsNullOrWhiteSpace(richiesta.Messaggio))
                return Results.BadRequest(new { errore = "Messaggio vuoto." });

            var tenantId = Tenant(user);
            // Il consumo si conta per trasparenza (pannello AI), ma non blocca la chat.
            await consumi.ProvaConsumaAsync(tenantId, limiteMensile: 0, ct);

            var messaggi = new List<MessaggioChatAi>
            {
                new()
                {
                    Ruolo = "system",
                    Contenuto = await IstruzioniAsync(tenantId, richiesta.AziendaId, aziende, ct),
                },
            };
            messaggi.AddRange(Ripulisci(richiesta.Storia));
            messaggi.Add(new MessaggioChatAi { Ruolo = "user", Contenuto = richiesta.Messaggio });

            string? testo = null;
            AzionePropostaAi? azione = null;

            try
            {
                for (int giro = 0; giro < MassimoGiri; giro++)
                {
                    var risposta = await chat.ParlaAsync(messaggi, AiChatStrumenti.Catalogo, ct);

                    // Nessuno strumento da usare, oppure una proposta è già pronta: si chiude
                    // il turno prendendo solo il testo (niente tool_calls appesi nella storia).
                    if (risposta.Chiamate.Count == 0 || azione is not null)
                    {
                        testo = risposta.Testo;
                        messaggi.Add(new MessaggioChatAi { Ruolo = "assistant", Contenuto = testo ?? "" });
                        break;
                    }

                    messaggi.Add(new MessaggioChatAi
                    {
                        Ruolo = "assistant",
                        Contenuto = risposta.Testo,
                        Chiamate = [.. risposta.Chiamate],
                    });

                    foreach (var chiamata in risposta.Chiamate)
                    {
                        string esito;
                        if (AiChatStrumenti.ÈScrittura(chiamata.Nome))
                        {
                            var proposta = await strumenti.PreparaScritturaAsync(
                                tenantId, chiamata.Nome, chiamata.ArgomentiJson, ct);
                            azione ??= proposta;
                            esito = DescriviAttesa(proposta);
                        }
                        else if (AiChatStrumenti.EsisteStrumento(chiamata.Nome))
                        {
                            esito = await strumenti.EseguiLetturaAsync(
                                tenantId, chiamata.Nome, chiamata.ArgomentiJson, ct);
                        }
                        else
                        {
                            esito = $"{{\"errore\":\"Strumento '{chiamata.Nome}' inesistente.\"}}";
                        }
                        messaggi.Add(new MessaggioChatAi
                        {
                            Ruolo = "tool",
                            ChiamataId = chiamata.Id,
                            Contenuto = esito,
                        });
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                return Results.Ok(new RispostaChatEndpoint(ex.Message, messaggi.Skip(1).ToList(), null, false));
            }

            if (string.IsNullOrWhiteSpace(testo))
                testo = azione is not null
                    ? "Ho preparato l'operazione qui sotto: controlla i dati e confermala tu."
                    : "Non ho una risposta da darti: riprova a spiegarmi cosa ti serve.";

            // La storia torna al client senza il system: lo rimette il server ogni volta.
            return Results.Ok(new RispostaChatEndpoint(testo, messaggi.Skip(1).ToList(), azione, true));
        }).RequireAuthorization();

        // Conferma dell'utente: qui, e solo qui, l'operazione viene eseguita davvero.
        app.MapPost("/api/ai/chat/azione", async (
            RichiestaAzioneChatAi richiesta, AiChatStrumenti strumenti,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!AiChatStrumenti.ÈScrittura(richiesta.Strumento))
                return Results.BadRequest(new { errore = "Operazione non eseguibile." });

            var (riuscita, messaggio, esito) = await strumenti.EseguiScritturaAsync(
                Tenant(user), richiesta.Strumento, richiesta.ArgomentiJson, ct);

            // La storia registra l'esito, così l'assistente sa cosa è stato fatto davvero
            // (e non ripropone la stessa operazione al messaggio successivo).
            var storia = Ripulisci(richiesta.Storia);
            storia.Add(new MessaggioChatAi
            {
                Ruolo = "user",
                Contenuto = riuscita
                    ? $"[sistema] Ho confermato l'operazione «{richiesta.Strumento}». Esito: {messaggio}"
                    : $"[sistema] Ho confermato l'operazione «{richiesta.Strumento}» ma NON è riuscita: {messaggio}",
            });
            storia.Add(new MessaggioChatAi { Ruolo = "assistant", Contenuto = messaggio });

            return Results.Ok(new EsitoAzioneChatEndpoint(riuscita, messaggio, storia, esito));
        }).RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Che cosa manca perché la chat parta, detto a chi deve metterci mano (non a un tecnico):
    /// o il provider è spento, o c'è il provider ma non la chiave.
    /// </summary>
    public static string CosaManca(AiOptions opzioni) =>
        string.IsNullOrWhiteSpace(opzioni.ApiKey)
            && !opzioni.Provider.Equals("Spento", StringComparison.OrdinalIgnoreCase)
            ? $"Manca solo la chiave del provider {opzioni.Provider}: incollala nella variabile "
                + "Ai__ApiKey (su Render: servizio paghepro → Environment), il servizio si riavvia da solo."
            : "Chat AI spenta: imposta Ai__Provider (es. DeepSeek) e Ai__ApiKey nelle variabili "
                + "d'ambiente del server.";

    /// <summary>Risultato che il modello riceve al posto dell'esecuzione: sta aspettando l'utente.</summary>
    private static string DescriviAttesa(AzionePropostaAi proposta)
    {
        string mancanti = proposta.Mancanti.Count == 0
            ? "Non manca nulla."
            : "Mancano: " + string.Join("; ", proposta.Mancanti.Select(m =>
                $"{m.Etichetta} ({m.Perche}){(m.Bloccante ? " [necessario]" : "")}")) + ".";
        return "Operazione NON eseguita: è in attesa della conferma dell'utente, che la vede "
            + $"già compilata sullo schermo. Campi valorizzati: {proposta.Campi.Count}. {mancanti} "
            + "Ora spiega all'utente in poche righe cosa hai preparato e chiedigli i dati mancanti.";
    }

    /// <summary>
    /// Istruzioni di sistema: chi è l'assistente, cosa può fare e — soprattutto — cosa non
    /// deve inventare. Il contesto (data, azienda scelta) arriva dal server, non dal client.
    /// </summary>
    private static async Task<string> IstruzioniAsync(
        Guid tenantId, Guid? aziendaId, AziendeService aziende, CancellationToken ct)
    {
        string contesto = "L'utente non ha ancora scelto un'azienda: se serve, usa elenco_aziende.";
        if (aziendaId is { } id)
        {
            var sintesi = await aziende.GetSintesiAsync(tenantId, id, ct);
            if (sintesi is not null)
                contesto = $"Azienda su cui sta lavorando: «{sintesi.Azienda.RagioneSociale}» "
                    + $"(aziendaId {sintesi.Azienda.Id}, {sintesi.NumeroDipendenti} dipendenti). "
                    + "Se l'utente non nomina un'altra azienda, riferisciti a questa.";
        }

        string oggi = DateTime.UtcNow.ToString("dddd d MMMM yyyy", new CultureInfo("it-IT"));

        return $"""
            Sei Aurora, l'assistente dello studio dentro PaghePro, il software italiano di paghe,
            fatturazione e contabilità che l'utente sta usando. Parli italiano, in modo diretto e
            concreto, come un collega esperto di amministrazione del personale.

            Oggi è {oggi}. {contesto}

            Come lavori:
            - Per sapere com'è messo l'archivio usa gli strumenti di lettura (aziende, dipendenti,
              scadenze): guarda i dati veri invece di tirare a indovinare.
            - Per registrare o correggere qualcosa usa gli strumenti di scrittura. Non eseguono
              nulla: preparano una scheda che l'utente conferma con un bottone. Dopo averla
              preparata, di' in poche righe cosa hai compilato e che cosa manca ancora.
            - Non inventare MAI dati: partite IVA, codici fiscali, matricole INPS, codici sede,
              CSC, P.A.T. INAIL, ATECO, IBAN, codici catastali, CCNL, livelli, settore, regime
              fiscale e tipo contribuente si scrivono solo se te li ha dati l'utente o se li hai
              letti con uno strumento. Se un dato non c'è, lascialo vuoto e chiedilo. Vale anche
              per i campi che sembrano ovvi: un panificio non è per forza artigianato, e da quel
              campo dipendono gli inquadramenti INPS.
            - Puoi dedurre solo ciò che è meccanico: la sigla della provincia dal comune indicato,
              il formato di una data, le maiuscole. Nient'altro.
            - I calcoli di cedolini, contributi, IRPEF e F24 li fa il motore del programma: non
              produrre importi a mano e non promettere numeri.
            - Se l'utente incolla del testo (email, visura, UNILAV, appunti), estrai i campi che
              ci sono davvero e prepara l'operazione elencando cosa manca.
            - Risposte brevi: poche righe, elenchi puntati solo quando servono davvero.
            """;
    }

    /// <summary>
    /// Ripulisce la storia che arriva dal client: niente system (lo mette il server) e niente
    /// ruoli sconosciuti. Il tenant e i dati veri li decide comunque il server.
    /// </summary>
    private static List<MessaggioChatAi> Ripulisci(List<MessaggioChatAi>? storia) =>
        storia is null
            ? []
            : [.. storia.Where(m => m.Ruolo is "user" or "assistant" or "tool")];
}
