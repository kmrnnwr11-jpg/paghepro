using System.Security.Claims;
using Paghe.Persistence;
using Paghe.Persistence.Ai;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Api;

/// <summary>
/// Endpoint del Usage Governor (G2): preflight, emissione, trasparenza («Cosa vede l'AI»),
/// revoca/arresto e verifica della catena audit. Il grant nasce SOLO da qui (UI + policy),
/// mai dal prompt; l'enforcement resta in <see cref="AiGrantService"/>.
/// </summary>
public static class AiGovernorEndpoints
{
    public static IEndpointRouteBuilder MapAiGovernor(this IEndpointRouteBuilder app)
    {
        var gruppo = app.MapGroup("/api/ai/governor").RequireAuthorization();

        // Catalogo e policy correnti: la UI del preflight si costruisce da qui.
        gruppo.MapGet("/catalogo", (ClaimsPrincipal user, AiRolloutPolicy rollout) => Results.Ok(new
        {
            policyVersione = AiUsagePolicyStore.Versione,
            a3 = new
            {
                proposte = rollout.ProposteAbilitate(TenantCorrente(user), UtenteCorrente(user))
                    && RuoloScrittura(user),
                conferma = rollout.EffettiAbilitati(TenantCorrente(user), UtenteCorrente(user))
                    && RuoloScrittura(user),
            },
            finalita = AiUsagePolicyStore.Tutte.Select(p => new
            {
                p.PurposeCode,
                p.Descrizione,
                p.ScopeMassimo,
                p.AutonomiaMassima,
                risorse = p.RisorseAmmesse,
                operazioni = p.OperazioniAmmesse,
                p.TtlMassimoMinuti,
                p.QuotaLettureDefault,
            }),
            risorse = ProfessionalResourceCatalog.Tutte
                .OrderBy(r => r.Nome)
                .Select(r => new
                {
                    r.Nome,
                    r.Modulo,
                    classe = r.Classe.ToString(),
                    concedibile = ProfessionalResourceCatalog.ConcedibileInG2(r.Classe),
                    r.Note,
                }),
            capability = AiCapabilityRegistry.Tutte.Select(c => new
            {
                c.CapabilityId,
                c.Modulo,
                c.StatoLocale,
                c.ModalitaSupportate,
                c.RisorseUsate,
                c.PurposeCode,
                c.EffettiEsterni,
                c.GateRichiesto,
            }),
        }));

        gruppo.MapPost("/preflight", async (
            ClaimsPrincipal user, AiGrantService servizio, RichiestaGrant richiesta, CancellationToken ct) =>
        {
            if (RichiedeA3(richiesta) && !RuoloScrittura(user))
                return Results.Ok(new EsitoPreflight(
                    false, "Il ruolo corrente non può richiedere operazioni A3.", null));
            return Results.Ok(await servizio.PreflightAsync(
                TenantCorrente(user), UtenteCorrente(user), richiesta, ct,
                user.FindFirst(ClaimTypes.Role)?.Value));
        });

        gruppo.MapPost("/grants", async (
            ClaimsPrincipal user, AiGrantService servizio, RichiestaGrant richiesta, CancellationToken ct) =>
        {
            if (RichiedeA3(richiesta) && !RuoloScrittura(user))
                return Results.Forbid();
            var (grant, errore) = await servizio.EmettiAsync(
                TenantCorrente(user), UtenteCorrente(user), richiesta, ct,
                user.FindFirst(ClaimTypes.Role)?.Value);
            return grant is null ? Results.BadRequest(new { errore }) : Results.Ok(grant);
        });

        gruppo.MapGet("/grants", async (ClaimsPrincipal user, AiGrantService servizio, CancellationToken ct) =>
            Results.Ok(await servizio.GrantsAsync(TenantCorrente(user), UtenteCorrente(user), ct)));

        // «Cosa vede l'AI»: manifest + eventi audit del singolo mandato.
        gruppo.MapGet("/grants/{id:guid}", async (
            ClaimsPrincipal user, AiGrantService servizio, Guid id, CancellationToken ct) =>
        {
            var esito = await servizio.CosaVedeAsync(
                TenantCorrente(user), UtenteCorrente(user), id, ct);
            return esito is null
                ? Results.NotFound(new { errore = "Mandato non trovato." })
                : Results.Ok(new
                {
                    manifest = esito.Value.Manifest,
                    eventi = esito.Value.Eventi.Select(e => new
                    {
                        e.Sequenza,
                        e.Evento,
                        e.Dettaglio,
                        e.Motivo,
                        e.QuandoUtc,
                    }),
                });
        });

        gruppo.MapPost("/grants/{id:guid}/revoca", async (
            ClaimsPrincipal user, AiGrantService servizio, Guid id, CancellationToken ct) =>
            await servizio.RevocaAsync(TenantCorrente(user), UtenteCorrente(user), id, ct)
                ? Results.Ok(new { revocato = true })
                : Results.NotFound(new { errore = "Mandato non trovato." }));

        // «Arresta AI»: revoca tutti i mandati attivi dell'operatore.
        gruppo.MapPost("/arresta", async (ClaimsPrincipal user, AiGrantService servizio, CancellationToken ct) =>
            Results.Ok(new { revocati = await servizio.ArrestaTuttoAsync(TenantCorrente(user), UtenteCorrente(user), ct) }));

        // Query broker: permesso + dati minimizzati in un passo solo; lo scope (azienda,
        // periodo) viene dal mandato, mai dai parametri del client.
        gruppo.MapPost("/query", async (
            ClaimsPrincipal user, ProfessionalQueryBroker broker, RichiestaQuery richiesta, CancellationToken ct) =>
        {
            var esito = await broker.LeggiAsync(
                TenantCorrente(user), UtenteCorrente(user), richiesta.Lease, richiesta.Risorsa, ct);
            return esito.Consentito ? Results.Ok(esito) : Results.Json(esito, statusCode: 403);
        });

        // Seam diagnostico: una richiesta HTTP consuma sempre una sola unità. La quantità reale
        // dei risultati è calcolata esclusivamente dal broker e non è dichiarabile dal browser.
        gruppo.MapPost("/accesso", async (
            ClaimsPrincipal user, AiGrantService servizio, RichiestaAccesso richiesta, CancellationToken ct) =>
        {
            if ((richiesta.Operazione ?? "").Trim().ToLowerInvariant() is not ("read" or "search"))
                return Results.Json(new
                {
                    consentito = false,
                    motivo = "Questo endpoint diagnostico accetta soltanto lettura e ricerca.",
                }, statusCode: 403);
            var esito = await servizio.AutorizzaAccessoAsync(
                TenantCorrente(user), UtenteCorrente(user), richiesta.Lease,
                richiesta.Risorsa, richiesta.Operazione, quantita: 1, ct);
            return esito.Consentito ? Results.Ok(esito) : Results.Json(esito, statusCode: 403);
        });

        // Verifica tamper-evident della catena audit (solo admin/super-admin).
        gruppo.MapGet("/audit/verifica", async (
            ClaimsPrincipal user, AiGrantService servizio, CancellationToken ct) =>
        {
            var ruolo = user.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.Equals(ruolo, RuoloPolicy.Admin, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(ruolo, AuthService.RuoloSuperAdmin, StringComparison.OrdinalIgnoreCase))
                return Results.Forbid();
            return Results.Ok(await servizio.VerificaCatenaAsync(TenantCorrente(user), ct));
        });

        return app;
    }

    public sealed record RichiestaAccesso(string? Lease, string? Risorsa, string? Operazione);

    public sealed record RichiestaQuery(string Lease, string Risorsa);

    private static Guid TenantCorrente(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var tenantId)
            ? tenantId
            : throw new InvalidOperationException("Tenant non presente nel token.");

    private static Guid UtenteCorrente(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("Utente non presente nel token.");

    private static bool RuoloScrittura(ClaimsPrincipal user)
    {
        var ruolo = user.FindFirst(ClaimTypes.Role)?.Value;
        return string.Equals(ruolo, RuoloPolicy.Operatore, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruolo, RuoloPolicy.Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruolo, AuthService.RuoloSuperAdmin, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RichiedeA3(RichiestaGrant richiesta) =>
        (richiesta.Operazioni ?? []).Any(op =>
            string.Equals(op?.Trim(), "draft", StringComparison.OrdinalIgnoreCase)
            || string.Equals(op?.Trim(), "apply_reversible", StringComparison.OrdinalIgnoreCase));
}
