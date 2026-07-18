using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using Paghe.Core.Ai;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Api;

/// <summary>Casella email/PEC restituita all'app. La password non è mai inclusa: solo se è impostata.</summary>
public sealed record CasellaEmailDto(
    Guid Id, string Etichetta, string Tipo, string Host, int Porta, string Utente,
    bool Ssl, bool Attiva, bool PasswordImpostata, DateTime? UltimaSincronizzazioneIl);

public sealed record SalvaCasellaEmailRequest(
    string Etichetta, string Tipo, string Host, int Porta,
    string Utente, string? Password, bool Ssl, bool Attiva);

public sealed record MessaggioEmailDto(
    Guid Id, Guid CasellaId, string Mittente, string Oggetto, DateTime DataRicezione,
    string Anteprima, bool Letto, bool? Urgente, bool? Pubblicita,
    bool? UrgenteProposto, bool? PubblicitaProposto,
    decimal? ConfidenzaUrgente, decimal? ConfidenzaPubblicita, string? FonteProposta);

public sealed record ConfermaTriageRequest(bool? Urgente, bool? Pubblicita);
public sealed record MarcaLettoRequest(bool Letto);

/// <summary>
/// Endpoint del modulo Posta (email/PEC): collegamento caselle self-service, sincronizzazione
/// IMAP on-demand ed elenco messaggi con triage. Il triage (urgente/pubblicità) resta sempre una
/// proposta finché l'utente non la conferma con <c>/conferma</c> — Email/PEC sono fuori dal
/// perimetro AI diretto approvato: l'AI propone soltanto, mai agisce da sola.
/// </summary>
public static class PostaEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    private static CasellaEmailDto ToDto(CasellaEmailEntity c) =>
        new(c.Id, c.Etichetta, c.Tipo, c.Host, c.Porta, c.Utente,
            c.Ssl, c.Attiva, !string.IsNullOrWhiteSpace(c.Password), c.UltimaSincronizzazioneIl);

    private static MessaggioEmailDto ToDto(MessaggioEmailEntity m) =>
        new(m.Id, m.CasellaId, m.Mittente, m.Oggetto, m.DataRicezione, m.Anteprima, m.Letto,
            m.Urgente, m.Pubblicita, m.UrgenteProposto, m.PubblicitaProposto,
            m.ConfidenzaUrgente, m.ConfidenzaPubblicita, m.FonteProposta);

    public static IEndpointRouteBuilder MapPosta(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/posta/caselle", async (ClaimsPrincipal user, CasellaEmailService svc) =>
        {
            var caselle = await svc.ElencoCaselleAsync(Tenant(user));
            return Results.Ok(caselle.Select(ToDto));
        }).RequireAuthorization();

        app.MapPost("/api/posta/caselle", async (
            SalvaCasellaEmailRequest req, ClaimsPrincipal user, CasellaEmailService svc) =>
        {
            var c = await svc.CreaCasellaAsync(Tenant(user), new CasellaEmailInput(
                req.Etichetta, req.Tipo, req.Host, req.Porta, req.Utente, req.Password, req.Ssl, req.Attiva));
            return Results.Ok(ToDto(c));
        }).RequireAuthorization();

        app.MapDelete("/api/posta/caselle/{id:guid}", async (
            Guid id, ClaimsPrincipal user, CasellaEmailService svc) =>
            await svc.EliminaCasellaAsync(Tenant(user), id) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization();

        app.MapPost("/api/posta/caselle/{id:guid}/sincronizza", async (
            Guid id, ClaimsPrincipal user, CasellaEmailService svc) =>
        {
            var esito = await svc.SincronizzaAsync(Tenant(user), id);
            return Results.Ok(new { nuoviMessaggi = esito.NuoviMessaggi, errore = esito.Errore });
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        app.MapGet("/api/posta/messaggi", async (
            Guid? casellaId, bool? letto, bool? urgente, bool? pubblicita, bool? soloProposte,
            ClaimsPrincipal user, CasellaEmailService svc) =>
        {
            var messaggi = await svc.ElencoMessaggiAsync(Tenant(user),
                new FiltriMessaggiEmail(casellaId, letto, urgente, pubblicita, soloProposte));
            return Results.Ok(messaggi.Select(ToDto));
        }).RequireAuthorization();

        app.MapGet("/api/posta/messaggi/{id:guid}/corpo", async (
            Guid id, ClaimsPrincipal user, CasellaEmailService svc) =>
        {
            var (trovato, corpo, errore) = await svc.LeggiCorpoAsync(Tenant(user), id);
            if (!trovato) return Results.NotFound();
            return Results.Ok(new { corpo, errore });
        }).RequireAuthorization();

        app.MapPost("/api/posta/messaggi/{id:guid}/letto", async (
            Guid id, MarcaLettoRequest req, ClaimsPrincipal user, CasellaEmailService svc) =>
            await svc.MarcaLettoAsync(Tenant(user), id, req.Letto) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization();

        // Proponi con AI: l'euristica (header List-Unsubscribe/List-Id) vince sempre e non
        // consuma AI; l'AI viene chiamata solo per i campi che l'euristica lascia indecisi, e solo
        // se lo studio è dentro il limite mensile del canone. Il risultato resta una proposta.
        app.MapPost("/api/posta/messaggi/{id:guid}/proponi-ai", async (
            Guid id, ClaimsPrincipal user, CasellaEmailService svc,
            IEstrattoreAiDocumenti ai, ConsumoAiService consumi, AiOptions aiOptions) =>
        {
            var tenantId = Tenant(user);
            var m = await svc.OttieniMessaggioAsync(tenantId, id);
            if (m is null) return Results.NotFound();

            bool pubblicitaEuristica = m.FonteProposta == "Euristica" && m.PubblicitaProposto == true;
            PropostaAi? propostaAi = null;
            if (ai.Attivo && !pubblicitaEuristica)
            {
                if (await consumi.ProvaConsumaAsync(tenantId, aiOptions.LimiteMensileChiamate))
                {
                    try
                    {
                        var testo = $"Oggetto: {m.Oggetto}\n\n{m.Anteprima}";
                        propostaAi = await ai.EstraiAsync("email", testo, CompletamentoTriageEmail.CampiTriage);
                    }
                    catch (Exception)
                    {
                        // L'AI non deve mai bloccare il flusso: si resta sulla sola euristica.
                    }
                }
            }

            var proposta = CompletamentoTriageEmail.Interpreta(
                pubblicitaEuristica, propostaAi, aiOptions.SogliaConfidenza);
            await svc.SalvaPropostaAsync(tenantId, id, proposta);
            var aggiornato = await svc.OttieniMessaggioAsync(tenantId, id);
            return Results.Ok(ToDto(aggiornato!));
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        app.MapPost("/api/posta/messaggi/{id:guid}/conferma", async (
            Guid id, ConfermaTriageRequest req, ClaimsPrincipal user, CasellaEmailService svc) =>
            await svc.ConfermaPropostaAsync(Tenant(user), id, req.Urgente, req.Pubblicita)
                ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization();

        return app;
    }
}
