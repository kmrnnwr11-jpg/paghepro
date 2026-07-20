using System.Security.Claims;
using Paghe.Persistence;
using Paghe.Persistence.Gdpr;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Api;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapRegistroAttivita(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit", async (
            ClaimsPrincipal user,
            AuditLogService servizio,
            int? pagina,
            int? dimensione,
            string? categoria,
            string? azione,
            string? entita,
            DateTimeOffset? daUtc,
            DateTimeOffset? aUtc,
            CancellationToken cancellationToken) =>
        {
            var ruolo = user.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.Equals(ruolo, RuoloPolicy.Admin, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(ruolo, AuthService.RuoloSuperAdmin, StringComparison.OrdinalIgnoreCase))
                return Results.Forbid();

            var daNormalizzata = daUtc?.UtcDateTime;
            var aNormalizzata = aUtc?.UtcDateTime;
            if (daNormalizzata is not null && aNormalizzata is not null
                && daNormalizzata > aNormalizzata)
                return Results.BadRequest(new { errore = "La data iniziale non può superare quella finale." });

            var risultato = await servizio.ElencaAsync(
                TenantCorrente(user),
                pagina ?? 1,
                dimensione ?? 50,
                categoria,
                azione,
                entita,
                daNormalizzata,
                aNormalizzata,
                cancellationToken);
            return Results.Ok(risultato);
        }).RequireAuthorization();

        return app;
    }

    private static Guid TenantCorrente(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var tenantId)
            ? tenantId
            : throw new InvalidOperationException("Tenant non presente nel token.");
}
