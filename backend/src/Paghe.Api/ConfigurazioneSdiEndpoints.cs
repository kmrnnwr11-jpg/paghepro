using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>Configurazione SdI restituita all'app. L'ApiKey non è mai inclusa: solo se è impostata.</summary>
public sealed record ConfigurazioneSdiDto(
    string Modalita, string Provider, string BaseUrl, bool ApiKeyImpostata,
    string EndpointInvio, string EndpointEsito, string CampoId, string CampoStato,
    bool FirmaLocale, bool Attivo);

/// <summary>Richiesta di salvataggio della configurazione SdI (ApiKey null = non modificare).</summary>
public sealed record SalvaConfigurazioneSdiRequest(
    string Modalita, string? Provider, string? BaseUrl, string? ApiKey,
    string? EndpointInvio, string? EndpointEsito, string? CampoId, string? CampoStato,
    bool FirmaLocale, bool Attivo);

/// <summary>
/// Endpoint per il collegamento self-service del provider SdI da parte dell'utente, isolati per tenant.
/// Mappati come metodo di estensione per ridurre al minimo le modifiche a Program.cs.
/// </summary>
public static class ConfigurazioneSdiEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    private static ConfigurazioneSdiDto ToDto(Paghe.Persistence.Entities.ConfigurazioneSdiEntity c) =>
        new(c.Modalita, c.Provider, c.BaseUrl, !string.IsNullOrWhiteSpace(c.ApiKey),
            c.EndpointInvio, c.EndpointEsito, c.CampoId, c.CampoStato, c.FirmaLocale, c.Attivo);

    public static IEndpointRouteBuilder MapConfigurazioneSdi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/configurazione-sdi", async (ClaimsPrincipal user, ConfigurazioneSdiService svc) =>
        {
            var c = await svc.GetAsync(Tenant(user));
            return Results.Ok(c is null
                ? new ConfigurazioneSdiDto("Manuale", "", "", false, "invoices", "invoices/{id}", "id", "status", false, false)
                : ToDto(c));
        }).RequireAuthorization();

        app.MapPut("/api/configurazione-sdi", async (
            SalvaConfigurazioneSdiRequest req, ClaimsPrincipal user, ConfigurazioneSdiService svc) =>
        {
            var c = await svc.SalvaAsync(Tenant(user), new ConfigurazioneSdiInput(
                Modalita: req.Modalita,
                Provider: req.Provider ?? "",
                BaseUrl: req.BaseUrl ?? "",
                ApiKey: req.ApiKey,
                EndpointInvio: string.IsNullOrWhiteSpace(req.EndpointInvio) ? "invoices" : req.EndpointInvio!,
                EndpointEsito: string.IsNullOrWhiteSpace(req.EndpointEsito) ? "invoices/{id}" : req.EndpointEsito!,
                CampoId: string.IsNullOrWhiteSpace(req.CampoId) ? "id" : req.CampoId!,
                CampoStato: string.IsNullOrWhiteSpace(req.CampoStato) ? "status" : req.CampoStato!,
                FirmaLocale: req.FirmaLocale,
                Attivo: req.Attivo));
            return Results.Ok(ToDto(c));
        }).RequireAuthorization();

        // Verifica connessione: contatta il provider per dire all'utente se le credenziali rispondono.
        app.MapPost("/api/configurazione-sdi/verifica", async (
            ClaimsPrincipal user, ConfigurazioneSdiService svc, IHttpClientFactory httpFactory) =>
        {
            var c = await svc.GetAsync(Tenant(user));
            if (c is null)
                return Results.Ok(new { ok = false, messaggio = "Nessuna configurazione salvata." });
            if (c.Modalita != "Rest")
                return Results.Ok(new { ok = true, messaggio = $"Modalità «{c.Modalita}»: nessun provider esterno da verificare." });
            if (string.IsNullOrWhiteSpace(c.BaseUrl))
                return Results.Ok(new { ok = false, messaggio = "Manca l'indirizzo base (URL) del provider." });

            try
            {
                var http = httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                using var msg = new HttpRequestMessage(HttpMethod.Get, c.BaseUrl);
                if (!string.IsNullOrWhiteSpace(c.ApiKey))
                    msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", c.ApiKey);
                using var resp = await http.SendAsync(msg);

                bool rifiutato = resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
                bool serverGiu = (int)resp.StatusCode >= 500;
                bool ok = !rifiutato && !serverGiu;
                string nota = rifiutato ? " Credenziali rifiutate: controlla l'API key."
                    : serverGiu ? " Il provider ha un problema temporaneo."
                    : " Provider raggiungibile.";
                return Results.Ok(new { ok, messaggio = $"Risposta provider: HTTP {(int)resp.StatusCode}.{nota}" });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { ok = false, messaggio = $"Provider non raggiungibile: {ex.Message}" });
            }
        }).RequireAuthorization();

        return app;
    }
}
