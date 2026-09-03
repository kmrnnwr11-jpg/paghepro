using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Ai;
using Paghe.Persistence.Entities;

namespace Paghe.Api;

/// <summary>
/// Consenso pieno ad Aurora, deciso dall'utente alla prima apertura o dal pannello. Con il
/// consenso attivo la chat emette da sola il mandato di lettura Guidata/S2 sul cliente
/// selezionato (vedi <see cref="AiChatEndpoints"/>): l'utente non passa più dal pannello per
/// far leggere l'archivio ad Aurora. Le scritture restano sul protocollo A3 con conferma.
/// </summary>
public static class AiConsensoEndpoints
{
    public static IEndpointRouteBuilder MapAiConsenso(this IEndpointRouteBuilder app)
    {
        var gruppo = app.MapGroup("/api/ai/consenso").RequireAuthorization();

        gruppo.MapGet("", async (PagheDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var (tenantId, utenteId) = Identita(user);
            var consenso = await db.ConsensiAurora.AsNoTracking().SingleOrDefaultAsync(
                c => c.TenantId == tenantId && c.UtenteId == utenteId, ct);
            return Results.Ok(new
            {
                attivo = consenso is not null && consenso.RevocatoUtc is null,
                concessoUtc = consenso is { RevocatoUtc: null } ? consenso.ConcessoUtc : (DateTime?)null,
                // La SPA mostra l'avviso della prima apertura solo se non è mai stato deciso.
                giaDeciso = consenso is not null,
            });
        });

        gruppo.MapPost("", async (
            PagheDbContext db, AiRolloutPolicy rollout, ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var (tenantId, utenteId) = Identita(user);
            if (tenantId == Guid.Empty || utenteId == Guid.Empty)
                return Results.BadRequest(new { errore = "Identità della sessione non valida." });
            var consenso = await db.ConsensiAurora.SingleOrDefaultAsync(
                c => c.TenantId == tenantId && c.UtenteId == utenteId, ct);
            if (consenso is null)
            {
                consenso = new ConsensoAuroraEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UtenteId = utenteId,
                };
                db.ConsensiAurora.Add(consenso);
            }
            consenso.ConcessoUtc = DateTime.UtcNow;
            consenso.RevocatoUtc = null;
            await db.SaveChangesAsync(ct);
            // La coppia entra subito nella strada A3 del consenso (proposte con conferma).
            rollout.RegistraConsenso(tenantId, utenteId);
            return Results.Ok(new { attivo = true, concessoUtc = consenso.ConcessoUtc });
        }).RequireRateLimiting("sensibili");

        gruppo.MapDelete("", async (
            PagheDbContext db, AiGrantService governor, AiRolloutPolicy rollout,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            var (tenantId, utenteId) = Identita(user);
            // La strada A3 del consenso si chiude per prima: nessuna nuova proposta o
            // conferma mentre revochiamo i mandati.
            rollout.RimuoviConsenso(tenantId, utenteId);
            var consenso = await db.ConsensiAurora.SingleOrDefaultAsync(
                c => c.TenantId == tenantId && c.UtenteId == utenteId, ct);
            if (consenso is not null && consenso.RevocatoUtc is null)
            {
                consenso.RevocatoUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            // I mandati emessi in automatico dalla chat muoiono con il consenso: revoca
            // immediata, non si aspetta la scadenza naturale.
            foreach (var lease in AiChatEndpoints.SvuotaMandatiAutomatici(tenantId, utenteId))
            {
                var (manifest, _) = await governor.RisolviSessioneAsync(
                    tenantId, utenteId, lease, ct);
                if (manifest is not null)
                    await governor.RevocaAsync(tenantId, utenteId, manifest.GrantId, ct);
            }
            return Results.Ok(new { attivo = false });
        }).RequireRateLimiting("sensibili");

        return app;
    }

    /// <summary>Il consenso vale solo per la coppia esatta tenant/utente del token.</summary>
    internal static Task<bool> ConsensoAttivoAsync(
        PagheDbContext db, Guid tenantId, Guid utenteId, CancellationToken ct) =>
        tenantId == Guid.Empty || utenteId == Guid.Empty
            ? Task.FromResult(false)
            : db.ConsensiAurora.AsNoTracking().AnyAsync(
                c => c.TenantId == tenantId && c.UtenteId == utenteId && c.RevocatoUtc == null,
                ct);

    private static (Guid TenantId, Guid UtenteId) Identita(ClaimsPrincipal user)
    {
        Guid tenantId = Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var t)
            ? t : Guid.Empty;
        Guid utenteId = Guid.TryParse(user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var u)
            ? u : Guid.Empty;
        return (tenantId, utenteId);
    }
}
